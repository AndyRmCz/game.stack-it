import uuid

from sqlalchemy import select
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncSession

from app.models import DailyStreak, Skin, UserSkin, Wallet, WalletTransaction


class WalletRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    def add(self, wallet: Wallet) -> None:
        self._session.add(wallet)

    async def get(self, user_id: uuid.UUID) -> Wallet | None:
        return await self._session.get(Wallet, user_id)

    async def lock(self, user_id: uuid.UUID) -> Wallet | None:
        """SELECT ... FOR UPDATE: serializes every balance change of one user."""
        return await self._session.scalar(
            select(Wallet)
            .where(Wallet.user_id == user_id)
            .with_for_update()
            .execution_options(populate_existing=True)
        )

    async def find_transaction(self, user_id: uuid.UUID, key: str) -> WalletTransaction | None:
        return await self._session.scalar(
            select(WalletTransaction).where(
                WalletTransaction.user_id == user_id, WalletTransaction.idempotency_key == key
            )
        )

    def add_transaction(self, tx: WalletTransaction) -> None:
        self._session.add(tx)

    async def recent_transactions(self, user_id: uuid.UUID, limit: int) -> list[WalletTransaction]:
        rows = await self._session.scalars(
            select(WalletTransaction)
            .where(WalletTransaction.user_id == user_id)
            .order_by(WalletTransaction.created_at.desc(), WalletTransaction.id.desc())
            .limit(limit)
        )
        return list(rows)


class SkinRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def catalog(self) -> list[Skin]:
        return list(await self._session.scalars(select(Skin).order_by(Skin.sort_order)))

    async def get(self, skin_id: str) -> Skin | None:
        return await self._session.get(Skin, skin_id)

    async def owned_ids(self, user_id: uuid.UUID) -> set[str]:
        rows = await self._session.scalars(
            select(UserSkin.skin_id).where(UserSkin.user_id == user_id)
        )
        return set(rows)

    def grant(self, user_id: uuid.UUID, skin_id: str) -> None:
        self._session.add(UserSkin(user_id=user_id, skin_id=skin_id))


class StreakRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def get(self, user_id: uuid.UUID) -> DailyStreak | None:
        return await self._session.get(DailyStreak, user_id)

    async def lock_or_create(self, user_id: uuid.UUID) -> DailyStreak:
        await self._session.execute(
            insert(DailyStreak)
            .values(user_id=user_id, current_streak=0)
            .on_conflict_do_nothing(index_elements=[DailyStreak.user_id])
        )
        streak = await self._session.scalar(
            select(DailyStreak)
            .where(DailyStreak.user_id == user_id)
            .with_for_update()
            .execution_options(populate_existing=True)
        )
        assert streak is not None
        return streak
