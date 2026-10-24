import uuid
from datetime import date

from app.domain.missions import CHEST_REWARD, KINDS, DayStats, missions_for, progress


def test_three_missions_a_day_one_per_difficulty_and_no_repeats() -> None:
    user = uuid.UUID(int=7)
    missions = missions_for(user, date(2026, 11, 2))
    assert [m.slot for m in missions] == [0, 1, 2]
    assert len({m.kind for m in missions}) == 3
    assert all(m.kind in KINDS for m in missions)
    assert [m.reward for m in missions] == sorted(m.reward for m in missions)
    assert CHEST_REWARD > 0


def test_the_same_player_gets_the_same_missions_all_day_and_new_ones_tomorrow() -> None:
    user = uuid.UUID(int=7)
    today = missions_for(user, date(2026, 11, 2))
    assert today == missions_for(user, date(2026, 11, 2))
    week = {tuple(m.kind for m in missions_for(user, date(2026, 11, d))) for d in range(2, 9)}
    assert len(week) > 1


def test_progress_reads_the_days_runs() -> None:
    stats = DayStats(runs=4, perfects=11, best_placed=27, best_combo=6, played_daily=True)
    assert progress("runs", stats) == 4
    assert progress("perfects", stats) == 11
    assert progress("reach", stats) == 27
    assert progress("combo", stats) == 6
    assert progress("daily", stats) == 1
    assert progress("daily", DayStats(0, 0, 0, 0, False)) == 0
