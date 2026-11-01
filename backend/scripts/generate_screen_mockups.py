"""Mockups of Milli's screens for the store listing, drawn from the game's own layout (Ui
constants and screens in unity/.../Scripts/UI), look tokens, strings and fonts.

    uv run python -m scripts.generate_screen_mockups            # English
    uv run python -m scripts.generate_screen_mockups --lang es  # Spanish

Writes store/mockups/NN.png (the raw 1170 x 2532 screens, 390 x 844 points at 3x) and frames
each one under its caption in store/screenshots-<lang>/NN.png.

These are illustrations, not captures: replace them with real phone screenshots (framed by
scripts.generate_store_art --screens) before the production listing, as Play asks for
screenshots that show the actual game.
"""

import argparse
from collections.abc import Callable
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from app.web import theme
from scripts import art
from scripts.generate_store_art import OUT, framed_screenshot

ROOT = Path(__file__).resolve().parents[2]
FONTS = ROOT / "unity" / "Assets" / "_Project" / "Fonts"
K = 3  # pixels per point
W, H = 390, 844  # the UI's reference size (Ui.RefWidth / RefHeight)
SAFE_TOP, SAFE_BOTTOM = 47, 34
SIDE, TOP_GAP, BOTTOM_GAP = 20, 12, 28  # Ui.Side, TopGap, BottomGap
ICON_BUTTON, PRIMARY_H, PILL_H, NAV_H = 48, 72, 44, 72
# Material Symbols code points (UI/Fonts.cs, Icons)
ICONS = {
    "pause": 0xE034, "play": 0xE037, "gift": 0xE8F6, "bolt": 0xEA0B, "friends": 0xEA21,
    "ranks": 0xF20C, "shop": 0xF1CC, "settings": 0xE8B8, "share": 0xE6B8, "replay": 0xE042,
    "home": 0xE9B2, "ticket": 0xE638, "close": 0xE5CD,
}  # fmt: skip

TIMES = chr(0xD7)  # the strings' "Perfect \u00d7{0}"
STRINGS = {
    "en": {
        "best": "Best {0}", "have_code": "Got a challenge code?", "play": "Play",
        "nav": ("Friends", "Ranks", "Shop", "Settings"),
        "perfect_combo": "Perfect " + TIMES + "{0}",
        "almost_perfect": "{0} mm from perfect", "intro_title": "@{0} stacked",
        "score_to_beat": "Score to beat",
        "rules": "Same blocks, same speed, no continues. Beat the score.",
        "accept": "Accept challenge", "later": "Not now", "rival": "Beat @{0} · {1}",
        "passed": "You passed @{0}!", "new_record": "New record", "coins": "+{0} coins",
        "challenge": "Challenge a friend", "replay": "Play again",
        "captions": (
            "One tap. Stack to the sky.", "Every millimetre counts.",
            "Duel friends with one link.", "Pass your friends mid-run.",
            "Beat it, then share it.",
        ),
    },
    "es": {
        "best": "Récord {0}", "have_code": "¿Tienes un código de reto?", "play": "Jugar",
        "nav": ("Amigos", "Ranking", "Tienda", "Ajustes"),
        "perfect_combo": "Perfecto " + TIMES + "{0}",
        "almost_perfect": "A {0} mm de perfecto", "intro_title": "@{0} apiló",
        "score_to_beat": "Puntos a superar",
        "rules": "Mismos bloques, misma velocidad, sin continuar. Supera la puntuación.",
        "accept": "Aceptar reto", "later": "Ahora no", "rival": "Supera a @{0} · {1}",
        "passed": "¡Superaste a @{0}!", "new_record": "Nuevo récord", "coins": "+{0} monedas",
        "challenge": "Reta a un amigo", "replay": "Otra vez",
        "captions": (
            "Un toque. Apila hasta el cielo.", "Cada milímetro cuenta.",
            "Reta a tus amigos con un link.", "Supera a tus amigos en plena partida.",
            "Supéralo y compártelo.",
        ),
    },
}  # fmt: skip


def font(name: str, size: float) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / name), round(size * K))


