from typing import Any

from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncSession

from app.models import ClientEvent


class TelemetryRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def insert_new(self, rows: list[dict[str, Any]]) -> int:
        """Inserts the rows whose id is not stored yet; returns how many were new."""
        if not rows:
            return 0
        result = await self._session.scalars(
            insert(ClientEvent)
            .values(rows)
            .on_conflict_do_nothing(index_elements=[ClientEvent.id])
            .returning(ClientEvent.id)
        )
        return len(result.all())
