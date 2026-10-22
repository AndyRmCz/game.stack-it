from fastapi import APIRouter

from app.api.deps import (
    DailyServiceDep,
    IdempotencyKeyDep,
    ShopServiceDep,
    UserIdDep,
    WalletServiceDep,
)
from app.api.schemas import (
    DailyResponse,
    ShopItemBody,
    ShopResponse,
    SkinRequest,
    TransactionBody,
    WalletResponse,
)
from app.services.shop import DailyStatus, ShopView

router = APIRouter(tags=["economy"])


def _shop(view: ShopView) -> ShopResponse:
    return ShopResponse(
        balance=view.balance,
        items=[
            ShopItemBody(
                skin_id=i.skin.id,
                kind=i.skin.kind,
                price=i.skin.price,
                owned=i.owned,
                equipped=i.equipped,
            )
            for i in view.items
        ],
    )


def _daily(s: DailyStatus) -> DailyResponse:
    return DailyResponse(
        streak=s.streak,
        claimed_today=s.claimed_today,
        today=s.today,
        next_reward=s.next_reward,
        claimed_amount=s.claimed_amount,
        balance=s.balance,
    )


@router.get("/wallet")
async def get_wallet(user_id: UserIdDep, wallets: WalletServiceDep) -> WalletResponse:
    view = await wallets.get(user_id)
    return WalletResponse(
        balance=view.balance,
        transactions=[
            TransactionBody(
                amount=t.amount,
                balance_after=t.balance_after,
                kind=t.kind,
                reference=t.reference,
                created_at=t.created_at,
            )
            for t in view.transactions
        ],
    )


@router.get("/shop")
async def get_shop(user_id: UserIdDep, shop: ShopServiceDep) -> ShopResponse:
    return _shop(await shop.catalog(user_id))


@router.post("/shop/purchase")
async def purchase(
    body: SkinRequest, key: IdempotencyKeyDep, user_id: UserIdDep, shop: ShopServiceDep
) -> ShopResponse:
    return _shop(await shop.purchase(user_id, body.skin_id, key))


@router.post("/skins/equip")
async def equip(body: SkinRequest, user_id: UserIdDep, shop: ShopServiceDep) -> ShopResponse:
    return _shop(await shop.equip(user_id, body.skin_id))


@router.get("/daily")
async def daily_status(user_id: UserIdDep, daily: DailyServiceDep) -> DailyResponse:
    return _daily(await daily.status(user_id))


@router.post("/daily/claim")
async def claim_daily(
    key: IdempotencyKeyDep, user_id: UserIdDep, daily: DailyServiceDep
) -> DailyResponse:
    return _daily(await daily.claim(user_id, key))
