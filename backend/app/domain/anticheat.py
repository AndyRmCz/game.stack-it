"""Server-side verdict on a run: replay is authoritative, the client claim is only checked."""

from dataclasses import dataclass
from datetime import timedelta
from typing import Literal

from app.domain.sim import InputEvent, InvalidRun, RunResult, replay
from app.domain.sim.engine import TICK_RATE

# Wall-clock time from run start to submission must cover most of the simulated time.
MIN_ELAPSED_RATIO = 0.8
ELAPSED_SLACK_SECONDS = 2.0
# Long runs that are (almost) all perfects are flagged for review instead of being counted.
PRECISION_MIN_PLACED = 50
PRECISION_MAX_RATIO = 0.98

Status = Literal["verified", "flagged", "rejected"]


@dataclass(frozen=True, slots=True)
class Verdict:
    status: Status
    reason: str | None
    result: RunResult | None


def assess_run(
    *,
    seed: int,
    events: list[InputEvent],
    claimed_score: int,
    continues_allowed: bool,
    elapsed: timedelta,
) -> Verdict:
    try:
        result = replay(seed, events, continues_allowed)
    except InvalidRun as exc:
        return Verdict("rejected", exc.code, None)

    if not result.ended:
        return Verdict("rejected", "run_not_finished", result)
    if result.score != claimed_score:
        return Verdict("rejected", "score_mismatch", result)

    game_seconds = result.tick / TICK_RATE
    if elapsed.total_seconds() < game_seconds * MIN_ELAPSED_RATIO - ELAPSED_SLACK_SECONDS:
        return Verdict("rejected", "submitted_too_fast", result)

    if result.placed >= PRECISION_MIN_PLACED and result.perfects / result.placed >= (
        PRECISION_MAX_RATIO
    ):
        return Verdict("flagged", "superhuman_precision", result)

    return Verdict("verified", None, result)
