"""Shared fixtures. Tests run against a real PostgreSQL (docker compose locally, a service
container in CI), never SQLite, so constraints, locking and JSONB behave like production."""

import asyncio
import os
from collections.abc import AsyncIterator
from pathlib import Path

import pytest
from alembic import command
from alembic.config import Config
from fastapi import FastAPI
from httpx import ASGITransport, AsyncClient
from sqlalchemy import text
from sqlalchemy.ext.asyncio import (
    AsyncEngine,
    AsyncSession,
    async_sessionmaker,
    create_async_engine,
)

from app.core.db import get_session
from app.main import create_app

TEST_DATABASE_URL = os.environ.get(
    "TEST_DATABASE_URL", "postgresql+asyncpg://stackit:stackit@localhost:5432/stackit_test"
)
BACKEND_DIR = Path(__file__).resolve().parent.parent


def _alembic_upgrade_head() -> None:
    cfg = Config(BACKEND_DIR / "alembic.ini", attributes={"configure_logger": False})
    cfg.set_main_option("script_location", str(BACKEND_DIR / "alembic"))
    cfg.set_main_option("sqlalchemy.url", TEST_DATABASE_URL.replace("%", "%%"))
    command.upgrade(cfg, "head")


@pytest.fixture(scope="session")
async def engine() -> AsyncIterator[AsyncEngine]:
    engine = create_async_engine(TEST_DATABASE_URL)
    if not (engine.url.database or "").endswith("_test"):
        pytest.exit(f"Refusing to wipe non-test database {engine.url.database!r}", returncode=2)
    async with engine.begin() as conn:
        await conn.execute(text("DROP SCHEMA public CASCADE"))
        await conn.execute(text("CREATE SCHEMA public"))
    # Build the schema through the real migrations so a broken migration fails the suite.
    # Alembic's env.py calls asyncio.run(), so it needs a thread without a running loop.
    await asyncio.to_thread(_alembic_upgrade_head)
    yield engine
    await engine.dispose()


@pytest.fixture
async def db_session(engine: AsyncEngine) -> AsyncIterator[AsyncSession]:
    """Session inside an outer transaction that is rolled back after each test.

    `create_savepoint` turns the services' own `session.begin()` / commit into savepoints, so
    service code runs unchanged while nothing leaks between tests.
    """
    async with engine.connect() as conn:
        outer = await conn.begin()
        session = AsyncSession(
            bind=conn, join_transaction_mode="create_savepoint", expire_on_commit=False
        )
        try:
            yield session
        finally:
            await session.close()
            await outer.rollback()


@pytest.fixture
def app(db_session: AsyncSession) -> FastAPI:
    """App whose requests all share the rolled-back test session."""
    app = create_app()

    async def _session_override() -> AsyncIterator[AsyncSession]:
        yield db_session

    app.dependency_overrides[get_session] = _session_override
    return app


@pytest.fixture
async def client(app: FastAPI) -> AsyncIterator[AsyncClient]:
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as ac:
        yield ac


_KEEP_TABLES = {"alembic_version", "skins"}


@pytest.fixture
async def committed(engine: AsyncEngine) -> AsyncIterator[async_sessionmaker[AsyncSession]]:
    """Real, independently committing sessions for concurrency tests. Tables are emptied after."""
    yield async_sessionmaker(engine, expire_on_commit=False)
    async with engine.begin() as conn:
        tables = (
            await conn.execute(text("SELECT tablename FROM pg_tables WHERE schemaname = 'public'"))
        ).scalars()
        names = ", ".join(t for t in tables if t not in _KEEP_TABLES)
        await conn.execute(text(f"TRUNCATE {names} CASCADE"))
