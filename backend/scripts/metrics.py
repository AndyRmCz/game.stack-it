"""Creates the metric views and prints a report (docs/PLAN.md, "Measurement findings").

    uv run python -m scripts.metrics apply              # create / refresh the views
    uv run python -m scripts.metrics report [--days 14] # print the last days

Uses DATABASE_URL like the API. Against Railway: `railway run uv run python -m scripts.metrics ...`
"""

import argparse
import asyncio
from datetime import UTC, datetime, timedelta
from pathlib import Path

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine

from app.core.config import get_settings

SQL_FILE = Path(__file__).with_name("metrics.sql")

# Report sections: newest first, limited to the last N days (except the all-time ones).
REPORTS: dict[str, str] = {
    "dau": "SELECT * FROM metrics.dau WHERE day >= :since ORDER BY day DESC",
    "retention": "SELECT * FROM metrics.retention WHERE cohort_day >= :since "
    "ORDER BY cohort_day DESC",
    "session_daily": "SELECT * FROM metrics.session_daily WHERE day >= :since ORDER BY day DESC",
    "runs_daily": "SELECT * FROM metrics.runs_daily WHERE day >= :since ORDER BY day DESC",
    "ads_daily": "SELECT * FROM metrics.ads_daily WHERE day >= :since ORDER BY day DESC, format",
    "offers_daily": "SELECT * FROM metrics.offers_daily WHERE day >= :since "
    "ORDER BY day DESC, placement",
    "revenue_daily": "SELECT * FROM metrics.revenue_daily WHERE day >= :since ORDER BY day DESC",
    "economy_daily": "SELECT * FROM metrics.economy_daily WHERE day >= :since "
    "ORDER BY day DESC, kind",
    "quit_points": "SELECT * FROM metrics.quit_points ORDER BY sessions DESC",
    "item_sales": "SELECT * FROM metrics.item_sales ORDER BY price",
}


def statements() -> list[str]:
    """The SQL file split into statements (each ends with a semicolon at the end of a line)."""
    lines = [
        line
        for line in SQL_FILE.read_text(encoding="utf-8").splitlines()
        if not line.lstrip().startswith("--")
    ]
    return [s.strip() for s in "\n".join(lines).split(";\n") if s.strip().rstrip(";")]


async def apply_views(session: AsyncSession) -> None:
    for statement in statements():
        await session.execute(text(statement.rstrip(";")))


async def report(session: AsyncSession, days: int) -> dict[str, list[dict]]:
    since = (datetime.now(UTC) - timedelta(days=days)).date()
    sections = {}
    for name, sql in REPORTS.items():
        rows = await session.execute(text(sql), {"since": since})
        sections[name] = [dict(r._mapping) for r in rows]
    return sections


def _print(sections: dict[str, list[dict]]) -> None:
    for name, rows in sections.items():
        print(f"\n== {name} ({len(rows)} rows)")
        if not rows:
            continue
        columns = list(rows[0])
        widths = [max(len(c), *(len(str(r[c])) for r in rows)) for c in columns]
        print("  ".join(c.ljust(w) for c, w in zip(columns, widths, strict=True)))
        for row in rows:
            print("  ".join(str(row[c]).ljust(w) for c, w in zip(columns, widths, strict=True)))


async def _main(command: str, days: int) -> None:
    engine = create_async_engine(get_settings().database_url)
    try:
        async with AsyncSession(engine) as session, session.begin():
            if command == "apply":
                await apply_views(session)
                print(f"Applied {len(statements())} statements from {SQL_FILE.name}.")
            else:
                _print(await report(session, days))
    finally:
        await engine.dispose()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("command", choices=["apply", "report"])
    parser.add_argument("--days", type=int, default=14)
    args = parser.parse_args()
    asyncio.run(_main(args.command, args.days))


if __name__ == "__main__":
    main()
