import itertools
from datetime import UTC, date, datetime

import pytest

from app.domain.economy import (
    DAILY_REWARDS,
    coins_for_run,
    daily_reward,
    next_streak,
    week_start,
)


@pytest.mark.parametrize(("score", "coins"), [(0, 0), (4, 0), (5, 1), (49, 9), (507, 101)])
def test_coins_for_run(score: int, coins: int) -> None:
    assert coins_for_run(score) == coins


def test_daily_rewards_escalate_then_hold_at_the_top_tier() -> None:
    rewards = [daily_reward(day) for day in range(1, 10)]

    assert rewards[:7] == list(DAILY_REWARDS)
    assert all(a < b for a, b in itertools.pairwise(rewards[:7]))
    assert rewards[7:] == [DAILY_REWARDS[-1]] * 2


@pytest.mark.parametrize(
    ("last", "current", "today", "expected"),
    [
        (None, 0, date(2026, 3, 2), 1),  # first claim ever
        (date(2026, 3, 1), 4, date(2026, 3, 2), 5),  # consecutive day
        (date(2026, 2, 28), 4, date(2026, 3, 2), 1),  # missed a day: reset
        (date(2025, 12, 31), 9, date(2026, 1, 1), 10),  # across a year boundary
    ],
)
def test_next_streak(last: date | None, current: int, today: date, expected: int) -> None:
    assert next_streak(last, current, today) == expected


def test_next_streak_refuses_a_second_claim_on_the_same_day() -> None:
    with pytest.raises(ValueError):
        next_streak(date(2026, 3, 2), 3, date(2026, 3, 2))


@pytest.mark.parametrize(
    ("now", "expected"),
    [
        (datetime(2026, 10, 19, 0, 0, tzinfo=UTC), datetime(2026, 10, 19, tzinfo=UTC)),  # Mon 00:00
        (datetime(2026, 10, 18, 23, 59, 59, tzinfo=UTC), datetime(2026, 10, 12, tzinfo=UTC)),
        (datetime(2026, 10, 21, 14, 30, tzinfo=UTC), datetime(2026, 10, 19, tzinfo=UTC)),
        (datetime(2027, 1, 1, 12, 0, tzinfo=UTC), datetime(2026, 12, 28, tzinfo=UTC)),
    ],
)
def test_week_starts_monday_midnight_utc(now: datetime, expected: datetime) -> None:
    assert week_start(now) == expected
