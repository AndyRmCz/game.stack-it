import random

from app.domain.economy import DAILY_REWARDS, coins_for_run
from scripts.economy_model import PlayerProfile, days_to_afford, play_run, summarize


def test_a_run_is_played_by_the_real_engine_and_ends_with_a_miss():
    run = play_run(seed=7, sigma_ms=50, use_continue=False, rng=random.Random(1))
    assert run.placed >= 0
    assert 0 <= run.perfects <= run.placed
    assert run.score >= run.placed  # 1 point per block, plus combo bonuses
    assert run.seconds > 0


def test_the_continue_is_taken_once_and_never_lowers_the_score():
    a = play_run(seed=11, sigma_ms=50, use_continue=False, rng=random.Random(3))
    b = play_run(seed=11, sigma_ms=50, use_continue=True, rng=random.Random(3))
    assert b.continues == 1
    assert b.score >= a.score


def test_more_precise_players_score_more():
    precise = summarize(sigma_ms=25, use_continue=False, runs=300, rng=random.Random(5))
    sloppy = summarize(sigma_ms=70, use_continue=False, runs=300, rng=random.Random(5))
    assert precise.score_mean > sloppy.score_mean
    assert precise.perfect_rate > sloppy.perfect_rate


def test_summary_coins_follow_the_economy_rule():
    s = summarize(sigma_ms=35, use_continue=False, runs=200, rng=random.Random(9))
    assert s.coins_median == coins_for_run(s.score_median)


def test_days_to_afford_counts_run_coins_doubles_and_the_daily_gift():
    # 10 coins a run, 2 runs a day, no doubles: 20 a day from runs plus the gift.
    profile = PlayerProfile("t", coins_per_run=10, runs_per_day=2, doubled_share=0, gift_share=1)
    assert days_to_afford(DAILY_REWARDS[0] + 20, profile) == 1
    assert days_to_afford(DAILY_REWARDS[0] + DAILY_REWARDS[1] + 40, profile) == 2


def test_doubling_shortens_the_wait():
    base = PlayerProfile("a", coins_per_run=5, runs_per_day=10, doubled_share=0, gift_share=0)
    doubled = PlayerProfile("b", coins_per_run=5, runs_per_day=10, doubled_share=1, gift_share=0)
    assert days_to_afford(3000, doubled) < days_to_afford(3000, base)
