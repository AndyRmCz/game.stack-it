import uuid

from httpx import AsyncClient
from sqlalchemy import update
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.economy import DAILY_REWARDS
from app.models import Wallet
from tests.factories import Player, guest


async def _fund(db: AsyncSession, player: Player, coins: int) -> None:
    async with db.begin():
        await db.execute(
            update(Wallet).where(Wallet.user_id == uuid.UUID(player.user_id)).values(balance=coins)
        )


def _key(value: str) -> dict:
    return {"Idempotency-Key": value}


async def test_purchase_retry_with_same_key_charges_once(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    await _fund(db_session, player, 500)
    headers = player.headers | _key("buy-glass-1")

    first = await client.post("/v1/shop/purchase", json={"skin_id": "glass"}, headers=headers)
    retry = await client.post("/v1/shop/purchase", json={"skin_id": "glass"}, headers=headers)

    assert first.status_code == retry.status_code == 200
    assert first.json()["balance"] == retry.json()["balance"] == 300
    glass = next(i for i in retry.json()["items"] if i["skin_id"] == "glass")
    assert glass["owned"] is True
    wallet = (await client.get("/v1/wallet", headers=player.headers)).json()
    assert [t["amount"] for t in wallet["transactions"]] == [-200]


async def test_purchase_rules(client: AsyncClient, db_session: AsyncSession) -> None:
    player = await guest(client)
    await _fund(db_session, player, 250)
    h = player.headers

    missing_key = await client.post("/v1/shop/purchase", json={"skin_id": "glass"}, headers=h)
    poor = await client.post("/v1/shop/purchase", json={"skin_id": "gold"}, headers=h | _key("k1"))
    default = await client.post(
        "/v1/shop/purchase", json={"skin_id": "classic"}, headers=h | _key("k2")
    )
    await client.post("/v1/shop/purchase", json={"skin_id": "glass"}, headers=h | _key("k3"))
    twice = await client.post(
        "/v1/shop/purchase", json={"skin_id": "glass"}, headers=h | _key("k4")
    )

    assert missing_key.status_code == 400
    assert poor.json()["error"]["code"] == "insufficient_funds"
    assert default.json()["error"]["code"] == "already_owned"
    assert twice.json()["error"]["code"] == "already_owned"
    assert (await client.get("/v1/wallet", headers=h)).json()["balance"] == 50


async def test_equip_requires_ownership(client: AsyncClient, db_session: AsyncSession) -> None:
    player = await guest(client)
    await _fund(db_session, player, 400)
    h = player.headers

    locked = await client.post("/v1/skins/equip", json={"skin_id": "magma"}, headers=h)
    await client.post("/v1/shop/purchase", json={"skin_id": "magma"}, headers=h | _key("m"))
    equipped = await client.post("/v1/skins/equip", json={"skin_id": "magma"}, headers=h)

    assert locked.status_code == 409
    assert equipped.status_code == 200
    assert (await client.get("/v1/me", headers=h)).json()["equipped_block_skin"] == "magma"


async def test_daily_reward_once_per_day_and_idempotent(client: AsyncClient) -> None:
    player = await guest(client)
    h = player.headers

    status = (await client.get("/v1/daily", headers=h)).json()
    claim = await client.post("/v1/daily/claim", headers=h | _key("day-1"))
    retry = await client.post("/v1/daily/claim", headers=h | _key("day-1"))
    second = await client.post("/v1/daily/claim", headers=h | _key("day-1-again"))

    assert status == {
        **status,
        "streak": 0,
        "claimed_today": False,
        "next_reward": DAILY_REWARDS[0],
    }
    assert claim.json()["claimed_amount"] == DAILY_REWARDS[0]
    assert retry.json()["balance"] == claim.json()["balance"] == DAILY_REWARDS[0]
    assert second.status_code == 409
