import uuid
from dataclasses import dataclass

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.errors import NotFound
from app.models import User
from app.repositories.economy import WalletRepository
from app.repositories.runs import RunRepository
from app.repositories.users import IdentityRepository, UserRepository


@dataclass(frozen=True, slots=True)
class Profile:
    user: User
    providers: list[str]
    best_score: int
    coins: int


class ProfileService:
    def __init__(
        self,
        session: AsyncSession,
        users: UserRepository,
        identities: IdentityRepository,
        runs: RunRepository,
        wallets: WalletRepository,
    ) -> None:
        self._session = session
        self._users = users
        self._identities = identities
        self._runs = runs
        self._wallets = wallets

    async def get(self, user_id: uuid.UUID) -> Profile:
        async with self._session.begin():
            return await self._load(user_id)

    async def rename(self, user_id: uuid.UUID, display_name: str) -> Profile:
        async with self._session.begin():
            user = await self._users.get(user_id)
            if user is None:
                raise NotFound("user_not_found")
            user.display_name = display_name
            await self._session.flush()
            return await self._load(user_id)

    async def delete_account(self, user_id: uuid.UUID) -> None:
        """Permanently deletes the user and everything tied to them (store requirement)."""
        async with self._session.begin():
            await self._users.delete(user_id)

    async def _load(self, user_id: uuid.UUID) -> Profile:
        user = await self._users.get(user_id)
        if user is None:
            raise NotFound("user_not_found")
        wallet = await self._wallets.get(user_id)
        return Profile(
            user=user,
            providers=await self._identities.providers_for(user_id),
            best_score=await self._runs.best_score(user_id),
            coins=wallet.balance if wallet else 0,
        )
