import uuid
from dataclasses import dataclass
from datetime import datetime, timedelta
from typing import Literal

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import Settings
from app.core.errors import BadRequest, Conflict, NotFound
from app.domain.economy import week_start
from app.models import Challenge, User
from app.repositories.runs import RunRepository
from app.repositories.social import ChallengeRepository, FriendRepository, ScoreRow
from app.repositories.users import UserRepository
from app.services.common import Clock, normalize_code, unique_code, utcnow

CHALLENGE_CODE_LENGTH = 7


@dataclass(frozen=True, slots=True)
class ChallengeView:
    challenge: Challenge
    creator_name: str
    url: str
    expired: bool
    is_own: bool
    my_best: int | None


class ChallengeService:
    def __init__(
        self,
        session: AsyncSession,
        settings: Settings,
        challenges: ChallengeRepository,
        runs: RunRepository,
        users: UserRepository,
        clock: Clock = utcnow,
    ) -> None:
        self._session = session
        self._settings = settings
        self._challenges = challenges
        self._runs = runs
        self._users = users
        self._clock = clock

    def url_for(self, code: str) -> str:
        return f"{self._settings.public_base_url}/c/{code}"

    async def create(self, user_id: uuid.UUID, run_id: uuid.UUID) -> ChallengeView:
        """Turns one of the user's verified runs into a shareable duel (idempotent per run)."""
        async with self._session.begin():
            run = await self._runs.get_for_update(run_id)
            if run is None or run.user_id != user_id:
                raise NotFound("run_not_found")
            if run.status != "verified" or run.verified_score is None:
                raise Conflict("run_not_verified")
            challenge = await self._challenges.get_by_run(run.id)
            if challenge is None:
                challenge = Challenge(
                    code=await unique_code(CHALLENGE_CODE_LENGTH, self._challenges.code_taken),
                    creator_id=user_id,
                    run_id=run.id,
                    seed=run.seed,
                    score=run.verified_score,
                    placed=run.placed or 0,
                    expires_at=self._clock() + timedelta(days=self._settings.challenge_ttl_days),
                )
                self._challenges.add(challenge)
                await self._session.flush()
            return await self._view(challenge, viewer_id=user_id)

    async def get(self, code: str, viewer_id: uuid.UUID | None) -> ChallengeView:
        async with self._session.begin():
            challenge = await self._challenges.get_by_code(normalize_code(code))
            if challenge is None:
                raise NotFound("challenge_not_found")
            return await self._view(challenge, viewer_id)

    async def _view(self, challenge: Challenge, viewer_id: uuid.UUID | None) -> ChallengeView:
        creator: User | None = await self._users.get(challenge.creator_id)
        my_best = None
        if viewer_id is not None and viewer_id != challenge.creator_id:
            my_best = await self._challenges.best_attempt_score(challenge.id, viewer_id)
        return ChallengeView(
            challenge=challenge,
            creator_name=creator.display_name if creator else "",
            url=self.url_for(challenge.code),
            expired=challenge.expires_at <= self._clock(),
            is_own=viewer_id == challenge.creator_id,
            my_best=my_best,
        )


@dataclass(frozen=True, slots=True)
class LeaderboardEntry:
    rank: int
    row: ScoreRow
    is_me: bool


@dataclass(frozen=True, slots=True)
class Leaderboard:
    period: str
    since: datetime | None
    entries: list[LeaderboardEntry]


class FriendService:
    def __init__(
        self,
        session: AsyncSession,
        friends: FriendRepository,
        users: UserRepository,
        clock: Clock = utcnow,
    ) -> None:
        self._session = session
        self._friends = friends
        self._users = users
        self._clock = clock

    async def list(self, user_id: uuid.UUID) -> list[ScoreRow]:
        async with self._session.begin():
            return await self._friends.list_with_best(user_id)

    async def add_by_code(self, user_id: uuid.UUID, friend_code: str) -> User:
        async with self._session.begin():
            friend = await self._users.get_by_friend_code(normalize_code(friend_code))
            if friend is None:
                raise NotFound("friend_code_not_found")
            if friend.id == user_id:
                raise BadRequest("cannot_friend_yourself")
            await self._friends.add(user_id, friend.id)
            return friend

    async def remove(self, user_id: uuid.UUID, friend_id: uuid.UUID) -> None:
        async with self._session.begin():
            if not await self._friends.remove(user_id, friend_id):
                raise NotFound("friend_not_found")

    async def leaderboard(
        self, user_id: uuid.UUID, period: Literal["all_time", "weekly"]
    ) -> Leaderboard:
        since = week_start(self._clock()) if period == "weekly" else None
        async with self._session.begin():
            rows = await self._friends.leaderboard(user_id, since)
        entries, rank, previous = [], 0, None
        for i, row in enumerate(rows, start=1):
            if row.best_score != previous:  # ties share a rank
                rank, previous = i, row.best_score
            entries.append(LeaderboardEntry(rank, row, row.user_id == user_id))
        return Leaderboard(period, since, entries)
