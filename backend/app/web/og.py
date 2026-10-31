"""Open Graph preview images (1200 x 630) for challenge links, in the Neon look: the background
gradient and centre glow, the hero tower, the score and "Beat my score". Rendered with Pillow
from the shared tokens, cached per (score, language)."""

import math
from functools import lru_cache
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from app.web import theme
from app.web.i18n import Lang, t

WIDTH, HEIGHT = 1200, 630
LOOK = "neon"
FONTS = Path(__file__).with_name("fonts")


def _font(name: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / name), size)


def _rgb(name: str) -> tuple[int, int, int, int]:
    return theme.rgba(theme.token(LOOK, name))


def _background() -> Image.Image:
    top, mid, bottom = (theme.rgba(c) for c in theme.token(LOOK, "bg.gradient"))
    mid_at = float(theme.token(LOOK, "bg.gradientMid"))
    column = Image.new("RGBA", (1, HEIGHT))
    for y in range(HEIGHT):
        f = y / (HEIGHT - 1)
        a, b, k = (
            (top, mid, f / mid_at) if f < mid_at else (mid, bottom, (f - mid_at) / (1 - mid_at))
        )
        column.putpixel((0, y), (*(round(a[i] + (b[i] - a[i]) * k) for i in range(3)), 255))
    image = column.resize((WIDTH, HEIGHT))
    glow = theme.token(LOOK, "bg.centerGlow")
    r, g, b, alpha = theme.rgba(glow["color"])
    if alpha:
        radius = glow["radius"] * WIDTH * 0.6
        mask = Image.new("L", (WIDTH, HEIGHT), 0)
        cx, cy = 330, HEIGHT * 0.52
        ImageDraw.Draw(mask).ellipse(
            (cx - radius, cy - radius, cx + radius, cy + radius), fill=alpha
        )
        mask = mask.filter(ImageFilter.GaussianBlur(radius * 0.45))
        image = Image.composite(Image.new("RGBA", image.size, (r, g, b, 255)), image, mask)
    return image


def _block(
    draw: ImageDraw.ImageDraw,
    cx: float,
    base_y: float,
    side: float,
    height: float,
    faces: list[str],
) -> None:
    """One isometric block whose bottom face is centred at (cx, base_y)."""
    c30 = math.cos(math.radians(30))
    ex = (c30 * side / 2, -side / 4)  # +x: right and up (screen y grows down)
    ez = (-c30 * side / 2, -side / 4)  # +z: left and up
    front = (cx - ex[0] - ez[0], base_y - ex[1] - ez[1])
    right = (cx + ex[0] - ez[0], base_y + ex[1] - ez[1])
    left = (cx - ex[0] + ez[0], base_y - ex[1] + ez[1])
    back = (cx + ex[0] + ez[0], base_y + ex[1] + ez[1])

    def up(p: tuple[float, float]) -> tuple[float, float]:
        return p[0], p[1] - height

    top, lside, rside = (theme.rgba(c) for c in faces)
    draw.polygon([left, front, up(front), up(left)], fill=lside)
    draw.polygon([front, right, up(right), up(front)], fill=rside)
    draw.polygon([up(front), up(right), up(back), up(left)], fill=top)


def _tower(image: Image.Image) -> None:
    # (cx, centre) is the base's bottom-face centre; its front corner sits half a side lower.
    cx, centre, side = 320.0, 455.0, 190.0
    base_height = side * 0.9
    foot = centre + side / 2
    shadow = Image.new("L", image.size, 0)
    alpha = _rgb("block.groundShadow")[3]
    ImageDraw.Draw(shadow).ellipse((cx - 175, foot - 40, cx + 175, foot + 22), fill=alpha)
    shadow = shadow.filter(ImageFilter.GaussianBlur(14))
    r, g, b, _ = _rgb("block.groundShadow")
    tint = Image.new("RGBA", image.size, (r, g, b, 255))
    image.alpha_composite(Image.composite(tint, Image.new("RGBA", image.size), shadow))

    # The blocks, plus a soft halo of themselves so the Neon glow reads in the preview too.
    blocks = Image.new("RGBA", image.size)
    draw = ImageDraw.Draw(blocks)
    _block(draw, cx, centre, side, base_height, theme.token(LOOK, "block.base"))
    # The moving block rests on the base's top, slid a little along x (mid-swing).
    _block(
        draw,
        cx + 30,
        centre - base_height - 17,
        side,
        side * 0.3,
        theme.token(LOOK, "block.moving"),
    )
    image.alpha_composite(blocks.filter(ImageFilter.GaussianBlur(28)))
    image.alpha_composite(blocks)


def _glowing_text(
    image: Image.Image,
    xy: tuple[float, float],
    text: str,
    font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int, int],
    anchor: str = "ls",
) -> None:
    glow = theme.token(LOOK, "text.logoGlow")
    r, g, b, a = theme.rgba(glow["color"])
    if a:
        layer = Image.new("RGBA", image.size)
        ImageDraw.Draw(layer).text(xy, text, font=font, fill=(r, g, b, a), anchor=anchor)
        image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(glow["blur"] * 0.6)))
    ImageDraw.Draw(image).text(xy, text, font=font, fill=fill, anchor=anchor)


@lru_cache(maxsize=256)
def render(score: int | None, lang: Lang) -> bytes:
    image = _background()
    _tower(image)
    primary = _rgb("text.primary")
    secondary = _rgb("text.secondary")
    x = 640
    _glowing_text(image, (x, 170), "milli", _font("Fredoka-Bold.ttf", 64), primary)
    if score is None:
        _glowing_text(
            image, (x, 360), t(lang, "og_generic"), _font("Fredoka-Bold.ttf", 76), primary
        )
    else:
        _glowing_text(image, (x, 420), str(score), _font("Fredoka-Bold.ttf", 230), primary)
        overline = t(lang, "og_beat").upper()
        ImageDraw.Draw(image).text(
            (x + 6, 500),
            overline,
            font=_font("Nunito-ExtraBold.ttf", 38),
            fill=secondary,
            anchor="ls",
        )
    buffer = BytesIO()
    image.convert("RGB").save(buffer, "PNG", optimize=True)
    return buffer.getvalue()
