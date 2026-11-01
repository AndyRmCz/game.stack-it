from functools import lru_cache
from typing import Literal, Self

from pydantic import field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

_ASYNC_DRIVER_SCHEME = "postgresql+asyncpg://"
_PLAIN_SCHEMES = ("postgres://", "postgresql://")
_DEV_JWT_SECRET = "dev-only-insecure-jwt-secret-change-me-please"


def _csv(value: str) -> list[str]:
    return [item.strip() for item in value.split(",") if item.strip()]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    environment: Literal["local", "test", "production"] = "local"
    log_level: str = "INFO"

    database_url: str = "postgresql+asyncpg://stackit:stackit@localhost:5432/stackit"

    # Single source of truth for every public link (challenge URLs, OG tags, well-known files).
    # Switching to a custom domain is a change to this one variable.
    public_base_url: str = "http://localhost:8000"

    # Comma-separated list. Empty means no cross-origin browser access (the app is native).
    cors_allowed_origins: str = ""

    # Placeholder bundle / package ID, kept in one place (mirrors the Unity project setting).
    app_bundle_id: str = "game.milli.com"
    app_url_scheme: str = "milli"

    # --- Auth ---
    jwt_secret: str = _DEV_JWT_SECRET
    access_token_ttl_seconds: int = 900
    refresh_token_ttl_days: int = 60
    # Comma-separated OAuth client IDs accepted as the `aud` of Google ID tokens (the Android
    # app sends tokens issued for the *web* client ID; iOS for the iOS client ID).
    google_client_ids: str = ""
    # Comma-separated `aud` values for Sign in with Apple. Defaults to the bundle ID.
    apple_client_ids: str = ""

    # --- Gameplay ---
    run_ttl_hours: int = 48
    challenge_ttl_days: int = 7
    rate_limit_enabled: bool = True

    # --- Web / store links ---
    apple_team_id: str = ""
    android_sha256_fingerprints: str = ""
    ios_app_store_url: str = ""
    android_play_store_url: str = ""
    admob_publisher_id: str = ""
    support_email: str = "support@example.com"

    @field_validator("database_url")
    @classmethod
    def _use_asyncpg_driver(cls, value: str) -> str:
        # Railway and most providers hand out plain `postgresql://` URLs; SQLAlchemy needs the
        # async driver named explicitly.
        for scheme in _PLAIN_SCHEMES:
            if value.startswith(scheme):
                return _ASYNC_DRIVER_SCHEME + value.removeprefix(scheme)
        return value

    @field_validator("public_base_url")
    @classmethod
    def _strip_trailing_slash(cls, value: str) -> str:
        return value.rstrip("/")

    @model_validator(mode="after")
    def _production_requires_real_secret(self) -> Self:
        if self.environment == "production" and (
            self.jwt_secret == _DEV_JWT_SECRET or len(self.jwt_secret) < 32
        ):
            raise ValueError("JWT_SECRET must be set to 32+ random characters in production")
        return self

    @property
    def cors_origins(self) -> list[str]:
        return _csv(self.cors_allowed_origins)

    @property
    def google_audiences(self) -> list[str]:
        return _csv(self.google_client_ids)

    @property
    def apple_audiences(self) -> list[str]:
        return _csv(self.apple_client_ids) or [self.app_bundle_id]

    @property
    def android_fingerprints(self) -> list[str]:
        return _csv(self.android_sha256_fingerprints)

    @property
    def play_store_url(self) -> str:
        return (
            self.android_play_store_url
            or f"https://play.google.com/store/apps/details?id={self.app_bundle_id}"
        )


@lru_cache
def get_settings() -> Settings:
    return Settings()