class Screen:
    """One 390 x 844 pt screen in a look; every coordinate below is in points."""

    def __init__(self, look: str) -> None:
        self.look = look
        self.image = self._background()

    def tok(self, name: str) -> object:
        return theme.token(self.look, name)

    def color(self, name: str) -> art.RGBA:
        return theme.rgba(self.tok(name))  # type: ignore[arg-type]

    def _background(self) -> Image.Image:
        top, mid, bottom = (theme.rgba(c) for c in theme.token(self.look, "bg.gradient"))
        mid_at = float(theme.token(self.look, "bg.gradientMid"))
        column = Image.new("RGBA", (1, H * K))
        for y in range(H * K):
            f = y / (H * K - 1)
            a, b, k = (
                (top, mid, f / mid_at) if f < mid_at else (mid, bottom, (f - mid_at) / (1 - mid_at))
            )
            column.putpixel((0, y), tuple(round(a[i] + (b[i] - a[i]) * k) for i in range(4)))
        image = column.resize((W * K, H * K))
        glow = theme.token(self.look, "bg.centerGlow")
        r, g, b, a = theme.rgba(glow["color"])
        if a:
            radius, cx, cy = glow["radius"] * W * K, W * K / 2, H * K * 0.45
            layer = Image.new("RGBA", image.size)
            ImageDraw.Draw(layer).ellipse(
                (cx - radius, cy - radius, cx + radius, cy + radius), fill=(r, g, b, a)
            )
            image.alpha_composite(art.soft(layer, radius * 0.45))
        return image

    # --- primitives ---
    def layer_draw(self, paint: Callable[[ImageDraw.ImageDraw], None], blur: float = 0) -> None:
        layer = Image.new("RGBA", self.image.size)
        paint(ImageDraw.Draw(layer))
        self.image.alpha_composite(art.soft(layer, blur * K) if blur else layer)

    def rrect(self, box: tuple[float, float, float, float], radius: float, fill: art.RGBA,
              border: art.RGBA | None = None, width: float = 0) -> None:  # fmt: skip
        scaled = tuple(v * K for v in box)
        self.layer_draw(
            lambda d: d.rounded_rectangle(
                scaled, radius * K, fill=fill, outline=border if width else None,
                width=max(1, round(width * K)) if width else 0,
            )
        )  # fmt: skip

    def glow(self, box: tuple[float, float, float, float], radius: float, token: str) -> None:
        g = self.tok(token)
        color = theme.rgba(g["color"])  # type: ignore[index]
        if color[3]:
            dy = g["y"]  # type: ignore[index]
            shifted = (box[0], box[1] + dy, box[2], box[3] + dy)
            self.layer_draw(
                lambda d: d.rounded_rectangle(
                    tuple(v * K for v in shifted), radius * K, fill=color
                ),
                blur=g["blur"] * 0.5,  # type: ignore[index]
            )

    def text(self, xy: tuple[float, float], s: str, f: ImageFont.FreeTypeFont, fill: art.RGBA,
             anchor: str = "mm", spacing: float = 0, glow: bool = False) -> None:  # fmt: skip
        x, y = xy[0] * K, xy[1] * K
        if glow:
            g = self.tok("text.logoGlow")
            gc = theme.rgba(g["color"])  # type: ignore[index]
            if gc[3]:
                self.layer_draw(
                    lambda d: self._spaced(d, (x, y), s, f, gc, anchor, spacing), g["blur"] * 0.6
                )  # type: ignore[index]
        self._spaced(ImageDraw.Draw(self.image), (x, y), s, f, fill, anchor, spacing)

    @staticmethod
    def _spaced(d: ImageDraw.ImageDraw, xy: tuple[float, float], s: str, f: ImageFont.FreeTypeFont,
                fill: art.RGBA, anchor: str, spacing: float) -> None:  # fmt: skip
        if not spacing:
            d.text(xy, s, font=f, fill=fill, anchor=anchor)
            return
        widths = [f.getlength(c) + spacing * K for c in s]
        total = sum(widths) - spacing * K
        x = xy[0] - (total / 2 if anchor[0] == "m" else 0)
        for c, w in zip(s, widths, strict=True):
            d.text((x, xy[1]), c, font=f, fill=fill, anchor="l" + anchor[1])
            x += w

    def icon(self, center: tuple[float, float], name: str, size: float, fill: art.RGBA,
             filled: bool = False) -> None:  # fmt: skip
        f = font(
            "MaterialSymbolsRounded-Filled.ttf" if filled else "MaterialSymbolsRounded-Outline.ttf",
            size,
        )
        ImageDraw.Draw(self.image).text(
            (center[0] * K, center[1] * K), chr(ICONS[name]), font=f, fill=fill, anchor="mm"
        )

    # --- components (UiKit) ---
    def icon_button(self, x: float, y: float, name: str) -> None:
        box = (x, y, x + ICON_BUTTON, y + ICON_BUTTON)
        self.rrect(
            box,
            16,
            self.color("surface"),
            self.color("surface.border"),
            float(self.tok("surface.borderWidth")),
        )
        self.icon((x + ICON_BUTTON / 2, y + ICON_BUTTON / 2), name, 22, self.color("text.primary"))

    def coin(self, center: tuple[float, float], r: float) -> None:
        cx, cy = center[0] * K, center[1] * K
        d = ImageDraw.Draw(self.image)
        d.ellipse((cx - r * K, cy - r * K, cx + r * K, cy + r * K), fill=self.color("coin.ring"))
        ri = r * 0.74 * K
        d.ellipse((cx - ri, cy - ri, cx + ri, cy + ri), fill=self.color("coin.fill"))

    def coin_pill(self, right: float, y: float, amount: str) -> None:
        f = font("Fredoka-Bold.ttf", 18)
        w = 16 + 22 + 8 + f.getlength(amount) / K + 16
        self.rrect(
            (right - w, y, right, y + 40),
            20,
            self.color("surface"),
            self.color("surface.border"),
            float(self.tok("surface.borderWidth")),
        )
        self.coin((right - w + 16 + 11, y + 20), 11)
        self.text((right - 16, y + 20), amount, f, self.color("text.primary"), anchor="rm")

    def primary(self, y: float, label: str, icon: str) -> None:
        box = (SIDE, y, W - SIDE, y + PRIMARY_H)
        self.glow(box, 24, "accent.glow")
        self.rrect(box, 24, self.color("accent.edge"))
        self.rrect((box[0], box[1], box[2], box[3] - 6), 24, self.color("accent"))
        f = font("Fredoka-Bold.ttf", 26)
        w = 22 + 12 + f.getlength(label) / K
        x0, cy = W / 2 - w / 2, y + (PRIMARY_H - 6) / 2
        self.icon((x0 + 11, cy), icon, 22, self.color("accent.onAccent"), filled=True)
        self.text((x0 + 34, cy), label, f, self.color("accent.onAccent"), anchor="lm")

    def pill(self, center_x: float, y: float, label: str, icon: str | None) -> None:
        f = font("Fredoka-SemiBold.ttf", 18)
        w = 18 + (18 + 8 if icon else 0) + f.getlength(label) / K + 18
        x0 = center_x - w / 2
        self.rrect(
            (x0, y, x0 + w, y + PILL_H),
            PILL_H / 2,
            self.color("secondary.fill"),
            self.color("secondary.border"),
            float(self.tok("secondary.borderWidth")),
        )
        ink = self.color("secondary.text")
        if icon:
            self.icon((x0 + 18 + 9, y + PILL_H / 2), icon, 18, ink)
        self.text((x0 + 18 + (26 if icon else 0), y + PILL_H / 2), label, f, ink, anchor="lm")

    def nav(self, labels: tuple[str, ...]) -> None:
        y = H - SAFE_BOTTOM - BOTTOM_GAP + 14 - NAV_H
        self.rrect(
            (SIDE, y, W - SIDE, y + NAV_H),
            22,
            self.color("surface.nav"),
            self.color("surface.navBorder"),
            1,
        )
        f = font("Nunito-Bold.ttf", 12)
        step = (W - 2 * SIDE - 16) / len(labels)
        for i, (label, name) in enumerate(
            zip(labels, ("friends", "ranks", "shop", "settings"), strict=True)
        ):
            cx = SIDE + 8 + step * (i + 0.5)
            self.icon((cx, y + 28), name, 22, self.color("nav.label"))
            self.text((cx, y + 52), label, f, self.color("nav.label"))

    def overline(self, xy: tuple[float, float], s: str, token: str = "text.secondary") -> None:
        self.text(xy, s.upper(), font("Nunito-ExtraBold.ttf", 13), self.color(token), spacing=3)

    def chip(
        self, center: tuple[float, float], label: str, fill: str, ink: str, size: float = 14
    ) -> float:
        f = font("Fredoka-Bold.ttf", size)
        w, h = f.getlength(label) / K + 28, size + 16
        box = (center[0] - w / 2, center[1] - h / 2, center[0] + w / 2, center[1] + h / 2)
        self.rrect(box, h / 2, self.color(fill))
        self.text(center, label, f, self.color(ink))
        return w

    # --- the game world ---
    def tower(
        self,
        top_y: float,
        blocks: int,
        side: float,
        moving: float | None,
        foot_y: float | None = None,
    ) -> float:
        """The stack in the game's iso view; the newest block's top face centre at top_y.
        Returns the top face centre's y. Ramp colours change every 10 blocks, like the game."""
        ramp = self.tok("block.ramp")
        slab = side * 0.26
        layer = Image.new("RGBA", self.image.size)
        cx = W / 2 * K
        s = side * K
        bottom_c = top_y * K + s / 2 + slab * K * blocks  # bottom-face centre of the lowest block
        if foot_y is not None:
            bottom_c = foot_y * K - s / 2
        trims = {}
        width, depth, ox, oz = s, s, 0.0, 0.0
        for i in range(blocks):
            if i in (blocks - 9, blocks - 4):  # two slices, as a real run has
                cut = s * 0.06
                if i % 2:
                    width -= cut
                    ox += cut / 2
                else:
                    depth -= cut
                    oz += cut / 2
            trims[i] = (width, depth, ox, oz)
        for i in range(blocks):
            width, depth, ox, oz = trims[i]
            stop = i / 10
            a, b = ramp[int(stop) % len(ramp)], ramp[(int(stop) + 1) % len(ramp)]
            top = art.mix(a, b, stop - int(stop))
            hexa = "#{:02X}{:02X}{:02X}".format(*top[:3])
            c = art.along_x((cx, bottom_c - slab * K * i), ox)
            c = art.toward_viewer(c, oz)
            height = slab * K * (3 if i == 0 and foot_y is not None else 1)
            if i == 0 and foot_y is not None:
                c = (c[0], c[1] + slab * K * 2)
            art.iso_box(layer, c, width, depth, height, art.faces(hexa), round_px=s * 0.015)
        top_c = art.toward_viewer(
            art.along_x((cx, bottom_c - slab * K * blocks), trims[blocks - 1][2]),
            trims[blocks - 1][3],
        )
        if moving is not None:
            mc = art.along_x((top_c[0], top_c[1] - slab * K * 0.15), moving * s)
            art.iso_box(
                layer,
                mc,
                trims[blocks - 1][0],
                trims[blocks - 1][1],
                slab * K,
                [theme.rgba(c) for c in self.tok("block.moving")],
                round_px=s * 0.015,
            )
        if self.tok("block.emission"):
            self.image.alpha_composite(art.soft(layer, 10 * K))
        self.image.alpha_composite(layer)
        return top_c[1] / K

    def near_miss(self, center: tuple[float, float], mm: str, template: str) -> None:
        """The HUD's gold arcade callout: gradient letters, dark outline, hard shadow, italic."""
        before, after = template.split("{0}")

        def layout(pt: float) -> tuple[list[tuple[str, ImageFont.FreeTypeFont]], float]:
            big, small = font("Fredoka-Bold.ttf", pt * 1.45), font("Fredoka-Bold.ttf", pt)
            parts = [(before, small), (mm, big), (after, small)]
            return parts, sum(f.getlength(t) + 3 * K * len(t) for t, f in parts)

        # Size 34 in a 360 pt box, shrinking to fit like the label's OneLine (min 20).
        pt = 34.0
        parts, total = layout(pt)
        if total > 330 * K:
            pt = max(20.0, pt * 330 * K / total)
            parts, total = layout(pt)
        size = (round(total + 80 * K), round(120 * K))
        mask, outline = Image.new("L", size), Image.new("L", size)
        x, base = 40 * K, size[1] * 0.68
        for t, f in parts:
            for ch in t:
                ImageDraw.Draw(mask).text((x, base), ch, font=f, fill=255, anchor="ls")
                ImageDraw.Draw(outline).text(
                    (x, base), ch, font=f, fill=255, anchor="ls", stroke_width=round(3 * K)
                )
                x += f.getlength(ch) + 3 * K
        layer = Image.new("RGBA", size)
        shadow = Image.new("RGBA", size, theme.rgba(theme.token("neon", "arcade.shadow")))
        shadow.putalpha(outline.transform(size, Image.AFFINE, (1, 0, 0, 0, 1, -3 * K)))
        layer.alpha_composite(shadow)
        ink = Image.new("RGBA", size, theme.rgba(theme.token("neon", "arcade.outline")))
        ink.putalpha(outline)
        layer.alpha_composite(ink)
        top, bottom = art.gold()[0], art.gold()[1]
        column = Image.new("RGBA", (1, size[1]))
        y0, y1 = base - 40 * K, base
        for y in range(size[1]):
            t = min(1, max(0, (y - y0) / (y1 - y0)))
            column.putpixel(
                (0, y), tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(4))
            )
        fill = column.resize(size)
        fill.putalpha(mask)
        layer.alpha_composite(fill)
        shear = 0.18
        layer = layer.transform(
            size, Image.AFFINE, (1, shear, -shear * base, 0, 1, 0), Image.BICUBIC
        )
        self.image.alpha_composite(
            layer, (round(center[0] * K - size[0] / 2), round(center[1] * K - base + 20 * K))
        )


def _wrap(s: str, f: ImageFont.FreeTypeFont, width: float) -> list[str]:
    lines: list[str] = []
    for word in s.split():
        if lines and f.getlength(lines[-1] + " " + word) <= width * K:
            lines[-1] += " " + word
        else:
            lines.append(word)
    return lines


def home(t: dict) -> Image.Image:
    s = Screen("neon")
    y = SAFE_TOP + TOP_GAP
    s.icon_button(SIDE, y, "gift")
    s.icon_button(SIDE + ICON_BUTTON + 10, y, "bolt")
    s.coin_pill(W - SIDE, y + 4, "1,240")
    s.text(
        (W / 2, y + 100),
        "milli",
        font("Fredoka-Bold.ttf", 58),
        s.color("text.primary"),
        spacing=2,
        glow=True,
    )
    s.overline((W / 2, y + 146), t["best"].format(87))
    foot = 520
    s.tower(0, 6, 120, None, foot_y=foot)
    art.blocky(
        s.image, (W / 2 + 112) * K, (foot - 4) * K, 34 * K, (W / 2 * K, 330 * K), s.tok("accent2")
    )
    nav_y = H - SAFE_BOTTOM - BOTTOM_GAP + 14 - NAV_H
    play_y = nav_y - 16 - PRIMARY_H
    s.primary(play_y, t["play"], "play")
    s.pill(W / 2, play_y - 14 - PILL_H, t["have_code"], "ticket")
    s.nav(t["nav"])
    return s.image.convert("RGB")


