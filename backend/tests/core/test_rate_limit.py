from app.core.rate_limit import RateLimiter


class Clock:
    def __init__(self) -> None:
        self.now = 1000.0

    def __call__(self) -> float:
        return self.now


def test_blocks_after_limit_and_resets_with_the_window() -> None:
    clock = Clock()
    limiter = RateLimiter(clock)

    assert [limiter.hit("auth:1.2.3.4", 3, 60) for _ in range(3)] == [None, None, None]
    retry_after = limiter.hit("auth:1.2.3.4", 3, 60)
    assert retry_after is not None and 0 < retry_after <= 60

    clock.now += 60
    assert limiter.hit("auth:1.2.3.4", 3, 60) is None


def test_keys_are_independent() -> None:
    limiter = RateLimiter(Clock())
    for _ in range(2):
        limiter.hit("auth:a", 2, 60)

    assert limiter.hit("auth:a", 2, 60) is not None
    assert limiter.hit("auth:b", 2, 60) is None
