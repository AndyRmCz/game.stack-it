from collections.abc import AsyncIterator
from functools import lru_cache
from typing import Annotated

from fastapi import Depends
from sqlalchemy.ext.asyncio import (
    AsyncEngine,
    AsyncSession,
    async_sessionmaker,
    create_async_engine,
)

from app.core.config import get_settings

# Connections older than this are replaced when next taken from the pool, so none lives forever.
POOL_RECYCLE_SECONDS = 1800


def build_engine(url: str) -> AsyncEngine:
    """No pool_pre_ping: with asyncpg it costs three round trips (BEGIN, ";", ROLLBACK) every time
    a connection is taken, and a request takes two. A connection the server dropped fails the one
    query that uses it instead; the pool then replaces it, and the app retries 5xx answers."""
    return create_async_engine(url, pool_recycle=POOL_RECYCLE_SECONDS)


@lru_cache
def get_engine() -> AsyncEngine:
    return build_engine(get_settings().database_url)


@lru_cache
def get_sessionmaker() -> async_sessionmaker[AsyncSession]:
    return async_sessionmaker(get_engine(), expire_on_commit=False)


async def get_session() -> AsyncIterator[AsyncSession]:
    """One session per request. Services own the transaction via `async with session.begin()`."""
    async with get_sessionmaker()() as session:
        yield session


SessionDep = Annotated[AsyncSession, Depends(get_session)]
