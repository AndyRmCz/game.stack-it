import secrets
from collections.abc import Awaitable, Callable
from datetime import UTC, datetime

Clock = Callable[[], datetime]

# No 0/O, 1/I/L or U: codes are read aloud and typed by hand.
CODE_ALPHABET = "23456789ABCDEFGHJKMNPQRSTVWXYZ"


def utcnow() -> datetime:
    return datetime.now(UTC)


def random_code(length: int) -> str:
    return "".join(secrets.choice(CODE_ALPHABET) for _ in range(length))


async def unique_code(length: int, taken: Callable[[str], Awaitable[bool]]) -> str:
    """Random code not yet in use. The unique constraint remains the final guard on races."""
    for _ in range(10):
        code = random_code(length)
        if not await taken(code):
            return code
    raise RuntimeError("could not generate a unique code")


def normalize_code(code: str) -> str:
    return code.strip().upper()
