import uuid
from datetime import datetime
from typing import Any

from sqlalchemy import DateTime, ForeignKey, Index, String, func
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.orm import Mapped, mapped_column

from app.models.base import Base


class ClientEvent(Base):
    """Product telemetry from the app (docs/PLAN.md, "Measurement findings").

    The id is generated on the device, so a re-sent batch never stores an event twice.
    `occurred_at` is the client's time when plausible, else `received_at`.
    """

    __tablename__ = "client_events"
    __table_args__ = (
        Index("ix_client_events_name_occurred", "name", "occurred_at"),
        Index("ix_client_events_user_occurred", "user_id", "occurred_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    session_id: Mapped[uuid.UUID]
    name: Mapped[str] = mapped_column(String(40))
    props: Mapped[dict[str, Any]] = mapped_column(JSONB)
    app_version: Mapped[str] = mapped_column(String(32))
    platform: Mapped[str] = mapped_column(String(16))
    client_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    occurred_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    received_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )
