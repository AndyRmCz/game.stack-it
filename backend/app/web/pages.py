"""Server-rendered pages. Plain f-strings with explicit escaping: four small pages do not need a
template engine. Every interpolated user value goes through `e()`.

They wear the app's looks: colours are CSS custom properties generated from the shared tokens
(Neon by default, Pop when the browser prefers light), with the app's fonts, shapes and buttons."""

from html import escape
from urllib.parse import quote

from app.web import theme
from app.web.i18n import Lang, t

FONTS = (
    "https://fonts.googleapis.com/css2?family=Fredoka:wght@600;700"
    "&family=Nunito:wght@700;800&display=swap"
)

CSS = """
:root {
  --display: "Fredoka", "Segoe UI", system-ui, sans-serif;
  --body: "Nunito", system-ui, -apple-system, sans-serif;
}
* { box-sizing: border-box; }
html, body { margin: 0; }
body {
  min-height: 100svh; color: var(--text-primary); font: 700 1rem/1.5 var(--body);
  background:
    radial-gradient(circle at 50% 38%, var(--bg-centerGlow-color),
  transparent calc(var(--bg-centerGlow-radius) * 100vw)),
    linear-gradient(180deg, var(--bg-gradient-1) 0%,
  var(--bg-gradient-2) calc(var(--bg-gradientMid) * 100%),
      var(--bg-gradient-3) 100%) fixed;
}
a { color: var(--secondary-text); }
:focus-visible { outline: 3px solid var(--accent); outline-offset: 3px; border-radius: 8px; }
main { max-width: 24.375rem; margin: 0 auto; padding: 1.75rem 1.25rem 2rem; }
.logo { font: 700 2.4rem/1 var(--display); letter-spacing: .04em; text-align: center;
  margin: .5rem 0 1.5rem;
  text-shadow: 0 0 var(--text-logoGlow-blur) var(--text-logoGlow-color); }
.overline { font: 800 .8125rem/1 var(--body); letter-spacing: .23em; text-transform: uppercase;
  color: var(--text-secondary); margin: 0 0 .6rem; text-align: center; }
h1 { font: 700 1.75rem/1.15 var(--display); margin: 0 0 .25rem; text-align: center; }
.score { font: 700 6rem/1 var(--display); text-align: center; margin: .25rem 0 .25rem;
  text-shadow: 0 0 var(--text-logoGlow-blur) var(--text-logoGlow-color); }
.lede { color: var(--text-secondary); margin: 0 0 1.25rem; text-align: center; }

/* Signature: the rival's tower in the look's colours, with their ghost line at the top. */
.hero { position: relative; display: flex; justify-content: center; margin: .5rem 0 1.5rem; }
.hero svg { width: 11rem; height: auto; overflow: visible; }
.ghost { position: absolute; left: 0; right: 0; top: .9rem; border-top: 2px dashed var(--accent2);
  filter: drop-shadow(0 0 6px var(--accent2-glow-color)); }
.ghost span { position: absolute; right: 0; top: -1.7rem; font: 800 .8rem/1 var(--body);
  padding: .3rem .6rem;
  border-radius: 999px; background: var(--accent2); color: var(--accent2-onAccent2); }
.moving { animation: slide 2.4s ease-in-out infinite alternate; }
@keyframes slide { from { transform: translate(-10px, 6px);
  } to { transform: translate(10px, -6px); } }

.actions { display: grid; gap: .75rem; justify-items: center; }
.btn-primary { display: flex; align-items: center; justify-content: center; gap: .75rem;
  width: 100%;
  min-height: 4.5rem; padding: 0 1.25rem 6px; border-radius: 1.5rem; text-decoration: none;
  font: 700 1.625rem/1 var(--display); letter-spacing: .02em;
  background: var(--accent); color: var(--accent-onAccent);
  box-shadow: 0 6px 0 var(--accent-edge),
  0 var(--accent-glow-y) var(--accent-glow-blur) var(--accent-glow-color);
  transition: transform 60ms ease-out, box-shadow 60ms ease-out;
  animation: breathe 2.4s ease-in-out infinite; }
.btn-primary:active { transform: translateY(4px); box-shadow: 0 2px 0 var(--accent-edge); }
@keyframes breathe { 50% { scale: 1.03; } }
.pill { display: inline-flex; align-items: center; gap: .5rem; min-height: 2.75rem;
  padding: 0 1.125rem;
  border-radius: 999px; text-decoration: none; font: 800 .9375rem/1 var(--body);
  color: var(--secondary-text); background: var(--secondary-fill);
  border: var(--secondary-borderWidth) solid var(--secondary-border); }
.code { margin: 1.5rem auto 0; text-align: center; color: var(--text-secondary); font-size: .9rem; }
.code b { display: inline-block; margin-left: .35rem; padding: .35rem .8rem; border-radius: 999px;
  font: 700 1.05rem/1 var(--display); letter-spacing: .14em; color: var(--text-primary);
  background: var(--surface); border: var(--surface-borderWidth) solid var(--surface-border); }
.notice { border-radius: 18px; padding: .9rem 1rem; margin: 0 0 1.25rem; background: var(--surface);
  border: var(--surface-borderWidth) solid var(--surface-border); color: var(--text-primary); }
footer { margin-top: 2rem; font-size: .8rem; text-align: center; color: var(--text-secondary); }
footer a { color: var(--text-secondary); }
.prose { max-width: 40rem; }
.prose h1 { text-align: left; font-size: 1.75rem; margin-top: 1rem; }
.prose h2 { font: 700 1.15rem/1.3 var(--display); margin: 2rem 0 .5rem;
  color: var(--secondary-text); }
.prose p, .prose li { color: var(--text-primary); font-weight: 700; }
@media (prefers-reduced-motion: reduce) { .btn-primary, .moving { animation: none; } }
"""

