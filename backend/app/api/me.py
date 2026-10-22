from fastapi import APIRouter, status

from app.api.deps import ProfileServiceDep, UserIdDep
from app.api.schemas import ProfileResponse, RenameRequest
from app.services.profile import Profile

router = APIRouter(prefix="/me", tags=["profile"])


def _profile(p: Profile) -> ProfileResponse:
    return ProfileResponse(
        user_id=p.user.id,
        display_name=p.user.display_name,
        friend_code=p.user.friend_code,
        best_score=p.best_score,
        coins=p.coins,
        equipped_block_skin=p.user.equipped_block_skin,
        equipped_theme=p.user.equipped_theme,
        providers=p.providers,
    )


@router.get("")
async def get_profile(user_id: UserIdDep, profiles: ProfileServiceDep) -> ProfileResponse:
    return _profile(await profiles.get(user_id))


@router.patch("")
async def rename(
    body: RenameRequest, user_id: UserIdDep, profiles: ProfileServiceDep
) -> ProfileResponse:
    return _profile(await profiles.rename(user_id, body.display_name))


@router.delete("", status_code=status.HTTP_204_NO_CONTENT)
async def delete_account(user_id: UserIdDep, profiles: ProfileServiceDep) -> None:
    await profiles.delete_account(user_id)
