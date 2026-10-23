from typing import Annotated

from fastapi import APIRouter, Header, Query
from fastapi.responses import HTMLResponse, JSONResponse, PlainTextResponse, Response

from app.api.deps import ChallengeServiceDep, SettingsDep
from app.core.errors import NotFound
from app.web import og, pages
from app.web.i18n import pick_lang
from app.web.legal import DELETION, PRIVACY, TERMS

router = APIRouter(include_in_schema=False)

AcceptLanguage = Annotated[str | None, Header()]
LangOverride = Annotated[str | None, Query(alias="lang")]


@router.get("/c/{code}", response_class=HTMLResponse)
async def challenge_landing(
    code: str,
    challenges: ChallengeServiceDep,
    settings: SettingsDep,
    accept_language: AcceptLanguage = None,
    lang: LangOverride = None,
) -> HTMLResponse:
    language = pick_lang(accept_language, lang)
    base = settings.public_base_url
    try:
        view = await challenges.get(code, viewer_id=None)
    except NotFound:
        return HTMLResponse(
            pages.not_found_page(language, base, settings.play_store_url), status_code=404
        )
    html = pages.challenge_page(
        lang=language,
        base_url=base,
        code=view.challenge.code,
        rival=view.creator_name,
        score=view.challenge.score,
        placed=view.challenge.placed,
        expired=view.expired,
        app_scheme=settings.app_url_scheme,
        ios_url=settings.ios_app_store_url,
        play_url=settings.play_store_url,
    )
    return HTMLResponse(html, headers={"Cache-Control": "public, max-age=60"})


@router.get("/og/{code}.png")
async def challenge_preview_image(
    code: str,
    challenges: ChallengeServiceDep,
    accept_language: AcceptLanguage = None,
    lang: LangOverride = None,
) -> Response:
    """The link preview (Open Graph) image: the score to beat in the Neon look."""
    language = pick_lang(accept_language, lang)
    try:
        view = await challenges.get(code, viewer_id=None)
        score: int | None = view.challenge.score
    except NotFound:
        score = None  # still a valid image: crawlers cache whatever they get
    return Response(
        og.render(score, language),
        media_type="image/png",
        headers={"Cache-Control": "public, max-age=86400"},
    )


@router.get("/.well-known/apple-app-site-association")
async def apple_app_site_association(settings: SettingsDep) -> JSONResponse:
    app_id = f"{settings.apple_team_id}.{settings.app_bundle_id}"
    return JSONResponse(
        {"applinks": {"details": [{"appIDs": [app_id], "components": [{"/": "/c/*"}]}]}}
    )


@router.get("/.well-known/assetlinks.json")
async def asset_links(settings: SettingsDep) -> JSONResponse:
    return JSONResponse(
        [
            {
                "relation": ["delegate_permission/common.handle_all_urls"],
                "target": {
                    "namespace": "android_app",
                    "package_name": settings.app_bundle_id,
                    "sha256_cert_fingerprints": settings.android_fingerprints,
                },
            }
        ]
    )


@router.get("/app-ads.txt", response_class=PlainTextResponse)
async def app_ads_txt(settings: SettingsDep) -> PlainTextResponse:
    if not settings.admob_publisher_id:
        return PlainTextResponse("", status_code=404)
    # f08c47fec0942fa0 is Google's fixed certification authority ID for app-ads.txt.
    return PlainTextResponse(
        f"google.com, {settings.admob_publisher_id}, DIRECT, f08c47fec0942fa0\n"
    )


def _legal(doc: dict, settings, accept_language: str | None, lang: str | None) -> HTMLResponse:
    language = pick_lang(accept_language, lang)
    title, sections = doc[language]
    filled = [(h, body.format(support_email=settings.support_email)) for h, body in sections]
    return HTMLResponse(pages.legal_page(language, settings.public_base_url, title, filled))


@router.get("/privacy", response_class=HTMLResponse)
async def privacy(
    settings: SettingsDep, accept_language: AcceptLanguage = None, lang: LangOverride = None
) -> HTMLResponse:
    return _legal(PRIVACY, settings, accept_language, lang)


@router.get("/terms", response_class=HTMLResponse)
async def terms(
    settings: SettingsDep, accept_language: AcceptLanguage = None, lang: LangOverride = None
) -> HTMLResponse:
    return _legal(TERMS, settings, accept_language, lang)


@router.get("/delete-account", response_class=HTMLResponse)
async def delete_account_info(
    settings: SettingsDep, accept_language: AcceptLanguage = None, lang: LangOverride = None
) -> HTMLResponse:
    # Google Play requires a web page explaining how to request account deletion.
    return _legal(DELETION, settings, accept_language, lang)
