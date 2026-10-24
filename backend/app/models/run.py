import uuid
from datetime import date, datetime
from typing import Any

from sqlalchemy import (
    BigInteger,
    CheckConstraint,
    Date,
    DateTime,
    ForeignKey,
    Index,
    SmallInteger,
    String,
    func,
    text,
)
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.orm import Mapped, mapped_column

from app.models.base import Base

RUN_STATUSES = ("started", "verified", "flagged", "rejected")


class Run(Base):
    __tablename__ = "runs"
    __table_args__ = (
        CheckConstraint(f"status IN {RUN_STATUSES}", name="status_known"),
        CheckConstraint("seed >= 0 AND seed <= 4294967295", name="seed_uint32"),
        # Leaderboards: best verified score per user, all time and since a week start.
        Index(
            "ix_runs_verified_user_score",
            "user_id",
            text("verified_score DESC"),
            postgresql_where=text("status = 'verified'"),
        ),
        # The Daily Tower board: best verified score per user on a given day.
        Index(
            "ix_runs_verified_daily_user",
            "daily_date",
            "user_id",
            postgresql_include=["verified_score"],
            postgresql_where=text("status = 'verified' AND daily_date IS NOT NULL"),
        ),
        Index(
            "ix_runs_verified_user_finished",
            "user_id",
            "finished_at",
            postgresql_include=["verified_score"],
            postgresql_where=text("status = 'verified'"),
        ),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    challenge_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("challenges.id", ondelete="SET NULL", use_alter=True)
    )
    seed: Mapped[int] = mapped_column(BigInteger)
    # Set on Daily Tower runs: the UTC day whose seed this run plays.
    daily_date: Mapped[date | None] = mapped_column(Date)
    sim_version: Mapped[int] = mapped_column(SmallInteger)
    continues_allowed: Mapped[bool]
    status: Mapped[str] = mapped_column(String(16), default="started")
    reject_reason: Mapped[str | None] = mapped_column(String(64))
    events: Mapped[list[dict[str, Any]] | None] = mapped_column(JSONB)
    claimed_score: Mapped[int | None]
    verified_score: Mapped[int | None]
    placed: Mapped[int | None]
    perfects: Mapped[int | None]
    max_combo: Mapped[int | None]
    continues_used: Mapped[int | None]
    coins_earned: Mapped[int] = mapped_column(default=0)
    coins_doubled: Mapped[bool] = mapped_column(default=False)
    started_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    finished_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
