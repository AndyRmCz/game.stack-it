"""Store listing art for Milli, drawn with the kit in scripts/art.py.

    uv run python -m scripts.generate_store_art                 # logo, icon, feature graphic
    uv run python -m scripts.generate_store_art --screens DIR   # also frame phone screenshots
    uv run python -m scripts.generate_store_art --screens DIR --lang es
    uv run python -m scripts.generate_store_art --screens DIR --app-store   # Apple's exact size

Writes to store/ at the repo root:
- logo-neon.png / logo-pop.png: the "milli" wordmark on transparent, for dark / light grounds.
  Blocky dots the first i, and the gold slab dots the second, landed a hair off (the game's
  "0.4 mm from perfect" moment).
- icon-512.png: the Play Store icon (the Neon app icon from scripts.generate_app_icons).
- feature-graphic.png: Play's 1024 x 500 feature graphic (no alpha, as Play requires): Blocky
  in the Neon night, looking up at the gold slab on its head, beside the wordmark.
- screenshots-<lang>/NN.png: with --screens, each phone screenshot in DIR (sorted by name) on a
  1080 x 1920 card under a caption, with Blocky sitting on the phone, reacting to it. Play
  refuses screenshots longer than 2:1, and most phones now capture 20:9, so raw captures can't
  be uploaded as they are.
- screenshots-ios-<lang>/NN.png: with --app-store, the same cards at 1320 x 2868, the size App
  Store Connect takes for iPhones (it accepts exact sizes only).
"""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from app.web import theme
from app.web.og import _font
from scripts import art
from scripts.generate_app_icons import icon

OUT = Path(__file__).resolve().parents[2] / "store"
DOTLESS_I = chr(0x131)  # the display font's dotless i
PLAY_CARD = (1080, 1920)
APP_STORE_CARD = (1320, 2868)
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
# Blocky's reaction to each caption, a pose baked in scripts/blocky.
POSES = ("cheer-up", "nervous", "smug-right", "happy", "joy")
LOOKS_RIGHT = {"nervous", "smug-right"}  # these sit on the left, looking at the phone


def wordmark(look: str, size: int) -> Image.Image:
    """The wordmark at a Sora size of `size` px, cropped to its ink and soft shadow."""
    ink = art.CREAM if look == "neon" else art.INK
    font = _font("Sora-Bold.ttf", size)
    pad = size // 2
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    x0, y0, x1, y1 = probe.textbbox((0, 0), WORD, font=font, anchor="ls")
    width, height = round(x1 - x0) + 2 * pad, round(y1 - y0) + 2 * pad
    origin = (pad - x0, height - pad - y1)  # baseline position
    image = Image.new("RGBA", (width, height))

    # A soft shadow below the letters, so the mark sits on any ground; violet light at night.
    shadow = Image.new("RGBA", image.size)
    night = look == "neon"
    drop = (origin[0], origin[1] + (0 if night else size * 0.05))
    shade = art.rgba(art.VIOLET, 150) if night else (30, 18, 60, 90)
    ImageDraw.Draw(shadow).text(drop, WORD, font=font, fill=shade, anchor="ls")
    image.alpha_composite(art.soft(shadow, size * (0.06 if night else 0.04)))
    ImageDraw.Draw(image).text(origin, WORD, font=font, fill=art.rgba(ink), anchor="ls")

    # Blocky dots the first i; the gold slab dots the second, landed a hair off.
    sx0, sy0, sx1, _ = probe.textbbox((0, 0), DOTLESS_I, font=font, anchor="ls")
    stem = sx1 - sx0
    foot = origin[1] + sy0 - stem * 0.2  # the gap a dot leaves above the stem
    for i in (1, 4):
        cx = origin[0] + probe.textlength(WORD[:i], font=font) + (sx0 + sx1) / 2
        if i == 1:
            art.blocky(image, cx, foot, stem * 1.12, "happy", glow=night)
        else:
            art.slab(image, cx + stem * 0.2, foot, stem * 1.1, glow=False, tall=2.2)
    return image.crop(image.getbbox())


