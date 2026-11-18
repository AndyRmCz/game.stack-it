"""Draws the 1024x1024 app icons, Neon (the store icon) and Pop, with the drawing kit in
scripts/art.py.

    uv run python -m scripts.generate_app_icons

Writes unity/Assets/_Project/Art/Icons/AppIcon-Neon.png and AppIcon-Pop.png: Blocky, in glass,
looking up at the gold slab that landed a hair off on its head, in the look's own background
(Neon's night with stars, Pop's daybreak). Kept inside the platforms' round and rounded-square
masks.

Also writes Neon's two Android adaptive layers, AppIcon-Neon-Background.png (the night) and
AppIcon-Neon-Foreground.png (Blocky and the slab on transparent), for Player Settings > Android >
Icon > Adaptive: Android 8+ crops the icon to the launcher's own shape and only keeps its centre
whole, so the full icon there would lose Blocky's edges.
"""

import math
from pathlib import Path

from PIL import Image

from scripts import art

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / "unity" / "Assets" / "_Project" / "Art" / "Icons"
BLOCK, FOOT = 0.55 * SIZE, 0.88 * SIZE  # Blocky's width, and where it stands
SAFE = 66 / 108  # an adaptive icon's centre circle, the part every launcher mask shows whole


def icon(look: str) -> Image.Image:
    image = _ground(look, SIZE / 2, FOOT, BLOCK)
    art.blocky(image, SIZE / 2, FOOT, BLOCK, "wonder", glow=look == "neon", hat=True)
    return art.grain(image, 0.1).convert("RGB")


def adaptive(look: str) -> tuple[Image.Image, Image.Image]:
    """The icon as Android's two adaptive layers: the background, and Blocky with the slab on
    transparent, scaled and centred so all of it fits in the SAFE circle."""
    # Where Blocky and the slab end up, drawn once at the icon's size around (0, 0) = the foot.
    probe = Image.new("RGBA", (2 * SIZE, 2 * SIZE))
    art.blocky(probe, SIZE, SIZE, BLOCK, "wonder", glow=False, hat=True)
    x0, y0, x1, y1 = probe.getchannel("A").point(lambda v: 255 if v > 128 else 0).getbbox()
    x0, x1, y0, y1 = x0 - SIZE, x1 - SIZE, y0 - SIZE, y1 - SIZE
    # Everything is drawn in proportion to the block, so scaling it scales the box around the foot.
    k = 0.97 * (SAFE * SIZE / 2) / math.hypot((x1 - x0) / 2, (y1 - y0) / 2)
    block = BLOCK * k
    cx = SIZE / 2 - k * (x0 + x1) / 2
    foot = SIZE / 2 - k * (y0 + y1) / 2

    night = look == "neon"
    background = art.grain(_ground(look, cx, foot, block), 0.1).convert("RGB")
    figure = Image.new("RGBA", (SIZE, SIZE))
    art.blocky(figure, cx, foot, block, "wonder", glow=night, hat=True)
    foreground = art.grain(figure, 0.1)
    foreground.putalpha(figure.getchannel("A"))
    return background, foreground


def _ground(look: str, cx: float, foot: float, block: float) -> Image.Image:
    """The look's background, lit (Neon: by the glass) or shaded (Pop) where Blocky stands."""
    image = art.backdrop((SIZE, SIZE), look)
    night = look == "neon"
    art.ground_light(
        image,
        (cx, foot),
        block * 1.4,
        art.VIOLET if night else "#3A2A9C",
        140 if night else 70,
    )
    return image


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for look, name in (("neon", "AppIcon-Neon.png"), ("pop", "AppIcon-Pop.png")):
        path = OUT / name
        icon(look).save(path, optimize=True)
        print(f"wrote {path}")
    for layer, image in zip(("Background", "Foreground"), adaptive("neon"), strict=True):
        path = OUT / f"AppIcon-Neon-{layer}.png"
        image.save(path, optimize=True)
        print(f"wrote {path}")
