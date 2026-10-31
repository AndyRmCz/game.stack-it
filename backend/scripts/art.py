"""Drawing kit for Milli's icon and store art (Pillow), shared by generate_app_icons and
generate_store_art.

The art tells the game's own story: the "daybreak" sky the tower climbs through (dusk into
night, Palette.SkyKeys in Unity), soft pastel blocks, and Blocky, the mascot who stands at the
foot of the tower and looks up, here at a gold slab that landed a hair off. Gold (arcade.*
tokens) is the brand's "perfect" colour; a plum ink line makes it all read as drawn.
"""

import math
import random
from collections.abc import Sequence

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from app.web import theme

Point = tuple[float, float]
RGBA = tuple[int, int, int, int]

# The daybreak sky's dusk moment (night above, ember at the horizon), and its morning one.
DUSK = ("#1E2346", "#8E6FB5", "#FFB59A")
MORNING = ("#C9C3F0", "#DCCBE0", "#FFDCCB")
HILL = "#2E2450"
CREAM = "#FFF4E8"
PEACH = "#FFE0CF"
INK = "#1B2440"
BLUSH = "#FF9AA8"
LINE = "#2A1F45"  # plum ink, the sunset sky's last moment
C30 = math.cos(math.radians(30))


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


def sky(size: tuple[int, int], stops: Sequence[str], stars: bool, seed: int = 7) -> Image.Image:
    """Vertical three-stop sky, the horizon's warm glow, and stars thinning towards it."""
    width, height = size
    top, mid, bottom = (theme.rgba(c) for c in stops)
    column = Image.new("RGBA", (1, height))
    for y in range(height):
        f = y / (height - 1)
        a, b, k = (top, mid, f / 0.55) if f < 0.55 else (mid, bottom, (f - 0.55) / 0.45)
        k = k * k * (3 - 2 * k)  # smoothstep: no visible bands
        column.putpixel((0, y), tuple(round(a[i] + (b[i] - a[i]) * k) for i in range(4)))
    image = column.resize(size)

    glow = Image.new("RGBA", size)
    r = max(width, height) * 0.55
    ImageDraw.Draw(glow).ellipse(
        (width / 2 - r, height - r * 0.35, width / 2 + r, height + r * 0.35),
        fill=rgba(stops[2], 150),
    )
    image.alpha_composite(soft(glow, r * 0.3))

    if stars:
        rng = random.Random(seed)
        draw = ImageDraw.Draw(image)
        unit = min(width, height) / 500
        for _ in range(int(width * height / 2600)):
            x, y = (
                rng.uniform(0, width),
                rng.uniform(0, height * 0.6) ** 1.15 / (height * 0.6) ** 0.15,
            )
            fade = 1 - y / (height * 0.62)
            s = rng.choice((0.6, 0.8, 1.0, 1.0, 1.4)) * unit
            draw.ellipse(
                (x - s, y - s, x + s, y + s),
                fill=rgba(CREAM, int(255 * fade * rng.uniform(0.4, 1))),
            )
        for _ in range(3):  # a few that twinkle
            x, y = rng.uniform(width * 0.05, width * 0.95), rng.uniform(height * 0.04, height * 0.3)
            long, thin = 7 * unit, 1.1 * unit
            draw.ellipse((x - long, y - thin, x + long, y + thin), fill=rgba(CREAM, 220))
            draw.ellipse((x - thin, y - long, x + thin, y + long), fill=rgba(CREAM, 220))
    return image


def hill(image: Image.Image, top: float, color: str = HILL) -> None:
    """A gentle rolling ground across the bottom, so the tower stands somewhere."""
    width, height = image.size
    points = [(0.0, float(height))]
    for i in range(41):
        x = width * i / 40
        points.append((x, top + math.sin(i / 40 * math.pi * 2.2 + 0.6) * height * 0.012))
    points.append((float(width), float(height)))
    layer = Image.new("RGBA", image.size)
    ImageDraw.Draw(layer).polygon(points, fill=rgba(color))
    image.alpha_composite(layer)


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


def contact_shadow(image: Image.Image, c: Point, width: float, alpha: int = 90) -> None:
    layer = Image.new("RGBA", image.size)
    ImageDraw.Draw(layer).ellipse(
        (c[0] - width / 2, c[1] - width * 0.12, c[0] + width / 2, c[1] + width * 0.12),
        fill=(20, 12, 40, alpha),
    )
    image.alpha_composite(soft(layer, width * 0.06))


