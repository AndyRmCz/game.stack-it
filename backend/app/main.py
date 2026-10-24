from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import APIRouter, FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from app.api import auth, economy, health, me, missions, runs, social, telemetry
from app.clients.identity import build_verifiers
from app.core.config import get_settings
from app.core.db import get_engine
from app.core.errors import install_error_handlers
from app.core.logging import configure_logging
from app.core.middleware import install_request_logging
from app.core.rate_limit import RateLimiter
from app.web import routes as web

STATIC_DIR = Path(__file__).resolve().parent / "web" / "static"


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncIterator[None]:
    yield
    await get_engine().dispose()


def create_app() -> FastAPI:
    settings = get_settings()
    configure_logging(settings.log_level)

    app = FastAPI(title="Stack IT API", version="0.1.0", lifespan=lifespan)
    app.state.rate_limiter = RateLimiter()
    app.state.identity_verifiers = build_verifiers(
        settings.google_audiences, settings.apple_audiences
    )
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.cors_origins,
        allow_methods=["GET", "POST", "PATCH", "DELETE"],
        allow_headers=["Authorization", "Content-Type", "Idempotency-Key"],
    )
    install_error_handlers(app)
    install_request_logging(app)

    v1 = APIRouter(prefix="/v1")
    for module in (auth, me, runs, social, economy, missions, telemetry):
        v1.include_router(module.router)
    app.include_router(health.router)
    app.include_router(v1)
    app.include_router(web.router)
    app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")
    return app


app = create_app()
