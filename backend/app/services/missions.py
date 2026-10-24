import uuid
from dataclasses import dataclass
from datetime import UTC, date, datetime, time

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.errors import BadRequest, Conflict
from app.domain.missions import (
    CHEST_REWARD,
    CHEST_SLOT,
    Mission,
    claim_key,
    missions_for,
    progress,
)
from app.repositories.runs import RunRepository
from app.services.common import Clock, utcnow
from app.services.wallet import WalletLedger


@dataclass(frozen=True, slots=True)
class MissionState:
    mission: Mission
    progress: int
    claimed: bool

    @property
    def done(self) -> bool:
        return self.progress >= self.mission.target


@dataclass(frozen=True, slots=True)
class MissionsView:
    day: date
    missions: list[MissionState]
    chest_claimed: bool
    balance: int | None = None

    @property
    def chest_ready(self) -> bool:
        return all(m.done for m in self.missions)


class MissionService:
    def __init__(
        self,
        session: AsyncSession,
        runs: RunRepository,
        ledger: WalletLedger,
        clock: Clock = utcnow,
    ) -> None:
        self._session = session
        self._runs = runs
        self._ledger = ledger
        self._clock = clock

    async def today(self, user_id: uuid.UUID) -> MissionsView:
        async with self._session.begin():
            return await self._view(user_id, self._clock().date())

    async def claim(self, user_id: uuid.UUID, slot: int) -> MissionsView:
        """Pays a finished mission (or the chest) once; claiming again returns the same state."""
        day = self._clock().date()
        async with self._session.begin():
            await self._ledger.lock(user_id)  # serializes claims by the same player
            view = await self._view(user_id, day)
            if slot == CHEST_SLOT:
                if not view.chest_ready:
                    raise Conflict("mission_incomplete")
                reward, reference = CHEST_REWARD, "chest"
            elif 0 <= slot < len(view.missions):
                state = view.missions[slot]
                if not state.done:
                    raise Conflict("mission_incomplete")
                reward, reference = state.mission.reward, state.mission.kind
            else:
                raise BadRequest("unknown_mission")
            key = claim_key(day, slot)
            entry = await self._ledger.find(user_id, key)
            balance = None
            if entry is None:
                applied = await self._ledger.apply(
                    user_id, reward, kind="mission", key=key, reference=reference
                )
                balance = applied.balance
            view = await self._view(user_id, day)
            if balance is None:
                balance = (await self._ledger.lock(user_id)).balance
            return MissionsView(view.day, view.missions, view.chest_claimed, balance)

    async def _view(self, user_id: uuid.UUID, day: date) -> MissionsView:
        since = datetime.combine(day, time(), tzinfo=UTC)
        stats = await self._runs.day_stats(user_id, since, day)
        states = [
            MissionState(
                m,
                progress(m.kind, stats),
                await self._ledger.find(user_id, claim_key(day, m.slot)) is not None,
            )
            for m in missions_for(user_id, day)
        ]
        chest = await self._ledger.find(user_id, claim_key(day, CHEST_SLOT)) is not None
        return MissionsView(day, states, chest)
