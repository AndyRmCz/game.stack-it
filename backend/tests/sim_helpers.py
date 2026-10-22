"""Helpers that play a StackSim like a (perfectly informed) player."""

from app.domain.sim import MIN_TAP_GAP_TICKS, PERFECT_TOLERANCE, TRAVEL, InputEvent, StackSim


def _tap_when(sim: StackSim, hit) -> int:
    """Advance tick by tick from the earliest legal tap until `hit(offset, dir)`, then tap."""
    t = sim.moving.spawn_tick + MIN_TAP_GAP_TICKS
    while True:
        sim.advance_to(t)
        if hit(sim.moving.offset, sim.moving.dir):
            sim.apply(InputEvent(t, "tap"))
            return t
        t += 1


def _tap_perfect(sim: StackSim) -> None:
    _tap_when(sim, lambda o, _: abs(o) <= PERFECT_TOLERANCE)


def _tap_past(sim: StackSim, distance: int) -> None:
    """Tap once the block has passed the centre by `distance` in its direction of travel."""
    _tap_when(sim, lambda o, d: o * d >= distance)


def _miss(sim: StackSim) -> None:
    _tap_when(sim, lambda o, _: abs(o) >= TRAVEL - 2_400)
