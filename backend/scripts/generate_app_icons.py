"""Draws the 1024x1024 app icons, Neon (the store icon) and Pop, with the drawing kit in
scripts/art.py.

    uv run python -m scripts.generate_app_icons

Writes unity/Assets/_Project/Art/Icons/AppIcon-Neon.png and AppIcon-Pop.png: Blocky, in glass,
looking up at the gold slab that landed a hair off on its head, in the look's own background
(Neon's night with stars, Pop's daybreak). Kept inside the platforms' round and rounded-square
masks.
"""

from pathlib import Path

from PIL import Image

from scripts import art

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / "unity" / "Assets" / "_Project" / "Art" / "Icons"
BLOCK, FOOT = 0.55 * SIZE, 0.88 * SIZE  # Blocky's width, and where it stands


def icon(look: str) -> Image.Image:
    image = art.backdrop((SIZE, SIZE), look)
    night = look == "neon"
    # Neon: the glass lights the ground. Pop: a violet shade under it.
    art.ground_light(
        image,
        (SIZE / 2, FOOT),
        BLOCK * 1.4,
        art.VIOLET if night else "#3A2A9C",
        140 if night else 70,
    )
    art.blocky(image, SIZE / 2, FOOT, BLOCK, "wonder", glow=night, hat=True)
    return art.grain(image, 0.1).convert("RGB")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for look, name in (("neon", "AppIcon-Neon.png"), ("pop", "AppIcon-Pop.png")):
        path = OUT / name
        icon(look).save(path, optimize=True)
        print(f"wrote {path}")