def _hud(s: Screen, score: int) -> None:
    y = SAFE_TOP + TOP_GAP
    s.icon_button(SIDE, y, "pause")
    s.text(
        (W / 2, y + 40 + 45),
        str(score),
        font("Fredoka-Bold.ttf", 72),
        s.color("text.primary"),
        glow=True,
    )


def near_miss(t: dict) -> Image.Image:
    s = Screen("neon")
    s.tower(500, 26, 118, -0.3)
    _hud(s, 42)
    s.chip(
        (W / 2, SAFE_TOP + TOP_GAP + 136), t["perfect_combo"].format(3), "accent", "accent.onAccent"
    )
    s.near_miss((W / 2, H * (1 - 0.68)), "0.4", t["almost_perfect"])
    return s.image.convert("RGB")


def challenge(t: dict) -> Image.Image:
    s = Screen("neon")
    s.icon_button(SIDE, SAFE_TOP + TOP_GAP, "close")
    s.text(
        (W / 2, 250),
        t["intro_title"].format("maya"),
        font("Fredoka-Bold.ttf", 30),
        s.color("text.primary"),
    )
    s.overline((W / 2, 300), t["score_to_beat"])
    s.text((W / 2, 372), "120", font("Fredoka-Bold.ttf", 96), s.color("text.primary"), glow=True)
    body = font("Nunito-Bold.ttf", 16)
    for i, line in enumerate(_wrap(t["rules"], body, 300)):
        s.text((W / 2, 460 + i * 24), line, body, s.color("text.secondary"))
    s.primary(560, t["accept"], "play")
    s.pill(W / 2, 560 + PRIMARY_H + 16, t["later"], "close")
    art.blocky(s.image, W / 2 * K, 800 * K, 46 * K, (W / 2 * K, 372 * K), s.tok("accent2"))
    return s.image.convert("RGB")


