from collections.abc import AsyncIterator

from httpx import ASGITransport, AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine

from app.core.db import get_session
from app.main import create_app


async def test_health_ok_when_database_reachable(client: AsyncClient) -> None:
    response = await client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "database": "ok"}


async def test_health_503_when_database_unreachable() -> None:
    # Port 1 is never a Postgres server: the connection is refused immediately.
    dead_engine = create_async_engine("postgresql+asyncpg://x:x@127.0.0.1:1/x")
    app = create_app()

    async def _dead_session() -> AsyncIterator[AsyncSession]:
        async with AsyncSession(dead_engine) as session:
            yield session

    app.dependency_overrides[get_session] = _dead_session
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as ac:
        response = await ac.get("/health")
    await dead_engine.dispose()

    assert response.status_code == 503
    assert response.json() == {"status": "degraded", "database": "unreachable"}
