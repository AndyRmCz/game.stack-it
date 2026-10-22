import hashlib
import secrets
import uuid
from datetime import UTC, datetime, timedelta

import jwt

from app.core.config import Settings
from app.core.errors import Unauthorized

_ALGORITHM = "HS256"


def create_access_token(user_id: uuid.UUID, settings: Settings, now: datetime | None = None) -> str:
    issued = now or datetime.now(UTC)
    payload = {
        "sub": str(user_id),
        "typ": "access",
        "iat": issued,
        "exp": issued + timedelta(seconds=settings.access_token_ttl_seconds),
    }
    return jwt.encode(payload, settings.jwt_secret, algorithm=_ALGORITHM)


def decode_access_token(token: str, settings: Settings) -> uuid.UUID:
    try:
        payload = jwt.decode(
            token, settings.jwt_secret, algorithms=[_ALGORITHM], options={"require": ["exp", "sub"]}
        )
    except jwt.ExpiredSignatureError as exc:
        raise Unauthorized("token_expired") from exc
    except jwt.PyJWTError as exc:
        raise Unauthorized("invalid_token") from exc
    if payload.get("typ") != "access":
        raise Unauthorized("invalid_token")
    try:
        return uuid.UUID(payload["sub"])
    except ValueError as exc:
        raise Unauthorized("invalid_token") from exc


def new_opaque_token() -> str:
    return secrets.token_urlsafe(32)


def sha256_hex(value: str) -> str:
    return hashlib.sha256(value.encode()).hexdigest()


def constant_time_equals(a: str, b: str) -> bool:
    return secrets.compare_digest(a.encode(), b.encode())
