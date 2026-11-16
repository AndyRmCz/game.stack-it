from httpx import AsyncClient
from sqlalchemy import event
from sqlalchemy.ext.asyncio import AsyncEngine, AsyncSession

from tests.factories import guest, verified_run


async def test_new_players_see_three_unclaimed_missions_and_a_chest(client: AsyncClient) -> None:
    player = await guest(client)

    body = (await client.get("/v1/missions", headers=player.headers)).json()

    assert [m["slot"] for m in body["missions"]] == [0, 1, 2]
    assert all(m["progress"] == 0 and not m["claimed"] for m in body["missions"])
    assert body["chest"]["ready"] is False


async def test_finished_missions_pay_once_and_unfinished_ones_cannot_be_claimed(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    # Three daily runs of 16 blocks with 6 perfects in a row finish every easy mission.
    for _ in range(3):
        await verified_run(client, db_session, player, 6, 10, start_body={"daily": True})
    before = (await client.get("/v1/wallet", headers=player.headers)).json()["balance"]
    missions = (await client.get("/v1/missions", headers=player.headers)).json()["missions"]
    easy = missions[0]
    assert easy["progress"] >= easy["target"]

    first = await client.post("/v1/missions/claim", json={"slot": 0}, headers=player.headers)
    again = await client.post("/v1/missions/claim", json={"slot": 0}, headers=player.headers)

    assert first.status_code == again.status_code == 200
    assert first.json()["missions"][0]["claimed"] is True
    assert first.json()["balance"] == again.json()["balance"] == before + easy["reward"]
    unfinished = [m for m in missions if m["progress"] < m["target"]]
    if unfinished:
        refused = await client.post(
            "/v1/missions/claim", json={"slot": unfinished[0]["slot"]}, headers=player.headers
        )
        assert refused.status_code == 409
        assert refused.json()["error"]["code"] == "mission_incomplete"
    chest = await client.post("/v1/missions/claim", json={"slot": 3}, headers=player.headers)
    assert chest.status_code == (200 if not unfinished else 409)


async def test_reading_missions_checks_every_claim_in_one_query(
    client: AsyncClient, engine: AsyncEngine
) -> None:
    # Each query is a round trip to the database; one per claim made the screen slow to open.
    player = await guest(client)
    selects = []

    def _count(conn, cursor, statement, *args) -> None:
        if statement.lstrip().upper().startswith("SELECT"):
            selects.append(statement)

    event.listen(engine.sync_engine, "before_cursor_execute", _count)
    try:
        response = await client.get("/v1/missions", headers=player.headers)
    finally:
        event.remove(engine.sync_engine, "before_cursor_execute", _count)

    assert response.status_code == 200
    assert len(selects) == 3, "the token's user, the day's runs, and the claims"
