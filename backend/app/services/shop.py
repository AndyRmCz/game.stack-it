import uuid
from dataclasses import dataclass
from datetime import date

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.errors import BadRequest, Conflict, NotFound
from app.domain.economy import daily_reward, next_streak
from app.models import Skin, User
from app.repositories.economy import SkinRepository, StreakRepository, WalletRepository
from app.repositories.users import UserRepository
from app.services.common import Clock, utcnow
from app.services.wallet import WalletLedger


@dataclass(frozen=True, slots=True)
class ShopItem:
    skin: Skin
    owned: bool
    equipped: bool


@dataclass(frozen=True, slots=True)
class ShopView:
    balance: int
    items: list[ShopItem]


class ShopService:
    def __init__(
        self,
        session: AsyncSession,
        skins: SkinRepository,
        users: UserRepository,
        wallets: WalletRepository,
        ledger: WalletLedger,
    ) -> None:
        self._session = session
        self._skins = skins
        self._users = users
        self._wallets = wallets
        self._ledger = ledger

    async def catalog(self, user_id: uuid.UUID) -> ShopView:
        async with self._session.begin():
            return await self._view(user_id)

    async def purchase(self, user_id: uuid.UUID, skin_id: str, idempotency_key: str) -> ShopView:
        key = f"purchase:{idempotency_key}"
        async with self._session.begin():
            # Lock first: concurrent purchases by the same user are serialized from here on.
            await self._ledger.lock(user_id)
            if await self._ledger.find(user_id, key) is not None:
                return await self._view(user_id)  # retry of a completed purchase
            skin = await self._skins.get(skin_id)
            if skin is None:
                raise NotFound("skin_not_found")
            if skin.is_default or skin_id in await self._skins.owned_ids(user_id):
                raise Conflict("already_owned")
            await self._ledger.apply(
                user_id, -skin.price, kind="purchase", key=key, reference=skin.id
            )
            self._skins.grant(user_id, skin.id)
            await self._session.flush()
            return await self._view(user_id)

    async def equip(self, user_id: uuid.UUID, skin_id: str) -> ShopView:
        async with self._session.begin():
            skin = await self._skins.get(skin_id)
            if skin is None:
                raise NotFound("skin_not_found")
            if not skin.is_default and skin_id not in await self._skins.owned_ids(user_id):
                raise Conflict("skin_not_owned")
            user = await self._user(user_id)
            if skin.kind == "block":
                user.equipped_block_skin = skin.id
            elif skin.kind == "music":
                user.equipped_music = skin.id
            else:
                user.equipped_theme = skin.id
            await self._session.flush()
            return await self._view(user_id)

    async def _user(self, user_id: uuid.UUID) -> User:
        user = await self._users.get(user_id)
        if user is None:
            raise NotFound("user_not_found")
        return user

    async def _view(self, user_id: uuid.UUID) -> ShopView:
        user = await self._user(user_id)
        owned = await self._skins.owned_ids(user_id)
        wallet = await self._wallets.get(user_id)
        equipped = {user.equipped_block_skin, user.equipped_theme, user.equipped_music}
        items = [
            ShopItem(skin, skin.is_default or skin.id in owned, skin.id in equipped)
            for skin in await self._skins.catalog()
        ]
        return ShopView(wallet.balance if wallet else 0, items)


@dataclass(frozen=True, slots=True)
class DailyStatus:
    streak: int
    claimed_today: bool
    today: date
    next_reward: int
    claimed_amount: int | None = None
    balance: int | None = None


class DailyRewardService:
    def __init__(
        self,
        session: AsyncSession,
        streaks: StreakRepository,
        ledger: WalletLedger,
        clock: Clock = utcnow,
    ) -> None:
        self._session = session
        self._streaks = streaks
        self._ledger = ledger
        self._clock = clock

    async def status(self, user_id: uuid.UUID) -> DailyStatus:
        today = self._clock().date()
        async with self._session.begin():
            streak = await self._streaks.get(user_id)
        last = streak.last_claim_date if streak else None
        current = streak.current_streak if streak else 0
        if last == today:
            return DailyStatus(current, True, today, daily_reward(current + 1))
        return DailyStatus(current, False, today, daily_reward(next_streak(last, current, today)))

    async def claim(self, user_id: uuid.UUID, idempotency_key: str) -> DailyStatus:
        if not idempotency_key:
            raise BadRequest("idempotency_key_required")
        key = f"daily:{idempotency_key}"
        today = self._clock().date()
        async with self._session.begin():
            wallet = await self._ledger.lock(user_id)
            previous = await self._ledger.find(user_id, key)
            streak = await self._streaks.lock_or_create(user_id)
            if previous is not None:
                return DailyStatus(
                    streak.current_streak,
                    True,
                    today,
                    daily_reward(streak.current_streak + 1),
                    claimed_amount=previous.amount,
                    balance=wallet.balance,
                )
            if streak.last_claim_date == today:
                raise Conflict("already_claimed")
            day = next_streak(streak.last_claim_date, streak.current_streak, today)
            reward = daily_reward(day)
            streak.current_streak, streak.last_claim_date = day, today
            entry = await self._ledger.apply(
                user_id, reward, kind="daily_reward", key=key, reference=today.isoformat()
            )
            return DailyStatus(
                day,
                True,
                today,
                daily_reward(day + 1),
                claimed_amount=reward,
                balance=entry.balance,
            )
