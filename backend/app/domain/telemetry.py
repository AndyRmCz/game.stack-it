"""Product telemetry rules: which events exist, what they may carry, and when they happened.

The event list is `shared/telemetry/events.json` (copied next to this module because the Docker
build context is `backend/`). Props are flat scalars: anything else, an unknown event or an
undeclared prop rejects the event, so client bugs show up instead of polluting the data.
"""

import json
import math
from datetime import UTC, datetime, timedelta
from pathlib import Path

_EVENTS_FILE = Path(__file__).with_name("telemetry_events.json")
EVENTS: dict[str, frozenset[str]] = {
    name: frozenset(keys)
    for name, keys in json.loads(_EVENTS_FILE.read_text(encoding="utf-8"))["events"].items()
}

MAX_BATCH = 100
MAX_PROPS = 12
MAX_PROP_STRING = 64
MAX_PROP_NUMBER = 10**12
# Events queued offline arrive late; older (or future) client clocks are not trusted.
TRUSTED_PAST = timedelta(days=7)
TRUSTED_FUTURE = timedelta(minutes=5)

PropValue = str | int | float | bool | None


def _valid_value(value: object) -> bool:
    if value is None or isinstance(value, bool):
        return True
    if isinstance(value, str):
        return len(value) <= MAX_PROP_STRING
    if isinstance(value, int):
        return abs(value) <= MAX_PROP_NUMBER
    if isinstance(value, float):
        return math.isfinite(value) and abs(value) <= MAX_PROP_NUMBER
    return False


def clean_event(name: str, props: dict[str, object]) -> dict[str, PropValue] | None:
    """The props to store, or None when the event must be rejected."""
    allowed = EVENTS.get(name)
    if allowed is None or len(props) > MAX_PROPS:
        return None
    if any(key not in allowed or not _valid_value(value) for key, value in props.items()):
        return None
    return dict(props)  # type: ignore[arg-type]


def occurred_at(client_at: datetime, received_at: datetime) -> datetime:
    """The client's timestamp when it is plausible, else the time the server received it."""
    if client_at.tzinfo is None:
        client_at = client_at.replace(tzinfo=UTC)
    if received_at - TRUSTED_PAST <= client_at <= received_at + TRUSTED_FUTURE:
        return client_at
    return received_at
