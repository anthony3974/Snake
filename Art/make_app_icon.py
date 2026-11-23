#!/usr/bin/env python3
"""Draws the Android app icon: a classic snake on a checkered grass board, heading for a berry.

Colours match the "meadow" theme in Assets/Scripts/Visuals/Theme.cs. Writes, into Assets/Icons/:
  AppIcon_Background.png  adaptive icon background layer (432 px, 108 dp at 4x)
  AppIcon_Foreground.png  adaptive icon foreground layer (432 px, transparent)
  AppIcon.png             legacy icon: both layers on a rounded square (512 px)
  AppIcon_Round.png       round icon: both layers in a circle (512 px)

Assets/Editor/SnakeAndroidBuild.cs puts them into Player Settings. Needs Pillow.

    python3 Art/make_app_icon.py [--preview out.png]
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "Icons")
LAYER = 432
SS = 2


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


BOARD_LIGHT = hex_rgb("#AAD751")
BOARD_DARK = hex_rgb("#A0CB4C")
HEAD = hex_rgb("#4674E9")
TAIL = hex_rgb("#355BC2")
BERRY = hex_rgb("#E53935")
LEAF = hex_rgb("#43A047")
PUPIL = hex_rgb("#141920")

CELL = 40
ORIGIN = 96  # a 6x6 grid of 40 px cells, centred in the 432 px layer
# Tail to head, as (column, row): along the bottom, up the right, back along the middle, up and
# right again toward the berry.
PATH = [(1, 4), (2, 4), (3, 4), (4, 4), (4, 3), (4, 2), (3, 2), (2, 2), (1, 2), (1, 1), (2, 1)]
BERRY_CELL = (4, 1)


def centre(cell, s):
    c, r = cell
    return ((ORIGIN + CELL * c + CELL / 2) * s, (ORIGIN + CELL * r + CELL / 2) * s)


def mix(a, b, t):
    return tuple(int(x + (y - x) * t) for x, y in zip(a, b))


def background(size):
    s = size / LAYER
    img = Image.new("RGBA", (size, size), BOARD_LIGHT + (255,))
    d = ImageDraw.Draw(img)
    for r in range(-3, 10):
        for c in range(-3, 10):
            if (r + c) & 1:
                x0 = int((ORIGIN + CELL * c) * s)
                y0 = int((ORIGIN + CELL * r) * s)
                x1 = int((ORIGIN + CELL * (c + 1)) * s)
                y1 = int((ORIGIN + CELL * (r + 1)) * s)
                d.rectangle([x0, y0, x1, y1], fill=BOARD_DARK + (255,))
    return img


def foreground(size):
    s = size / LAYER
    snake = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(snake)
    width = int(30 * s)
    n = len(PATH) - 1
    for i in range(n):
        colour = mix(TAIL, HEAD, (i + 0.5) / n) + (255,)
        a, b = centre(PATH[i], s), centre(PATH[i + 1], s)
        d.line([a, b], fill=colour, width=width)
        d.ellipse([a[0] - width / 2, a[1] - width / 2, a[0] + width / 2, a[1] + width / 2], fill=colour)
    # Head: a little bigger, eyes looking right at the berry.
    hx, hy = centre(PATH[-1], s)
    r = 19 * s
    d.ellipse([hx - r, hy - r, hx + r, hy + r], fill=HEAD + (255,))
    for dy in (-8, 8):
        ex, ey = hx + 6 * s, hy + dy * s
        d.ellipse([ex - 6 * s, ey - 6 * s, ex + 6 * s, ey + 6 * s], fill=(255, 255, 255, 255))
        d.ellipse([ex - 1 * s, ey - 3.2 * s, ex + 5.4 * s, ey + 3.2 * s], fill=PUPIL + (255,))

    # Berry with a leaf.
    bx, by = centre(BERRY_CELL, s)
    br = 15 * s
    berry = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bd = ImageDraw.Draw(berry)
    bd.ellipse([bx - br, by - br + 3 * s, bx + br, by + br + 3 * s], fill=BERRY + (255,))
    bd.ellipse([bx - br * 0.45, by - br * 0.35, bx - br * 0.05, by + br * 0.05], fill=(255, 175, 175, 255))
    bd.polygon([(bx, by - br + 4 * s), (bx + 12 * s, by - br - 8 * s), (bx + 2 * s, by - br - 4 * s)], fill=LEAF + (255,))
    bd.line([(bx, by - br + 4 * s), (bx - 2 * s, by - br - 6 * s)], fill=LEAF + (255,), width=int(3 * s))

    # Subtle matte drop shadow behind both (no neon glow).
    both = snake.copy()
    both.alpha_composite(berry)
    alpha = both.split()[3]
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow_layer = Image.new("RGBA", (size, size), (0, 0, 0, 52))
    shadow.paste(shadow_layer, (0, int(4 * s)), alpha)
    shadow = shadow.filter(ImageFilter.GaussianBlur(3 * s))

    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    img.alpha_composite(shadow)
    img.alpha_composite(both)
    return img


def masked(layer_bg, layer_fg, size, shape):
    """Both layers cropped to the middle 72 dp, scaled to [size], in a rounded square or circle."""
    full = layer_bg.copy()
    full.alpha_composite(layer_fg)
    side = full.width
    crop = side * 18 // 108
    full = full.crop((crop, crop, side - crop, side - crop)).resize((size, size), Image.LANCZOS)
    mask = Image.new("L", (size * SS, size * SS), 0)
    d = ImageDraw.Draw(mask)
    if shape == "circle":
        d.ellipse([0, 0, size * SS - 1, size * SS - 1], fill=255)
    else:
        d.rounded_rectangle([0, 0, size * SS - 1, size * SS - 1], radius=int(size * SS * 0.22), fill=255)
    full.putalpha(mask.resize((size, size), Image.LANCZOS))
    return full


def main():
    bg_big = background(LAYER * SS)
    fg_big = foreground(LAYER * SS)
    bg = bg_big.resize((LAYER, LAYER), Image.LANCZOS)
    fg = fg_big.resize((LAYER, LAYER), Image.LANCZOS)
    if "--preview" in sys.argv:
        out = sys.argv[sys.argv.index("--preview") + 1]
        masked(bg, fg, 288, "circle").save(out)
        print("preview:", out)
        return
    os.makedirs(OUT, exist_ok=True)
    bg.save(os.path.join(OUT, "AppIcon_Background.png"))
    fg.save(os.path.join(OUT, "AppIcon_Foreground.png"))
    masked(bg_big, fg_big, 512, "square").save(os.path.join(OUT, "AppIcon.png"))
    masked(bg_big, fg_big, 512, "circle").save(os.path.join(OUT, "AppIcon_Round.png"))
    print("wrote", ", ".join(sorted(f for f in os.listdir(OUT) if f.endswith(".png"))))


if __name__ == "__main__":
    main()
