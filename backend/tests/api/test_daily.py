from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from tests.api.test_friends_leaderboards import _befriend
from tests.factories import guest, verified_run

DAILY = {"daily": True}


async def test_everyone_plays_the_same_daily_tower(client: AsyncClient) -> None:
    a, b = await guest(client), await guest(client)

    first = (await client.post("/v1/runs", json=DAILY, headers=a.headers)).json()
    second = (await client.post("/v1/runs", json=DAILY, headers=b.headers)).json()
    normal = (await client.post("/v1/runs", json={}, headers=a.headers)).json()

    assert first["seed"] == second["seed"]
    assert first["daily_number"] == second["daily_number"] >= 1
    assert normal["daily_number"] is None


async def test_a_run_cannot_be_both_daily_and_a_challenge(client: AsyncClient) -> None:
    player = await guest(client)
    body = {"daily": True, "challenge_code": "ABCDEFG"}

    response = await client.post("/v1/runs", json=body, headers=player.headers)

    assert response.status_code == 400
    assert response.json()["error"]["code"] == "daily_and_challenge"


async def test_the_daily_board_ranks_friends_by_todays_daily_runs_only(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    me, friend = await guest(client), await guest(client)
    await _befriend(client, me, friend)
    await verified_run(client, db_session, me, perfect=8, sliced=2)  # a normal run: not counted
    await verified_run(client, db_session, me, perfect=2, sliced=2, start_body=DAILY)
    await verified_run(client, db_session, friend, perfect=4, sliced=2, start_body=DAILY)

    board = (await client.get("/v1/leaderboards/friends?period=daily", headers=me.headers)).json()

    assert board["period"] == "daily"
    scores = [(e["is_me"], e["score"]) for e in board["entries"]]
    assert [is_me for is_me, _ in scores] == [False, True]
    assert scores[0][1] > scores[1][1]
