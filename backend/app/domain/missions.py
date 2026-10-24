"""Daily missions: three goals per player per UTC day (easy, medium, hard), plus a chest.

Progress is read from the day's verified runs, so nothing new is stored; a claim is a wallet
ledger entry whose idempotency key names the day and slot, so it can only pay once.
Rewards are provisional (docs/PLAN.md, idea 5): to tune against real coin rates.
"""

import hashlib
import random
import uuid
from dataclasses import dataclass
from datetime import date

# kind -> targets for the easy, medium and hard slot
KINDS: dict[str, tuple[int, int, int]] = {
    "runs": (3, 6, 10),  # play N runs
    "perfects": (5, 12, 25),  # N perfect drops in total
    "reach": (15, 30, 50),  # place N blocks in one run
    "combo": (3, 6, 10),  # N perfects in a row
    "daily": (1, 1, 1),  # play the Daily Tower
}
REWARDS = (20, 30, 45)  # coins for the easy, medium and hard slot
CHEST_REWARD = 50  # all three done
CHEST_SLOT = 3


@dataclass(frozen=True, slots=True)
class Mission:
    slot: int
    kind: str
    target: int
    reward: int


@dataclass(frozen=True, slots=True)
class DayStats:
    runs: int
    perfects: int
    best_placed: int
    best_combo: int
    played_daily: bool


def missions_for(user_id: uuid.UUID, day: date) -> list[Mission]:
    """The player's missions for the day: always the same for that player and day."""
    digest = hashlib.sha256(f"missions:{user_id}:{day.isoformat()}".encode()).digest()
    kinds = random.Random(digest).sample(sorted(KINDS), 3)
    return [
        Mission(slot, kind, KINDS[kind][slot], REWARDS[slot]) for slot, kind in enumerate(kinds)
    ]


def progress(kind: str, stats: DayStats) -> int:
    match kind:
        case "runs":
            return stats.runs
        case "perfects":
            return stats.perfects
        case "reach":
            return stats.best_placed
        case "combo":
            return stats.best_combo
        case "daily":
            return int(stats.played_daily)
    raise ValueError(kind)


def claim_key(day: date, slot: int) -> str:
    return f"mission:{day.isoformat()}:{slot}"
