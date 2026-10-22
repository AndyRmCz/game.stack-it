"""Regenerates shared/sim-vectors/*.json from the Python simulation.

    uv run python scripts/generate_sim_vectors.py

Inputs come from a scripted, deterministic "bot" so vectors are reproducible. Review the diff
before committing: a changed vector means a changed game rule, which needs a SIM_VERSION bump.
"""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.domain.sim import (
    MIN_TAP_GAP_TICKS,
    SIM_VERSION,
    TRAVEL,
    InputEvent,
    StackSim,
)
from app.domain.sim.prng import Pcg32
from app.domain.sim.vectors import expected_for

OUT_DIR = Path(__file__).resolve().parents[2] / "shared" / "sim-vectors"


class Bot:
    """Plays a StackSim by aiming at offsets; records the resulting input events."""

    def __init__(self, seed: int, continues_allowed: bool = True) -> None:
        self.sim = StackSim(seed=seed, continues_allowed=continues_allowed)
        self.events: list[InputEvent] = []

    def tap_at(self, aim: int, wait: int = 0) -> None:
        """Taps on the first tick (after `wait` extra ticks) where the block passed `aim`."""
        sim = self.sim
        t = sim.moving.spawn_tick + MIN_TAP_GAP_TICKS + wait
        while True:
            sim.advance_to(t)
            m = sim.moving
            if (m.offset - aim) * m.dir >= 0 and abs(m.offset - aim) <= m.speed:
                break
            t += 1
        event = InputEvent(t, "tap")
        sim.apply(event)
        self.events.append(event)

    def miss(self) -> None:
        lo, hi = self.sim.top.span(self.sim.moving.axis)
        reach = min(hi - lo + 1_000, TRAVEL - 1)
        self.tap_at(reach * self.sim.moving.dir)

    def continue_after(self, ticks: int) -> None:
        event = InputEvent(self.sim.tick + ticks, "continue")
        self.sim.apply(event)
        self.events.append(event)

    def play(self, blocks: int, rng: Pcg32, perfect_pct: int, spread: int) -> None:
        """Places up to `blocks` blocks; stops early if a tap misses."""
        for _ in range(blocks):
            if self.sim.ended:
                return
            if rng.next_u32() % 100 < perfect_pct:
                aim = 0
            else:
                magnitude = 500 + rng.next_u32() % spread
                aim = magnitude if rng.next_u32() & 1 else -magnitude
            self.tap_at(aim)


def scripted(seed: int, script, continues_allowed: bool = True) -> list[InputEvent]:
    bot = Bot(seed, continues_allowed)
    script(bot)
    return bot.events


def _perfect_streak(bot: Bot) -> None:
    for _ in range(12):
        bot.tap_at(0)
    bot.miss()


def _mixed(bot: Bot) -> None:
    bot.play(40, Pcg32(1), perfect_pct=30, spread=4_000)
    if not bot.sim.ended:
        bot.miss()


def _continue(bot: Bot) -> None:
    bot.play(10, Pcg32(2), perfect_pct=40, spread=3_000)
    if not bot.sim.ended:
        bot.miss()
    bot.continue_after(45)
    bot.play(10, Pcg32(3), perfect_pct=40, spread=3_000)
    if not bot.sim.ended:
        bot.miss()


def _long_run(bot: Bot) -> None:
    bot.play(170, Pcg32(4), perfect_pct=90, spread=400)
    if not bot.sim.ended:
        bot.miss()


def _late_taps(bot: Bot) -> None:
    for i in range(8):
        bot.tap_at(0 if i % 2 else 1_200, wait=150 + 37 * i)
    bot.miss()


def _short(bot: Bot) -> None:
    bot.play(6, Pcg32(5), perfect_pct=50, spread=5_000)
    if not bot.sim.ended:
        bot.miss()


def _unfinished(bot: Bot) -> None:
    bot.play(5, Pcg32(6), perfect_pct=50, spread=2_000)


def _events(*pairs: tuple[int, str]) -> list[InputEvent]:
    return [InputEvent(t, kind) for t, kind in pairs]


def build_vectors() -> list[dict]:
    challenge_events = scripted(77, lambda b: (b.play(3, Pcg32(7), 50, 2_000), b.miss()))
    challenge_events.append(InputEvent(challenge_events[-1].t + 20, "continue"))
    finished = scripted(81, lambda b: (b.tap_at(0), b.miss()))

    cases: list[tuple[str, str, int, bool, list[InputEvent]]] = [
        (
            "perfect_streak",
            "12 perfects (combo cap, regrow) then a miss",
            42,
            True,
            scripted(42, _perfect_streak),
        ),
        (
            "mixed_slices",
            "Random slices and perfects until a miss",
            0xDEADBEEF,
            True,
            scripted(0xDEADBEEF, _mixed),
        ),
        (
            "continue_used",
            "Miss, rewarded continue, then another miss",
            7,
            True,
            scripted(7, _continue),
        ),
        (
            "long_run_speed_cap",
            "170 mostly-perfect blocks: speed cap and regrow",
            123456789,
            True,
            scripted(123456789, _long_run),
        ),
        (
            "late_taps_bounce",
            "Taps after the block bounced at the travel edges",
            2024,
            True,
            scripted(2024, _late_taps),
        ),
        ("seed_zero", "Seed 0", 0, True, scripted(0, _short)),
        ("seed_max", "Seed 0xFFFFFFFF", 0xFFFFFFFF, True, scripted(0xFFFFFFFF, _short)),
        (
            "unfinished",
            "Run without a final miss (not ended)",
            31337,
            True,
            scripted(31337, _unfinished),
        ),
        (
            "error_continue_in_challenge",
            "Continue is not allowed in challenge runs",
            77,
            False,
            challenge_events,
        ),
        (
            "error_tap_too_early",
            "Tap before MIN_TAP_GAP_TICKS",
            5,
            True,
            _events((MIN_TAP_GAP_TICKS - 1, "tap")),
        ),
        (
            "error_out_of_order",
            "Event ticks going backwards",
            5,
            True,
            _events((90, "tap"), (80, "tap")),
        ),
        (
            "error_after_game_over",
            "Tap after a miss without continue",
            81,
            True,
            [*finished, InputEvent(finished[-1].t + 30, "tap")],
        ),
        (
            "error_continue_without_miss",
            "Continue while still playing",
            5,
            True,
            _events((60, "continue")),
        ),
        ("error_unknown_event", "Unknown event type", 5, True, _events((60, "jump"))),
    ]
    vectors = []
    for name, description, seed, continues_allowed, events in cases:
        vectors.append(
            {
                "name": name,
                "description": description,
                "sim_version": SIM_VERSION,
                "seed": seed,
                "continues_allowed": continues_allowed,
                "events": [{"t": e.t, "type": e.type} for e in events],
                "expected": expected_for(seed, events, continues_allowed),
            }
        )
    return vectors


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for old in OUT_DIR.glob("*.json"):
        old.unlink()
    for vector in build_vectors():
        path = OUT_DIR / f"{vector['name']}.json"
        path.write_text(json.dumps(vector, indent=1) + "\n", encoding="utf-8", newline="\n")
        expected = vector["expected"]
        summary = expected["error"] or f"score={expected['final']['score']}"
        print(f"{path.name}: {len(vector['events'])} events, {summary}")


if __name__ == "__main__":
    main()
