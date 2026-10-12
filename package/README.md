# Cartur's Cooking

More from the Cooking skill you already have. Food remembers who cooked it, and a better cook's food
fills you up more. From level 50 a meal can come out perfect - and a perfect meal makes you Happy.

Inspired by **Smoothbrain's [Cooking](https://thunderstore.io/c/valheim/p/Smoothbrain/Cooking/)**
(retired in 2026), by Smoothbrain and [blaxxun-boop](https://github.com/blaxxun-boop/Cooking) -
credit to them for the idea and the design. This is a separate mod written for Valheim 1.0.

## What it does

Everything builds on the game's own Cooking skill. There is no second skill to grind, and the game's
own Cooking bonuses - faster cooking and the chance of an extra portion - are kept.

| | |
| --- | --- |
| **Better food** | Food carries the level of whoever cooked it. At 100 it gives 30% more health, stamina, health regen and eitr. Every level counts. |
| **Perfect meals** | From level 50 a meal can turn out perfect - the chance is your level minus 50, so 50% at 100. Eating one makes you **Happy**: 10% faster for 3 minutes. |
| **Stacks** | Food from different cooks stays in separate stacks - levels are rounded to steps of 5. |
| **Kept on death** | Cooking is not lowered when you die. |
| **Experience** | Taking food off a cooking station gives 5 Cooking experience; cauldron and oven meals give 5x the usual. |

The bonus shows on the food's tooltip, and survives logging out with it still in your belly.

## Settings

`BepInEx/config/com.jekkle.valheim.carturcooking.cfg`

| Setting | Default | What it does |
| --- | --- | --- |
| `HealthBonus` | 0.3 | Extra health on food cooked at level 100. 0 = vanilla. |
| `StaminaBonus` | 0.3 | Extra stamina at level 100. |
| `RegenBonus` | 0.3 | Extra health regen at level 100. |
| `EitrBonus` | 0.3 | Extra eitr at level 100. |
| `LevelStep` | 5 | Cook levels round to steps of this size; different steps stack separately. |
| `PerfectLevel` | 50 | Level from which meals can be perfect. 0 = never. |
| `HappyMinutes` | 3 | How long Happy lasts. |
| `HappySpeed` | 0.1 | Extra speed while Happy. |
| `StationXp` | 5 | Cooking experience for taking food off a cooking station. |
| `CraftXpFactor` | 5 | Multiplies the experience for cauldron and oven meals. |
| `DeathLoss` | 0 | Percent of Cooking lost on death. |
| `EnforceVersion` | true | On a server, refuse clients on a different version. |

Install it on the server too and its numbers apply to everyone.

## Install

r2modman or Thunderstore Mod Manager. Needs BepInEx.

---

*Free, and always will be. If it improved your game you can [tip me on Patreon](https://www.patreon.com/c/cartur).*

**[Discord](https://discord.gg/nd5RqpwNkz)** — bug reports, install help, and mod requests.
Bug reports get their own thread so nothing is lost in a chat scroll, and requests are voted on.

**More from Cartur:**
[Map Pins](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Map_Pins/) ·
[HD Blood](https://thunderstore.io/c/valheim/p/Cartur/Carturs_HD_Blood/) ·
[Compass and Clock](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Compass_and_Clock/) ·
[Safe Stamina](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Safe_Stamina/) ·
[Follow Command](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Follow_Command/) ·
[Flooring](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Flooring/) ·
[UI](https://thunderstore.io/c/valheim/p/Cartur/Carturs_UI_HUD/) ·
[Waste Management](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Waste_Management/) ·
[Feeding Trough](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Feeding_Trough/) ·
[Build Camera](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Build_Camera/) ·
[Combat Text](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Combat_Text/) ·
[Farming](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Farming/) ·
[Mining](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Mining/) ·
[Sailing](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Sailing/) ·
[Blacksmithing](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Blacksmithing/) ·
[Jewelcrafting](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Jewelcrafting/)
