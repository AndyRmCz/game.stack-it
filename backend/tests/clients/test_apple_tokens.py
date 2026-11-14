import time
from urllib.parse import parse_qs

import httpx
import jwt
import pytest
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec

from app.clients.apple_tokens import (
    AppleTokenClient,
    AppleTokenError,
    DisabledAppleTokens,
    build_apple_tokens,
)
from app.core.config import Settings

_KEY = ec.generate_private_key(ec.SECP256R1())
_PEM = _KEY.private_bytes(
    serialization.Encoding.PEM,
    serialization.PrivateFormat.PKCS8,
    serialization.NoEncryption(),
).decode()


def _client(handler) -> tuple[AppleTokenClient, list[httpx.Request]]:
    requests: list[httpx.Request] = []

    def record(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        return handler(request)

    client = AppleTokenClient(
        team_id="TEAM123456",
        key_id="KEY1234567",
        private_key=_PEM,
        client_id="game.milli.com",
        transport=httpx.MockTransport(record),
    )
    return client, requests


def _form(request: httpx.Request) -> dict[str, str]:
    return {k: v[0] for k, v in parse_qs(request.content.decode()).items()}


async def test_a_code_is_redeemed_for_a_refresh_token() -> None:
    client, requests = _client(lambda _: httpx.Response(200, json={"refresh_token": "r.abc"}))

    assert await client.redeem("one-time-code") == "r.abc"

    assert str(requests[0].url) == "https://appleid.apple.com/auth/token"
    form = _form(requests[0])
    assert form["client_id"] == "game.milli.com"
    assert form["grant_type"] == "authorization_code"
    assert form["code"] == "one-time-code"


async def test_the_client_secret_is_a_short_lived_es256_token_signed_with_our_key() -> None:
    client, requests = _client(lambda _: httpx.Response(200, json={"refresh_token": "r.abc"}))

    await client.redeem("one-time-code")

    secret = _form(requests[0])["client_secret"]
    assert jwt.get_unverified_header(secret)["kid"] == "KEY1234567"
    claims = jwt.decode(
        secret,
        _KEY.public_key(),
        algorithms=["ES256"],
        audience="https://appleid.apple.com",
        issuer="TEAM123456",
    )
    assert claims["sub"] == "game.milli.com"
    assert claims["exp"] - time.time() <= 600


async def test_revoking_names_the_token_and_its_kind() -> None:
    client, requests = _client(lambda _: httpx.Response(200))

    await client.revoke("r.abc")

    assert str(requests[0].url) == "https://appleid.apple.com/auth/revoke"
    form = _form(requests[0])
    assert form["client_id"] == "game.milli.com"
    assert form["token"] == "r.abc"
    assert form["token_type_hint"] == "refresh_token"
    assert "client_secret" in form


async def test_apple_refusing_a_code_is_an_error() -> None:
    client, _ = _client(lambda _: httpx.Response(400, json={"error": "invalid_grant"}))

    with pytest.raises(AppleTokenError):
        await client.redeem("expired-code")


async def test_a_reply_without_a_token_is_an_error() -> None:
    client, _ = _client(lambda _: httpx.Response(200, json={}))

    with pytest.raises(AppleTokenError):
        await client.redeem("one-time-code")


async def test_apple_being_unreachable_is_an_error() -> None:
    def unreachable(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("no route", request=request)

    client, _ = _client(unreachable)

    with pytest.raises(AppleTokenError):
        await client.revoke("r.abc")


async def test_a_bad_private_key_is_an_error_not_a_crash() -> None:
    client = AppleTokenClient(
        team_id="TEAM123456", key_id="KEY1234567", private_key="nonsense", client_id="id"
    )

    with pytest.raises(AppleTokenError):
        await client.redeem("one-time-code")


async def test_without_a_key_nothing_happens() -> None:
    tokens = build_apple_tokens(Settings(apple_team_id="", apple_key_id="", apple_private_key=""))

    assert isinstance(tokens, DisabledAppleTokens)
    assert await tokens.redeem("one-time-code") is None
    await tokens.revoke("r.abc")


def test_a_private_key_on_one_line_works() -> None:
    # Hosting dashboards often keep a variable on one line, with literal \n.
    settings = Settings(
        apple_team_id="TEAM123456",
        apple_key_id="KEY1234567",
        apple_private_key=_PEM.replace("\n", "\\n"),
    )

    assert settings.apple_signing_key == _PEM
    assert isinstance(build_apple_tokens(settings), AppleTokenClient)
