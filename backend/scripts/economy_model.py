"""Models runs, coins and shop timing with the real simulation (PLAN.md, measurement findings).

    uv run python -m scripts.economy_model [--runs 3000]

A simulated player aims at the tick where the moving block crosses the centre on its first
pass and misses it by a Gaussian timing error (sigma, in ms). The engine decides placements,
perfects, combos and the miss. The sigmas and the player habits are assumptions: calibrate
them with real data (a player's perfects / placed maps onto sigma through the perfect rate).
"""

import argparse
import math
import random
import statistics
from dataclasses import dataclass

from app.domain.economy import coins_for_run, daily_reward
from app.domain.sim.engine import MIN_TAP_GAP_TICKS, TICK_RATE, TRAVEL, InputEvent, StackSim

MS_PER_TICK = 1000 / TICK_RATE
CONTINUE_PAUSE_TICKS = 90  # the continue offer and the ad, before play resumes
SIGMAS_MS = (25, 35, 50, 70)


@dataclass(frozen=True)
class Run:
    score: int
    placed: int
    perfects: int
    max_combo: int
    continues: int
    seconds: float


@dataclass(frozen=True)
class Summary:
    sigma_ms: float
    use_continue: bool
    runs: int
    score_p10: int
    score_median: int
    score_p90: int
    score_mean: float
    placed_mean: float
    perfect_rate: float
    coins_mean: float
    coins_median: int
    seconds_mean: float


@dataclass(frozen=True)
class PlayerProfile:
    name: str
    coins_per_run: float
    runs_per_day: int
    doubled_share: float  # share of runs whose coins are doubled with a rewarded ad
    gift_share: float  # share of days the daily gift is claimed


def play_run(seed: int, sigma_ms: float, use_continue: bool, rng: random.Random) -> Run:
    sim = StackSim(seed=seed, continues_allowed=True)
    t = 0
    while True:
        m = sim.moving
        ideal = m.spawn_tick + round(TRAVEL / m.speed)
        error = round(rng.gauss(0, sigma_ms) / MS_PER_TICK)
        t = max(m.spawn_tick + MIN_TAP_GAP_TICKS, ideal + error)
        step = sim.apply(InputEvent(type="tap", t=t))
        if step.kind != "miss":
            continue
        if use_continue and sim.continues_used < sim.max_continues:
            t += CONTINUE_PAUSE_TICKS
            sim.apply(InputEvent(type="continue", t=t))
            continue
        return Run(
            sim.score, sim.placed, sim.perfects, sim.max_combo, sim.continues_used, t / TICK_RATE
        )


def _percentile(values: list[int], p: int) -> int:
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, p * len(ordered) // 100)]


def summarize(sigma_ms: float, use_continue: bool, runs: int, rng: random.Random) -> Summary:
    played = [play_run(rng.getrandbits(32), sigma_ms, use_continue, rng) for _ in range(runs)]
    scores = [r.score for r in played]
    coins = [coins_for_run(s) for s in scores]
    placed = sum(r.placed for r in played)
    return Summary(
        sigma_ms=sigma_ms,
        use_continue=use_continue,
        runs=runs,
        score_p10=_percentile(scores, 10),
        score_median=int(statistics.median_low(scores)),
        score_p90=_percentile(scores, 90),
        score_mean=statistics.mean(scores),
        placed_mean=placed / runs,
        perfect_rate=sum(r.perfects for r in played) / max(1, placed),
        coins_mean=statistics.mean(coins),
        coins_median=int(statistics.median_low(coins)),
        seconds_mean=statistics.mean(r.seconds for r in played),
    )


def _gift(day: int, gift_share: float) -> float:
    """Expected gift on day `day` (1-based). Missed days break the streak, so it restarts on
    average every 1 / (1 - share) days."""
    if gift_share >= 1:
        return daily_reward(day)
    if gift_share <= 0:
        return 0.0
    streak = max(1, math.ceil(1 / (1 - gift_share)))
    return gift_share * daily_reward((day - 1) % streak + 1)


def days_to_afford(price: int, profile: PlayerProfile, limit: int = 3650) -> int:
    per_day = profile.coins_per_run * profile.runs_per_day * (1 + profile.doubled_share)
    total = 0.0
    for day in range(1, limit + 1):
        total += per_day + _gift(day, profile.gift_share)
        if total >= price:
            return day
    return limit


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--runs", type=int, default=3000, help="runs per row")
    parser.add_argument("--seed", type=int, default=1234)
    args = parser.parse_args()
    rng = random.Random(args.seed)

    print("sigma_ms continue  p10  p50  p90  mean  placed  perfect  coins(mean/p50)  seconds")
    by_sigma: dict[float, Summary] = {}
    for sigma in SIGMAS_MS:
        for use_continue in (False, True):
            s = summarize(sigma, use_continue, args.runs, rng)
            if not use_continue:
                by_sigma[sigma] = s
            print(
                f"{sigma:>8} {use_continue!s:>8} {s.score_p10:>4} {s.score_median:>4} "
                f"{s.score_p90:>4} {s.score_mean:>5.1f} {s.placed_mean:>7.1f} "
                f"{s.perfect_rate:>8.3f} {s.coins_mean:>9.2f}/{s.coins_median:<5} "
                f"{s.seconds_mean:>8.1f}"
            )

    profiles = [
        PlayerProfile("casual", by_sigma[50].coins_mean, 12, 0.2, 0.6),
        PlayerProfile("regular", by_sigma[35].coins_mean, 24, 0.3, 1.0),
        PlayerProfile("engaged", by_sigma[25].coins_mean, 40, 0.4, 1.0),
    ]
    print("\nDays until a player can afford a price (saving only for it):")
    prices = (200, 500, 1000, 1500, 3000, 9400)
    print("profile  " + " ".join(f"{p:>6}" for p in prices))
    for profile in profiles:
        print(f"{profile.name:<8} " + " ".join(f"{days_to_afford(p, profile):>6}" for p in prices))


if __name__ == "__main__":
    main()
