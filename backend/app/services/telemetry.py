import uuid
from dataclasses import dataclass
from datetime import UTC, datetime
from typing import Any

from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.telemetry import clean_event, occurred_at
from app.repositories.telemetry import TelemetryRepository


@dataclass(frozen=True, slots=True)
class IncomingEvent:
    id: uuid.UUID
    name: str
    session_id: uuid.UUID
    at: datetime
    props: dict[str, Any]


@dataclass(frozen=True, slots=True)
class IngestResult:
    accepted: int
    rejected: int


class TelemetryService:
    def __init__(self, session: AsyncSession, events: TelemetryRepository) -> None:
        self._session = session
        self._events = events

    async def ingest(
        self,
        user_id: uuid.UUID,
        app_version: str,
        platform: str,
        events: list[IncomingEvent],
        now: datetime | None = None,
    ) -> IngestResult:
        """Stores the valid events; rejected ones are counted, not stored. Re-sent ids are
        neither accepted nor rejected: they were stored the first time."""
        received = now or datetime.now(UTC)
        rows = []
        for event in events:
            props = clean_event(event.name, event.props)
            if props is None:
                continue
            client_at = event.at if event.at.tzinfo else event.at.replace(tzinfo=UTC)
            rows.append(
                {
                    "id": event.id,
                    "user_id": user_id,
                    "session_id": event.session_id,
                    "name": event.name,
                    "props": props,
                    "app_version": app_version,
                    "platform": platform,
                    "client_at": client_at,
                    "occurred_at": occurred_at(client_at, received),
                    "received_at": received,
                }
            )
        async with self._session.begin():
            accepted = await self._events.insert_new(rows)
        return IngestResult(accepted=accepted, rejected=len(events) - len(rows))
