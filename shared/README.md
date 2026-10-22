# shared/

Language-neutral contracts used by both the backend (Python) and the client (C#).

- `SIMULATION.md` — the rules both implementations follow.
- `sim-vectors/` — golden test vectors for the deterministic simulation: seed + input events →
  expected per-block state and final score. Generated from the Python implementation and replayed
  by both test suites (`backend/tests/domain/test_sim_vectors.py`, `unity/.../SimVectorTests.cs`).
  **Never edit vectors by hand**: run `backend/scripts/generate_sim_vectors.py` and review the diff.
