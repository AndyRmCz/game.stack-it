"""Golden vectors shared with the C# client. If these fail, client and server disagree."""

import json
from pathlib import Path

import pytest

from app.domain.sim import SIM_VERSION, InputEvent
from app.domain.sim.vectors import expected_for

VECTOR_DIR = Path(__file__).resolve().parents[3] / "shared" / "sim-vectors"
VECTORS = sorted(VECTOR_DIR.glob("*.json"))


def test_vectors_exist() -> None:
    assert len(VECTORS) >= 10


@pytest.mark.parametrize("path", VECTORS, ids=lambda p: p.stem)
def test_simulation_reproduces_vector(path: Path) -> None:
    vector = json.loads(path.read_text(encoding="utf-8"))
    assert vector["sim_version"] == SIM_VERSION
    events = [InputEvent(e["t"], e["type"]) for e in vector["events"]]

    actual = expected_for(vector["seed"], events, vector["continues_allowed"])

    assert actual == vector["expected"]
