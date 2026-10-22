from typing import Literal

from fastapi import APIRouter, Depends, status

from app.api.deps import AuthServiceDep, UserIdDep
from app.api.schemas import GuestAuthRequest, ProviderTokenRequest, RefreshRequest, TokenResponse
from app.core.rate_limit import rate_limit
from app.services.auth import AuthResult

router = APIRouter(prefix="/auth", tags=["auth"], dependencies=[Depends(rate_limit("auth", 30))])

Provider = Literal["google", "apple"]


def _tokens(result: AuthResult) -> TokenResponse:
    return TokenResponse(
        user_id=result.user_id,
        access_token=result.access_token,
        refresh_token=result.refresh_token,
        expires_in=result.expires_in,
        is_new_user=result.is_new_user,
    )


@router.post("/guest")
async def guest(body: GuestAuthRequest, auth: AuthServiceDep) -> TokenResponse:
    return _tokens(await auth.guest(body.device_id, body.device_secret))


@router.post("/{provider}/login")
async def provider_login(
    provider: Provider, body: ProviderTokenRequest, auth: AuthServiceDep
) -> TokenResponse:
    return _tokens(await auth.login(provider, body.id_token, body.nonce))


@router.post("/{provider}/link", status_code=status.HTTP_204_NO_CONTENT)
async def provider_link(
    provider: Provider, body: ProviderTokenRequest, user_id: UserIdDep, auth: AuthServiceDep
) -> None:
    await auth.link(user_id, provider, body.id_token, body.nonce)


@router.post("/refresh")
async def refresh(body: RefreshRequest, auth: AuthServiceDep) -> TokenResponse:
    return _tokens(await auth.refresh(body.refresh_token))


@router.post("/logout", status_code=status.HTTP_204_NO_CONTENT)
async def logout(body: RefreshRequest, auth: AuthServiceDep) -> None:
    await auth.logout(body.refresh_token)
