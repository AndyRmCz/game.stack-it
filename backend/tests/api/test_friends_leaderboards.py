import uuid
from datetime import UTC, datetime, timedelta

from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.economy import week_start
from app.models import Run
from tests.factories import Player, guest


async def _friend_code(client: AsyncClient, player: Player) -> str:
    return (await client.get("/v1/me", headers=player.headers)).json()["friend_code"]


async def _befriend(client: AsyncClient, a: Player, b: Player) -> None:
    code = await _friend_code(client, b)
    response = await client.post("/v1/friends", json={"friend_code": code}, headers=a.headers)
    assert response.status_code == 201


async def _seed_run(
    db: AsyncSession,
    player: Player,
    score: int,
    finished: datetime,
    status="verified",
    placed: int | None = None,
):
    async with db.begin():
        db.add(
            Run(
                user_id=uuid.UUID(player.user_id),
                seed=1,
                sim_version=1,
                continues_allowed=True,
                status=status,
                verified_score=score if status != "rejected" else None,
                placed=placed,
                finished_at=finished,
            )
        )


async def test_friend_codes_create_symmetric_friendships(client: AsyncClient) -> None:
    a, b = await guest(client), await guest(client)

    await _befriend(client, a, b)
    await _befriend(client, b, a)  # idempotent the other way round

    assert [f["user_id"] for f in (await client.get("/v1/friends", headers=b.headers)).json()] == [
        a.user_id
    ]
    removed = await client.delete(f"/v1/friends/{b.user_id}", headers=a.headers)
    assert removed.status_code == 204
    assert (await client.get("/v1/friends", headers=b.headers)).json() == []


async def test_friend_list_carries_best_score_and_its_height_for_the_ghost(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    me, friend = await guest(client), await guest(client)
    await _befriend(client, me, friend)
    now = datetime.now(UTC)
    await _seed_run(db_session, friend, 30, now, placed=25)
    await _seed_run(db_session, friend, 55, now, placed=41)
    await _seed_run(db_session, friend, 900, now, placed=300, status="rejected")

    friends = (await client.get("/v1/friends", headers=me.headers)).json()

    assert [(f["best_score"], f["best_placed"]) for f in friends] == [(55, 41)]


async def test_cannot_befriend_yourself_or_unknown_codes(client: AsyncClient) -> None:
    a = await guest(client)

    own = await client.post(
        "/v1/friends", json={"friend_code": await _friend_code(client, a)}, headers=a.headers
    )
    unknown = await client.post("/v1/friends", json={"friend_code": "ZZZZ9999"}, headers=a.headers)

    assert own.status_code == 400
    assert unknown.status_code == 404


async def test_leaderboards_rank_friends_by_best_verified_score(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    me, friend, rival, stranger = [await guest(client) for _ in range(4)]
    await _befriend(client, me, friend)
    await _befriend(client, me, rival)
    monday = week_start(datetime.now(UTC))
    await _seed_run(db_session, me, 40, monday + timedelta(minutes=5))
    await _seed_run(db_session, friend, 90, monday - timedelta(seconds=1))  # last week
    await _seed_run(db_session, friend, 30, monday + timedelta(hours=1))
    await _seed_run(db_session, rival, 40, monday + timedelta(hours=2))
    await _seed_run(db_session, rival, 500, monday + timedelta(hours=3), status="flagged")
    await _seed_run(db_session, stranger, 999, monday + timedelta(hours=1))

    all_time = (await client.get("/v1/leaderboards/friends", headers=me.headers)).json()
    weekly = (await client.get("/v1/leaderboards/friends?period=weekly", headers=me.headers)).json()

    assert [(e["rank"], e["score"]) for e in all_time["entries"]] == [(1, 90), (2, 40), (2, 40)]
    assert [e["user_id"] for e in weekly["entries"]][-1] == friend.user_id
    assert [(e["rank"], e["score"]) for e in weekly["entries"]] == [(1, 40), (1, 40), (3, 30)]
    assert weekly["since"].startswith(monday.date().isoformat())
    assert sum(e["is_me"] for e in weekly["entries"]) == 1
