import pytest

from app.core.config import Settings


@pytest.mark.parametrize(
    "raw",
    [
        "postgresql://u:p@db.railway.internal:5432/railway",
        "postgres://u:p@db.railway.internal:5432/railway",
        "postgresql+asyncpg://u:p@db.railway.internal:5432/railway",
    ],
)
def test_database_url_is_normalized_to_asyncpg(raw: str) -> None:
    # Railway injects plain postgresql:// URLs; without the driver the app fails at startup.
    settings = Settings(database_url=raw)

    assert settings.database_url == "postgresql+asyncpg://u:p@db.railway.internal:5432/railway"


def test_public_base_url_has_no_trailing_slash() -> None:
    # Links are built as f"{public_base_url}/c/{code}"; a trailing slash would produce "//c/".
    settings = Settings(public_base_url="https://stackit.up.railway.app/")

    assert settings.public_base_url == "https://stackit.up.railway.app"
