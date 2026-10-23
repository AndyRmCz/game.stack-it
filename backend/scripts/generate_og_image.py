"""Writes the generic 1200x630 Open Graph card (no score) to app/web/static/og-challenge.png.

    uv run python -m scripts.generate_og_image

Challenge links get a per-challenge image from /og/{code}.png (app/web/og.py); this static copy
is the same renderer's generic card, kept for anything that wants a fixed URL.
"""

from pathlib import Path

from app.web import og

OUT = Path(__file__).resolve().parent.parent / "app" / "web" / "static" / "og-challenge.png"

if __name__ == "__main__":
    OUT.write_bytes(og.render(None, "en"))
    print(f"wrote {OUT}")
