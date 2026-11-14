from fastapi import FastAPI
from httpx import AsyncClient
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.clients.apple_tokens import AppleTokenError
from app.clients.identity import IdentityTokenError, VerifiedIdentity
from app.models import Run, User, Wallet
from tests.factories import guest, verified_run


class FakeVerifier:
    """Accepts tokens of the form 'valid:<subject>'."""

    async def verify(self, id_token: str, nonce: str | None = None) -> VerifiedIdentity:
        if not id_token.startswith("valid:"):
            raise IdentityTokenError("bad token")
        return VerifiedIdentity(subject=id_token.removeprefix("valid:"), email=None)


class FakeAppleTokens:
    """Hands out 'refresh:<code>'; `broken` stands for Apple refusing or being down."""

    def __init__(self, broken: bool = False) -> None:
        self.broken = broken
        self.redeemed: list[str] = []
        self.revoked: list[str] = []

    async def redeem(self, authorization_code: str) -> str | None:
        if self.broken:
            raise AppleTokenError("refused")
        self.redeemed.append(authorization_code)
        return f"refresh:{authorization_code}"

    async def revoke(self, refresh_token: str) -> None:
        if self.broken:
            raise AppleTokenError("refused")
        self.revoked.append(refresh_token)


def _use_fake_providers(app: FastAPI) -> None:
    app.state.identity_verifiers = {"google": FakeVerifier(), "apple": FakeVerifier()}


def _use_fake_apple_tokens(app: FastAPI, broken: bool = False) -> FakeAppleTokens:
    _use_fake_providers(app)
    app.state.apple_tokens = FakeAppleTokens(broken)
    return app.state.apple_tokens


def _bearer(tokens: dict) -> dict[str, str]:
    return {"Authorization": f"Bearer {tokens['access_token']}"}


async def _apple_login(client: AsyncClient, subject: str, code: str | None) -> dict:
    body = {**_token(subject), "authorization_code": code}
    response = await client.post("/v1/auth/apple/login", json=body)
    assert response.status_code == 200
    return response.json()


def _token(subject: str) -> dict:
    return {"id_token": f"valid:{subject}".ljust(24, "x")}


async def test_guest_account_is_bound_to_device_secret(client: AsyncClient) -> None:
    body = {"device_id": "device-abc-123", "device_secret": "s" * 40}
    first = (await client.post("/v1/auth/guest", json=body)).json()
    again = (await client.post("/v1/auth/guest", json=body)).json()
    stolen = await client.post("/v1/auth/guest", json={**body, "device_secret": "x" * 40})

    assert first["is_new_user"] is True
    assert again["user_id"] == first["user_id"] and again["is_new_user"] is False
    assert stolen.status_code == 401
    assert stolen.json()["error"]["code"] == "invalid_device_credentials"


async def test_new_account_has_profile_and_empty_wallet(client: AsyncClient) -> None:
    player = await guest(client)

    profile = (await client.get("/v1/me", headers=player.headers)).json()

    assert profile["coins"] == 0
    assert profile["best_score"] == 0
    assert profile["providers"] == ["device"]
    assert len(profile["friend_code"]) == 8


async def test_refresh_rotates_and_reuse_revokes_the_family(client: AsyncClient) -> None:
    player = await guest(client)

    rotated = await client.post("/v1/auth/refresh", json={"refresh_token": player.refresh_token})
    reuse = await client.post("/v1/auth/refresh", json={"refresh_token": player.refresh_token})
    after_theft = await client.post(
        "/v1/auth/refresh", json={"refresh_token": rotated.json()["refresh_token"]}
    )

    assert rotated.status_code == 200
    assert reuse.status_code == 401
    assert after_theft.status_code == 401  # the whole family died with the reuse


async def test_requests_without_valid_token_are_rejected(client: AsyncClient) -> None:
    assert (await client.get("/v1/me")).status_code == 401
    bad = await client.get("/v1/me", headers={"Authorization": "Bearer nope"})
    assert bad.json()["error"]["code"] == "invalid_token"


async def test_linking_google_keeps_guest_progress_on_a_new_device(
    app: FastAPI, client: AsyncClient
) -> None:
    _use_fake_providers(app)
    player = await guest(client)

    linked = await client.post("/v1/auth/google/link", json=_token("g-42"), headers=player.headers)
    login = (await client.post("/v1/auth/google/login", json=_token("g-42"))).json()

    assert linked.status_code == 204
    assert login["user_id"] == player.user_id
    assert login["is_new_user"] is False


