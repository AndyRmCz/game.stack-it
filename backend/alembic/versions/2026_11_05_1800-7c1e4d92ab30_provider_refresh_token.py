"""provider refresh token

Revision ID: 7c1e4d92ab30
Revises: 2915b45923e3
Create Date: 2026-11-05 18:00:00.000000

"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "7c1e4d92ab30"
down_revision: str | Sequence[str] | None = "2915b45923e3"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    """Upgrade schema."""
    op.add_column(
        "auth_identities",
        sa.Column("provider_refresh_token", sa.String(length=1024), nullable=True),
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_column("auth_identities", "provider_refresh_token")