def rival(t: dict) -> Image.Image:
    s = Screen("pop")
    s.tower(500, 22, 150, 0.38)
    line_y = 330
    accent2 = s.color("accent2")
    s.layer_draw(
        lambda d: d.line((0, line_y * K, W * K, line_y * K), fill=(*accent2[:3], 200), width=2 * K)
    )
    label = t["rival"].format("maya", 120)
    f = font("Fredoka-Bold.ttf", 12)
    w = f.getlength(label) / K + 24
    s.rrect((W - SIDE - w, line_y - 15, W - SIDE, line_y + 15), 15, accent2)
    s.text((W - SIDE - w / 2, line_y), label, f, s.color("accent2.onAccent2"))
    _hud(s, 63)
    s.chip(
        (W / 2, SAFE_TOP + TOP_GAP + 186),
        t["passed"].format("leo"),
        "accent2",
        "accent2.onAccent2",
        20,
    )
    return s.image.convert("RGB")


def game_over(t: dict) -> Image.Image:
    s = Screen("neon")
    s.tower(330, 30, 130, None)
    s.layer_draw(lambda d: d.rectangle((0, 0, W * K, H * K), fill=s.color("overlay.dim")))
    s.icon_button(SIDE, SAFE_TOP + TOP_GAP, "home")
    top = 300
    s.rrect((0, top, W, H + 40), 22, s.color("surface.raised"))
    # A new best: Blocky cheers under the gold slab and the sheet takes its colour (Blocky.cs).
    s.rrect((0, top, W, H + 40), 22, (*s.color("accent2")[:3], round(255 * 0.14)))
    art.blocky(s.image, 300 * K, (top + 6) * K, 64 * K, (300 * K, 0), s.tok("accent2"), hat=True)
    s.chip((W / 2, top + 36), t["new_record"].upper(), "accent2", "accent2.onAccent2", 13)
    s.text(
        (W / 2, top + 120), "87", font("Fredoka-Bold.ttf", 88), s.color("text.primary"), glow=True
    )
    coins = t["coins"].format(24)
    f = font("Fredoka-Bold.ttf", 20)
    w = f.getlength(coins) / K
    s.coin((W / 2 - w / 2 - 14, top + 190), 11)
    s.text((W / 2 + 14, top + 190), coins, f, s.color("coin.fill"))
    s.primary(top + 236, t["challenge"], "share")
    s.pill(W / 2, top + 236 + PRIMARY_H + 16, t["replay"], "replay")
    return s.image.convert("RGB")


SCREENS = (home, near_miss, challenge, rival, game_over)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--lang", choices=sorted(STRINGS), default="en")
    args = parser.parse_args()
    t = STRINGS[args.lang]
    raw, framed = OUT / "mockups" / args.lang, OUT / f"screenshots-{args.lang}"
    raw.mkdir(parents=True, exist_ok=True)
    framed.mkdir(parents=True, exist_ok=True)
    for n, (draw, caption) in enumerate(zip(SCREENS, t["captions"], strict=True)):
        screen = draw(t)
        screen.save(raw / f"{n + 1:02d}.png", optimize=True)
        framed_screenshot(screen, caption).save(framed / f"{n + 1:02d}.png", optimize=True)
    print(f"wrote {framed}")


if __name__ == "__main__":
    main()
