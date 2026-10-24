from fastapi import APIRouter, Depends

from app.api.deps import TelemetryServiceDep, UserIdDep
from app.api.schemas import TelemetryBatchRequest, TelemetryBatchResponse
from app.core.rate_limit import rate_limit
from app.services.telemetry import IncomingEvent

router = APIRouter(prefix="/events", tags=["telemetry"])


@router.post("", dependencies=[Depends(rate_limit("events", 30))])
async def post_events(
    body: TelemetryBatchRequest, user_id: UserIdDep, telemetry: TelemetryServiceDep
) -> TelemetryBatchResponse:
    result = await telemetry.ingest(
        user_id,
        body.app_version,
        body.platform,
        [IncomingEvent(e.id, e.name, e.session_id, e.at, e.props) for e in body.events],
    )
    return TelemetryBatchResponse(accepted=result.accepted, rejected=result.rejected)
