from datetime import UTC, datetime, timedelta

from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import get_settings
from app.models import Challenge, Run, User


async def _challenge(db: AsyncSession, name: str, expires_in: timedelta) -> Challenge:
    async with db.begin():
        user = User(display_name=name, friend_code="WEB" + str(abs(hash(name)) % 10000).zfill(5))
        db.add(user)
        await db.flush()
        run = Run(user_id=user.id, seed=7, sim_version=1, continues_allowed=True, status="verified")
        db.add(run)
        await db.flush()
        challenge = Challenge(
            code="K7QX9PA" if expires_in.total_seconds() > 0 else "XPRD234",
            creator_id=user.id,
            run_id=run.id,
            seed=7,
            score=48,
            placed=31,
            expires_at=datetime.now(UTC) + expires_in,
        )
        db.add(challenge)
    return challenge


async def test_landing_page_has_rich_preview_and_app_links(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    await _challenge(db_session, "Nova", timedelta(days=3))

    response = await client.get("/c/k7qx9pa", headers={"Accept-Language": "en-US,en;q=0.9"})

    assert response.status_code == 200
    html = response.text
    base = get_settings().public_base_url
    assert '<meta property="og:title" content="Can you beat Nova&#x27;s 48?">' in html
    assert f'<meta property="og:url" content="{base}/c/K7QX9PA">' in html
    assert f'content="{base}/og/K7QX9PA.png"' in html
    assert 'href="milli://c/K7QX9PA"' in html
    # Play Store link carries the code through the Install Referrer for fresh installs.
    assert "referrer=challenge%3DK7QX9PA" in html


async def test_landing_page_speaks_spanish(client: AsyncClient, db_session: AsyncSession) -> None:
    await _challenge(db_session, "Nova", timedelta(days=3))

    response = await client.get("/c/K7QX9PA", headers={"Accept-Language": "es-MX,es;q=0.9"})

    assert '<html lang="es">' in response.text
    assert "¿Puedes superar los 48 de Nova?" in response.text


async def test_display_names_are_escaped(client: AsyncClient, db_session: AsyncSession) -> None:
    await _challenge(db_session, '<script>x</script>"', timedelta(days=3))

    html = (await client.get("/c/K7QX9PA")).text

    assert "<script>x</script>" not in html
    assert "&lt;script&gt;" in html


async def test_expired_challenge_still_renders_with_expired_notice(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    await _challenge(db_session, "Nova", timedelta(days=-1))

    response = await client.get("/c/XPRD234")

    assert response.status_code == 200
    assert "This challenge has expired" in response.text


async def test_unknown_challenge_is_404(client: AsyncClient) -> None:
    response = await client.get("/c/NOPE123")

    assert response.status_code == 404
    assert "Challenge not found" in response.text


async def test_well_known_files_are_generated_from_settings(client: AsyncClient) -> None:
    settings = get_settings()
    settings.apple_team_id = "ABCDE12345"
    settings.android_sha256_fingerprints = "AA:BB, CC:DD"
    try:
        aasa = await client.get("/.well-known/apple-app-site-association")
        links = await client.get("/.well-known/assetlinks.json")
    finally:
        settings.apple_team_id = ""
        settings.android_sha256_fingerprints = ""

    assert aasa.headers["content-type"].startswith("application/json")
    detail = aasa.json()["applinks"]["details"][0]
    assert detail["appIDs"] == [f"ABCDE12345.{settings.app_bundle_id}"]
    assert detail["components"] == [{"/": "/c/*"}]
    target = links.json()[0]["target"]
    assert target["package_name"] == settings.app_bundle_id
    assert target["sha256_cert_fingerprints"] == ["AA:BB", "CC:DD"]


async def test_app_ads_txt_lists_the_admob_publisher(client: AsyncClient) -> None:
    settings = get_settings()
    settings.admob_publisher_id = "pub-1234567890"
    try:
        response = await client.get("/app-ads.txt")
    finally:
        settings.admob_publisher_id = ""

    assert response.text.strip() == "google.com, pub-1234567890, DIRECT, f08c47fec0942fa0"


async def test_account_deletion_page_explains_in_app_and_email_paths(client: AsyncClient) -> None:
    # Google Play requires a web resource for account deletion requests.
    en = await client.get("/delete-account")
    es = await client.get("/delete-account", headers={"Accept-Language": "es"})

    assert en.status_code == 200
    assert "Delete account" in en.text and get_settings().support_email in en.text
    assert "Eliminar cuenta" in es.text
