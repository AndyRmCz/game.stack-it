from datetime import date, timedelta

from app.domain.daily import DAILY_EPOCH, daily_number, daily_seed


def test_the_daily_number_counts_days_from_launch() -> None:
    assert daily_number(DAILY_EPOCH) == 1
    assert daily_number(DAILY_EPOCH + timedelta(days=141)) == 142


def test_everyone_gets_the_same_seed_on_a_day_and_a_new_one_the_next() -> None:
    day = date(2026, 11, 2)
    assert daily_seed("secret", day) == daily_seed("secret", day)
    assert daily_seed("secret", day) != daily_seed("secret", day + timedelta(days=1))


def test_the_seed_is_a_uint32_and_depends_on_the_server_secret() -> None:
    day = date(2026, 11, 2)
    seed = daily_seed("secret", day)
    assert 0 <= seed <= 0xFFFFFFFF
    assert daily_seed("another secret", day) != seed
