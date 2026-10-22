"""Deterministic stack simulation. Integer math only; mirrors StackIt.Core.StackSim in C#.

The rules are specified in shared/SIMULATION.md; golden vectors in shared/sim-vectors keep the
two implementations in lockstep.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Literal

from app.domain.sim.prng import Pcg32

SIM_VERSION = 1
TICK_RATE = 60

BLOCK_SIZE = 30_000
_HALF_BLOCK = 15_000
TRAVEL = 45_000
PERFECT_TOLERANCE = 450
SPEED_BASE = 900
SPEED_RAMP = 12
SPEED_MAX = 2_400
SPEED_JITTER = 150
REGROW_EVERY = 4
REGROW_HALF = 750
COMBO_BONUS_CAP = 5
MIN_TAP_GAP_TICKS = 6
MAX_RUN_TICKS = 216_000
MAX_CONTINUES = 1

Axis = Literal["x", "z"]
EventType = Literal["tap", "continue"]


def perfect_tolerance(speed: int) -> int:
    """Perfect window grows with speed so every pass has at least one tick inside it."""
    return PERFECT_TOLERANCE + speed // 2


class InvalidRun(Exception):
    def __init__(self, code: str) -> None:
        super().__init__(code)
        self.code = code


class Phase(Enum):
    PLAYING = "playing"
    AWAITING_CONTINUE = "awaiting_continue"


@dataclass(frozen=True, slots=True)
class InputEvent:
    t: int
    type: str


@dataclass(frozen=True, slots=True)
class Block:
    x0: int
    x1: int
    z0: int
    z1: int

    def span(self, axis: Axis) -> tuple[int, int]:
        return (self.x0, self.x1) if axis == "x" else (self.z0, self.z1)

    def with_span(self, axis: Axis, lo: int, hi: int) -> "Block":
        if axis == "x":
            return Block(lo, hi, self.z0, self.z1)
        return Block(self.x0, self.x1, lo, hi)


@dataclass(slots=True)
class MovingBlock:
    axis: Axis
    offset: int
    dir: int
    speed: int
    spawn_tick: int


@dataclass(frozen=True, slots=True)
class Step:
    kind: Literal["place", "miss", "continue"]
    t: int
    n: int
    axis: Axis | None = None
    offset: int = 0
    block: Block | None = None
    perfect: bool = False
    combo: int = 0
    gained: int = 0
    score: int = 0


@dataclass
class StackSim:
    seed: int
    continues_allowed: bool = True
    tick: int = 0
    phase: Phase = Phase.PLAYING
    top: Block = field(
        default_factory=lambda: Block(-_HALF_BLOCK, _HALF_BLOCK, -_HALF_BLOCK, _HALF_BLOCK)
    )
    placed: int = 0
    score: int = 0
    combo: int = 0
    max_combo: int = 0
    perfects: int = 0
    continues_used: int = 0
    steps: list[Step] = field(default_factory=list)
    moving: MovingBlock = field(init=False)
    _rng: Pcg32 = field(init=False, repr=False)

    def __post_init__(self) -> None:
        self._rng = Pcg32(self.seed)
        self._spawn(0)

    @property
    def ended(self) -> bool:
        return self.phase is Phase.AWAITING_CONTINUE

    @property
    def max_continues(self) -> int:
        return MAX_CONTINUES if self.continues_allowed else 0

    def _spawn(self, t: int) -> None:
        r = self._rng.next_u32()
        speed = min(SPEED_BASE + SPEED_RAMP * self.placed, SPEED_MAX) + (r >> 1) % (
            SPEED_JITTER + 1
        )
        start_low = (r & 1) == 0
        self.moving = MovingBlock(
            axis="x" if self.placed % 2 == 0 else "z",
            offset=-TRAVEL if start_low else TRAVEL,
            dir=1 if start_low else -1,
            speed=speed,
            spawn_tick=t,
        )
        self.tick = t

    def advance_to(self, t: int) -> None:
        if t < self.tick:
            raise InvalidRun("events_out_of_order")
        if self.phase is not Phase.PLAYING:
            self.tick = t
            return
        m = self.moving
        for _ in range(t - self.tick):
            m.offset += m.dir * m.speed
            if m.offset > TRAVEL:
                m.offset = 2 * TRAVEL - m.offset
                m.dir = -1
            elif m.offset < -TRAVEL:
                m.offset = -2 * TRAVEL - m.offset
                m.dir = 1
        self.tick = t

    def apply(self, event: InputEvent) -> Step:
        if event.type not in ("tap", "continue"):
            raise InvalidRun("unknown_event")
        if event.t > MAX_RUN_TICKS:
            raise InvalidRun("run_too_long")
        if event.t < self.tick:
            raise InvalidRun("events_out_of_order")
        step = self._tap(event.t) if event.type == "tap" else self._continue(event.t)
        self.steps.append(step)
        return step

    def _tap(self, t: int) -> Step:
        if self.phase is not Phase.PLAYING:
            raise InvalidRun("event_after_game_over")
        if t < self.moving.spawn_tick + MIN_TAP_GAP_TICKS:
            raise InvalidRun("tap_too_early")
        self.advance_to(t)

        m = self.moving
        lo, hi = self.top.span(m.axis)
        n = self.placed + 1
        offset = m.offset

        if abs(offset) <= perfect_tolerance(m.speed):
            perfect = True
            self.combo += 1
            self.perfects += 1
            if self.combo % REGROW_EVERY == 0:
                lo, hi = lo - REGROW_HALF, hi + REGROW_HALF
                if hi - lo > BLOCK_SIZE:
                    hi = lo + BLOCK_SIZE
        elif abs(offset) >= hi - lo:
            self.combo = 0
            self.phase = Phase.AWAITING_CONTINUE
            return Step(kind="miss", t=t, n=n, axis=m.axis, offset=offset, score=self.score)
        else:
            perfect = False
            self.combo = 0
            lo, hi = max(lo + offset, lo), min(hi + offset, hi)

        gained = 1 + (min(self.combo, COMBO_BONUS_CAP) if perfect else 0)
        self.placed = n
        self.score += gained
        self.max_combo = max(self.max_combo, self.combo)
        self.top = self.top.with_span(m.axis, lo, hi)
        step = Step(
            kind="place",
            t=t,
            n=n,
            axis=m.axis,
            offset=offset,
            block=self.top,
            perfect=perfect,
            combo=self.combo,
            gained=gained,
            score=self.score,
        )
        self._spawn(t)
        return step

    def _continue(self, t: int) -> Step:
        if self.phase is not Phase.AWAITING_CONTINUE:
            raise InvalidRun("continue_without_miss")
        if self.continues_used >= self.max_continues:
            raise InvalidRun("continue_not_allowed")
        self.continues_used += 1
        self.phase = Phase.PLAYING
        self._spawn(t)
        return Step(kind="continue", t=t, n=self.placed + 1, score=self.score)


@dataclass(frozen=True, slots=True)
class RunResult:
    score: int
    placed: int
    perfects: int
    max_combo: int
    continues_used: int
    ended: bool
    tick: int
    steps: list[Step]


def replay(seed: int, events: list[InputEvent], continues_allowed: bool) -> RunResult:
    """Re-simulates a whole run. Raises InvalidRun on any illegal input."""
    sim = StackSim(seed=seed, continues_allowed=continues_allowed)
    for event in events:
        sim.apply(event)
    return RunResult(
        score=sim.score,
        placed=sim.placed,
        perfects=sim.perfects,
        max_combo=sim.max_combo,
        continues_used=sim.continues_used,
        ended=sim.ended,
        tick=sim.tick,
        steps=sim.steps,
    )
