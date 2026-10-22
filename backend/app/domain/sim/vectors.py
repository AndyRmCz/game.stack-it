"""JSON shape of golden simulation vectors (shared/sim-vectors). Mirrored by the C# tests."""

from typing import Any

from app.domain.sim.engine import InputEvent, InvalidRun, RunResult, Step, replay


def step_to_dict(step: Step) -> dict[str, Any]:
    out: dict[str, Any] = {"kind": step.kind, "n": step.n, "t": step.t}
    if step.kind in ("place", "miss"):
        out |= {"axis": step.axis, "offset": step.offset}
    if step.kind == "place" and step.block is not None:
        out |= {
            "x0": step.block.x0,
            "x1": step.block.x1,
            "z0": step.block.z0,
            "z1": step.block.z1,
            "perfect": step.perfect,
            "combo": step.combo,
            "gained": step.gained,
        }
    out["score"] = step.score
    return out


def result_to_dict(result: RunResult) -> dict[str, Any]:
    return {
        "error": None,
        "steps": [step_to_dict(s) for s in result.steps],
        "final": {
            "score": result.score,
            "placed": result.placed,
            "perfects": result.perfects,
            "max_combo": result.max_combo,
            "continues_used": result.continues_used,
            "ended": result.ended,
            "tick": result.tick,
        },
    }


def expected_for(seed: int, events: list[InputEvent], continues_allowed: bool) -> dict[str, Any]:
    try:
        return result_to_dict(replay(seed, events, continues_allowed))
    except InvalidRun as exc:
        return {"error": exc.code}
