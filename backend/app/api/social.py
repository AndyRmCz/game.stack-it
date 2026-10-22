import uuid
from typing import Literal

from fastapi import APIRouter, status

from app.api.deps import ChallengeServiceDep, FriendServiceDep, OptionalUserIdDep, UserIdDep
from app.api.schemas import (
    AddFriendRequest,
    ChallengeResponse,
    CreateChallengeRequest,
    FriendBody,
    LeaderboardEntryBody,
    LeaderboardResponse,
)
from app.services.social import ChallengeView

router = APIRouter(tags=["social"])


def _challenge(view: ChallengeView) -> ChallengeResponse:
    c = view.challenge
    return ChallengeResponse(
        code=c.code,
        url=view.url,
        creator_id=c.creator_id,
        creator_name=view.creator_name,
        score=c.score,
        placed=c.placed,
        expires_at=c.expires_at,
        expired=view.expired,
        is_own=view.is_own,
        my_best=view.my_best,
    )


@router.post("/challenges", status_code=status.HTTP_201_CREATED)
async def create_challenge(
    body: CreateChallengeRequest, user_id: UserIdDep, challenges: ChallengeServiceDep
) -> ChallengeResponse:
    return _challenge(await challenges.create(user_id, body.run_id))


@router.get("/challenges/{code}")
async def get_challenge(
    code: str, user_id: OptionalUserIdDep, challenges: ChallengeServiceDep
) -> ChallengeResponse:
    return _challenge(await challenges.get(code, user_id))


@router.get("/friends")
async def list_friends(user_id: UserIdDep, friends: FriendServiceDep) -> list[FriendBody]:
    rows = await friends.list(user_id)
    return [
        FriendBody(
            user_id=r.user_id,
            display_name=r.display_name,
            best_score=r.best_score,
            best_placed=r.best_placed,
        )
        for r in rows
    ]


@router.post("/friends", status_code=status.HTTP_201_CREATED)
async def add_friend(
    body: AddFriendRequest, user_id: UserIdDep, friends: FriendServiceDep
) -> FriendBody:
    friend = await friends.add_by_code(user_id, body.friend_code)
    return FriendBody(user_id=friend.id, display_name=friend.display_name, best_score=None)


@router.delete("/friends/{friend_id}", status_code=status.HTTP_204_NO_CONTENT)
async def remove_friend(
    friend_id: uuid.UUID, user_id: UserIdDep, friends: FriendServiceDep
) -> None:
    await friends.remove(user_id, friend_id)


@router.get("/leaderboards/friends")
async def friends_leaderboard(
    user_id: UserIdDep,
    friends: FriendServiceDep,
    period: Literal["all_time", "weekly"] = "all_time",
) -> LeaderboardResponse:
    board = await friends.leaderboard(user_id, period)
    return LeaderboardResponse(
        period=period,
        since=board.since,
        entries=[
            LeaderboardEntryBody(
                rank=e.rank,
                user_id=e.row.user_id,
                display_name=e.row.display_name,
                score=e.row.best_score or 0,
                is_me=e.is_me,
            )
            for e in board.entries
        ],
    )
