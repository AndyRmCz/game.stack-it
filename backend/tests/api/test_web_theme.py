"""The web pages wear the app's looks: the shared tokens, Neon by default, Pop in light mode."""

import json
from datetime import timedelta
from io import BytesIO
from pathlib import Path

from httpx import AsyncClient
from PIL import Image
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import get_settings
from app.web import theme
from tests.api.test_web import _challenge

SHARED = Path(__file__).resolve().parents[3] / "shared" / "theme" / "themes.json"


def test_backend_copy_of_the_tokens_matches_the_shared_file() -> None:
    # The Docker build context is backend/, so it ships a copy; it must never drift.
    assert theme.THEMES_FILE.read_text(encoding="utf-8") == SHARED.read_text(encoding="utf-8")


def test_css_variables_cover_every_token_of_both_looks() -> None:
    tokens = json.loads(SHARED.read_text(encoding="utf-8"))["themes"]
    for look in ("pop", "neon"):
        css = theme.css_variables(look)
        for name, value in tokens[look]["tokens"].items():
            prefix = "--" + name.replace(".", "-")
            assert prefix in css, f"{look} is missing {prefix}"
            if isinstance(value, str):
                assert f"{prefix}: {value.lower()};" in css


async def test_landing_page_is_neon_by_default_and_pop_in_light_mode(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    await _challenge(db_session, "Nova", timedelta(days=3))

    html = (await client.get("/c/K7QX9PA")).text

    neon_accent = theme.token("neon", "accent").lower()
    pop_accent = theme.token("pop", "accent").lower()
    root, light = html.split("@media (prefers-color-scheme: light)", 1)
    assert f"--accent: {neon_accent};" in root
    assert f"--accent: {pop_accent};" in light
    assert "fonts.googleapis.com/css2?family=Fredoka" in html and "Nunito" in html


async def test_landing_page_primary_action_opens_the_app(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    await _challenge(db_session, "Nova", timedelta(days=3))

    html = (await client.get("/c/K7QX9PA")).text

    assert 'class="btn btn-primary" href="milli://c/K7QX9PA">Open in Milli</a>' in html
    assert html.count('class="btn btn-primary"') == 1  # one primary action per page


async def test_preview_image_is_a_1200x630_png_with_the_score(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    await _challenge(db_session, "Nova", timedelta(days=3))

    page = (await client.get("/c/K7QX9PA")).text
    response = await client.get("/og/K7QX9PA.png")

    base = get_settings().public_base_url
    assert f'<meta property="og:image" content="{base}/og/K7QX9PA.png">' in page
    assert response.status_code == 200
    assert response.headers["content-type"] == "image/png"
    image = Image.open(BytesIO(response.content))
    assert image.size == (1200, 630)


async def test_preview_image_of_an_unknown_code_falls_back_to_the_generic_card(
    client: AsyncClient,
) -> None:
    response = await client.get("/og/NOPE123.png")

    assert response.status_code == 200
    assert Image.open(BytesIO(response.content)).size == (1200, 630)
