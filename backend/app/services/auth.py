import logging
import secrets
import uuid
from collections.abc import Mapping
from dataclasses import dataclass
from datetime import timedelta

from sqlalchemy.ext.asyncio import AsyncSession

from app.clients.identity import IdentityTokenError, IdentityVerifier, VerifiedIdentity
from app.core.config import Settings
from app.core.errors import BadRequest, Conflict, ServiceUnavailable, Unauthorized
from app.core.security import (
    constant_time_equals,
    create_access_token,
    new_opaque_token,
    sha256_hex,
)
from app.models import AuthIdentity, RefreshToken, User, Wallet
from app.repositories.economy import WalletRepository
from app.repositories.users import IdentityRepository, RefreshTokenRepository, UserRepository
from app.services.common import Clock, unique_code, utcnow

logger = logging.getLogger(__name__)

SOCIAL_PROVIDERS = ("google", "apple")
FRIEND_CODE_LENGTH = 8


@dataclass(frozen=True, slots=True)
class AuthResult:
    user_id: uuid.UUID
    access_token: str
    refresh_token: str
    expires_in: int
    is_new_user: bool


class AuthService:
    def __init__(
        self,
        session: AsyncSession,
        settings: Settings,
        verifiers: Mapping[str, IdentityVerifier],
        users: UserRepository,
        identities: IdentityRepository,
        tokens: RefreshTokenRepository,
        wallets: WalletRepository,
        clock: Clock = utcnow,
    ) -> None:
        self._session = session
        self._settings = settings
        self._verifiers = verifiers
        self._users = users
        self._identities = identities
        self._tokens = tokens
        self._wallets = wallets
        self._clock = clock

    # --- sign-in flows -------------------------------------------------------------------------

    async def guest(self, device_id: str, device_secret: str) -> AuthResult:
        """Signs in (or creates) the guest account bound to this device.

        The device keeps a random secret in secure storage; knowing the device ID alone is not
        enough to take over the account.
        """
        secret_hash = sha256_hex(device_secret)
        async with self._session.begin():
            identity = await self._identities.get("device", device_id)
            if identity is not None:
                if not constant_time_equals(identity.secret_hash or "", secret_hash):
                    raise Unauthorized("invalid_device_credentials")
                return await self._issue(identity.user_id, is_new=False)
            user = await self._create_user()
            self._identities.add(
                AuthIdentity(
                    user_id=user.id, provider="device", subject=device_id, secret_hash=secret_hash
                )
            )
            return await self._issue(user.id, is_new=True)

    async def login(self, provider: str, id_token: str, nonce: str | None) -> AuthResult:
        """Signs in with Google/Apple; creates a fresh account the first time."""
        verified = await self._verify(provider, id_token, nonce)
        async with self._session.begin():
            identity = await self._identities.get(provider, verified.subject)
            if identity is not None:
                return await self._issue(identity.user_id, is_new=False)
            user = await self._create_user()
            self._identities.add(
                AuthIdentity(
                    user_id=user.id,
                    provider=provider,
                    subject=verified.subject,
                    email=verified.email,
                )
            )
            return await self._issue(user.id, is_new=True)

    async def link(self, user_id: uuid.UUID, provider: str, id_token: str, nonce: str | None):
        """Attaches Google/Apple to the current (usually guest) account to keep its progress."""
        verified = await self._verify(provider, id_token, nonce)
        async with self._session.begin():
            identity = await self._identities.get(provider, verified.subject)
            if identity is not None:
                if identity.user_id == user_id:
                    return
                # The client offers "switch to that account" (login) instead.
                raise Conflict("identity_in_use")
            if await self._identities.has_provider(user_id, provider):
                raise Conflict("provider_already_linked")
            self._identities.add(
                AuthIdentity(
                    user_id=user_id,
                    provider=provider,
                    subject=verified.subject,
                    email=verified.email,
                )
            )

    async def refresh(self, refresh_token: str) -> AuthResult:
        now = self._clock()
        reused_family: uuid.UUID | None = None
        async with self._session.begin():
            row = await self._tokens.get_for_update(sha256_hex(refresh_token))
            if row is None or row.expires_at <= now:
                raise Unauthorized("invalid_refresh_token")
            if row.revoked_at is not None:
                # A rotated token came back: assume theft and kill the whole session family.
                await self._tokens.revoke_family(row.family_id, now)
                reused_family = row.family_id
            else:
                row.revoked_at = now
                return await self._issue(row.user_id, is_new=False, family_id=row.family_id)
        logger.warning("refresh token reuse detected", extra={"family_id": str(reused_family)})
        raise Unauthorized("invalid_refresh_token")

    async def logout(self, refresh_token: str) -> None:
        async with self._session.begin():
            row = await self._tokens.get_for_update(sha256_hex(refresh_token))
            if row is not None:
                await self._tokens.revoke_family(row.family_id, self._clock())

    # --- helpers --------------------------------------------------------------------------------

    async def _verify(self, provider: str, id_token: str, nonce: str | None) -> VerifiedIdentity:
        if provider not in SOCIAL_PROVIDERS:
            raise BadRequest("unknown_provider")
        verifier = self._verifiers.get(provider)
        if verifier is None:
            raise ServiceUnavailable("provider_not_configured")
        try:
            return await verifier.verify(id_token, nonce)
        except IdentityTokenError as exc:
            logger.info("identity token rejected", extra={"provider": provider, "reason": str(exc)})
            raise Unauthorized("invalid_identity_token") from exc

    async def _create_user(self) -> User:
        user = User(
            display_name=f"Player{secrets.randbelow(9000) + 1000}",
            friend_code=await unique_code(FRIEND_CODE_LENGTH, self._users.friend_code_taken),
        )
        self._users.add(user)
        await self._session.flush()
        self._wallets.add(Wallet(user_id=user.id, balance=0))
        return user

    async def _issue(
        self, user_id: uuid.UUID, *, is_new: bool, family_id: uuid.UUID | None = None
    ) -> AuthResult:
        now = self._clock()
        refresh = new_opaque_token()
        self._tokens.add(
            RefreshToken(
                user_id=user_id,
                token_hash=sha256_hex(refresh),
                family_id=family_id or uuid.uuid4(),
                expires_at=now + timedelta(days=self._settings.refresh_token_ttl_days),
            )
        )
        return AuthResult(
            user_id=user_id,
            access_token=create_access_token(user_id, self._settings, now),
            refresh_token=refresh,
            expires_in=self._settings.access_token_ttl_seconds,
            is_new_user=is_new,
        )
