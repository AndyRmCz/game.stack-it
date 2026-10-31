"""Store listing art for Milli, drawn with the kit in scripts/art.py.

    uv run python -m scripts.generate_store_art                 # logo, icon, feature graphic
    uv run python -m scripts.generate_store_art --screens DIR   # also frame phone screenshots
    uv run python -m scripts.generate_store_art --screens DIR --lang es

Writes to store/ at the repo root:
- logo-neon.png / logo-pop.png: the "milli" wordmark on transparent, for dark / light grounds.
  The i's are dotted with little blocks the size of a dot: one in the letters' colour, and a
  gold one landed a hair off (the game's "0.4 mm from perfect" moment).
- icon-512.png: the Play Store icon (the Neon app icon from scripts.generate_app_icons).
- feature-graphic.png: Play's 1024 x 500 feature graphic (no alpha, as Play requires): the
  tower at dusk, its gold slab landed a hair off, Blocky looking up at it.
- screenshots-<lang>/NN.png: with --screens, each phone screenshot in DIR (sorted by name) on a
  1080 x 1920 card under a caption. Play refuses screenshots longer than 2:1, and most phones
  now capture 20:9, so raw captures can't be uploaded as they are.
"""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from app.web import theme
from app.web.og import _font
from scripts import art
from scripts.generate_app_icons import CORAL, icon

OUT = Path(__file__).resolve().parents[2] / "store"
DOTLESS_I = chr(0x131)  # Fredoka's dotless i
WORD = f"m{DOTLESS_I}ll{DOTLESS_I}"  # the dots are drawn as blocks
CAPTIONS = {
    "en": [
        "One tap. Stack to the sky.",
        "Every millimetre counts.",
        "Duel friends with one link.",
        "Same tower for everyone, daily.",
        "Neon or Pop. Your look.",
    ],
    "es": [
        "Un toque. Apila hasta el cielo.",
        "Cada milímetro cuenta.",
        "Reta a tus amigos con un link.",
        "La misma torre para todos, cada día.",
        "Neon o Pop. Tu estilo.",
    ],
}


def wordmark(look: str, size: int) -> Image.Image:
    """The wordmark at a Fredoka size of `size` px, cropped to its ink and soft shadow."""
    ink = art.CREAM if look == "neon" else art.INK
    font = _font("Fredoka-Bold.ttf", size)
    pad = size // 2
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    x0, y0, x1, y1 = probe.textbbox((0, 0), WORD, font=font, anchor="ls")
    width, height = round(x1 - x0) + 2 * pad, round(y1 - y0) + 2 * pad
    origin = (pad - x0, height - pad - y1)  # baseline position
    image = Image.new("RGBA", (width, height))

    # A soft shadow below the letters, so the mark sits on any ground without a glow.
    shadow = Image.new("RGBA", image.size)
    drop = (origin[0], origin[1] + size * 0.05)
    ImageDraw.Draw(shadow).text(drop, WORD, font=font, fill=(30, 18, 60, 90), anchor="ls")
    image.alpha_composite(art.soft(shadow, size * 0.04))
    ImageDraw.Draw(image).text(origin, WORD, font=font, fill=art.rgba(ink), anchor="ls")

    # A block over each dotless i, the size of the dot it replaces.
    sx0, sy0, sx1, _ = probe.textbbox((0, 0), DOTLESS_I, font=font, anchor="ls")
    stem = sx1 - sx0
    side = stem * 1.12 / (2 * art.C30)  # iso width = 2 * side * cos30
    dots = ((1, art.faces(ink), 0.0), (4, art.gold(), 0.22))
    for i, colors, nudge in dots:
        left = probe.textlength(WORD[:i], font=font)
        cx = origin[0] + left + (sx0 + sx1) / 2
        bottom = origin[1] + sy0 - stem * 0.32  # the gap a dot leaves above the stem
        c = art.along_x((cx, bottom - side / 2), side * nudge)
        art.iso_box(image, c, side, side, side * 0.62, colors)
    return image.crop(image.getbbox())


def feature_graphic(lang: str = "en") -> Image.Image:
    width, height = 1024, 500
    image = art.sky((width, height), art.DUSK, stars=True)
    ground = 432
    art.hill(image, ground)
    ramp = theme.token("pop", "block.ramp")
    top = art.tower(image, (265, ground + 22), 136, ramp)
    art.blocky(image, 398, ground + 24, 62, top, CORAL)

    mark = wordmark("neon", 168)
    image.alpha_composite(mark, (int(722 - mark.width / 2), int(205 - mark.height / 2)))
    ImageDraw.Draw(image).text(
        (722, 352),
        CAPTIONS[lang][1],
        font=_font("Nunito-ExtraBold.ttf", 38),
        fill=art.rgba(art.PEACH),
        anchor="mm",
    )
    return art.grain(image).convert("RGB")


def _wrap(text: str, font: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    lines: list[str] = []
    for word in text.split():
        if lines and font.getlength(lines[-1] + " " + word) <= max_width:
            lines[-1] += " " + word
        else:
            lines.append(word)
    return lines


def framed_screenshot(shot: Image.Image, caption: str) -> Image.Image:
    """A phone screenshot under a caption on a 1080 x 1920 card (9:16, inside Play's 2:1)."""
    width, height = 1080, 1920
    image = art.grain(art.sky((width, height), art.DUSK, stars=True))
    font = _font("Fredoka-Bold.ttf", 84)
    y = 190
    for line in _wrap(caption, font, width - 160):
        ImageDraw.Draw(image).text(
            (width / 2, y), line, font=font, fill=art.rgba(art.CREAM), anchor="ms"
        )
        y += 100

    box_top, box_bottom, margin = y + 10, height - 70, 110
    scale = min((width - 2 * margin) / shot.width, (box_bottom - box_top) / shot.height)
    w, h = round(shot.width * scale), round(shot.height * scale)
    x, top = (width - w) // 2, box_bottom - h
    radius = round(w * 0.07)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius, fill=255)

    halo = Image.new("RGBA", image.size)
    ImageDraw.Draw(halo).rounded_rectangle(
        (x - 6, top - 6, x + w + 6, top + h + 6), radius + 6, fill=art.rgba(art.PEACH, 120)
    )
    image.alpha_composite(art.soft(halo, 18))
    image.paste(shot.convert("RGB").resize((w, h), Image.LANCZOS), (x, top), mask)
    return image.convert("RGB")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--screens", type=Path, help="folder of raw phone screenshots")
    parser.add_argument("--lang", choices=sorted(CAPTIONS), default="en")
    args = parser.parse_args()

    OUT.mkdir(exist_ok=True)
    for look in ("neon", "pop"):
        wordmark(look, 320).save(OUT / f"logo-{look}.png", optimize=True)
    icon("neon").resize((512, 512), Image.LANCZOS).save(OUT / "icon-512.png", optimize=True)
    feature_graphic(args.lang).save(OUT / "feature-graphic.png", optimize=True)
    if args.screens:
        shots = sorted(
            p for p in args.screens.iterdir() if p.suffix.lower() in (".png", ".jpg", ".jpeg")
        )
        folder = OUT / f"screenshots-{args.lang}"
        folder.mkdir(exist_ok=True)
        captions = CAPTIONS[args.lang]
        for n, path in enumerate(shots):
            framed = framed_screenshot(Image.open(path), captions[n % len(captions)])
            framed.save(folder / f"{n + 1:02d}.png", optimize=True)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
