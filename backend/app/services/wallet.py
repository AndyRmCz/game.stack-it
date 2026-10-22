import uuid
from dataclasses import dataclass

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.errors import Conflict, NotFound
from app.models import Wallet, WalletTransaction
from app.repositories.economy import WalletRepository


@dataclass(frozen=True, slots=True)
class LedgerEntry:
    amount: int
    balance: int
    replayed: bool


class WalletLedger:
    """Balance changes. Must run inside a transaction owned by the calling service.

    Each change locks the wallet row (SELECT ... FOR UPDATE), writes an append-only ledger row
    and is idempotent per (user, key): a repeated key returns the original entry unchanged.
    """

    def __init__(self, wallets: WalletRepository) -> None:
        self._wallets = wallets

    async def lock(self, user_id: uuid.UUID) -> Wallet:
        wallet = await self._wallets.lock(user_id)
        if wallet is None:
            raise NotFound("wallet_not_found")
        return wallet

    async def find(self, user_id: uuid.UUID, key: str) -> WalletTransaction | None:
        return await self._wallets.find_transaction(user_id, key)

    async def apply(
        self, user_id: uuid.UUID, amount: int, *, kind: str, key: str, reference: str | None = None
    ) -> LedgerEntry:
        wallet = await self.lock(user_id)
        existing = await self._wallets.find_transaction(user_id, key)
        if existing is not None:
            return LedgerEntry(existing.amount, wallet.balance, replayed=True)
        new_balance = wallet.balance + amount
        if new_balance < 0:
            raise Conflict("insufficient_funds")
        wallet.balance = new_balance
        self._wallets.add_transaction(
            WalletTransaction(
                user_id=user_id,
                amount=amount,
                balance_after=new_balance,
                kind=kind,
                reference=reference,
                idempotency_key=key,
            )
        )
        return LedgerEntry(amount, new_balance, replayed=False)


@dataclass(frozen=True, slots=True)
class WalletView:
    balance: int
    transactions: list[WalletTransaction]


class WalletService:
    def __init__(self, session: AsyncSession, wallets: WalletRepository) -> None:
        self._session = session
        self._wallets = wallets

    async def get(self, user_id: uuid.UUID, limit: int = 20) -> WalletView:
        async with self._session.begin():
            wallet = await self._wallets.get(user_id)
            if wallet is None:
                raise NotFound("wallet_not_found")
            txs = await self._wallets.recent_transactions(user_id, limit)
            return WalletView(wallet.balance, txs)
