import pytest

from app.domain.sim import (
    BLOCK_SIZE,
    MIN_TAP_GAP_TICKS,
    PERFECT_TOLERANCE,
    TRAVEL,
    InputEvent,
    InvalidRun,
    Phase,
    StackSim,
    perfect_tolerance,
    replay,
)
from tests.sim_helpers import _miss, _tap_past, _tap_perfect


def test_first_block_spawns_at_an_edge_moving_inward() -> None:
    sim = StackSim(seed=1)

    assert sim.moving.axis == "x"
    assert abs(sim.moving.offset) == TRAVEL
    assert sim.moving.dir == (1 if sim.moving.offset < 0 else -1)


def test_offset_stays_within_travel_and_bounces() -> None:
    sim = StackSim(seed=1)
    dirs = set()
    for t in range(1, 400):
        sim.advance_to(t)
        dirs.add(sim.moving.dir)
        assert -TRAVEL <= sim.moving.offset <= TRAVEL

    assert dirs == {1, -1}


def test_perfect_snaps_exactly_onto_the_top() -> None:
    sim = StackSim(seed=5)
    before = sim.top

    _tap_perfect(sim)

    assert sim.top == before
    assert sim.steps[-1].perfect


def test_slice_keeps_only_the_overlap_and_resets_combo() -> None:
    sim = StackSim(seed=3)
    _tap_perfect(sim)
    assert sim.combo == 1

    _tap_past(sim, 5_000)  # second block moves on Z

    step = sim.steps[-1]
    assert step.kind == "place" and not step.perfect
    assert sim.combo == 0
    assert sim.top.z1 - sim.top.z0 == BLOCK_SIZE - abs(step.offset)
    assert sim.top.x1 - sim.top.x0 == BLOCK_SIZE
    assert sim.score == 2 + 1  # perfect (1 + combo bonus 1), then a plain slice (1)


def test_perfect_window_scales_with_speed_and_is_inclusive() -> None:
    for seed in range(40):
        sim = StackSim(seed=seed)
        speed = sim.moving.speed
        tolerance = perfect_tolerance(speed)
        _tap_past(sim, -tolerance - 400)  # arrive around the edge of the window
        step = sim.steps[-1]
        assert step.perfect == (abs(step.offset) <= tolerance)
    assert perfect_tolerance(900) == PERFECT_TOLERANCE + 450


def test_every_pass_has_a_tick_inside_the_perfect_window() -> None:
    # At 60 Hz the block moves `speed` units per tick; the window must be wider than that,
    # otherwise hitting a perfect would depend on luck instead of timing.
    for speed in range(900, 2_551):
        assert 2 * perfect_tolerance(speed) + 1 > speed


def test_combo_bonus_grows_and_is_capped() -> None:
    sim = StackSim(seed=11)
    gained = []
    for _ in range(7):
        _tap_perfect(sim)
        gained.append(sim.steps[-1].gained)

    assert gained == [2, 3, 4, 5, 6, 6, 6]


def test_every_fourth_perfect_regrows_the_block_up_to_full_size() -> None:
    sim = StackSim(seed=13)
    _tap_past(sim, 3_000)  # slice on X
    width_after_slice = sim.top.x1 - sim.top.x0
    for _ in range(4):
        _tap_perfect(sim)  # Z, X, Z, X: the 4th consecutive perfect is on X

    assert sim.combo == 4
    assert sim.top.x1 - sim.top.x0 == min(width_after_slice + 1_500, BLOCK_SIZE)
    assert sim.top.x1 - sim.top.x0 > width_after_slice


def test_regrow_never_exceeds_block_size() -> None:
    sim = StackSim(seed=13)
    for _ in range(8):
        _tap_perfect(sim)

    assert sim.top.x1 - sim.top.x0 == BLOCK_SIZE
    assert sim.top.z1 - sim.top.z0 == BLOCK_SIZE


def test_miss_waits_for_continue_and_marks_the_run_ended() -> None:
    sim = StackSim(seed=17)
    _miss(sim)

    assert sim.phase is Phase.AWAITING_CONTINUE
    assert sim.ended
    assert sim.placed == 0
    assert sim.steps[-1].kind == "miss"


def test_continue_respawns_and_keeps_score() -> None:
    sim = StackSim(seed=17)
    _tap_perfect(sim)
    _miss(sim)
    score, top = sim.score, sim.top

    sim.apply(InputEvent(sim.tick + 30, "continue"))

    assert sim.phase is Phase.PLAYING
    assert not sim.ended
    assert (sim.score, sim.top, sim.combo, sim.continues_used) == (score, top, 0, 1)
    assert sim.moving.spawn_tick == sim.tick


def test_second_continue_is_rejected() -> None:
    sim = StackSim(seed=21)
    _miss(sim)
    sim.apply(InputEvent(sim.tick + 5, "continue"))
    _miss(sim)

    with pytest.raises(InvalidRun) as exc:
        sim.apply(InputEvent(sim.tick + 5, "continue"))
    assert exc.value.code == "continue_not_allowed"


def test_continue_rejected_when_disabled_for_challenges() -> None:
    sim = StackSim(seed=19, continues_allowed=False)
    _miss(sim)

    with pytest.raises(InvalidRun) as exc:
        sim.apply(InputEvent(sim.tick + 5, "continue"))
    assert exc.value.code == "continue_not_allowed"


def test_tap_after_game_over_is_rejected() -> None:
    sim = StackSim(seed=23)
    _miss(sim)

    with pytest.raises(InvalidRun) as exc:
        sim.apply(InputEvent(sim.tick + 20, "tap"))
    assert exc.value.code == "event_after_game_over"


@pytest.mark.parametrize(
    ("events", "code"),
    [
        ([InputEvent(MIN_TAP_GAP_TICKS - 1, "tap")], "tap_too_early"),
        ([InputEvent(80, "tap"), InputEvent(70, "tap")], "events_out_of_order"),
        ([InputEvent(50, "continue")], "continue_without_miss"),
        ([InputEvent(50, "jump")], "unknown_event"),
        ([InputEvent(216_001, "tap")], "run_too_long"),
    ],
)
def test_invalid_inputs_are_rejected(events: list[InputEvent], code: str) -> None:
    with pytest.raises(InvalidRun) as exc:
        replay(seed=1, events=events, continues_allowed=True)
    assert exc.value.code == code


def test_replay_reproduces_a_live_session() -> None:
    sim = StackSim(seed=29)
    _tap_perfect(sim)
    _tap_past(sim, 2_000)
    _tap_perfect(sim)
    _miss(sim)
    sim.apply(InputEvent(sim.tick + 40, "continue"))
    _tap_past(sim, 1_000)
    _miss(sim)
    events = [InputEvent(s.t, "continue" if s.kind == "continue" else "tap") for s in sim.steps]

    result = replay(seed=29, events=events, continues_allowed=True)

    assert result.steps == sim.steps
    assert (result.score, result.placed, result.continues_used) == (sim.score, 4, 1)
    assert result.ended


def test_seed_changes_the_block_sequence() -> None:
    speeds = {StackSim(seed=s).moving.speed for s in range(20)}

    assert len(speeds) > 5
