"""Drawing kit for Milli's icon and store art (Pillow), shared by generate_app_icons,
generate_store_art and generate_screen_mockups.

The art is the game's Neon night (bg.* tokens) with Blocky in it: the ultraviolet glass mascot,
baked with the game's own faces by unity/Tools/blocky_stills.py into scripts/blocky/. Its story
is the gold slab (arcade.* tokens, the brand's "perfect" colour) landed a hair off on its head.
"""

import math
import random
from collections.abc import Sequence
from functools import cache
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from app.web import theme

Point = tuple[float, float]
RGBA = tuple[int, int, int, int]

CREAM = "#FFF4E8"
INK = "#1B2440"
LINE = "#2A1F45"  # plum ink for outlined boxes
VIOLET = "#7A55FA"  # the light inside Blocky's glass
C30 = math.cos(math.radians(30))
STILLS = Path(__file__).with_name("blocky")
PER_BLOCK, FOOT = 640, 0.04  # the stills' pixels per block width, and their foot above the bottom
HAT_OFF, HAT_SEAT = 0.13, 0.97  # the slab lands a hair right of centre, on top of the head


def rgba(color: str, alpha: int | None = None) -> RGBA:
    r, g, b, a = theme.rgba(color)
    return r, g, b, a if alpha is None else alpha


def shade(color: str, k: float) -> RGBA:
    r, g, b, a = theme.rgba(color)
    return round(r * k), round(g * k), round(b * k), a


def mix(a: str, b: str, t: float) -> RGBA:
    x, y = theme.rgba(a), theme.rgba(b)
    return tuple(round(x[i] + (y[i] - x[i]) * t) for i in range(4))  # type: ignore[return-value]


def gold() -> list[RGBA]:
    """Top, left and right faces of a gold block."""
    top = theme.token("neon", "arcade.goldTop")
    bottom = theme.token("neon", "arcade.goldBottom")
    return [rgba(top), rgba(bottom), shade(bottom, 0.8)]


def faces(top: str) -> list[RGBA]:
    """A block's faces from its top colour, shaded like the game (0.85 left, 0.70 right)."""
    return [rgba(top), shade(top, 0.85), shade(top, 0.70)]


def soft(layer: Image.Image, radius: float) -> Image.Image:
    """Blurs premultiplied, so a glow keeps its colour instead of fading to grey at the edges."""
    return layer.convert("RGBa").filter(ImageFilter.GaussianBlur(radius)).convert("RGBA")


def backdrop(size: tuple[int, int], look: str = "neon", seed: int = 7) -> Image.Image:
    """A look's background (bg.gradient, bg.centerGlow) and, at night, stars thinning downwards."""
    width, height = size
    top, mid, bottom = (theme.rgba(c) for c in theme.token(look, "bg.gradient"))
    mid_at = float(theme.token(look, "bg.gradientMid"))
    column = Image.new("RGBA", (1, height))
    for y in range(height):
        f = y / (height - 1)
        a, b, k = (
            (top, mid, f / mid_at) if f < mid_at else (mid, bottom, (f - mid_at) / (1 - mid_at))
        )
        k = k * k * (3 - 2 * k)  # smoothstep: no visible bands
        column.putpixel((0, y), tuple(round(a[i] + (b[i] - a[i]) * k) for i in range(4)))
    image = column.resize(size)

    glow = theme.token(look, "bg.centerGlow")
    color = theme.rgba(glow["color"])
    if color[3]:
        r = glow["radius"] * max(width, height)
        layer = Image.new("RGBA", size)
        cy = height * 0.58
        ImageDraw.Draw(layer).ellipse((width / 2 - r, cy - r, width / 2 + r, cy + r), fill=color)
        image.alpha_composite(soft(layer, r * 0.45))

    if look == "neon":  # the night look
        rng = random.Random(seed)
        draw = ImageDraw.Draw(image)
        unit = max(width, height) / 900
        for _ in range(int(width * height / 9000)):
            x, y = rng.uniform(0, width), rng.uniform(0, height * 0.6)
            fade = (1 - y / (height * 0.63)) * rng.uniform(0.35, 1)
            s = rng.choice((0.6, 0.8, 1.0, 1.0, 1.5)) * unit
            draw.ellipse((x - s, y - s, x + s, y + s), fill=rgba(CREAM, int(255 * fade)))
    return image


def ground_light(image: Image.Image, c: Point, width: float, color: str, alpha: int) -> None:
    """A flat pool of light (or, on a light ground, shade) where something stands."""
    layer = Image.new("RGBA", image.size)
    rx, ry = width / 2, width * 0.06
    ImageDraw.Draw(layer).ellipse(
        (c[0] - rx, c[1] - ry, c[0] + rx, c[1] + ry), fill=rgba(color, alpha)
    )
    image.alpha_composite(soft(layer, width * 0.035))


def grain(image: Image.Image, amount: float = 0.3) -> Image.Image:
    """Paper grain, so the flat fills feel printed rather than rendered."""
    noise = Image.effect_noise(image.size, 48).convert("RGB")
    base = image.convert("RGB")
    return Image.blend(base, ImageChops.soft_light(base, noise), amount).convert("RGBA")


