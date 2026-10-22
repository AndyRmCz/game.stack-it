"""Races on real, independently committing connections: the guarantees that matter for money."""

import asyncio
import uuid

import pytest
from sqlalchemy import func, select, text
from sqlalchemy.exc import DBAPIError
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from app.core.errors import Conflict
from app.domain.economy import DAILY_REWARDS
from app.models import User, Wallet, WalletTransaction
from app.repositories.economy import SkinRepository, StreakRepository, WalletRepository
from app.repositories.users import UserRepository
from app.services.shop import DailyRewardService, ShopService
from app.services.wallet import WalletLedger

Sessions = async_sessionmaker[AsyncSession]


async def _user(sessions: Sessions, balance: int) -> uuid.UUID:
    async with sessions() as s, s.begin():
        user = User(display_name="Racer", friend_code=uuid.uuid4().hex[:8].upper())
        s.add(user)
        await s.flush()
        s.add(Wallet(user_id=user.id, balance=balance))
        return user.id


async def _balance(sessions: Sessions, user_id: uuid.UUID) -> tuple[int, int]:
    async with sessions() as s:
        balance = await s.scalar(select(Wallet.balance).where(Wallet.user_id == user_id))
        ledger = await s.scalar(select(func.count()).where(WalletTransaction.user_id == user_id))
        return balance, ledger


async def _gather_outcomes(*calls) -> list[str]:
    results = await asyncio.gather(*calls, return_exceptions=True)
    outcomes = []
    for r in results:
        if isinstance(r, Conflict):
            outcomes.append(r.code)
        elif isinstance(r, Exception):
            raise r
        else:
            outcomes.append("ok")
    return sorted(outcomes)


async def test_concurrent_daily_claims_pay_once(committed: Sessions) -> None:
    user_id = await _user(committed, 0)

    async def claim(key: str):
        async with committed() as s:
            service = DailyRewardService(s, StreakRepository(s), WalletLedger(WalletRepository(s)))
            return await service.claim(user_id, key)

    outcomes = await _gather_outcomes(*(claim(f"device-{i}") for i in range(5)))

    assert outcomes == ["already_claimed"] * 4 + ["ok"]
    assert await _balance(committed, user_id) == (DAILY_REWARDS[0], 1)


async def test_concurrent_purchases_never_overdraw(committed: Sessions) -> None:
    user_id = await _user(committed, 1000)  # enough for one of these, not both

    async def buy(skin: str):
        async with committed() as s:
            ledger = WalletLedger(WalletRepository(s))
            service = ShopService(
                s, SkinRepository(s), UserRepository(s), WalletRepository(s), ledger
            )
            return await service.purchase(user_id, skin, f"key-{skin}")

    outcomes = await _gather_outcomes(buy("circuit"), buy("aurora"), buy("matrix"))

    assert outcomes.count("ok") == 1
    balance, ledger_rows = await _balance(committed, user_id)
    assert balance >= 0 and ledger_rows == 1


async def test_same_idempotency_key_in_parallel_applies_once(committed: Sessions) -> None:
    user_id = await _user(committed, 0)

    async def credit():
        async with committed() as s, s.begin():
            return await WalletLedger(WalletRepository(s)).apply(
                user_id, 50, kind="test", key="same-key"
            )

    entries = await asyncio.gather(*(credit() for _ in range(5)))

    assert sorted(e.replayed for e in entries) == [False, True, True, True, True]
    assert await _balance(committed, user_id) == (50, 1)


async def test_database_enforces_non_negative_balance_and_append_only_ledger(
    committed: Sessions,
) -> None:
    user_id = await _user(committed, 10)
    async with committed() as s, s.begin():
        await WalletLedger(WalletRepository(s)).apply(user_id, 5, kind="test", key="k")

    with pytest.raises(DBAPIError):
        async with committed() as s, s.begin():
            await s.execute(
                text("UPDATE wallets SET balance = -1 WHERE user_id = :u"), {"u": user_id}
            )
    with pytest.raises(DBAPIError, match="append-only"):
        async with committed() as s, s.begin():
            await s.execute(text("UPDATE wallet_transactions SET amount = 999"))