TOWER_MAX_BLOCKS = 6


def e(value: object) -> str:
    return escape(str(value), quote=True)


def _shell(lang: Lang, title: str, body: str, head_extra: str = "", base_url: str = "") -> str:
    return f"""<!doctype html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="theme-color" content="{theme.token(theme.DEFAULT_LOOK, "bg.gradient")[0]}">
<title>{e(title)}</title>
{head_extra}
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="{FONTS}">
<style>{theme.color_scheme_css()}
{CSS}</style>
</head>
<body>
<main>
{body}
<footer><a href="{e(base_url)}/privacy?lang={lang}">{t(lang, "privacy")}</a> ·
<a href="{e(base_url)}/terms?lang={lang}">{t(lang, "terms")}</a></footer>
</main>
</body>
</html>"""


def _iso_block(
    cx: float, base_y: float, side: float, height: float, faces: tuple[str, str, str], cls: str = ""
) -> str:
    """One isometric block: three SVG polygons filled with CSS variables (it follows the look)."""
    hx, hy = side * 0.433, side * 0.25  # cos 30° and sin 30° of half the side
    front, right, left, back = (
        (cx, base_y),
        (cx + hx * 2, base_y - hy * 2),
        (cx - hx * 2, base_y - hy * 2),
        (cx, base_y - hy * 4),
    )

    def pts(*ps: tuple[float, float]) -> str:
        return " ".join(f"{x:.1f},{y:.1f}" for x, y in ps)

    def up(p: tuple[float, float]) -> tuple[float, float]:
        return p[0], p[1] - height

    top, lft, rgt = faces
    return (
        f'<g class="{cls}">'
        f'<polygon fill="var({lft})" points="{pts(left, front, up(front), up(left))}"/>'
        f'<polygon fill="var({rgt})" points="{pts(front, right, up(right), up(front))}"/>'
        f'<polygon fill="var({top})" points="{pts(up(front), up(right), up(back), up(left))}"/>'
        "</g>"
    )


