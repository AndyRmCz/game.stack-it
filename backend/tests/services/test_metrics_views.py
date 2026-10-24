"""The metric views (scripts/metrics.sql) compute the definitions in docs/PLAN.md, section I."""

import uuid
from datetime import UTC, datetime, timedelta

import pytest
from httpx import AsyncClient
from sqlalchemy import text, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.models import ClientEvent, User
from scripts.metrics import REPORTS, apply_views, report
from tests.factories import Player, guest, verified_run

DAY0 = datetime(2026, 1, 10, 9, 0, tzinfo=UTC)


@pytest.fixture(autouse=True)
async def views(db_session: AsyncSession) -> None:
    async with db_session.begin():
        await apply_views(db_session)


async def _created_on(db: AsyncSession, player: Player, when: datetime) -> uuid.UUID:
    user_id = uuid.UUID(player.user_id)
    async with db.begin():
        await db.execute(update(User).where(User.id == user_id).values(created_at=when))
    return user_id


async def _events(
    db: AsyncSession, user_id: uuid.UUID, session_id: uuid.UUID, at: datetime, *events
) -> None:
    async with db.begin():
        for i, (name, props) in enumerate(events):
            when = at + timedelta(seconds=i)
            db.add(
                ClientEvent(
                    id=uuid.uuid4(),
                    user_id=user_id,
                    session_id=session_id,
                    name=name,
                    props=props,
                    app_version="0.1.0",
                    platform="android",
                    client_at=when,
                    occurred_at=when,
                    received_at=when,
                )
            )


async def _rows(db: AsyncSession, sql: str, **params) -> list[dict]:
    async with db.begin():
        return [dict(r._mapping) for r in await db.execute(text(sql), params)]


async def test_retention_counts_players_active_on_day_n_of_their_cohort(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    returning = await _created_on(db_session, await guest(client), DAY0)
    await _created_on(db_session, await guest(client), DAY0)
    await _events(
        db_session, returning, uuid.uuid4(), DAY0 + timedelta(days=1), ("session_start", {})
    )

    [row] = await _rows(
        db_session, "SELECT * FROM metrics.retention WHERE cohort_day = :d", d=DAY0.date()
    )

    assert row["installs"] == 2
    assert row["d1_users"] == 1
    assert float(row["d1_pct"]) == 50.0
    assert row["d7_users"] == 0


async def test_one_session_gives_length_runs_ads_opt_in_revenue_and_quit_point(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    user_id = await _created_on(db_session, await guest(client), DAY0)
    session = uuid.uuid4()
    await _events(
        db_session,
        user_id,
        session,
        DAY0,
        ("session_start", {"session_index": 1}),
        ("screen_view", {"screen": "HomeScreen"}),
        ("run_start", {}),
        ("offer_shown", {"placement": "continue"}),
        ("offer_accepted", {"placement": "continue"}),
        ("ad_shown", {"format": "rewarded", "placement": "continue"}),
        ("ad_rewarded", {"format": "rewarded", "placement": "continue"}),
        ("ad_paid", {"format": "rewarded", "value_micros": 12000, "currency": "USD"}),
        ("run_start", {}),
        ("offer_shown", {"placement": "continue"}),
        ("offer_declined", {"placement": "continue"}),
        ("ad_shown", {"format": "interstitial", "placement": "between_runs"}),
        ("screen_view", {"screen": "ShopScreen"}),
        ("session_end", {"duration_s": 300, "runs": 2, "ads": 2}),
    )
    day = DAY0.date()

    [daily] = await _rows(db_session, "SELECT * FROM metrics.session_daily WHERE day = :d", d=day)
    [offer] = await _rows(db_session, "SELECT * FROM metrics.offers_daily WHERE day = :d", d=day)
    [money] = await _rows(db_session, "SELECT * FROM metrics.revenue_daily WHERE day = :d", d=day)
    quits = await _rows(db_session, "SELECT * FROM metrics.quit_points")

    assert daily["sessions"] == 1
    assert float(daily["median_session_s"]) == 300
    assert float(daily["runs_per_session"]) == 2
    assert float(daily["interstitials_per_session"]) == 1
    assert (offer["placement"], offer["shown"], offer["accepted"]) == ("continue", 2, 1)
    assert float(offer["opt_in_pct"]) == 50.0
    assert float(offer["completion_pct"]) == 100.0
    assert money["currency"] == "USD"
    assert float(money["revenue"]) == pytest.approx(0.012)
    assert float(money["arpdau"]) == pytest.approx(0.012)
    assert {q["screen"]: q["sessions"] for q in quits} == {"ShopScreen": 1}


async def test_runs_and_coins_come_from_the_server_tables(
    client: AsyncClient, db_session: AsyncSession
) -> None:
    player = await guest(client)
    run = await verified_run(client, db_session, player)
    today = datetime.now(UTC).date()

    [runs] = await _rows(db_session, "SELECT * FROM metrics.runs_daily WHERE day = :d", d=today)
    coins = await _rows(
        db_session,
        "SELECT * FROM metrics.economy_daily WHERE day = :d AND kind = 'run_reward'",
        d=today,
    )

    assert runs["verified"] == 1
    assert runs["median_score"] == run["score"]
    assert coins[0]["coins"] == run["coins_earned"]


async def test_the_report_runs_every_section(client: AsyncClient, db_session: AsyncSession) -> None:
    await verified_run(client, db_session, await guest(client))

    async with db_session.begin():
        sections = await report(db_session, days=14)

    assert set(sections) == set(REPORTS)
    assert sections["runs_daily"]
