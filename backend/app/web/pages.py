"""Server-rendered pages. Plain f-strings with explicit escaping: four small pages do not need a
template engine. Every interpolated user value goes through `e()`."""

from html import escape
from urllib.parse import quote

from app.web.i18n import Lang, t

FONTS = (
    "https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@600;700"
    "&family=Manrope:wght@400;600&display=swap"
)

CSS = """
:root {
  --night: #140a2e; --dusk: #3b1060; --magenta: #ff3d9a; --cyan: #2de2e6; --amber: #ffb84d;
  --moon: #f4f0ff; --lavender: #b9a8e0;
  --display: "Chakra Petch", "Segoe UI", system-ui, sans-serif;
  --body: "Manrope", system-ui, -apple-system, sans-serif;
}
* { box-sizing: border-box; }
html, body { margin: 0; }
body {
  min-height: 100svh; color: var(--moon); font: 400 1rem/1.55 var(--body);
  background: linear-gradient(180deg, var(--night) 0%, var(--dusk) 62%, #6a1b6e 100%) fixed;
}
a { color: var(--cyan); }
:focus-visible { outline: 3px solid var(--amber); outline-offset: 3px; border-radius: 6px; }
main { max-width: 30rem; margin: 0 auto; padding: 2.5rem 1.25rem 3rem; }
.eyebrow {
  font: 600 .78rem/1 var(--display); letter-spacing: .18em; text-transform: uppercase;
  color: var(--lavender); margin: 0 0 .9rem;
}
h1 { font: 700 clamp(1.9rem, 8vw, 2.6rem)/1.08 var(--display); margin: 0 0 .6rem; }
h1 .score { color: var(--amber); text-shadow: 0 0 18px rgba(255,184,77,.55); }
.lede { color: var(--lavender); margin: 0 0 1.75rem; }

/* Signature: the rival's tower with the ghost line at its top, as in the game. */
.tower { position: relative; padding-top: 2.4rem; margin: 0 0 2rem; display: flex;
  flex-direction: column-reverse; align-items: center; gap: 3px; }
.block { height: 10px; border-radius: 2px; background: var(--c);
  box-shadow: 0 0 10px var(--c), inset 0 1px 0 rgba(255,255,255,.45); }
.ghost { position: absolute; left: 0; right: 0; border-top: 2px dashed var(--cyan);
  filter: drop-shadow(0 0 6px var(--cyan)); }
.ghost span { position: absolute; right: 0; top: -1.6rem; font: 600 .8rem/1 var(--display);
  color: var(--cyan); letter-spacing: .06em; }
.block:last-child { animation: pulse 1.6s ease-in-out infinite; }
@keyframes pulse { 50% { filter: brightness(1.35); } }
@media (prefers-reduced-motion: reduce) { .block:last-child { animation: none; } }

.actions { display: grid; gap: .75rem; }
.btn { display: block; text-align: center; text-decoration: none; padding: 1rem 1.25rem;
  border-radius: 14px; font: 700 1.05rem/1 var(--display); letter-spacing: .04em; }
.btn-primary { background: var(--magenta); color: #fff;
  box-shadow: 0 0 0 1px rgba(255,255,255,.25) inset, 0 8px 28px rgba(255,61,154,.45); }
.btn-ghost { color: var(--moon); border: 1px solid rgba(244,240,255,.35); font-weight: 600; }
.code { margin: 1.75rem 0 0; color: var(--lavender); font-size: .92rem; }
.code b { font: 700 1.1rem/1 var(--display); color: var(--moon); letter-spacing: .14em; }
.notice { border-left: 3px solid var(--amber); padding: .2rem 0 .2rem .9rem; margin: 0 0 1.5rem; }
footer { margin-top: 2.5rem; font-size: .85rem; color: var(--lavender); }
footer a { color: var(--lavender); }
.prose { max-width: 40rem; }
.prose h1 { font-size: 2rem; }
.prose h2 { font: 700 1.15rem/1.3 var(--display); margin: 2rem 0 .5rem; color: var(--amber); }
.prose p, .prose li { color: #e2dbf5; }
"""