def feature_graphic(lang: str = "en") -> Image.Image:
    width, height = 1024, 500
    image = art.backdrop((width, height))
    cx, foot, block = 250, 452, 250
    art.ground_light(image, (cx, foot), block * 1.5, art.VIOLET, 150)
    art.blocky(image, cx, foot, block, "wonder", hat=True)

    mark = wordmark("neon", 150)
    image.alpha_composite(mark, (int(700 - mark.width / 2), int(215 - mark.height / 2)))
    ImageDraw.Draw(image).text(
        (700, 360),
        CAPTIONS[lang][1],
        font=_font("LexendDeca-SemiBold.ttf", 36),
        fill=art.rgba(theme.token("neon", "text.secondary")),
        anchor="mm",
    )
    return art.grain(image, 0.1).convert("RGB")


def _wrap(text: str, font: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    """One line if it fits, else two of about the same length, so no word is left alone."""
    words = text.split()
    if font.getlength(text) <= max_width or len(words) < 2:
        return [text]

    def longer(cut: int) -> float:
        return max(font.getlength(" ".join(words[:cut])), font.getlength(" ".join(words[cut:])))

    cut = min(range(1, len(words)), key=longer)
    return [" ".join(words[:cut]), " ".join(words[cut:])]


def framed_screenshot(
    shot: Image.Image,
    caption: str,
    size: tuple[int, int] = PLAY_CARD,
    pose: str | None = None,
) -> Image.Image:
    """A phone screenshot under a caption on a card: 9:16 by default, inside Play's 2:1. With
    pose, Blocky sits on the phone's top edge, reacting to the caption."""
    width, height = size
    k = width / PLAY_CARD[0]  # the layout is drawn for Play's card
    image = art.backdrop((width, height), seed=5)
    font = _font("Sora-Bold.ttf", round(88 * k))
    y = round(230 * k)
    for line in _wrap(caption, font, width - round(200 * k)):
        ImageDraw.Draw(image).text(
            (width / 2, y), line, font=font, fill=art.rgba(art.CREAM), anchor="ms"
        )
        y += round(104 * k)

    box_top, box_bottom, margin = y + round(120 * k), height - round(60 * k), round(150 * k)
    scale = min((width - 2 * margin) / shot.width, (box_bottom - box_top) / shot.height)
    w, h = round(shot.width * scale), round(shot.height * scale)
    x, top = (width - w) // 2, box_bottom - h
    radius = round(w * 0.07)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius, fill=255)

    halo = Image.new("RGBA", image.size)
    ImageDraw.Draw(halo).rounded_rectangle(
        (x - round(6 * k), top - round(6 * k), x + w + round(6 * k), top + h + round(6 * k)),
        radius + round(6 * k),
        fill=theme.rgba(theme.token("neon", "accent.glow")["color"]),
    )
    image.alpha_composite(art.soft(halo, round(22 * k)))
    image.paste(shot.convert("RGB").resize((w, h), Image.LANCZOS), (x, top), mask)
    if pose:
        block = 190 * k
        cx = x + block * 0.45 if pose in LOOKS_RIGHT else x + w - block * 0.45
        art.blocky(image, cx, top + block * 0.04, block, pose)
    return art.grain(image, 0.1).convert("RGB")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--screens", type=Path, help="folder of raw phone screenshots")
    parser.add_argument("--lang", choices=sorted(CAPTIONS), default="en")
    parser.add_argument("--app-store", action="store_true", help="cards for App Store Connect")
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
        folder = OUT / f"screenshots-{'ios-' if args.app_store else ''}{args.lang}"
        size = APP_STORE_CARD if args.app_store else PLAY_CARD
        folder.mkdir(exist_ok=True)
        captions = CAPTIONS[args.lang]
        for n, path in enumerate(shots):
            i = n % len(captions)
            framed = framed_screenshot(Image.open(path), captions[i], size, POSES[i])
            framed.save(folder / f"{n + 1:02d}.png", optimize=True)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
