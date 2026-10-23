"""Draws the 1024x1024 app icons, Neon (the store icon) and Pop, from the shared look tokens.

    uv run python -m scripts.generate_app_icons

Writes unity/Assets/_Project/Art/Icons/AppIcon-Neon.png and AppIcon-Pop.png: the look's
background gradient (and Neon's centre glow) behind the hero tower, the base block with the
moving block resting on it, as in the game.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

from app.web import theme
from app.web.og import _block

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / "unity" / "Assets" / "_Project" / "Art" / "Icons"


def icon(look: str) -> Image.Image:
    top, mid, bottom = (theme.rgba(c) for c in theme.token(look, "bg.gradient"))
    mid_at = float(theme.token(look, "bg.gradientMid"))
    column = Image.new("RGBA", (1, SIZE))
    for y in range(SIZE):
        f = y / (SIZE - 1)
        a, b, k = (
            (top, mid, f / mid_at) if f < mid_at else (mid, bottom, (f - mid_at) / (1 - mid_at))
        )
        column.putpixel((0, y), (*(round(a[i] + (b[i] - a[i]) * k) for i in range(3)), 255))
    image = column.resize((SIZE, SIZE))

    glow = theme.token(look, "bg.centerGlow")
    r, g, b, alpha = theme.rgba(glow["color"])
    if alpha:
        mask = Image.new("L", image.size, 0)
        radius = SIZE * glow["radius"]
        c = SIZE / 2
        ImageDraw.Draw(mask).ellipse((c - radius, c - radius, c + radius, c + radius), fill=alpha)
        mask = mask.filter(ImageFilter.GaussianBlur(radius * 0.45))
        image = Image.composite(Image.new("RGBA", image.size, (r, g, b, 255)), image, mask)

    # Sized to stay inside the platforms' rounded masks (~17% margin top and bottom).
    cx, centre, side = SIZE / 2, SIZE * 0.675, SIZE * 0.3
    base_height = side * 0.8
    shadow = Image.new("L", image.size, 0)
    foot = centre + side / 2
    shadow_alpha = theme.rgba(theme.token(look, "block.groundShadow"))[3] * 2
    ImageDraw.Draw(shadow).ellipse(
        (cx - side * 0.85, foot - 60, cx + side * 0.85, foot + 40), fill=min(255, shadow_alpha)
    )
    sr, sg, sb, _ = theme.rgba(theme.token(look, "block.groundShadow"))
    tint = Image.new("RGBA", image.size, (sr, sg, sb, 255))
    image.alpha_composite(
        Image.composite(
            tint, Image.new("RGBA", image.size), shadow.filter(ImageFilter.GaussianBlur(24))
        )
    )

    blocks = Image.new("RGBA", image.size)
    draw = ImageDraw.Draw(blocks)
    _block(draw, cx, centre, side, base_height, theme.token(look, "block.base"))
    _block(draw, cx + side * 0.12, centre - base_height - side * 0.07, side, side * 0.3,
           theme.token(look, "block.moving"))  # fmt: skip
    if theme.token(look, "block.emission"):
        image.alpha_composite(blocks.filter(ImageFilter.GaussianBlur(40)))
    image.alpha_composite(blocks)
    return image.convert("RGB")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for look, name in (("neon", "AppIcon-Neon.png"), ("pop", "AppIcon-Pop.png")):
        path = OUT / name
        icon(look).save(path, optimize=True)
        print(f"wrote {path}")
