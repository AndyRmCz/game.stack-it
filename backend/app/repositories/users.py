import uuid
from datetime import datetime

from sqlalchemy import delete, exists, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.models import AuthIdentity, RefreshToken, User


class UserRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def get(self, user_id: uuid.UUID) -> User | None:
        return await self._session.get(User, user_id)

    async def exists(self, user_id: uuid.UUID) -> bool:
        return bool(await self._session.scalar(select(exists().where(User.id == user_id))))

    async def get_by_friend_code(self, code: str) -> User | None:
        return await self._session.scalar(select(User).where(User.friend_code == code))

    async def friend_code_taken(self, code: str) -> bool:
        return bool(await self._session.scalar(select(exists().where(User.friend_code == code))))

    def add(self, user: User) -> None:
        self._session.add(user)

    async def delete(self, user_id: uuid.UUID) -> None:
        # Every dependent row is ON DELETE CASCADE (or SET NULL for other users' runs).
        await self._session.execute(delete(User).where(User.id == user_id))


class IdentityRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def get(self, provider: str, subject: str) -> AuthIdentity | None:
        return await self._session.scalar(
            select(AuthIdentity).where(
                AuthIdentity.provider == provider, AuthIdentity.subject == subject
            )
        )

    async def providers_for(self, user_id: uuid.UUID) -> list[str]:
        rows = await self._session.scalars(
            select(AuthIdentity.provider)
            .where(AuthIdentity.user_id == user_id)
            .order_by(AuthIdentity.id)
        )
        return list(rows)

    async def has_provider(self, user_id: uuid.UUID, provider: str) -> bool:
        return bool(
            await self._session.scalar(
                select(
                    exists().where(
                        AuthIdentity.user_id == user_id, AuthIdentity.provider == provider
                    )
                )
            )
        )

    def add(self, identity: AuthIdentity) -> None:
        self._session.add(identity)


class RefreshTokenRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def get_for_update(self, token_hash: str) -> RefreshToken | None:
        return await self._session.scalar(
            select(RefreshToken).where(RefreshToken.token_hash == token_hash).with_for_update()
        )

    def add(self, token: RefreshToken) -> None:
        self._session.add(token)

    async def revoke_family(self, family_id: uuid.UUID, now: datetime) -> None:
        await self._session.execute(
            update(RefreshToken)
            .where(RefreshToken.family_id == family_id, RefreshToken.revoked_at.is_(None))
            .values(revoked_at=now)
        )
