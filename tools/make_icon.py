"""Draws the 256x256 Thunderstore icon (a steaming bowl) and the 64x64 Happy status-effect icon.

Thunderstore rejects anything that is not exactly 256x256 PNG, and there is no art asset to crop
from, so both are drawn - same approach and background as the other Cartur icons.

    python tools/make_icon.py
"""
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SCALE = 4

BACKGROUND = (38, 34, 30, 255)
BOWL = (150, 98, 56, 255)
BOWL_EDGE = (78, 48, 24, 255)
STEW = (196, 120, 60, 255)
STEW_EDGE = (120, 64, 26, 255)
STEAM = (226, 214, 188, 255)
SUN = (246, 196, 60, 255)
SUN_EDGE = (150, 96, 20, 255)


def bowl_icon():
    size, s = 256, SCALE
    image = Image.new("RGBA", (size * s, size * s), BACKGROUND)
    draw = ImageDraw.Draw(image)
    # Three curls of steam.
    for x in (92, 128, 164):
        draw.arc([(x - 18) * s, 40 * s, (x + 18) * s, 84 * s], 90, 270, fill=STEAM, width=9 * s)
        draw.arc([(x - 18) * s, 76 * s, (x + 18) * s, 120 * s], 270, 90, fill=STEAM, width=9 * s)
    # Bowl: a half disc under a stew ellipse.
    draw.pieslice([40 * s, 80 * s, 216 * s, 236 * s], 0, 180, fill=BOWL, outline=BOWL_EDGE, width=6 * s)
    draw.ellipse([40 * s, 136 * s, 216 * s, 180 * s], fill=STEW, outline=STEW_EDGE, width=6 * s)
    draw.rectangle([96 * s, 226 * s, 160 * s, 238 * s], fill=BOWL_EDGE)
    image = image.resize((size, size), Image.LANCZOS)
    out = os.path.join(ROOT, "package", "icon.png")
    image.save(out)
    print("wrote", out, image.size)


def happy_icon():
    size, s = 64, SCALE
    image = Image.new("RGBA", (size * s, size * s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse([6 * s, 6 * s, 58 * s, 58 * s], fill=SUN, outline=SUN_EDGE, width=3 * s)
    draw.ellipse([21 * s, 22 * s, 27 * s, 30 * s], fill=SUN_EDGE)
    draw.ellipse([37 * s, 22 * s, 43 * s, 30 * s], fill=SUN_EDGE)
    draw.arc([18 * s, 26 * s, 46 * s, 48 * s], 20, 160, fill=SUN_EDGE, width=4 * s)
    image = image.resize((size, size), Image.LANCZOS)
    out = os.path.join(ROOT, "src", "Assets", "happy_icon.png")
    image.save(out)
    print("wrote", out, image.size)


bowl_icon()
happy_icon()