def tower(
    image: Image.Image, foot: Point, side: float, ramp: Sequence[str], slabs: int = 3
) -> Point:
    """Base block, pastel slabs up the ramp, then the gold slab landed a hair off.
    Returns the gold slab's top centre (where Blocky looks)."""
    base_h, slab_h = side * 0.72, side * 0.24
    line = side * 0.022
    contact_shadow(image, (foot[0], foot[1] - side * 0.02), side * 1.9)
    c = (foot[0], foot[1] - side / 2)  # bottom-face centre of the base
    iso_box(image, c, side, side, base_h, faces(ramp[0]), line_px=line)
    c = (c[0], c[1] - base_h)
    for i in range(slabs):
        iso_box(image, c, side, side, slab_h, faces(ramp[(i + 1) % len(ramp)]), line_px=line)
        c = (c[0], c[1] - slab_h)

    # The near miss: the gold slab landed a hair toward the viewer, its lip over the edge.
    iso_box(image, toward_viewer(c, side * 0.09), side, side, slab_h, gold(), line_px=line)
    return c[0], c[1] - slab_h - side * 0.25


def blocky(
    image: Image.Image,
    cx: float,
    bottom: float,
    w: float,
    look_at: Point,
    color: str,
    hat: bool = False,
) -> None:
    """The mascot, front on: a soft coral block with big eyes, a little "o" mouth and blush,
    looking at look_at. With hat, a gold slab balanced on its head, a hair off to the right."""
    # Seen as in the game: the front face square on, the top face foreshortened above it.
    contact_shadow(image, (cx, bottom), w * 1.25, 110)
    band, line = w * 0.3, w * 0.03
    x0, x1, y0 = cx - w / 2, cx + w / 2, bottom - w * 0.9
    top_face = [(x0, y0 - band), (x1, y0 - band), (x1, y0 + 1), (x0, y0 + 1)]
    body = [(x0, y0), (x1, y0), (x1, bottom), (x0, bottom)]
    layer, origin = _rounded(
        [(top_face, mix(color, CREAM, 0.35)), (body, rgba(color))], w * 0.08, line
    )
    image.alpha_composite(layer, origin)

    draw = ImageDraw.Draw(image)
    dx, dy = look_at[0] - cx, look_at[1] - (y0 + w * 0.4)
    n = math.hypot(dx, dy) or 1
    lx, ly = dx / n, dy / n
    for side in (-1, 1):
        ex, ey = cx + side * w * 0.18, y0 + w * 0.42
        rx, ry = w * 0.12, w * 0.15
        draw.ellipse((ex - rx, ey - ry, ex + rx, ey + ry), fill=rgba("#FFFFFF"))
        px, py, pr = ex + lx * rx * 0.42, ey + ly * ry * 0.45, w * 0.065
        draw.ellipse((px - pr, py - pr, px + pr, py + pr), fill=rgba("#1A1A24"))
        hr = pr * 0.38
        draw.ellipse(
            (px - pr * 0.45 - hr, py - pr * 0.45 - hr, px - pr * 0.45 + hr, py - pr * 0.45 + hr),
            fill=rgba("#FFFFFF"),
        )
    blush = Image.new("RGBA", image.size)
    for side in (-1, 1):
        bx, by = cx + side * w * 0.31, y0 + w * 0.66
        ImageDraw.Draw(blush).ellipse(
            (bx - w * 0.08, by - w * 0.045, bx + w * 0.08, by + w * 0.045), fill=rgba(BLUSH, 150)
        )
    image.alpha_composite(soft(blush, w * 0.02))
    mx, my = cx + lx * w * 0.03, y0 + w * 0.7
    draw.ellipse((mx - w * 0.04, my - w * 0.05, mx + w * 0.04, my + w * 0.05), fill=rgba("#5A1F2A"))

    if hat:
        # Same footprint as the head, landed a hair to the right: a strip of head shows left.
        off, hh = w * 0.09, w * 0.2
        hx0, hx1 = x0 + off, x1 + off
        g = gold()
        hat_front = [(hx0, y0 - hh), (hx1, y0 - hh), (hx1, y0), (hx0, y0)]
        hat_top = [
            (hx0, y0 - hh - band),
            (hx1, y0 - hh - band),
            (hx1, y0 - hh + 1),
            (hx0, y0 - hh + 1),
        ]
        layer, origin = _rounded([(hat_top, g[0]), (hat_front, g[1])], w * 0.05, line)
        image.alpha_composite(layer, origin)
