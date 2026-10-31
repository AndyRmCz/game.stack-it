"""Draws the 1024x1024 app icons, Neon (the store icon) and Pop, with the drawing kit in
scripts/art.py.

    uv run python -m scripts.generate_app_icons

Writes unity/Assets/_Project/Art/Icons/AppIcon-Neon.png and AppIcon-Pop.png: Blocky, the
mascot, balancing a gold slab that landed a hair off and looking up at it, on the daybreak
sky (dusk with stars for Neon, morning for Pop). Kept inside the platforms' round and
rounded-square masks.
"""

from pathlib import Path

from PIL import Image

from scripts import art

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / "unity" / "Assets" / "_Project" / "Art" / "Icons"
CORAL = "#FF6B5B"  # Blocky in the Pop look (accent2): the warmest of its two colours


def icon(look: str) -> Image.Image:
    night = look == "neon"
    image = art.sky((SIZE, SIZE), art.DUSK if night else art.MORNING, stars=night)
    ground = SIZE * 0.79
    art.hill(image, ground, art.HILL if night else "#B9A9D9")
    w = SIZE * 0.4
    cx = SIZE / 2 - w * 0.04
    art.blocky(
        image, cx, ground + SIZE * 0.02, w, (cx + w * 0.1, ground - w * 1.9), CORAL, hat=True
    )
    return art.grain(image).convert("RGB")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for look, name in (("neon", "AppIcon-Neon.png"), ("pop", "AppIcon-Pop.png")):
        path = OUT / name
        icon(look).save(path, optimize=True)
        print(f"wrote {path}")
