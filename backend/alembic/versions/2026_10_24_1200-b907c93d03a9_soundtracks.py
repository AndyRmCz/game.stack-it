"""soundtracks: shop items of kind 'music' and the equipped one per user

Revision ID: b907c93d03a9
Revises: 148c2f165bbf
Create Date: 2026-10-24 12:00:00.000000

"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "b907c93d03a9"
down_revision: str | Sequence[str] | None = "148c2f165bbf"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None

# Same ids and order as MusicStyles.Ids in the Unity client. Prices are provisional: to revisit
# with real coin rates (docs/PLAN.md, "Measurement findings" ▸ E).
SOUNDTRACKS = [
    ("music_classic", 0),
    ("music_synthwave", 800),
    ("music_lofi", 600),
    ("music_reggaeton", 1000),
    ("music_kpop", 1000),
    ("music_metal", 1200),
]
FIRST_SORT_ORDER = 12  # after the 7 block skins and 5 themes


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_constraint(op.f("ck_skins_kind_known"), "skins", type_="check")
    op.create_check_constraint(
        op.f("ck_skins_kind_known"), "skins", "kind IN ('block', 'theme', 'music')"
    )
    op.add_column(
        "users",
        sa.Column(
            "equipped_music",
            sa.String(length=32),
            server_default="music_classic",
            nullable=False,
        ),
    )
    skins = sa.table(
        "skins",
        sa.column("id", sa.String),
        sa.column("kind", sa.String),
        sa.column("price", sa.Integer),
        sa.column("sort_order", sa.Integer),
        sa.column("is_default", sa.Boolean),
    )
    op.bulk_insert(
        skins,
        [
            {
                "id": id_,
                "kind": "music",
                "price": price,
                "sort_order": FIRST_SORT_ORDER + i,
                "is_default": price == 0,
            }
            for i, (id_, price) in enumerate(SOUNDTRACKS)
        ],
    )


def downgrade() -> None:
    """Downgrade schema."""
    ids = tuple(id_ for id_, _ in SOUNDTRACKS)
    op.execute(
        sa.text("DELETE FROM user_skins WHERE skin_id IN :ids").bindparams(
            sa.bindparam("ids", ids, expanding=True)
        )
    )
    op.execute(
        sa.text("DELETE FROM skins WHERE id IN :ids").bindparams(
            sa.bindparam("ids", ids, expanding=True)
        )
    )
    op.drop_column("users", "equipped_music")
    op.drop_constraint(op.f("ck_skins_kind_known"), "skins", type_="check")
    op.create_check_constraint(op.f("ck_skins_kind_known"), "skins", "kind IN ('block', 'theme')")
