from datetime import UTC, datetime, timedelta

from httpx import AsyncClient
from sqlalchemy import update
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import get_settings
from app.models import Challenge
from tests.factories import backdate_run, guest, play, verified_run


async def _create_challenge(client, db, creator) -> tuple[dict, dict]:
    run = await verified_run(client, db, creator, perfect=4, sliced=3)
    response = await client.post(
        "/v1/challenges", json={"run_id": run["run_id"]}, headers=creator.headers
    )
    assert response.status_code == 201, response.text
    return run, response.json()


async def test_same_seed_duel_end_to_end(client: AsyncClient, db_session: AsyncSession) -> None:
    creator, friend = await guest(client), await guest(client)
    run, challenge = await _create_challenge(client, db_session, creator)
    assert challenge["url"] == f"{get_settings().public_base_url}/c/{challenge['code']}"

    start = (
        await client.post(
            "/v1/runs", json={"challenge_code": challenge["code"].lower()}, headers=friend.headers
        )
    ).json()

    # Same block sequence, no continues, and the rival's ghost data for the HUD.
    assert start["continues_allowed"] is False
    assert start["rival"]["score"] == run["score"]
    # Accepting a challenge made them friends, in both directions.
    for me, other in ((creator, friend), (friend, creator)):
        friends = (await client.get("/v1/friends", headers=me.headers)).json()
        assert [f["user_id"] for f in friends] == [other.user_id]

    events, score = play(start["seed"], perfect=8, sliced=4)
    await backdate_run(db_session, start["run_id"])
    result = (
        await client.post(
            f"/v1/runs/{start['run_id']}/submit",
            json={"events": events, "claimed_score": score},
            headers=friend.headers,
        )
    ).json()

    assert result["status"] == "verified"
    assert result["challenge"]["won"] is (score > run["score"])
    view = (await client.get(f"/v1/challenges/{challenge['code']}", headers=friend.headers)).json()
    assert view["my_best"] == score


async def test_creating_twice_returns_the_same_challenge(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    creator = await guest(client)
    run, first = await _create_challenge(client, db_session, creator)

    again = await client.post(
        "/v1/challenges", json={"run_id": run["run_id"]}, headers=creator.headers
    )

    assert again.json()["code"] == first["code"]


async def test_expired_and_own_challenges_cannot_be_played(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    creator, friend = await guest(client), await guest(client)
    _, challenge = await _create_challenge(client, db_session, creator)

    own = await client.post(
        "/v1/runs", json={"challenge_code": challenge["code"]}, headers=creator.headers
    )
    async with db_session.begin():
        await db_session.execute(
            update(Challenge).values(expires_at=datetime.now(UTC) - timedelta(minutes=1))
        )
    expired = await client.post(
        "/v1/runs", json={"challenge_code": challenge["code"]}, headers=friend.headers
    )

    assert own.status_code == 409
    assert expired.status_code == 410


async def test_continue_in_a_challenge_run_is_rejected(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    creator, friend = await guest(client), await guest(client)
    _, challenge = await _create_challenge(client, db_session, creator)
    start = (
        await client.post(
            "/v1/runs", json={"challenge_code": challenge["code"]}, headers=friend.headers
        )
    ).json()
    events, score = play(start["seed"], continue_after_miss=True)
    await backdate_run(db_session, start["run_id"])

    result = (
        await client.post(
            f"/v1/runs/{start['run_id']}/submit",
            json={"events": events, "claimed_score": score},
            headers=friend.headers,
        )
    ).json()

    assert (result["status"], result["reason"]) == ("rejected", "continue_not_allowed")


async def _play_challenge(client, db, player, code: str, perfect: int) -> int:
    start = (
        await client.post("/v1/runs", json={"challenge_code": code}, headers=player.headers)
    ).json()
    events, score = play(start["seed"], perfect=perfect, sliced=4)
    await backdate_run(db, start["run_id"])
    submitted = await client.post(
        f"/v1/runs/{start['run_id']}/submit",
        json={"events": events, "claimed_score": score},
        headers=player.headers,
    )
    assert submitted.json()["status"] == "verified"
    return score


async def test_the_creator_sees_how_friends_did_on_their_challenge(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    creator, winner, loser = await guest(client), await guest(client), await guest(client)
    run, challenge = await _create_challenge(client, db_session, creator)
    high = await _play_challenge(client, db_session, winner, challenge["code"], perfect=12)
    low = await _play_challenge(client, db_session, loser, challenge["code"], perfect=0)
    # Started but never finished: no result yet, so it isn't listed.
    await client.post(
        "/v1/runs", json={"challenge_code": challenge["code"]}, headers=winner.headers
    )

    response = await client.get("/v1/challenges/results", headers=creator.headers)

    assert response.status_code == 200, response.text
    results = response.json()
    assert [r["user_id"] for r in results] == [loser.user_id, winner.user_id]  # newest first
    newest, oldest = results
    assert newest["code"] == challenge["code"]
    assert newest["my_score"] == run["score"]
    assert (newest["score"], newest["won"]) == (low, low > run["score"])
    assert (oldest["score"], oldest["won"]) == (high, high > run["score"])
    assert oldest["placed"] > 0 and oldest["played_at"]
    # The players see nothing of this list: these are the creator's results.
    theirs = await client.get("/v1/challenges/results", headers=winner.headers)
    assert theirs.json() == []


async def test_challenge_results_need_a_signed_in_player(client: AsyncClient) -> None:
    response = await client.get("/v1/challenges/results")

    assert response.status_code == 401
