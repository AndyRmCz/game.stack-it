import json
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest

from app.domain.telemetry import (
    EVENTS,
    MAX_PROP_STRING,
    MAX_PROPS,
    clean_event,
    occurred_at,
)

SHARED = Path(__file__).resolve().parents[3] / "shared" / "telemetry" / "events.json"
BACKEND_COPY = Path(__file__).resolve().parents[2] / "app" / "domain" / "telemetry_events.json"
NOW = datetime(2026, 10, 23, 12, 0, tzinfo=UTC)


def test_backend_copy_of_the_event_list_matches_the_shared_file() -> None:
    assert BACKEND_COPY.read_bytes() == SHARED.read_bytes()


def test_every_event_declares_at_most_the_prop_limit() -> None:
    shared = json.loads(SHARED.read_text(encoding="utf-8"))["events"]
    assert set(EVENTS) == set(shared)
    assert all(len(keys) <= MAX_PROPS for keys in EVENTS.values())


def test_known_event_with_declared_props_is_kept() -> None:
    props = {"format": "rewarded", "placement": "continue"}
    assert clean_event("ad_shown", props) == props


def test_unknown_event_is_rejected() -> None:
    assert clean_event("purchase_real_money", {}) is None


def test_undeclared_prop_rejects_the_event() -> None:
    assert clean_event("ad_shown", {"format": "banner", "email": "x@y.z"}) is None


@pytest.mark.parametrize(
    "value",
    ["x" * (MAX_PROP_STRING + 1), [1, 2], {"a": 1}, 10**13, -(10**13), float("nan")],
)
def test_bad_prop_values_reject_the_event(value: object) -> None:
    assert clean_event("screen_view", {"screen": value}) is None


@pytest.mark.parametrize("value", ["Home", 3, 2.5, True, None])
def test_scalar_prop_values_are_accepted(value: object) -> None:
    assert clean_event("screen_view", {"screen": value}) == {"screen": value}


def test_client_time_is_trusted_inside_the_window() -> None:
    queued_offline = NOW - timedelta(days=2)
    assert occurred_at(queued_offline, NOW) == queued_offline


def test_client_time_outside_the_window_falls_back_to_server_time() -> None:
    assert occurred_at(NOW - timedelta(days=30), NOW) == NOW
    assert occurred_at(NOW + timedelta(hours=1), NOW) == NOW


def test_naive_client_time_is_treated_as_utc() -> None:
    naive = (NOW - timedelta(minutes=1)).replace(tzinfo=None)
    assert occurred_at(naive, NOW) == NOW - timedelta(minutes=1)