def _tower(placed: int, rival: str, lang: Lang) -> str:
    shown = max(2, min(placed // 6 + 2, TOWER_MAX_BLOCKS))
    side, height = 100.0, 26.0
    ground = 40 + shown * height + side
    parts = [
        f'<ellipse cx="80" cy="{ground - 2:.0f}" rx="78" ry="16" fill="var(--block-groundShadow)"/>'
    ]
    base = ("--block-base-1", "--block-base-2", "--block-base-3")
    for i in range(shown):
        parts.append(_iso_block(80, ground - i * height, side, height, base))
    moving = ("--block-moving-1", "--block-moving-2", "--block-moving-3")
    parts.append(_iso_block(80, ground - shown * height, side, height, moving, "moving"))
    alt = e(t(lang, "tower_alt", name=rival, n=placed))
    label = e(t(lang, "ghost", name=rival))
    return (
        f'<div class="hero" role="img" aria-label="{alt}">'
        f'<svg viewBox="0 0 160 {ground + 20:.0f}" aria-hidden="true">{"".join(parts)}</svg>'
        f'<div class="ghost"><span>{label}</span></div></div>'
    )


def challenge_page(
    *,
    lang: Lang,
    base_url: str,
    code: str,
    rival: str,
    score: int,
    placed: int,
    expired: bool,
    app_scheme: str,
    ios_url: str,
    play_url: str,
) -> str:
    page_url = f"{base_url}/c/{code}"
    title = t(lang, "og_title", name=rival, score=score)
    description = t(lang, "og_description")
    referrer = quote(f"challenge={code}", safe="")
    separator = "&" if "?" in play_url else "?"
    play_link = f"{play_url}{separator}referrer={referrer}"

    head = f"""<meta name="description" content="{e(description)}">
<meta property="og:type" content="website">
<meta property="og:site_name" content="Milli">
<meta property="og:title" content="{e(title)}">
<meta property="og:description" content="{e(description)}">
<meta property="og:url" content="{e(page_url)}">
<meta property="og:image" content="{e(base_url)}/og/{e(code)}.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta name="twitter:card" content="summary_large_image">"""

    notice = f'<p class="notice">{e(t(lang, "expired"))}</p>' if expired else ""
    primary = (
        ""
        if expired
        else f'<a class="btn btn-primary" href="{e(app_scheme)}://c/{e(code)}">'
        f"{e(t(lang, 'open_app'))}</a>"
    )
    ios = f'<a class="pill" href="{e(ios_url)}">{e(t(lang, "app_store"))}</a>' if ios_url else ""
    body = f"""<p class="logo">milli</p>
<p class="overline">{e(t(lang, "eyebrow"))}</p>
<h1>{e(t(lang, "headline_pre", name=rival))}</h1>
<p class="score">{score}</p>
<p class="overline">{e(t(lang, "score_to_beat"))}</p>
{_tower(placed, rival, lang)}
<p class="lede">{e(t(lang, "lede", n=placed))}</p>
{notice}
<div class="actions">
{primary}
<a class="pill" href="{e(play_link)}">{e(t(lang, "play_store"))}</a>
{ios}
</div>
<p class="code">{e(t(lang, "code_hint"))} <b>{e(code)}</b></p>"""
    return _shell(lang, title, body, head, base_url)


def not_found_page(lang: Lang, base_url: str, play_url: str) -> str:
    body = f"""<p class="logo">milli</p>
<h1>{e(t(lang, "not_found_title"))}</h1>
<p class="lede">{e(t(lang, "not_found_body"))}</p>
<div class="actions">
<a class="btn btn-primary" href="{e(play_url)}">{e(t(lang, "get_game"))}</a>
</div>"""
    return _shell(lang, t(lang, "not_found_title"), body, base_url=base_url)


def legal_page(lang: Lang, base_url: str, title: str, sections: list[tuple[str, str]]) -> str:
    parts = "".join(f"<h2>{e(h)}</h2><p>{e(p)}</p>" for h, p in sections)
    body = f'<div class="prose"><p class="logo">milli</p><h1>{e(title)}</h1>{parts}</div>'
    return _shell(lang, f"{title} · Milli", body, base_url=base_url)
