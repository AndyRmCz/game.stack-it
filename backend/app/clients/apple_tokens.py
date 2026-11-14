"""Sign in with Apple's tokens, so that deleting an account also ends it at Apple.

App Store guideline 5.1.1(v) asks apps with Sign in with Apple to revoke the user's tokens when
the account is deleted. The app gets a one-time authorization code at sign-in; we trade it for a
refresh token and keep that until deletion. Calls need a Sign in with Apple key (APPLE_KEY_ID,
APPLE_PRIVATE_KEY): without one this does nothing and sign-in works as usual.
"""

import time
from typing import Protocol

import httpx
import jwt

from app.core.config import Settings

APPLE_ISSUER = "https://appleid.apple.com"
APPLE_TOKEN_URL = "https://appleid.apple.com/auth/token"
APPLE_REVOKE_URL = "https://appleid.apple.com/auth/revoke"

_CLIENT_SECRET_TTL_SECONDS = 300
_TIMEOUT_SECONDS = 5


class AppleTokenError(Exception):
    pass


class AppleTokens(Protocol):
    async def redeem(self, authorization_code: str) -> str | None: ...

    async def revoke(self, refresh_token: str) -> None: ...


class DisabledAppleTokens:
    async def redeem(self, authorization_code: str) -> str | None:
        return None

    async def revoke(self, refresh_token: str) -> None:
        return None


class AppleTokenClient:
    def __init__(
        self,
        *,
        team_id: str,
        key_id: str,
        private_key: str,
        client_id: str,
        transport: httpx.AsyncBaseTransport | None = None,
    ) -> None:
        self._team_id = team_id
        self._key_id = key_id
        self._private_key = private_key
        self._client_id = client_id
        self._transport = transport

    def _client_secret(self) -> str:
        now = int(time.time())
        try:
            return jwt.encode(
                {
                    "iss": self._team_id,
                    "iat": now,
                    "exp": now + _CLIENT_SECRET_TTL_SECONDS,
                    "aud": APPLE_ISSUER,
                    "sub": self._client_id,
                },
                self._private_key,
                algorithm="ES256",
                headers={"kid": self._key_id},
            )
        except (jwt.PyJWTError, ValueError, TypeError) as exc:
            raise AppleTokenError("unusable private key") from exc

    async def _post(self, url: str, form: dict[str, str]) -> httpx.Response:
        data = {"client_id": self._client_id, "client_secret": self._client_secret(), **form}
        try:
            async with httpx.AsyncClient(
                timeout=_TIMEOUT_SECONDS, transport=self._transport
            ) as client:
                response = await client.post(url, data=data)
                response.raise_for_status()
                return response
        except httpx.HTTPError as exc:
            raise AppleTokenError(f"{url}: {type(exc).__name__}") from exc

    async def redeem(self, authorization_code: str) -> str | None:
        response = await self._post(
            APPLE_TOKEN_URL, {"code": authorization_code, "grant_type": "authorization_code"}
        )
        refresh_token = response.json().get("refresh_token")
        if not refresh_token:
            raise AppleTokenError("no refresh token in the reply")
        return refresh_token

    async def revoke(self, refresh_token: str) -> None:
        await self._post(
            APPLE_REVOKE_URL, {"token": refresh_token, "token_type_hint": "refresh_token"}
        )


def build_apple_tokens(settings: Settings) -> AppleTokens:
    if not (settings.apple_team_id and settings.apple_key_id and settings.apple_signing_key):
        return DisabledAppleTokens()
    return AppleTokenClient(
        team_id=settings.apple_team_id,
        key_id=settings.apple_key_id,
        private_key=settings.apple_signing_key,
        client_id=settings.apple_audiences[0],
    )
