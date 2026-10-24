import uuid
from datetime import UTC, datetime, timedelta

from httpx import AsyncClient
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.telemetry import MAX_BATCH
from app.models import ClientEvent
from tests.factories import guest


def _event(name: str = "screen_view", **props: object) -> dict:
    return {
        "id": str(uuid.uuid4()),
        "name": name,
        "session_id": "5f1b6c1e-0000-4000-8000-000000000001",
        "at": datetime.now(UTC).isoformat(),
        "props": props or {"screen": "HomeScreen"},
    }


def _batch(*events: dict) -> dict:
    return {"app_version": "0.1.0", "platform": "android", "events": list(events)}


async def _stored(db: AsyncSession, user_id: str) -> list[ClientEvent]:
    rows = await db.scalars(
        select(ClientEvent)
        .where(ClientEvent.user_id == uuid.UUID(user_id))
        .order_by(ClientEvent.occurred_at)
    )
    return list(rows)


async def test_events_are_stored_for_the_signed_in_player(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    shown = _event("ad_shown", format="rewarded", placement="continue")

    response = await client.post("/v1/events", json=_batch(shown), headers=player.headers)

    assert response.status_code == 200, response.text
    assert response.json() == {"accepted": 1, "rejected": 0}
    [row] = await _stored(db_session, player.user_id)
    assert row.name == "ad_shown"
    assert row.props == {"format": "rewarded", "placement": "continue"}
    assert row.app_version == "0.1.0"
    assert row.platform == "android"
    assert str(row.session_id) == shown["session_id"]


async def test_a_resent_batch_is_stored_once(client: AsyncClient, db_session: AsyncSession) -> None:
    player = await guest(client)
    batch = _batch(_event(), _event())

    first = await client.post("/v1/events", json=batch, headers=player.headers)
    retry = await client.post("/v1/events", json=batch, headers=player.headers)

    assert first.json() == {"accepted": 2, "rejected": 0}
    assert retry.json() == {"accepted": 0, "rejected": 0}
    assert len(await _stored(db_session, player.user_id)) == 2


async def test_unknown_events_and_props_are_rejected_but_the_rest_is_kept(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    batch = _batch(
        _event(),
        _event("purchase_real_money", sku="coins_1000"),
        _event("screen_view", screen="Home", email="a@b.c"),
    )

    response = await client.post("/v1/events", json=batch, headers=player.headers)

    assert response.json() == {"accepted": 1, "rejected": 2}
    assert [r.name for r in await _stored(db_session, player.user_id)] == ["screen_view"]


async def test_an_implausible_client_clock_is_replaced_by_server_time(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    stale = _event()
    stale["at"] = (datetime.now(UTC) - timedelta(days=90)).isoformat()

    await client.post("/v1/events", json=_batch(stale), headers=player.headers)

    [row] = await _stored(db_session, player.user_id)
    assert row.occurred_at == row.received_at
    assert row.client_at < row.received_at - timedelta(days=89)


async def test_batches_need_a_token_and_a_sane_size(client: AsyncClient) -> None:
    player = await guest(client)

    anonymous = await client.post("/v1/events", json=_batch(_event()))
    empty = await client.post("/v1/events", json=_batch(), headers=player.headers)
    too_big = await client.post(
        "/v1/events",
        json=_batch(*[_event() for _ in range(MAX_BATCH + 1)]),
        headers=player.headers,
    )

    assert anonymous.status_code == 401
    assert empty.status_code == 422
    assert too_big.status_code == 422


async def test_deleting_the_account_deletes_its_events(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    await client.post("/v1/events", json=_batch(_event(), _event()), headers=player.headers)

    await client.delete("/v1/me", headers=player.headers)

    remaining = await db_session.scalar(
        select(func.count())
        .select_from(ClientEvent)
        .where(ClientEvent.user_id == uuid.UUID(player.user_id))
    )
    assert remaining == 0
