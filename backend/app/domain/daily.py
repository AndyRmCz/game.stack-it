"""The Daily Tower: one seed per UTC day, the same for every player."""

import hashlib
import hmac
from datetime import date

DAILY_EPOCH = date(2026, 10, 24)  # Daily #1


def daily_number(day: date) -> int:
    """The number shown to players ("Daily #142")."""
    return (day - DAILY_EPOCH).days + 1


def daily_seed(secret: str, day: date) -> int:
    """A uint32 seed from the server secret and the date: same for everyone, unguessable ahead."""
    digest = hmac.new(secret.encode(), f"daily:{day.isoformat()}".encode(), hashlib.sha256)
    return int.from_bytes(digest.digest()[:4], "big")
