"""Request/response bodies of the v1 API. Mirrored by StackIt.Client.Api DTOs in Unity."""

import uuid
from datetime import date, datetime
from typing import Any, Literal

from pydantic import BaseModel, Field, field_validator

from app.domain.sim import MAX_RUN_TICKS
from app.domain.telemetry import MAX_BATCH, MAX_PROPS


class ErrorBody(BaseModel):
    code: str
    message: str


# --- auth --------------------------------------------------------------------------------------


class GuestAuthRequest(BaseModel):
    device_id: str = Field(min_length=8, max_length=128)
    device_secret: str = Field(min_length=32, max_length=256)


class ProviderTokenRequest(BaseModel):
    id_token: str = Field(min_length=20, max_length=8192)
    nonce: str | None = Field(default=None, max_length=256)


class RefreshRequest(BaseModel):
    refresh_token: str = Field(min_length=20, max_length=256)


class TokenResponse(BaseModel):
    user_id: uuid.UUID
    access_token: str
    refresh_token: str
    expires_in: int
    is_new_user: bool


# --- profile -----------------------------------------------------------------------------------


class ProfileResponse(BaseModel):
    user_id: uuid.UUID
    display_name: str
    friend_code: str
    best_score: int
    coins: int
    equipped_block_skin: str
    equipped_theme: str
    equipped_music: str
    providers: list[str]


class RenameRequest(BaseModel):
    display_name: str = Field(min_length=3, max_length=20)

    @field_validator("display_name")
    @classmethod
    def _clean(cls, value: str) -> str:
        value = " ".join(value.split())
        if len(value) < 3 or not all(ch.isalnum() or ch in " _.-" for ch in value):
            raise ValueError("letters, digits, spaces, _ . - only (3-20 characters)")
        return value


# --- runs --------------------------------------------------------------------------------------


class StartRunRequest(BaseModel):
    challenge_code: str | None = Field(default=None, max_length=10)
    daily: bool = False


class RivalBody(BaseModel):
    user_id: uuid.UUID
    display_name: str
    score: int
    placed: int


class StartRunResponse(BaseModel):
    run_id: uuid.UUID
    seed: int
    sim_version: int
    continues_allowed: bool
    challenge_code: str | None
    rival: RivalBody | None
    daily_number: int | None = None


class RunEvent(BaseModel):
    t: int = Field(ge=0, le=MAX_RUN_TICKS + 1)
    type: Literal["tap", "continue"]


class SubmitRunRequest(BaseModel):
    events: list[RunEvent] = Field(max_length=20_000)
    claimed_score: int = Field(ge=0)


class ChallengeOutcomeBody(BaseModel):
    code: str
    rival: RivalBody
    won: bool


class SubmitRunResponse(BaseModel):
    run_id: uuid.UUID
    status: Literal["verified", "flagged", "rejected"]
    reason: str | None
    score: int | None
    best_score: int
    new_best: bool
    coins_earned: int
    coins_doubled: bool
    balance: int
    challenge: ChallengeOutcomeBody | None


class BalanceResponse(BaseModel):
    balance: int


# --- challenges & friends ----------------------------------------------------------------------


class CreateChallengeRequest(BaseModel):
    run_id: uuid.UUID


class ChallengeResponse(BaseModel):
    code: str
    url: str
    creator_id: uuid.UUID
    creator_name: str
    score: int
    placed: int
    expires_at: datetime
    expired: bool
    is_own: bool
    my_best: int | None


class FriendBody(BaseModel):
    user_id: uuid.UUID
    display_name: str
    best_score: int | None
    best_placed: int | None = None


class AddFriendRequest(BaseModel):
    friend_code: str = Field(min_length=4, max_length=12)


class LeaderboardEntryBody(BaseModel):
    rank: int
    user_id: uuid.UUID
    display_name: str
    score: int
    is_me: bool


class LeaderboardResponse(BaseModel):
    period: Literal["all_time", "weekly", "daily"]
    since: datetime | None
    entries: list[LeaderboardEntryBody]


# --- economy -----------------------------------------------------------------------------------


class TransactionBody(BaseModel):
    amount: int
    balance_after: int
    kind: str
    reference: str | None
    created_at: datetime


class WalletResponse(BaseModel):
    balance: int
    transactions: list[TransactionBody]


class ShopItemBody(BaseModel):
    skin_id: str
    kind: Literal["block", "theme", "music"]
    price: int
    owned: bool
    equipped: bool


class ShopResponse(BaseModel):
    balance: int
    items: list[ShopItemBody]


class SkinRequest(BaseModel):
    skin_id: str = Field(min_length=1, max_length=32)


class DailyResponse(BaseModel):
    streak: int
    claimed_today: bool
    today: date
    next_reward: int
    claimed_amount: int | None
    balance: int | None


# --- telemetry ---------------------------------------------------------------------------------


class TelemetryEventBody(BaseModel):
    id: uuid.UUID
    name: str = Field(min_length=1, max_length=40)
    session_id: uuid.UUID
    at: datetime
    # Values are checked against the event list in app.domain.telemetry.
    props: dict[str, Any] = Field(default_factory=dict, max_length=MAX_PROPS)


class TelemetryBatchRequest(BaseModel):
    app_version: str = Field(min_length=1, max_length=32)
    platform: str = Field(min_length=1, max_length=16)
    events: list[TelemetryEventBody] = Field(min_length=1, max_length=MAX_BATCH)


class TelemetryBatchResponse(BaseModel):
    accepted: int
    rejected: int


# --- missions ----------------------------------------------------------------------------------


class MissionBody(BaseModel):
    slot: int
    kind: str
    target: int
    progress: int
    reward: int
    claimed: bool


class ChestBody(BaseModel):
    reward: int
    ready: bool
    claimed: bool


class MissionsResponse(BaseModel):
    day: date
    missions: list[MissionBody]
    chest: ChestBody
    balance: int | None = None


class ClaimMissionRequest(BaseModel):
    slot: int = Field(ge=0, le=3)
