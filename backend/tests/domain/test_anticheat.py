from datetime import timedelta

from app.domain.anticheat import Verdict, assess_run
from app.domain.sim import InputEvent, StackSim
from tests.sim_helpers import _miss, _tap_past, _tap_perfect


def _play(seed: int, perfect: int, sliced: int) -> tuple[list[InputEvent], int]:
    sim = StackSim(seed=seed)
    for _ in range(perfect):
        _tap_perfect(sim)
    for _ in range(sliced):
        _tap_past(sim, 300)
    _miss(sim)
    return [InputEvent(s.t, "tap") for s in sim.steps], sim.score


def _assess(events, claimed, elapsed=timedelta(minutes=5), continues_allowed=True) -> Verdict:
    return assess_run(
        seed=1234,
        events=events,
        claimed_score=claimed,
        continues_allowed=continues_allowed,
        elapsed=elapsed,
    )


def test_honest_run_is_verified() -> None:
    events, score = _play(1234, perfect=5, sliced=5)

    verdict = _assess(events, score)

    assert verdict.status == "verified"
    assert verdict.result is not None and verdict.result.score == score


def test_claimed_score_must_match_replay() -> None:
    events, score = _play(1234, perfect=5, sliced=5)

    verdict = _assess(events, score + 10)

    assert (verdict.status, verdict.reason) == ("rejected", "score_mismatch")


def test_invalid_inputs_reject_with_the_simulation_error_code() -> None:
    verdict = _assess([InputEvent(2, "tap")], 1)

    assert (verdict.status, verdict.reason) == ("rejected", "tap_too_early")


def test_run_must_end_with_a_miss() -> None:
    events, score = _play(1234, perfect=3, sliced=0)

    verdict = _assess(events[:-1], score)

    assert (verdict.status, verdict.reason) == ("rejected", "run_not_finished")


def test_run_submitted_faster_than_it_could_be_played_is_rejected() -> None:
    events, score = _play(1234, perfect=5, sliced=5)
    game_seconds = events[-1].t / 60

    verdict = _assess(events, score, elapsed=timedelta(seconds=game_seconds / 3))

    assert (verdict.status, verdict.reason) == ("rejected", "submitted_too_fast")


def test_superhuman_precision_is_flagged_not_counted() -> None:
    events, score = _play(1234, perfect=60, sliced=0)

    verdict = _assess(events, score)

    assert (verdict.status, verdict.reason) == ("flagged", "superhuman_precision")
    assert verdict.result is not None
