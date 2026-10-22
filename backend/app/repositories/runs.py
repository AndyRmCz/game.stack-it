import uuid

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models import Run


class RunRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    def add(self, run: Run) -> None:
        self._session.add(run)

    async def get_for_update(self, run_id: uuid.UUID) -> Run | None:
        return await self._session.scalar(
            select(Run)
            .where(Run.id == run_id)
            .with_for_update()
            .execution_options(populate_existing=True)
        )

    async def best_score(self, user_id: uuid.UUID, exclude_run: uuid.UUID | None = None) -> int:
        query = select(func.max(Run.verified_score)).where(
            Run.user_id == user_id, Run.status == "verified"
        )
        if exclude_run is not None:
            query = query.where(Run.id != exclude_run)
        return (await self._session.scalar(query)) or 0
