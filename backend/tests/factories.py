"""Small helpers shared by API tests: sign in, play a real run, backdate it."""

import secrets
import uuid
from dataclasses import dataclass
from datetime import timedelta

from httpx import AsyncClient
from sqlalchemy import update
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.sim import InputEvent, StackSim
from app.models import Run
from tests.sim_helpers import _miss, _tap_past, _tap_perfect


@dataclass
class Player:
    user_id: str
    token: str
    refresh_token: str

    @property
    def headers(self) -> dict[str, str]:
        return {"Authorization": f"Bearer {self.token}"}


async def guest(client: AsyncClient, device_id: str | None = None) -> Player:
    response = await client.post(
        "/v1/auth/guest",
        json={
            "device_id": device_id or f"device-{uuid.uuid4()}",
            "device_secret": secrets.token_urlsafe(32),
        },
    )
    assert response.status_code == 200, response.text
    body = response.json()
    return Player(body["user_id"], body["access_token"], body["refresh_token"])


def play(seed: int, perfect: int = 3, sliced: int = 3, continue_after_miss: bool = False):
    """Plays like a human would and returns (events as JSON, score)."""
    sim = StackSim(seed=seed, continues_allowed=continue_after_miss)
    for _ in range(perfect):
        _tap_perfect(sim)
    for _ in range(sliced):
        _tap_past(sim, 900)
    _miss(sim)
    if continue_after_miss:
        sim.apply(InputEvent(sim.tick + 30, "continue"))
        _tap_perfect(sim)
        _miss(sim)
    events = [{"t": s.t, "type": "continue" if s.kind == "continue" else "tap"} for s in sim.steps]
    return events, sim.score


async def backdate_run(db: AsyncSession, run_id: str, minutes: int = 10) -> None:
    """Moves the run start into the past so the wall-clock plausibility check passes."""
    async with db.begin():
        await db.execute(
            update(Run)
            .where(Run.id == uuid.UUID(run_id))
            .values(started_at=Run.started_at - timedelta(minutes=minutes))
        )


async def verified_run(
    client: AsyncClient,
    db: AsyncSession,
    player: Player,
    perfect: int = 3,
    sliced: int = 3,
    start_body: dict | None = None,
) -> dict:
    start = (await client.post("/v1/runs", json=start_body or {}, headers=player.headers)).json()
    events, score = play(start["seed"], perfect, sliced)
    await backdate_run(db, start["run_id"])
    response = await client.post(
        f"/v1/runs/{start['run_id']}/submit",
        json={"events": events, "claimed_score": score},
        headers=player.headers,
    )
    body = response.json()
    assert body["status"] == "verified", body
    return body
