import pytest
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError
from sqlalchemy.ext.asyncio import AsyncEngine

from app.core.db import build_engine
from tests.conftest import TEST_DATABASE_URL


async def test_a_pooled_connection_is_reused_without_a_ping(monkeypatch) -> None:
    # A ping costs three round trips with asyncpg (BEGIN, ";", ROLLBACK): more than most queries.
    engine = build_engine(TEST_DATABASE_URL)
    pings = []
    monkeypatch.setattr(engine.dialect, "do_ping", lambda conn: pings.append(conn) or True)
    try:
        for _ in range(3):
            async with engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
    finally:
        await engine.dispose()

    assert pings == []


async def test_a_dropped_connection_fails_one_query_and_the_next_one_works(
    engine: AsyncEngine,
) -> None:
    """Without a ping, a connection the server dropped (a Postgres restart) is found out by the
    query that uses it: that request fails (the app retries it), and the pool replaces it."""
    app_engine = build_engine(TEST_DATABASE_URL)
    try:
        async with app_engine.connect() as conn:
            pid = await conn.scalar(text("SELECT pg_backend_pid()"))
        async with engine.connect() as admin:
            await admin.execute(text("SELECT pg_terminate_backend(:pid)"), {"pid": pid})

        with pytest.raises(DBAPIError) as dropped:
            async with app_engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
        assert dropped.value.connection_invalidated

        async with app_engine.connect() as conn:
            assert await conn.scalar(text("SELECT 1")) == 1
    finally:
        await app_engine.dispose()
