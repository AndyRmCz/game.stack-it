"""In-memory fixed-window rate limiter.

Good for a single API instance (the Railway default). With several replicas, move the counters to
Redis; the dependency interface stays the same.
"""

import time
from collections.abc import Callable

from fastapi import Request

from app.core.config import get_settings
from app.core.errors import RateLimited

_PRUNE_THRESHOLD = 10_000


class RateLimiter:
    def __init__(self, clock: Callable[[], float] = time.monotonic) -> None:
        self._clock = clock
        self._windows: dict[str, tuple[float, int]] = {}

    def hit(self, key: str, limit: int, window_seconds: float) -> float | None:
        """Counts a hit. Returns None if allowed, else the seconds until the window resets."""
        now = self._clock()
        start, count = self._windows.get(key, (now, 0))
        if now - start >= window_seconds:
            start, count = now, 0
        if count >= limit:
            return window_seconds - (now - start)
        self._windows[key] = (start, count + 1)
        if len(self._windows) > _PRUNE_THRESHOLD:
            self._prune(now, window_seconds)
        return None

    def _prune(self, now: float, window_seconds: float) -> None:
        self._windows = {k: v for k, v in self._windows.items() if now - v[0] < window_seconds}


def rate_limit(bucket: str, limit: int, window_seconds: float = 60) -> Callable:
    """FastAPI dependency limiting requests per client IP for one bucket."""

    async def _dependency(request: Request) -> None:
        if not get_settings().rate_limit_enabled:
            return
        limiter: RateLimiter = request.app.state.rate_limiter
        client = request.client.host if request.client else "unknown"
        retry_after = limiter.hit(f"{bucket}:{client}", limit, window_seconds)
        if retry_after is not None:
            raise RateLimited(headers={"Retry-After": str(max(1, int(retry_after) + 1))})

    return _dependency
