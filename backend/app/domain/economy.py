"""Economy and calendar rules. Pure functions; the C# client mirrors coins_for_run for display."""

from datetime import UTC, date, datetime, time, timedelta

COINS_PER_SCORE_DIVISOR = 5
# Day 1..7 of a streak; from day 7 on the top tier repeats.
DAILY_REWARDS: tuple[int, ...] = (25, 35, 50, 70, 90, 120, 200)


def coins_for_run(score: int) -> int:
    return max(score, 0) // COINS_PER_SCORE_DIVISOR


def daily_reward(streak_day: int) -> int:
    return DAILY_REWARDS[min(max(streak_day, 1), len(DAILY_REWARDS)) - 1]


def next_streak(last_claim: date | None, current: int, today: date) -> int:
    """Streak value after claiming today. Raises if today was already claimed."""
    if last_claim == today:
        raise ValueError("already claimed today")
    if last_claim is not None and last_claim == today - timedelta(days=1):
        return current + 1
    return 1


def week_start(now: datetime) -> datetime:
    """Start of the leaderboard week: Monday 00:00 UTC."""
    utc = now.astimezone(UTC)
    monday = utc.date() - timedelta(days=utc.weekday())
    return datetime.combine(monday, time.min, tzinfo=UTC)
