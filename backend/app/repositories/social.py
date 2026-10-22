import uuid
from dataclasses import dataclass
from datetime import datetime

from sqlalchemy import and_, delete, exists, func, literal, select, union_all
from sqlalchemy.dialects.postgresql import distinct_on, insert
from sqlalchemy.ext.asyncio import AsyncSession

from app.models import Challenge, ChallengeAttempt, Friendship, Run, User


def ordered_pair(a: uuid.UUID, b: uuid.UUID) -> tuple[uuid.UUID, uuid.UUID]:
    # Postgres compares uuids bytewise, as Python does for UUID objects.
    return (a, b) if a < b else (b, a)


@dataclass(frozen=True, slots=True)
class ScoreRow:
    user_id: uuid.UUID
    display_name: str
    best_score: int | None
    best_placed: int | None = None


class FriendRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def add(self, a: uuid.UUID, b: uuid.UUID) -> bool:
        """Creates the friendship if missing. Returns True when it was created."""
        user_a, user_b = ordered_pair(a, b)
        result = await self._session.execute(
            insert(Friendship)
            .values(user_a=user_a, user_b=user_b)
            .on_conflict_do_nothing()
            .returning(Friendship.user_a)
        )
        return result.first() is not None

    async def remove(self, a: uuid.UUID, b: uuid.UUID) -> bool:
        user_a, user_b = ordered_pair(a, b)
        result = await self._session.execute(
            delete(Friendship)
            .where(Friendship.user_a == user_a, Friendship.user_b == user_b)
            .returning(Friendship.user_a)
        )
        return result.first() is not None

    def _friend_ids(self, user_id: uuid.UUID):
        return union_all(
            select(Friendship.user_b.label("id")).where(Friendship.user_a == user_id),
            select(Friendship.user_a.label("id")).where(Friendship.user_b == user_id),
        )

    async def list_with_best(self, user_id: uuid.UUID) -> list[ScoreRow]:
        friends = self._friend_ids(user_id).subquery()
        # Each friend's single best verified run: its score, and its height for the rival ghost.
        best = (
            select(Run.user_id, Run.verified_score.label("best"), Run.placed.label("placed"))
            .where(Run.status == "verified", Run.user_id.in_(select(friends.c.id)))
            .order_by(Run.user_id, Run.verified_score.desc(), Run.finished_at)
            .ext(distinct_on(Run.user_id))
            .subquery()
        )
        rows = await self._session.execute(
            select(User.id, User.display_name, best.c.best, best.c.placed)
            .join(friends, friends.c.id == User.id)
            .outerjoin(best, best.c.user_id == User.id)
            .order_by(best.c.best.desc().nulls_last(), User.display_name)
        )
        return [ScoreRow(r.id, r.display_name, r.best, r.placed) for r in rows]

    async def leaderboard(self, user_id: uuid.UUID, since: datetime | None) -> list[ScoreRow]:
        """Best verified score per member (the user + friends) with at least one verified run."""
        members = union_all(self._friend_ids(user_id), select(literal(user_id).label("id")))
        members = members.subquery()
        conditions = [Run.status == "verified", Run.user_id.in_(select(members.c.id))]
        if since is not None:
            conditions.append(Run.finished_at >= since)
        rows = await self._session.execute(
            select(User.id, User.display_name, func.max(Run.verified_score).label("best"))
            .join(Run, Run.user_id == User.id)
            .where(and_(*conditions))
            .group_by(User.id, User.display_name)
            .order_by(func.max(Run.verified_score).desc(), User.display_name)
        )
        return [ScoreRow(r.id, r.display_name, r.best) for r in rows]

    async def are_friends(self, a: uuid.UUID, b: uuid.UUID) -> bool:
        user_a, user_b = ordered_pair(a, b)
        return bool(
            await self._session.scalar(
                select(exists().where(Friendship.user_a == user_a, Friendship.user_b == user_b))
            )
        )


class ChallengeRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    def add(self, challenge: Challenge) -> None:
        self._session.add(challenge)

    def add_attempt(self, attempt: ChallengeAttempt) -> None:
        self._session.add(attempt)

    async def get(self, challenge_id: uuid.UUID) -> Challenge | None:
        return await self._session.get(Challenge, challenge_id)

    async def get_by_code(self, code: str) -> Challenge | None:
        return await self._session.scalar(select(Challenge).where(Challenge.code == code))

    async def get_by_run(self, run_id: uuid.UUID) -> Challenge | None:
        return await self._session.scalar(select(Challenge).where(Challenge.run_id == run_id))

    async def code_taken(self, code: str) -> bool:
        return bool(await self._session.scalar(select(exists().where(Challenge.code == code))))

    async def attempt_for_run(self, run_id: uuid.UUID) -> ChallengeAttempt | None:
        return await self._session.scalar(
            select(ChallengeAttempt).where(ChallengeAttempt.run_id == run_id)
        )

    async def best_attempt_score(self, challenge_id: uuid.UUID, user_id: uuid.UUID) -> int | None:
        return await self._session.scalar(
            select(func.max(ChallengeAttempt.score)).where(
                ChallengeAttempt.challenge_id == challenge_id, ChallengeAttempt.user_id == user_id
            )
        )