async def test_linking_an_identity_owned_by_another_account_conflicts(
    app: FastAPI, client: AsyncClient
) -> None:
    _use_fake_providers(app)
    await client.post("/v1/auth/apple/login", json=_token("a-1"))
    player = await guest(client)

    response = await client.post("/v1/auth/apple/link", json=_token("a-1"), headers=player.headers)

    assert response.status_code == 409
    assert response.json()["error"]["code"] == "identity_in_use"


async def test_invalid_provider_token_is_401(app: FastAPI, client: AsyncClient) -> None:
    _use_fake_providers(app)

    response = await client.post("/v1/auth/google/login", json={"id_token": "forged" * 5})

    assert response.status_code == 401


async def test_google_without_client_ids_says_so_instead_of_blaming_the_token(
    client: AsyncClient,
) -> None:
    """GOOGLE_CLIENT_IDS unset on the server: one curl should show it."""
    response = await client.post("/v1/auth/google/login", json=_token("g-1"))

    assert response.status_code == 503
    assert response.json()["error"]["code"] == "provider_not_configured"


async def test_account_deletion_removes_everything_and_kills_tokens(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    await verified_run(client, db_session, player)

    deleted = await client.delete("/v1/me", headers=player.headers)
    after = await client.get("/v1/me", headers=player.headers)

    assert deleted.status_code == 204
    assert after.status_code == 401
    async with db_session.begin():
        for model in (User, Run, Wallet):
            assert await db_session.scalar(select(func.count()).select_from(model)) == 0


async def test_deleting_an_apple_account_revokes_its_token_at_apple(
    app: FastAPI, client: AsyncClient
) -> None:
    apple = _use_fake_apple_tokens(app)
    tokens = await _apple_login(client, "apple-user", "code-1")

    deleted = await client.delete("/v1/me", headers=_bearer(tokens))

    assert deleted.status_code == 204
    assert apple.redeemed == ["code-1"]
    assert apple.revoked == ["refresh:code-1"]


async def test_linking_apple_to_a_guest_keeps_the_token_for_deletion(
    app: FastAPI, client: AsyncClient
) -> None:
    apple = _use_fake_apple_tokens(app)
    player = await guest(client)

    await client.post(
        "/v1/auth/apple/link",
        json={**_token("apple-user"), "authorization_code": "code-1"},
        headers=player.headers,
    )
    await client.delete("/v1/me", headers=player.headers)

    assert apple.revoked == ["refresh:code-1"]


async def test_signing_in_again_keeps_the_newest_token(app: FastAPI, client: AsyncClient) -> None:
    apple = _use_fake_apple_tokens(app)
    await _apple_login(client, "apple-user", "code-1")
    tokens = await _apple_login(client, "apple-user", "code-2")

    await client.delete("/v1/me", headers=_bearer(tokens))

    assert apple.revoked == ["refresh:code-2"]


async def test_signing_in_without_a_code_keeps_the_token_we_have(
    app: FastAPI, client: AsyncClient
) -> None:
    apple = _use_fake_apple_tokens(app)
    await _apple_login(client, "apple-user", "code-1")
    tokens = await _apple_login(client, "apple-user", None)

    await client.delete("/v1/me", headers=_bearer(tokens))

    assert apple.revoked == ["refresh:code-1"]


async def test_apple_refusing_a_code_never_blocks_sign_in(
    app: FastAPI, client: AsyncClient
) -> None:
    _use_fake_apple_tokens(app, broken=True)

    tokens = await _apple_login(client, "apple-user", "code-1")

    assert tokens["is_new_user"] is True


async def test_apple_being_down_never_blocks_deleting_an_account(
    app: FastAPI, client: AsyncClient
) -> None:
    apple = _use_fake_apple_tokens(app)
    tokens = await _apple_login(client, "apple-user", "code-1")
    apple.broken = True

    deleted = await client.delete("/v1/me", headers=_bearer(tokens))

    assert deleted.status_code == 204
    assert (await client.get("/v1/me", headers=_bearer(tokens))).status_code in (401, 404)


async def test_an_account_with_no_apple_token_revokes_nothing(
    app: FastAPI, client: AsyncClient
) -> None:
    apple = _use_fake_apple_tokens(app)
    player = await guest(client)

    await client.delete("/v1/me", headers=player.headers)

    assert apple.revoked == []


async def test_google_never_redeems_a_code(app: FastAPI, client: AsyncClient) -> None:
    apple = _use_fake_apple_tokens(app)

    response = await client.post(
        "/v1/auth/google/login", json={**_token("google-user"), "authorization_code": "code-1"}
    )

    assert response.status_code == 200
    assert apple.redeemed == []
