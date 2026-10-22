import logging
import secrets
import uuid
from dataclasses import dataclass
from datetime import timedelta

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import Settings
from app.core.errors import Conflict, Gone, NotFound
from app.domain.anticheat import assess_run
from app.domain.economy import coins_for_run
from app.domain.sim import SIM_VERSION, InputEvent
from app.models import Challenge, ChallengeAttempt, Run, User
from app.repositories.runs import RunRepository
from app.repositories.social import ChallengeRepository, FriendRepository
from app.repositories.users import UserRepository
from app.services.common import Clock, normalize_code, utcnow
from app.services.wallet import WalletLedger

logger = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class Rival:
    user_id: uuid.UUID
    display_name: str
    score: int
    placed: int


@dataclass(frozen=True, slots=True)
class StartedRun:
    run: Run
    challenge_code: str | None
    rival: Rival | None


@dataclass(frozen=True, slots=True)
class ChallengeOutcome:
    code: str
    rival: Rival
    won: bool


@dataclass(frozen=True, slots=True)
class SubmittedRun:
    run: Run
    best_score: int
    new_best: bool
    balance: int
    challenge: ChallengeOutcome | None


class RunService:
    """Starts runs (server-issued seed) and verifies submissions by replaying them."""

    def __init__(
        self,
        session: AsyncSession,
        settings: Settings,
        runs: RunRepository,
        challenges: ChallengeRepository,
        friends: FriendRepository,
        users: UserRepository,
        ledger: WalletLedger,
        clock: Clock = utcnow,
    ) -> None:
        self._session = session
        self._settings = settings
        self._runs = runs
        self._challenges = challenges
        self._friends = friends
        self._users = users
        self._ledger = ledger
        self._clock = clock

    async def start(self, user_id: uuid.UUID, challenge_code: str | None = None) -> StartedRun:
        async with self._session.begin():
            challenge = None
            if challenge_code:
                challenge = await self._open_challenge(normalize_code(challenge_code), user_id)
            run = Run(
                user_id=user_id,
                challenge_id=challenge.id if challenge else None,
                seed=challenge.seed if challenge else secrets.randbits(32),
                sim_version=SIM_VERSION,
                # Duels stay fair: nobody can buy a second life in a challenge.
                continues_allowed=challenge is None,
            )
            self._runs.add(run)
            await self._session.flush()
            rival = None
            if challenge is not None:
                self._challenges.add_attempt(
                    ChallengeAttempt(challenge_id=challenge.id, user_id=user_id, run_id=run.id)
                )
                # Accepting a challenge makes you friends with its creator.
                await self._friends.add(user_id, challenge.creator_id)
                rival = await self._rival(challenge)
            return StartedRun(run, challenge.code if challenge else None, rival)

    async def submit(
        self,
        user_id: uuid.UUID,
        run_id: uuid.UUID,
        events: list[InputEvent],
        claimed_score: int,
    ) -> SubmittedRun:
        now = self._clock()
        async with self._session.begin():
            run = await self._owned_run_for_update(user_id, run_id)
            if run.status == "started":
                await self._verify(run, events, claimed_score, now)
            # Already-submitted runs fall through: retries (offline queue) get the same answer.
            return await self._result(run)

    async def double_coins(self, user_id: uuid.UUID, run_id: uuid.UUID) -> int:
        """Rewarded-ad bonus: credits the run's coins a second time, once per run."""
        async with self._session.begin():
            run = await self._owned_run_for_update(user_id, run_id)
            if run.status != "verified" or run.coins_earned <= 0:
                raise Conflict("run_not_eligible")
            entry = await self._ledger.apply(
                user_id,
                run.coins_earned,
                kind="rewarded_double",
                key=f"double:{run.id}",
                reference=str(run.id),
            )
            run.coins_doubled = True
            return entry.balance

    # --- helpers --------------------------------------------------------------------------------

    async def _open_challenge(self, code: str, user_id: uuid.UUID) -> Challenge:
        challenge = await self._challenges.get_by_code(code)
        if challenge is None:
            raise NotFound("challenge_not_found")
        if challenge.expires_at <= self._clock():
            raise Gone("challenge_expired")
        if challenge.creator_id == user_id:
            raise Conflict("own_challenge")
        return challenge

    async def _owned_run_for_update(self, user_id: uuid.UUID, run_id: uuid.UUID) -> Run:
        run = await self._runs.get_for_update(run_id)
        if run is None or run.user_id != user_id:
            raise NotFound("run_not_found")
        return run

    async def _verify(self, run: Run, events: list[InputEvent], claimed: int, now) -> None:
        run.events = [{"t": e.t, "type": e.type} for e in events]
        run.claimed_score = claimed
        run.finished_at = now

        if now - run.started_at > timedelta(hours=self._settings.run_ttl_hours):
            run.status, run.reject_reason = "rejected", "run_expired"
            return

        verdict = assess_run(
            seed=run.seed,
            events=events,
            claimed_score=claimed,
            continues_allowed=run.continues_allowed,
            elapsed=now - run.started_at,
        )
        run.status, run.reject_reason = verdict.status, verdict.reason
        if verdict.result is not None:
            r = verdict.result
            run.verified_score = r.score if verdict.status != "rejected" else None
            run.placed, run.perfects = r.placed, r.perfects
            run.max_combo, run.continues_used = r.max_combo, r.continues_used
        if verdict.status != "verified":
            logger.info(
                "run not verified",
                extra={"run_id": str(run.id), "status": verdict.status, "reason": verdict.reason},
            )
            return

        run.coins_earned = coins_for_run(run.verified_score or 0)
        if run.coins_earned > 0:
            await self._ledger.apply(
                run.user_id,
                run.coins_earned,
                kind="run_reward",
                key=f"run:{run.id}",
                reference=str(run.id),
            )
        if run.challenge_id is not None:
            attempt = await self._challenges.attempt_for_run(run.id)
            challenge = await self._challenges.get(run.challenge_id)
            if attempt is not None and challenge is not None:
                attempt.score = run.verified_score
                attempt.won = (run.verified_score or 0) > challenge.score

    async def _result(self, run: Run) -> SubmittedRun:
        previous_best = await self._runs.best_score(run.user_id, exclude_run=run.id)
        score = run.verified_score if run.status == "verified" else None
        wallet = await self._ledger.lock(run.user_id)
        outcome = None
        if run.challenge_id is not None:
            challenge = await self._challenges.get(run.challenge_id)
            if challenge is not None:
                outcome = ChallengeOutcome(
                    code=challenge.code,
                    rival=await self._rival(challenge),
                    won=score is not None and score > challenge.score,
                )
        return SubmittedRun(
            run=run,
            best_score=max(previous_best, score or 0),
            new_best=score is not None and score > previous_best,
            balance=wallet.balance,
            challenge=outcome,
        )

    async def _rival(self, challenge: Challenge) -> Rival:
        creator: User | None = await self._users.get(challenge.creator_id)
        return Rival(
            user_id=challenge.creator_id,
            display_name=creator.display_name if creator else "",
            score=challenge.score,
            placed=challenge.placed,
        )
