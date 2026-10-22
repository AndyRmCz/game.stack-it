import asyncio
import logging
from typing import Literal

from fastapi import APIRouter, Response, status
from pydantic import BaseModel
from sqlalchemy import text
from sqlalchemy.exc import SQLAlchemyError

from app.core.db import SessionDep

logger = logging.getLogger(__name__)
router = APIRouter(tags=["health"])

DB_CHECK_TIMEOUT_SECONDS = 3


class HealthResponse(BaseModel):
    status: Literal["ok", "degraded"]
    database: Literal["ok", "unreachable"]


@router.get("/health")
async def health(session: SessionDep, response: Response) -> HealthResponse:
    # Railway's deploy healthcheck hits this; a 503 keeps a broken release from going live.
    try:
        async with asyncio.timeout(DB_CHECK_TIMEOUT_SECONDS):
            await session.execute(text("SELECT 1"))
    except (SQLAlchemyError, OSError, TimeoutError):
        logger.warning("health check: database unreachable", exc_info=True)
        response.status_code = status.HTTP_503_SERVICE_UNAVAILABLE
        return HealthResponse(status="degraded", database="unreachable")
    return HealthResponse(status="ok", database="ok")
