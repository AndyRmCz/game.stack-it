from fastapi import APIRouter

from app.api.deps import MissionServiceDep, UserIdDep
from app.api.schemas import ChestBody, ClaimMissionRequest, MissionBody, MissionsResponse
from app.domain.missions import CHEST_REWARD
from app.services.missions import MissionsView

router = APIRouter(prefix="/missions", tags=["missions"])


def _missions(view: MissionsView) -> MissionsResponse:
    return MissionsResponse(
        day=view.day,
        missions=[
            MissionBody(
                slot=s.mission.slot,
                kind=s.mission.kind,
                target=s.mission.target,
                progress=min(s.progress, s.mission.target),
                reward=s.mission.reward,
                claimed=s.claimed,
            )
            for s in view.missions
        ],
        chest=ChestBody(reward=CHEST_REWARD, ready=view.chest_ready, claimed=view.chest_claimed),
        balance=view.balance,
    )


@router.get("")
async def get_missions(user_id: UserIdDep, missions: MissionServiceDep) -> MissionsResponse:
    return _missions(await missions.today(user_id))


@router.post("/claim")
async def claim_mission(
    body: ClaimMissionRequest, user_id: UserIdDep, missions: MissionServiceDep
) -> MissionsResponse:
    """Slots 0-2 are the missions, 3 the chest. Idempotent: a second claim pays nothing."""
    return _missions(await missions.claim(user_id, body.slot))