def _rounded(
    polys: Sequence[tuple[Sequence[Point], RGBA]],
    round_px: float,
    line_px: float = 0.0,
    scale: int = 4,
) -> tuple[Image.Image, tuple[int, int]]:
    """Polygons drawn 4x, their silhouette's corners rounded and, with line_px, an ink outline
    like a drawn sticker. Scaled back; returns (image, origin)."""
    xs = [p[0] for pts, _ in polys for p in pts]
    ys = [p[1] for pts, _ in polys for p in pts]
    pad = 4 + math.ceil(line_px)
    x0, y0 = math.floor(min(xs)) - pad, math.floor(min(ys)) - pad
    w, h = math.ceil(max(xs)) + pad - x0, math.ceil(max(ys)) + pad - y0
    big = Image.new("RGBA", (w * scale, h * scale))
    draw = ImageDraw.Draw(big)
    for pts, color in polys:
        draw.polygon([((x - x0) * scale, (y - y0) * scale) for x, y in pts], fill=color)
    if round_px > 0:
        alpha = big.getchannel("A")
        rounded = alpha.filter(ImageFilter.GaussianBlur(round_px * scale)).point(
            lambda v: 255 if v >= 128 else 0
        )
        big.putalpha(ImageChops.darker(alpha, rounded))
    if line_px > 0:
        # Grow the silhouette by line_px: a straight edge blurred with sigma s stays above
        # 2.3% of full alpha out to about 2s, so blur with half the width and keep that.
        grown = (
            big.getchannel("A")
            .filter(ImageFilter.GaussianBlur(line_px * scale / 2))
            .point(lambda v: 255 if v >= 6 else 0)
        )
        outlined = Image.new("RGBA", big.size, rgba(LINE))
        outlined.putalpha(grown)
        outlined.alpha_composite(big)
        big = outlined
    return big.resize((w, h), Image.LANCZOS), (x0, y0)


def iso_box(
    image: Image.Image,
    c: Point,
    sx: float,
    sz: float,
    height: float,
    colors: Sequence[RGBA],
    round_px: float | None = None,
    line_px: float = 0.0,
) -> None:
    """An isometric box whose bottom face is centred at c, sx wide along x and sz along z."""
    ex = (C30 * sx / 2, -sx / 4)
    ez = (-C30 * sz / 2, -sz / 4)
    cx, cy = c
    front = (cx - ex[0] - ez[0], cy - ex[1] - ez[1])
    right = (cx + ex[0] - ez[0], cy + ex[1] - ez[1])
    left = (cx - ex[0] + ez[0], cy - ex[1] + ez[1])
    back = (cx + ex[0] + ez[0], cy + ex[1] + ez[1])

    def up(p: Point) -> Point:
        return p[0], p[1] - height

    top, lside, rside = colors
    polys = [
        ([left, front, up(front), up(left)], lside),
        ([front, right, up(right), up(front)], rside),
        ([up(front), up(right), up(back), up(left)], top),
    ]
    rounding = min(sx, sz, height) * 0.12 if round_px is None else round_px
    layer, origin = _rounded(polys, rounding, line_px)
    image.alpha_composite(layer, origin)


def along_x(c: Point, d: float) -> Point:
    """c moved d along the iso x axis (right and up on screen)."""
    return c[0] + d * C30, c[1] - d / 2


def toward_viewer(c: Point, d: float) -> Point:
    """c moved d along -z (right and down on screen), the side of the visible right face."""
    return c[0] + d * C30, c[1] + d / 2


@cache
def _still(name: str) -> Image.Image:
    return Image.open(STILLS / f"{name}.png").convert("RGBA")


def _place(image: Image.Image, sprite: Image.Image, x: float, y: float, glow: str | None) -> None:
    """Pastes sprite with its top-left at (x, y), over a soft glow of its shape in glow's colour."""
    if glow:
        radius = sprite.width * 0.07
        pad = math.ceil(radius * 3)
        halo = Image.new("RGBA", (sprite.width + 2 * pad, sprite.height + 2 * pad), rgba(glow))
        shape = Image.new("L", halo.size)
        shape.paste(sprite.getchannel("A"), (pad, pad))
        halo.putalpha(shape.filter(ImageFilter.GaussianBlur(radius)).point(lambda v: v * 7 // 10))
        image.alpha_composite(halo, (round(x) - pad, round(y) - pad))
    image.alpha_composite(sprite, (round(x), round(y)))


def _scaled(name: str, block: float) -> Image.Image:
    still = _still(name)
    k = block / PER_BLOCK
    return still.resize((round(still.width * k), round(still.height * k)), Image.LANCZOS)


def slab(
    image: Image.Image, cx: float, foot: float, block: float, glow: bool = True, tall: float = 1
) -> None:
    """The gold slab, block wide, its foot's centre at (cx, foot); tall stretches it upwards."""
    sprite = _scaled("slab", block)
    if tall != 1:
        sprite = sprite.resize((sprite.width, round(sprite.height * tall)), Image.LANCZOS)
    y = foot + FOOT * block - sprite.height
    _place(image, sprite, cx - sprite.width / 2, y, "#FFBE3C" if glow else None)


def blocky(
    image: Image.Image,
    cx: float,
    foot: float,
    block: float,
    pose: str,
    glow: bool = True,
    hat: bool = False,
) -> None:
    """Blocky, block pixels wide, standing with its foot's centre at (cx, foot): a pose baked in
    scripts/blocky. With hat, wearing the gold slab, landed a hair off."""
    sprite = _scaled(pose, block)
    _place(
        image,
        sprite,
        cx - sprite.width / 2,
        foot + FOOT * block - sprite.height,
        VIOLET if glow else None,
    )
    if hat:
        slab(image, cx + HAT_OFF * block, foot - HAT_SEAT * block, block, glow)
