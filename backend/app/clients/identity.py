"""Verification of Google and Sign in with Apple ID tokens (OIDC JWTs signed with RS256)."""

import hashlib
import time
from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from typing import Any, Protocol

import httpx
import jwt

GOOGLE_ISSUERS = ("https://accounts.google.com", "accounts.google.com")
GOOGLE_JWKS_URL = "https://www.googleapis.com/oauth2/v3/certs"
APPLE_ISSUERS = ("https://appleid.apple.com",)
APPLE_JWKS_URL = "https://appleid.apple.com/auth/keys"

_JWKS_TTL_SECONDS = 3600
_LEEWAY_SECONDS = 60


class IdentityTokenError(Exception):
    pass


@dataclass(frozen=True, slots=True)
class VerifiedIdentity:
    subject: str
    email: str | None


class IdentityVerifier(Protocol):
    async def verify(self, id_token: str, nonce: str | None = None) -> VerifiedIdentity: ...


def http_jwks_fetcher(url: str) -> Callable[[], Awaitable[dict[str, Any]]]:
    async def _fetch() -> dict[str, Any]:
        async with httpx.AsyncClient(timeout=5) as client:
            response = await client.get(url)
            response.raise_for_status()
            return response.json()

    return _fetch


class OidcVerifier:
    """Validates signature (JWKS, cached and refreshed on unknown `kid`), issuer, audience, expiry
    and, when the client sent one, the nonce. Apple stores the SHA-256 of the nonce; Google the
    raw value."""

    def __init__(
        self,
        *,
        issuers: tuple[str, ...],
        audiences: list[str],
        fetch_jwks: Callable[[], Awaitable[dict[str, Any]]],
        hash_nonce: bool,
    ) -> None:
        self._issuers = issuers
        self._audiences = audiences
        self._fetch_jwks = fetch_jwks
        self._hash_nonce = hash_nonce
        self._keys: dict[str, jwt.PyJWK] = {}
        self._fetched_at = 0.0

    async def _key_for(self, kid: str) -> jwt.PyJWK:
        stale = time.monotonic() - self._fetched_at > _JWKS_TTL_SECONDS
        if stale or kid not in self._keys:
            jwks = await self._fetch_jwks()
            self._keys = {k["kid"]: jwt.PyJWK(k) for k in jwks.get("keys", []) if "kid" in k}
            self._fetched_at = time.monotonic()
        if kid not in self._keys:
            raise IdentityTokenError("unknown signing key")
        return self._keys[kid]

    async def verify(self, id_token: str, nonce: str | None = None) -> VerifiedIdentity:
        if not self._audiences:
            raise IdentityTokenError("provider not configured")
        try:
            kid = jwt.get_unverified_header(id_token).get("kid")
            if not kid:
                raise IdentityTokenError("missing kid")
            key = await self._key_for(kid)
            claims = jwt.decode(
                id_token,
                key=key.key,
                algorithms=["RS256"],
                audience=self._audiences,
                options={"require": ["iss", "aud", "exp", "sub"]},
                leeway=_LEEWAY_SECONDS,
            )
        except jwt.PyJWTError as exc:
            raise IdentityTokenError(str(exc)) from exc

        if claims["iss"] not in self._issuers:
            raise IdentityTokenError("wrong issuer")
        if nonce is not None:
            expected = hashlib.sha256(nonce.encode()).hexdigest() if self._hash_nonce else nonce
            if claims.get("nonce") != expected:
                raise IdentityTokenError("nonce mismatch")
        return VerifiedIdentity(subject=str(claims["sub"]), email=claims.get("email"))


def build_verifiers(google_audiences: list[str], apple_audiences: list[str]) -> dict:
    return {
        "google": OidcVerifier(
            issuers=GOOGLE_ISSUERS,
            audiences=google_audiences,
            fetch_jwks=http_jwks_fetcher(GOOGLE_JWKS_URL),
            hash_nonce=False,
        ),
        "apple": OidcVerifier(
            issuers=APPLE_ISSUERS,
            audiences=apple_audiences,
            fetch_jwks=http_jwks_fetcher(APPLE_JWKS_URL),
            hash_nonce=True,
        ),
    }
