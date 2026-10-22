from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.economy import coins_for_run
from tests.factories import backdate_run, guest, play, verified_run


async def _start(client: AsyncClient, player, **body) -> dict:
    response = await client.post("/v1/runs", json=body, headers=player.headers)
    assert response.status_code == 200, response.text
    return response.json()


async def _submit(client: AsyncClient, player, run_id: str, events, score: int):
    return await client.post(
        f"/v1/runs/{run_id}/submit",
        json={"events": events, "claimed_score": score},
        headers=player.headers,
    )


async def test_honest_run_is_verified_and_pays_coins(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    start = await _start(client, player)
    events, score = play(start["seed"], perfect=5, sliced=4)
    await backdate_run(db_session, start["run_id"])

    body = (await _submit(client, player, start["run_id"], events, score)).json()

    assert body["status"] == "verified"
    assert body["score"] == score
    assert body["new_best"] is True
    assert body["coins_earned"] == coins_for_run(score) > 0
    assert body["balance"] == body["coins_earned"]
    assert start["continues_allowed"] is True


async def test_resubmitting_is_idempotent(client: AsyncClient, db_session: AsyncSession) -> None:
    # The offline queue retries submissions; a retry must never pay twice.
    player = await guest(client)
    start = await _start(client, player)
    events, score = play(start["seed"])
    await backdate_run(db_session, start["run_id"])

    first = (await _submit(client, player, start["run_id"], events, score)).json()
    second = (await _submit(client, player, start["run_id"], events, score)).json()

    assert second == first


async def test_tampered_score_is_rejected_and_pays_nothing(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    start = await _start(client, player)
    events, score = play(start["seed"])
    await backdate_run(db_session, start["run_id"])

    body = (await _submit(client, player, start["run_id"], events, score + 100)).json()
    board = (await client.get("/v1/leaderboards/friends", headers=player.headers)).json()

    assert (body["status"], body["reason"]) == ("rejected", "score_mismatch")
    assert body["coins_earned"] == 0 and body["balance"] == 0
    assert board["entries"] == []


async def test_impossible_tap_timings_are_rejected(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    start = await _start(client, player)
    await backdate_run(db_session, start["run_id"])

    body = (await _submit(client, player, start["run_id"], [{"t": 1, "type": "tap"}], 1)).json()

    assert (body["status"], body["reason"]) == ("rejected", "tap_too_early")


async def test_instant_submission_is_rejected(client: AsyncClient) -> None:
    player = await guest(client)
    start = await _start(client, player)
    events, score = play(start["seed"], perfect=8, sliced=8)

    body = (await _submit(client, player, start["run_id"], events, score)).json()

    assert (body["status"], body["reason"]) == ("rejected", "submitted_too_fast")


async def test_cannot_submit_someone_elses_run(client: AsyncClient) -> None:
    owner, thief = await guest(client), await guest(client)
    start = await _start(client, owner)

    response = await _submit(client, thief, start["run_id"], [], 0)

    assert response.status_code == 404


async def test_rewarded_continue_is_replayed(client: AsyncClient, db_session: AsyncSession) -> None:
    player = await guest(client)
    start = await _start(client, player)
    events, score = play(start["seed"], continue_after_miss=True)
    await backdate_run(db_session, start["run_id"])

    body = (await _submit(client, player, start["run_id"], events, score)).json()

    assert body["status"] == "verified"
    assert any(e["type"] == "continue" for e in events)


async def test_double_coins_pays_once(client: AsyncClient, db_session: AsyncSession) -> None:
    player = await guest(client)
    run = await verified_run(client, db_session, player, perfect=6, sliced=6)

    first = await client.post(f"/v1/runs/{run['run_id']}/double-coins", headers=player.headers)
    second = await client.post(f"/v1/runs/{run['run_id']}/double-coins", headers=player.headers)

    assert first.json()["balance"] == 2 * run["coins_earned"]
    assert second.json()["balance"] == first.json()["balance"]