# Height palette, bottom to top: the same shift the game uses as the stack grows.
TOWER_COLORS = ("#2de2e6", "#4fc3ff", "#7c8cff", "#b06cff", "#e04fd8", "#ff3d9a", "#ff7a6b")
TOWER_MAX_BLOCKS = 16


def e(value: object) -> str:
    return escape(str(value), quote=True)


def _shell(lang: Lang, title: str, body: str, head_extra: str = "", base_url: str = "") -> str:
    return f"""<!doctype html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="theme-color" content="#140a2e">
<title>{e(title)}</title>
{head_extra}
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="{FONTS}">
<style>{CSS}</style>
</head>
<body>
<main>
{body}
<footer><a href="{e(base_url)}/privacy?lang={lang}">{t(lang, "privacy")}</a> ·
<a href="{e(base_url)}/terms?lang={lang}">{t(lang, "terms")}</a></footer>
</main>
</body>
</html>"""


def _tower(placed: int, rival: str, lang: Lang) -> str:
    shown = max(3, min(placed, TOWER_MAX_BLOCKS))
    blocks = []
    for i in range(shown):
        color = TOWER_COLORS[i * len(TOWER_COLORS) // shown]
        width = 62 - (i * 18 // shown)  # the stack narrows as slices pile up
        blocks.append(f'<div class="block" style="--c:{color};width:{width}%"></div>')
    ghost_bottom = shown * 13
    label = e(t(lang, "ghost", name=rival))
    alt = e(t(lang, "tower_alt", name=rival, n=placed))
    return (
        f'<div class="tower" role="img" aria-label="{alt}">'
        + "".join(blocks)
        + f'<div class="ghost" style="bottom:{ghost_bottom}px"><span>{label}</span></div></div>'
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
<meta property="og:site_name" content="Stack IT">
<meta property="og:title" content="{e(title)}">
<meta property="og:description" content="{e(description)}">
<meta property="og:url" content="{e(page_url)}">
<meta property="og:image" content="{e(base_url)}/static/og-challenge.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta name="twitter:card" content="summary_large_image">"""

    notice = f'<p class="notice">{e(t(lang, "expired"))}</p>' if expired else ""
    primary = (
        ""
        if expired
        else f'<a class="btn btn-primary" href="{e(app_scheme)}://c/{e(code)}">'
        f"{e(t(lang, 'accept'))}</a>"
    )
    ios = (
        f'<a class="btn btn-ghost" href="{e(ios_url)}">{e(t(lang, "app_store"))}</a>'
        if ios_url
        else ""
    )
    body = f"""<p class="eyebrow">{e(t(lang, "eyebrow"))}</p>
<h1>{e(t(lang, "headline_pre", name=rival))} <span class="score">{score}</span>.
{e(t(lang, "headline_post"))}</h1>
<p class="lede">{e(t(lang, "lede", n=placed))}</p>
{notice}
{_tower(placed, rival, lang)}
<div class="actions">
{primary}
<a class="btn btn-ghost" href="{e(play_link)}">{e(t(lang, "play_store"))}</a>
{ios}
</div>
<p class="code">{e(t(lang, "code_hint"))} <b>{e(code)}</b></p>"""
    return _shell(lang, title, body, head, base_url)


def not_found_page(lang: Lang, base_url: str, play_url: str) -> str:
    body = f"""<p class="eyebrow">Stack IT</p>
<h1>{e(t(lang, "not_found_title"))}</h1>
<p class="lede">{e(t(lang, "not_found_body"))}</p>
<div class="actions">
<a class="btn btn-primary" href="{e(play_url)}">{e(t(lang, "get_game"))}</a>
</div>"""
    return _shell(lang, t(lang, "not_found_title"), body, base_url=base_url)


def legal_page(lang: Lang, base_url: str, title: str, sections: list[tuple[str, str]]) -> str:
    parts = "".join(f"<h2>{e(h)}</h2><p>{e(p)}</p>" for h, p in sections)
    body = f'<div class="prose"><p class="eyebrow">Stack IT</p><h1>{e(title)}</h1>{parts}</div>'
    return _shell(lang, f"{title} · Stack IT", body, base_url=base_url)
