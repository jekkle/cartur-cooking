using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CarturCooking
{
    /// <summary>
    /// More to the game's own Cooking skill: food carries its cook's level, and a better cook's food
    /// gives more health, stamina, regen and eitr. From level 50 a meal can turn out perfect, and
    /// eating it makes you Happy - faster for a few minutes. Inspired by Smoothbrain's Cooking
    /// (deprecated on Thunderstore in 2026); written from a behaviour spec and the game's code, none
    /// of theirs.
    ///
    /// Read off assembly_valheim (1.0), and the reason each patch exists:
    ///  - Vanilla Cooking already gives a bonus-yield chance and faster cauldron crafting; both are
    ///    left alone.
    ///  - The stamp lives in ItemData.m_customData, which the game saves and sends with the item.
    ///  - Stacks merge on name + quality + world level only (FindFreeStackItem, FindFreeStackSpace,
    ///    IsSameType, ItemDrop.AutoStackItems), so food from different cooks would blend into one
    ///    stack. Each of those is taught to compare stamps too.
    ///  - Eaten food (Player.Food) is recomputed every second from food.m_item.m_shared, and saved as
    ///    name + time only; Load rebuilds m_item from the prefab. So the bonus is added on top in
    ///    GetTotalFoodValue and the regen tick, and the eaten stamps are kept in the player's own
    ///    m_customData across a relog.
    ///  - A cooking station spawns the finished food on the station's owner (RPC_RemoveDoneItem),
    ///    who is often not the cook - so each player publishes their Cooking level on their ZDO.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jekkle.valheim.carturcooking";
        public const string PluginName = "Cartur's Cooking";
        public const string PluginVersion = "1.0.0";
        internal const string Name = PluginName;
        internal const string Version = PluginVersion;
        internal const string ServerSection = "1 - Server";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigFile Cfg;

        internal static ConfigEntry<float> HealthBonus;
        internal static ConfigEntry<float> StaminaBonus;
        internal static ConfigEntry<float> RegenBonus;
        internal static ConfigEntry<float> EitrBonus;
        internal static ConfigEntry<int> LevelStep;
        internal static ConfigEntry<int> PerfectLevel;
        internal static ConfigEntry<int> HappyMinutes;
        internal static ConfigEntry<float> HappySpeed;
        internal static ConfigEntry<float> StationXp;
        internal static ConfigEntry<float> CraftXpFactor;
        internal static ConfigEntry<int> DeathLoss;
        internal static ConfigEntry<bool> EnforceVersion;

        private void Awake()
        {
            Log = Logger;
            Cfg = Config;

            HealthBonus = Config.Bind(ServerSection, "HealthBonus", 0.3f,
                new ConfigDescription("Extra health on food cooked at Cooking 100. 0.3 = +30%, 0 = vanilla.", new AcceptableValueRange<float>(0f, 4f)));
            StaminaBonus = Config.Bind(ServerSection, "StaminaBonus", 0.3f,
                new ConfigDescription("Extra stamina on food cooked at Cooking 100. 0.3 = +30%, 0 = vanilla.", new AcceptableValueRange<float>(0f, 4f)));
            RegenBonus = Config.Bind(ServerSection, "RegenBonus", 0.3f,
                new ConfigDescription("Extra health regen on food cooked at Cooking 100. 0.3 = +30%, 0 = vanilla.", new AcceptableValueRange<float>(0f, 4f)));
            EitrBonus = Config.Bind(ServerSection, "EitrBonus", 0.3f,
                new ConfigDescription("Extra eitr on food cooked at Cooking 100. 0.3 = +30%, 0 = vanilla.", new AcceptableValueRange<float>(0f, 4f)));
            LevelStep = Config.Bind(ServerSection, "LevelStep", 5,
                new ConfigDescription("The cook's level is rounded to steps of this size. Food from cooks in different steps stacks separately.", new AcceptableValueRange<int>(1, 50)));
            PerfectLevel = Config.Bind(ServerSection, "PerfectLevel", 50,
                new ConfigDescription("Cooking level from which a meal can turn out perfect. The chance is your level minus this, in percent. 0 = never.", new AcceptableValueRange<int>(0, 100)));
            HappyMinutes = Config.Bind(ServerSection, "HappyMinutes", 3,
                new ConfigDescription("How long Happy lasts after eating a perfect meal, in minutes.", new AcceptableValueRange<int>(1, 60)));
            HappySpeed = Config.Bind(ServerSection, "HappySpeed", 0.1f,
                new ConfigDescription("Extra movement speed while Happy. 0.1 = +10%.", new AcceptableValueRange<float>(0f, 2f)));
            StationXp = Config.Bind(ServerSection, "StationXp", 5f,
                new ConfigDescription("Cooking experience for taking cooked food off a cooking station. Vanilla gives 0.4 for putting it on and 0.6 for taking it off.", new AcceptableValueRange<float>(0f, 50f)));
            CraftXpFactor = Config.Bind(ServerSection, "CraftXpFactor", 5f,
                new ConfigDescription("Multiplies the Cooking experience for food made at a cauldron or oven. 1 = vanilla.", new AcceptableValueRange<float>(0f, 20f)));
            DeathLoss = Config.Bind(ServerSection, "DeathLoss", 0,
                new ConfigDescription("Percent of your Cooking level lost on death, replacing the game's loss for this one skill. 0 = Cooking is never lost.", new AcceptableValueRange<int>(0, 100)));
            EnforceVersion = Config.Bind(ServerSection, "EnforceVersion", true,
                "On a server: refuse clients running a different version of this mod.");

            HappySpeed.SettingChanged += (_, __) => Cooking.ConfigureHappy();
            HappyMinutes.SettingChanged += (_, __) => Cooking.ConfigureHappy();

            try
            {
                Cooking.HappyIcon = Cooking.LoadIcon(Path.Combine(Path.GetDirectoryName(Info.Location) ?? "", "happy_icon.png"));
                Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginGuid);
                Log.LogInfo($"{PluginName} {PluginVersion} loaded - cook-level food stats, perfect meals and Happy on the Cooking skill.");
            }
            catch (Exception e)
            {
                Log.LogWarning($"{PluginName} failed to patch, game continues without it: {e}");
            }
        }

        private float _publishAt;

        private void Update()
        {
            if (Time.time >= _publishAt)
            {
                _publishAt = Time.time + 2f;
                Cooking.Publish();
            }
        }

        internal static IEnumerable<ConfigEntryBase> ServerOwnedEntries(HashSet<string> sections) =>
            Cfg.Keys.Where(k => sections.Contains(k.Section)).Select(k => Cfg[k]);

        internal static ConfigEntryBase FindEntry(string section, string key) =>
            Cfg.Keys.Where(k => k.Section == section && k.Key == key).Select(k => Cfg[k]).FirstOrDefault();

        internal static void WithoutSaving(Action change)
        {
            bool was = Cfg.SaveOnConfigSet;
            Cfg.SaveOnConfigSet = false;
            try { change(); }
            finally { Cfg.SaveOnConfigSet = was; }
        }
    }

    internal static class Cooking
    {
        internal const string LevelData = "cartur_cook";
        internal const string PerfectData = "cartur_perfect";
        internal const string EatenPrefix = "cartur_cooking_eaten ";
        internal static readonly int LevelKey = "cartur_cooking_level".GetStableHashCode();
        internal static readonly int HappyHash = "CarturHappy".GetStableHashCode();

        internal static Sprite HappyIcon;
        internal static SE_Stats Happy;

        /// Food in the game's sense: anything that gives both health and stamina when eaten.
        internal static bool IsFood(ItemDrop.ItemData item) =>
            item != null && item.m_shared.m_food > 0f && item.m_shared.m_foodStamina > 0f;

        internal static int CookLevel(ItemDrop.ItemData item) =>
            item != null && item.m_customData.TryGetValue(LevelData, out string s) && int.TryParse(s, out int v) ? v : 0;

        internal static bool IsPerfect(ItemDrop.ItemData item) =>
            item != null && item.m_customData.ContainsKey(PerfectData);

        /// Two items stack only if they carry the same cook level and the same perfect flag.
        internal static bool SameStamp(ItemDrop.ItemData a, ItemDrop.ItemData b) =>
            CookLevel(a) == CookLevel(b) && IsPerfect(a) == IsPerfect(b);

        internal static void Stamp(ItemDrop.ItemData item, int level)
        {
            int step = Mathf.Max(1, Plugin.LevelStep.Value);
            int stamped = Mathf.Clamp(Mathf.RoundToInt(level / (float)step) * step, 0, 100);
            if (stamped > 0)
                item.m_customData[LevelData] = stamped.ToString(CultureInfo.InvariantCulture);
            // Chance is (level - PerfectLevel) percent, so 50% at 100 with the default 50.
            int perfectAt = Plugin.PerfectLevel.Value;
            if (perfectAt > 0 && UnityEngine.Random.Range(0, 100) < stamped - perfectAt)
                item.m_customData[PerfectData] = "1";
        }

        internal static float Bonus(ItemDrop.ItemData item, ConfigEntry<float> perHundred) =>
            CookLevel(item) / 100f * perHundred.Value;

        internal static int LocalLevel() =>
            Player.m_localPlayer != null ? Mathf.RoundToInt(Player.m_localPlayer.GetSkillLevel(Skills.SkillType.Cooking)) : 0;

        /// Each player's Cooking level on their own character's ZDO, so a station's owner can stamp
        /// food with the level of whoever took it out.
        internal static void Publish()
        {
            Player p = Player.m_localPlayer;
            ZNetView nview = p != null ? p.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;
            int level = LocalLevel();
            if (nview.GetZDO().GetInt(LevelKey, -1) != level)
                nview.GetZDO().Set(LevelKey, level);
        }

        /// The published Cooking level of the player behind a network peer, or -1 if unknown.
        internal static int LevelOfPeer(long peer)
        {
            foreach (Player p in Player.GetAllPlayers())
            {
                ZNetView nview = p.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || nview.GetZDO().GetOwner() != peer)
                    continue;
                return p == Player.m_localPlayer ? LocalLevel() : nview.GetZDO().GetInt(LevelKey, 0);
            }
            return -1;
        }

        internal static Sprite LoadIcon(string path)
        {
            if (!File.Exists(path))
                return null;
            var tex = new Texture2D(2, 2);
            // By reflection on purpose: referencing ImageConversion.LoadImage at compile time drags in
            // ReadOnlySpan<byte>, which net472 + this game's netstandard.dll cannot resolve (same
            // wall Map Pins hit - see its CustomIcons.LoadImageViaReflection).
            if (LoadImage?.Invoke(null, new object[] { tex, File.ReadAllBytes(path), false }) is bool ok && ok)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return null;
        }

        private static readonly MethodInfo LoadImage = AccessTools.TypeByName("UnityEngine.ImageConversion")?
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "LoadImage" && m.GetParameters().Length == 3 && m.GetParameters()[1].ParameterType == typeof(byte[]));

        internal static void ConfigureHappy()
        {
            if (Happy == null)
                return;
            Happy.m_ttl = Plugin.HappyMinutes.Value * 60f;
            Happy.m_speedModifier = Plugin.HappySpeed.Value;
            Happy.m_tooltip = "You ate a perfect meal.";
        }
    }

    // ---- the Happy status effect -----------------------------------------------------------------

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class RegisterHappy
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (__instance.m_StatusEffects.Any(se => se != null && se.NameHash() == Cooking.HappyHash))
                return;
            SE_Stats se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "CarturHappy";
            se.m_name = "Happy";
            se.m_icon = Cooking.HappyIcon;
            Cooking.Happy = se;
            Cooking.ConfigureHappy();
            __instance.m_StatusEffects.Add(se);
        }
    }

    // ---- stamping --------------------------------------------------------------------------------

    // A cooking station spawns the done item on the station's owner; the sender is the peer of the
    // player who took it out, whose published level goes on the food.
    [HarmonyPatch(typeof(CookingStation), "RPC_RemoveDoneItem")]
    internal static class StationCook
    {
        internal static int Level = -1;
        private static void Prefix(long sender) => Level = Cooking.LevelOfPeer(sender);
        private static void Finalizer() => Level = -1;
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.OnCreateNew), typeof(ItemDrop), typeof(bool))]
    internal static class StampStationFood
    {
        private static void Postfix(ItemDrop item)
        {
            if (StationCook.Level < 0 || item == null || !Cooking.IsFood(item.m_itemData))
                return;
            Cooking.Stamp(item.m_itemData, StationCook.Level);
            ZNetView nview = item.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.IsOwner())
                ItemDrop.SaveToZDO(item.m_itemData, nview.GetZDO());
        }
    }

    // Food made at a cauldron or oven goes through Inventory.AddItem(ItemData) inside DoCrafting; it
    // is stamped there, before the inventory looks for a stack to merge it into.
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class CraftingFood
    {
        internal static bool Active;
        private static void Prefix(Recipe ___m_craftRecipe, Player player)
        {
            CraftingStation station = player != null ? player.GetCurrentCraftingStation() : null;
            Active = station != null && station.m_craftingSkill == Skills.SkillType.Cooking
                     && ___m_craftRecipe != null && Cooking.IsFood(___m_craftRecipe.m_item.m_itemData);
        }
        private static void Finalizer() => Active = false;
    }

    // ---- experience -----------------------------------------------------------------------------

    // Taking food off a station gives StationXp instead of vanilla's 0.6, and putting it on gives
    // nothing instead of 0.4: OnInteract raises the skill itself, so its raises are swallowed and one
    // is given here. Cauldron crafts multiply vanilla's raise.
    [HarmonyPatch(typeof(CookingStation), "OnInteract")]
    internal static class StationXp
    {
        internal static bool Swallow;
        private static void Prefix(CookingStation __instance, out bool __state)
        {
            __state = Traverse.Create(__instance).Method("HaveDoneItem").GetValue<bool>();
            Swallow = __instance.m_skill == Skills.SkillType.Cooking;
        }
        private static void Postfix(CookingStation __instance, bool __state, bool __result)
        {
            Swallow = false;
            if (__state && __result && __instance.m_skill == Skills.SkillType.Cooking && Player.m_localPlayer != null)
                Player.m_localPlayer.RaiseSkill(Skills.SkillType.Cooking, Plugin.StationXp.Value);
        }
        private static void Finalizer() => Swallow = false;
    }

    [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
    internal static class ScaleXp
    {
        private static bool Prefix(Skills.SkillType skill, ref float value)
        {
            if (skill != Skills.SkillType.Cooking)
                return true;
            if (StationXp.Swallow)
                return false;
            if (CraftingFood.Active)
                value *= Plugin.CraftXpFactor.Value;
            return true;
        }
    }

    // Cooking is taken out of the death loss and lowered by DeathLoss percent instead.
    [HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
    internal static class DeathLoss
    {
        private static readonly FieldInfo SkillData = AccessTools.Field(typeof(Skills), "m_skillData");

        private static void Prefix(Skills __instance, out Skills.Skill __state)
        {
            __state = null;
            if (!(SkillData?.GetValue(__instance) is Dictionary<Skills.SkillType, Skills.Skill> data)
                || !data.TryGetValue(Skills.SkillType.Cooking, out Skills.Skill cooking))
                return;
            __state = cooking;
            cooking.m_level -= cooking.m_level * Plugin.DeathLoss.Value / 100f;
            if (Plugin.DeathLoss.Value > 0)
                cooking.m_accumulator = 0f;
            data.Remove(Skills.SkillType.Cooking);
        }

        private static void Finalizer(Skills __instance, Skills.Skill __state)
        {
            if (__state != null && SkillData?.GetValue(__instance) is Dictionary<Skills.SkillType, Skills.Skill> data)
                data[Skills.SkillType.Cooking] = __state;
        }
    }

    // ---- stacking ---------------------------------------------------------------------------------

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
    internal static class AddingItem
    {
        internal static ItemDrop.ItemData Current;

        private static void Prefix(ItemDrop.ItemData item, out ItemDrop.ItemData __state)
        {
            __state = Current;
            if (CraftingFood.Active && Cooking.IsFood(item) && !item.m_customData.ContainsKey(Cooking.LevelData))
                Cooking.Stamp(item, Cooking.LocalLevel());
            Current = item;
        }

        private static void Finalizer(ItemDrop.ItemData __state) => Current = __state;
    }

    // FindFreeStackItem only sees the name, so the item being added is remembered above and a stack
    // with a different stamp is passed over for one that matches, or for an empty slot.
    [HarmonyPatch(typeof(Inventory), "FindFreeStackItem")]
    internal static class StackByStamp
    {
        private static void Postfix(Inventory __instance, ref ItemDrop.ItemData __result)
        {
            ItemDrop.ItemData adding = AddingItem.Current;
            if (__result == null || adding == null || Cooking.SameStamp(__result, adding))
                return;
            ItemDrop.ItemData match = null;
            foreach (ItemDrop.ItemData item in __instance.GetAllItems())
                if (item.m_shared.m_name == adding.m_shared.m_name && item.m_quality == adding.m_quality
                    && item.m_worldLevel == adding.m_worldLevel && item.m_stack < item.m_shared.m_maxStackSize
                    && Cooking.SameStamp(item, adding))
                {
                    match = item;
                    break;
                }
            __result = match;
        }
    }

    // Dropping one stack onto another (AddItem at a grid position) asks IsSameType.
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.IsSameType))]
    internal static class SameTypeByStamp
    {
        private static void Postfix(ItemDrop.ItemData __instance, ItemDrop.ItemData other, ref bool __result)
        {
            if (__result && !Cooking.SameStamp(__instance, other))
                __result = false;
        }
    }

    // CanAddItem counts free space in every stack of the same name; only matching stacks count.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
    internal static class SpaceByStamp
    {
        private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
        {
            if (!__result || !Cooking.IsFood(item))
                return;
            if (stack <= 0)
                stack = item.m_stack;
            int space = 0;
            foreach (ItemDrop.ItemData other in __instance.GetAllItems())
                if (other.m_shared.m_name == item.m_shared.m_name && other.m_worldLevel == item.m_worldLevel && Cooking.SameStamp(other, item))
                    space += Mathf.Max(0, other.m_shared.m_maxStackSize - other.m_stack);
            space += __instance.GetEmptySlots() * item.m_shared.m_maxStackSize;
            __result = space >= stack;
        }
    }

    // Food lying on the ground merges with every nearby drop of the same name. For food the original
    // is replaced (Prefix returns false) by the same merge with the stamp compared as well; anything
    // else runs the original untouched.
    [HarmonyPatch(typeof(ItemDrop), "AutoStackItems")]
    internal static class GroundStackByStamp
    {
        private static readonly FieldInfo Stacked = AccessTools.Field(typeof(ItemDrop), "m_haveAutoStacked");
        private static readonly MethodInfo Save = AccessTools.Method(typeof(ItemDrop), "Save");
        private static int _mask;

        private static bool Prefix(ItemDrop __instance)
        {
            ItemDrop.ItemData data = __instance.m_itemData;
            if (!Cooking.IsFood(data) || Stacked == null || Save == null)
                return true;
            if (data.m_shared.m_maxStackSize <= 1 || data.m_stack >= data.m_shared.m_maxStackSize || (bool)Stacked.GetValue(__instance))
                return false;
            Stacked.SetValue(__instance, true);
            if (_mask == 0)
                _mask = LayerMask.GetMask("item");
            bool merged = false;
            foreach (Collider c in Physics.OverlapSphere(__instance.transform.position, 4f, _mask))
            {
                ItemDrop other = c.attachedRigidbody ? c.attachedRigidbody.GetComponent<ItemDrop>() : null;
                ZNetView nview = other != null ? other.GetComponent<ZNetView>() : null;
                if (other == null || other == __instance || !other.m_itemData.m_shared.m_autoStack || nview == null
                    || !nview.IsValid() || !nview.IsOwner()
                    || other.m_itemData.m_shared.m_name != data.m_shared.m_name || other.m_itemData.m_quality != data.m_quality
                    || !Cooking.SameStamp(other.m_itemData, data))
                    continue;
                int room = data.m_shared.m_maxStackSize - data.m_stack;
                if (room == 0)
                    break;
                if (other.m_itemData.m_stack <= room)
                {
                    data.m_stack += other.m_itemData.m_stack;
                    merged = true;
                    nview.Destroy();
                }
            }
            if (merged)
                Save.Invoke(__instance, null);
            return false;
        }
    }

    // ---- eating ---------------------------------------------------------------------------------------

    // EatFood keeps the eaten item in Player.Food.m_item - but a refreshed food keeps the first one it
    // saw. The entry is pointed at a one-item copy of what was just eaten, so its stamp is current.
    [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
    internal static class Eat
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || item == null)
                return;
            foreach (Player.Food food in __instance.GetFoods())
                if (food.m_item != null && food.m_item.m_shared.m_name == item.m_shared.m_name)
                {
                    ItemDrop.ItemData copy = item.Clone();
                    copy.m_stack = 1;
                    food.m_item = copy;
                }
            if (Cooking.IsPerfect(item))
                __instance.GetSEMan().AddStatusEffect(Cooking.HappyHash, resetTime: true);
        }
    }

    [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
    internal static class FoodValue
    {
        private static void Postfix(Player __instance, ref float hp, ref float stamina, ref float eitr)
        {
            foreach (Player.Food food in __instance.GetFoods())
            {
                hp += food.m_health * Cooking.Bonus(food.m_item, Plugin.HealthBonus);
                stamina += food.m_stamina * Cooking.Bonus(food.m_item, Plugin.StaminaBonus);
                eitr += food.m_eitr * Cooking.Bonus(food.m_item, Plugin.EitrBonus);
            }
        }
    }

    // UpdateFood heals the summed m_foodRegen every 10 s and zeroes its timer; the stamped share is
    // healed right after, through the same regen multiplier.
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class FoodRegen
    {
        private static readonly FieldInfo Timer = AccessTools.Field(typeof(Player), "m_foodRegenTimer");

        private static void Prefix(Player __instance, float dt, bool forceUpdate, out bool __state) =>
            __state = !forceUpdate && Timer != null && (float)Timer.GetValue(__instance) + dt >= 10f;

        private static void Postfix(Player __instance, bool __state)
        {
            if (!__state)
                return;
            float extra = 0f;
            foreach (Player.Food food in __instance.GetFoods())
                extra += food.m_item.m_shared.m_foodRegen * Cooking.Bonus(food.m_item, Plugin.RegenBonus);
            if (extra <= 0f)
                return;
            float multiplier = 1f;
            __instance.GetSEMan().ModifyHealthRegen(ref multiplier);
            __instance.Heal(extra * multiplier);
        }
    }

    // Player.Save keeps eaten food as name + time; the stamps ride along in the player's own
    // m_customData and are put back on Load.
    [HarmonyPatch(typeof(Player), "Save")]
    internal static class SaveEaten
    {
        private static void Prefix(Player __instance)
        {
            foreach (string key in __instance.m_customData.Keys.Where(k => k.StartsWith(Cooking.EatenPrefix)).ToList())
                __instance.m_customData.Remove(key);
            foreach (Player.Food food in __instance.GetFoods())
            {
                int level = Cooking.CookLevel(food.m_item);
                if (level > 0)
                    __instance.m_customData[Cooking.EatenPrefix + food.m_name] = level.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    [HarmonyPatch(typeof(Player), "Load")]
    internal static class LoadEaten
    {
        private static void Postfix(Player __instance)
        {
            foreach (Player.Food food in __instance.GetFoods())
            {
                if (food.m_item == null || !__instance.m_customData.TryGetValue(Cooking.EatenPrefix + food.m_name, out string level))
                    continue;
                // Load points m_item at the prefab's own ItemData; copy it before writing to it.
                ItemDrop.ItemData copy = food.m_item.Clone();
                copy.m_customData[Cooking.LevelData] = level;
                food.m_item = copy;
            }
        }
    }

    // ---- tooltip ------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class Tooltip
    {
        private static void Postfix(ItemDrop.ItemData item, bool crafting, ref string __result)
        {
            if (!Cooking.IsFood(item))
                return;
            // A recipe shows what this cook would make; an item shows what its cook made.
            int level = crafting ? Cooking.LocalLevel() : Cooking.CookLevel(item);
            if (level <= 0)
                return;
            float f = level / 100f;
            var sb = new StringBuilder(__result);
            sb.Append($"\n\nCooked at level <color=orange>{level}</color>: ");
            sb.Append($"<color=orange>+{Mathf.Round(item.m_shared.m_food * f * Plugin.HealthBonus.Value)}</color> health, ");
            sb.Append($"<color=orange>+{Mathf.Round(item.m_shared.m_foodStamina * f * Plugin.StaminaBonus.Value)}</color> stamina");
            if (item.m_shared.m_foodEitr > 0f)
                sb.Append($", <color=orange>+{Mathf.Round(item.m_shared.m_foodEitr * f * Plugin.EitrBonus.Value)}</color> eitr");
            if (item.m_shared.m_foodRegen > 0f)
                sb.Append($", <color=orange>+{item.m_shared.m_foodRegen * f * Plugin.RegenBonus.Value:0.#}</color> regen");
            if (!crafting && Cooking.IsPerfect(item))
                sb.Append("\n<color=yellow>Cooked perfectly - eating it will make you Happy.</color>");
            __result = sb.ToString();
        }
    }
}
