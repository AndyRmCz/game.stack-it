# Import every model module here so `Base.metadata` is complete for Alembic autogenerate.
from app.models.base import Base
from app.models.economy import DailyStreak, Skin, UserSkin, Wallet, WalletTransaction
from app.models.run import Run
from app.models.social import Challenge, ChallengeAttempt, Friendship
from app.models.user import AuthIdentity, RefreshToken, User

__all__ = [
    "AuthIdentity",
    "Base",
    "Challenge",
    "ChallengeAttempt",
    "DailyStreak",
    "Friendship",
    "RefreshToken",
    "Run",
    "Skin",
    "User",
    "UserSkin",
    "Wallet",
    "WalletTransaction",
]
