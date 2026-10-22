import uuid

from fastapi import APIRouter, Depends

from app.api.deps import RunServiceDep, UserIdDep
from app.api.schemas import (
    BalanceResponse,
    ChallengeOutcomeBody,
    RivalBody,
    StartRunRequest,
    StartRunResponse,
    SubmitRunRequest,
    SubmitRunResponse,
)
from app.core.rate_limit import rate_limit
from app.domain.sim import InputEvent
from app.services.runs import Rival

router = APIRouter(prefix="/runs", tags=["runs"])


def _rival(rival: Rival) -> RivalBody:
    return RivalBody(
        user_id=rival.user_id,
        display_name=rival.display_name,
        score=rival.score,
        placed=rival.placed,
    )


@router.post("", dependencies=[Depends(rate_limit("run_start", 60))])
async def start_run(
    body: StartRunRequest, user_id: UserIdDep, runs: RunServiceDep
) -> StartRunResponse:
    started = await runs.start(user_id, body.challenge_code)
    return StartRunResponse(
        run_id=started.run.id,
        seed=started.run.seed,
        sim_version=started.run.sim_version,
        continues_allowed=started.run.continues_allowed,
        challenge_code=started.challenge_code,
        rival=_rival(started.rival) if started.rival else None,
    )


@router.post("/{run_id}/submit", dependencies=[Depends(rate_limit("run_submit", 30))])
async def submit_run(
    run_id: uuid.UUID, body: SubmitRunRequest, user_id: UserIdDep, runs: RunServiceDep
) -> SubmitRunResponse:
    events = [InputEvent(e.t, e.type) for e in body.events]
    result = await runs.submit(user_id, run_id, events, body.claimed_score)
    run = result.run
    return SubmitRunResponse(
        run_id=run.id,
        status=run.status,
        reason=run.reject_reason,
        score=run.verified_score if run.status == "verified" else None,
        best_score=result.best_score,
        new_best=result.new_best,
        coins_earned=run.coins_earned,
        coins_doubled=run.coins_doubled,
        balance=result.balance,
        challenge=ChallengeOutcomeBody(
            code=result.challenge.code,
            rival=_rival(result.challenge.rival),
            won=result.challenge.won,
        )
        if result.challenge
        else None,
    )


@router.post("/{run_id}/double-coins")
async def double_coins(
    run_id: uuid.UUID, user_id: UserIdDep, runs: RunServiceDep
) -> BalanceResponse:
    # Idempotent by construction: the ledger key is derived from the run id.
    return BalanceResponse(balance=await runs.double_coins(user_id, run_id))
