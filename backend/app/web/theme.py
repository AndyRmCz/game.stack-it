"""The app's look tokens for the web pages.

`themes.json` here is a byte-for-byte copy of shared/theme/themes.json (the Docker build context
is backend/; a test keeps the two identical). Tokens become CSS custom properties: `bg.gradient`
→ `--bg-gradient-1..3`, `accent.glow` → `--accent-glow-color/-y/-blur`, numbers stay unitless.
"""

import json
from functools import cache
from pathlib import Path
from typing import Any

THEMES_FILE = Path(__file__).with_name("themes.json")
DEFAULT_LOOK = "neon"


@cache
def _themes() -> dict[str, Any]:
    return json.loads(THEMES_FILE.read_text(encoding="utf-8"))


def token(look: str, name: str) -> Any:
    return _themes()["themes"][look]["tokens"][name]


def _var(name: str) -> str:
    return "--" + name.replace(".", "-")


def css_variables(look: str) -> str:
    """`--token: value;` declarations for one look."""
    lines: list[str] = []
    for name, value in _themes()["themes"][look]["tokens"].items():
        var = _var(name)
        if isinstance(value, str):
            lines.append(f"{var}: {value.lower()};")
        elif isinstance(value, list):
            lines.extend(f"{var}-{i}: {v.lower()};" for i, v in enumerate(value, start=1))
        elif isinstance(value, dict):
            for key, part in value.items():
                unit = "px" if key in ("y", "blur") else ""
                lines.append(
                    f"{var}-{key}: {part.lower() if isinstance(part, str) else f'{part}{unit}'};"
                )
        else:
            lines.append(f"{var}: {value};")
    return " ".join(lines)


def color_scheme_css() -> str:
    """Neon by default; Pop when the browser prefers a light colour scheme."""
    light = next(look for look, spec in _themes()["themes"].items() if not spec["dark"])
    return (
        f":root {{ color-scheme: dark; {css_variables(DEFAULT_LOOK)} }}\n"
        "@media (prefers-color-scheme: light) "
        f"{{ :root {{ color-scheme: light; {css_variables(light)} }} }}"
    )


def rgba(hex_color: str) -> tuple[int, int, int, int]:
    """#RRGGBB or #RRGGBBAA → (r, g, b, a) in 0..255."""
    h = hex_color.lstrip("#")
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    a = int(h[6:8], 16) if len(h) == 8 else 255
    return r, g, b, a
