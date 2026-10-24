"""Dependency wiring: builds services with their repositories for each request."""

import uuid
from typing import Annotated

from fastapi import Depends, Header, Request
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.config import Settings, get_settings
from app.core.db import SessionDep
from app.core.errors import BadRequest, Unauthorized
from app.core.security import decode_access_token
from app.repositories.economy import SkinRepository, StreakRepository, WalletRepository
from app.repositories.runs import RunRepository
from app.repositories.social import ChallengeRepository, FriendRepository
from app.repositories.telemetry import TelemetryRepository
from app.repositories.users import IdentityRepository, RefreshTokenRepository, UserRepository
from app.services.auth import AuthService
from app.services.missions import MissionService
from app.services.profile import ProfileService
from app.services.runs import RunService
from app.services.shop import DailyRewardService, ShopService
from app.services.social import ChallengeService, FriendService
from app.services.telemetry import TelemetryService
from app.services.wallet import WalletLedger, WalletService

SettingsDep = Annotated[Settings, Depends(get_settings)]
_bearer = HTTPBearer(auto_error=False)


async def current_user_id(
    session: SessionDep,
    settings: SettingsDep,
    credentials: Annotated[HTTPAuthorizationCredentials | None, Depends(_bearer)],
) -> uuid.UUID:
    if credentials is None:
        raise Unauthorized("missing_token")
    user_id = decode_access_token(credentials.credentials, settings)
    # Tokens of deleted accounts must stop working immediately, not when they expire.
    async with session.begin():
        if not await UserRepository(session).exists(user_id):
            raise Unauthorized("user_not_found")
    return user_id


async def optional_user_id(
    session: SessionDep,
    settings: SettingsDep,
    credentials: Annotated[HTTPAuthorizationCredentials | None, Depends(_bearer)],
) -> uuid.UUID | None:
    if credentials is None:
        return None
    return await current_user_id(session, settings, credentials)


def idempotency_key(
    key: Annotated[str | None, Header(alias="Idempotency-Key", max_length=64)] = None,
) -> str:
    if not key:
        raise BadRequest("idempotency_key_required")
    return key


UserIdDep = Annotated[uuid.UUID, Depends(current_user_id)]
OptionalUserIdDep = Annotated[uuid.UUID | None, Depends(optional_user_id)]
IdempotencyKeyDep = Annotated[str, Depends(idempotency_key)]


def _ledger(session: SessionDep) -> WalletLedger:
    return WalletLedger(WalletRepository(session))


def auth_service(request: Request, session: SessionDep, settings: SettingsDep) -> AuthService:
    return AuthService(
        session,
        settings,
        request.app.state.identity_verifiers,
        UserRepository(session),
        IdentityRepository(session),
        RefreshTokenRepository(session),
        WalletRepository(session),
    )


def profile_service(session: SessionDep) -> ProfileService:
    return ProfileService(
        session,
        UserRepository(session),
        IdentityRepository(session),
        RunRepository(session),
        WalletRepository(session),
    )


def run_service(session: SessionDep, settings: SettingsDep) -> RunService:
    return RunService(
        session,
        settings,
        RunRepository(session),
        ChallengeRepository(session),
        FriendRepository(session),
        UserRepository(session),
        _ledger(session),
    )


def challenge_service(session: SessionDep, settings: SettingsDep) -> ChallengeService:
    return ChallengeService(
        session,
        settings,
        ChallengeRepository(session),
        RunRepository(session),
        UserRepository(session),
    )


def friend_service(session: SessionDep) -> FriendService:
    return FriendService(session, FriendRepository(session), UserRepository(session))


def wallet_service(session: SessionDep) -> WalletService:
    return WalletService(session, WalletRepository(session))


def shop_service(session: SessionDep) -> ShopService:
    return ShopService(
        session,
        SkinRepository(session),
        UserRepository(session),
        WalletRepository(session),
        _ledger(session),
    )


def daily_service(session: SessionDep) -> DailyRewardService:
    return DailyRewardService(session, StreakRepository(session), _ledger(session))


def mission_service(session: SessionDep) -> MissionService:
    return MissionService(session, RunRepository(session), _ledger(session))


def telemetry_service(session: SessionDep) -> TelemetryService:
    return TelemetryService(session, TelemetryRepository(session))


AuthServiceDep = Annotated[AuthService, Depends(auth_service)]
ProfileServiceDep = Annotated[ProfileService, Depends(profile_service)]
RunServiceDep = Annotated[RunService, Depends(run_service)]
ChallengeServiceDep = Annotated[ChallengeService, Depends(challenge_service)]
FriendServiceDep = Annotated[FriendService, Depends(friend_service)]
WalletServiceDep = Annotated[WalletService, Depends(wallet_service)]
ShopServiceDep = Annotated[ShopService, Depends(shop_service)]
DailyServiceDep = Annotated[DailyRewardService, Depends(daily_service)]
TelemetryServiceDep = Annotated[TelemetryService, Depends(telemetry_service)]
MissionServiceDep = Annotated[MissionService, Depends(mission_service)]
