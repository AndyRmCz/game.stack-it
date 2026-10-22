# Stack IT — Build Plan

Executed top to bottom. Each milestone ends with `ruff` + `pytest` green (backend) and its
checkboxes ticked. Choices not fixed by the brief are logged in [DECISIONS.md](DECISIONS.md).

Legend: `[x]` done and verified by running it · `[~]` done but only compile-checked or reviewed
(needs a Unity editor license or a device to verify — see "Verification status" at the bottom).

## M1 — Repo scaffolding

- [x] Monorepo layout: `/backend`, `/unity`, `/shared`, `/docs`
- [x] `.gitignore` (Python + Unity + signing material), `.gitattributes`, `.editorconfig`
- [x] Backend project with `uv` (Python 3.12), FastAPI, SQLAlchemy async, Alembic, ruff, pytest
- [x] Layered package skeleton: `api/ services/ repositories/ clients/ domain/ core/ web/ models/`
- [x] `core/config.py` (pydantic-settings, single `PUBLIC_BASE_URL`, Railway URL normalization)
- [x] `core/db.py` session dependency, `core/logging.py` JSON logs
- [x] `/health` with DB check (503 when DB unreachable)
- [x] Async Alembic env reading `DATABASE_URL`; naming convention on metadata
- [x] Test harness: real Postgres, schema built via migrations, per-test rollback, `_test` DB guard
- [x] `Dockerfile`, `docker-compose.yml` (Postgres + API, hot reload), `.env.example`
- [x] GitHub Actions: ruff, ruff format, pytest, `alembic check` against a Postgres service
- [x] `docs/PLAN.md`, `docs/DECISIONS.md`

## M2 — Deterministic simulation + golden vectors

- [x] Spec in `shared/SIMULATION.md` (units, PCG32, constants, rules, error codes, vector format)
- [x] Python PCG32 matching the pcg-c-basic reference output
- [x] Python simulation `app/domain/sim` (tests first: slicing, perfect, combo cap, regrow, bounce,
      miss, continue, invalid inputs, replay)
- [x] Generator `backend/scripts/generate_sim_vectors.py` → 14 vectors in `shared/sim-vectors`
      (incl. 170-block run past the speed cap, continue, extreme seeds, 6 error cases)
- [x] Python test replays every vector
- [x] C# PCG32 + simulation in `unity/Assets/_Project/Scripts/Core` (asmdef `noEngineReferences`)
- [x] Shared NUnit tests (EditMode folder) replay the same vectors — run with `dotnet test`
- [x] `unity/DotNetTests`: netstandard2.1 / C# 9 build of Core+Client + test runner; CI workflow
      `client-logic.yml`
- [~] Same tests inside Unity's EditMode runner (identical files; needs a licensed editor)

## M3 — Auth + runs with replay verification

- [x] Models + single initial migration for the whole schema (users, identities, refresh tokens,
      runs, friendships, challenges, attempts, wallets, ledger, skins, user_skins, streaks)
- [x] JWT access tokens + opaque rotating refresh tokens with reuse detection (family revoke)
- [x] Guest auth bound to device ID + device secret; Google/Apple login and linking
- [x] `clients/identity.py`: OIDC verification (JWKS cache + rotation, iss/aud/exp, nonce)
- [x] Profile (get / rename) endpoint
- [x] Start run (server seed) / submit run (replay, claimed-score check, wall-clock plausibility,
      superhuman-precision flag); idempotent resubmission for the offline queue
- [x] Continue: one per run, rejected in challenge runs
- [x] Rate limiting (auth, run start, run submit); request-ID JSON access logs
- [x] Tests: identity verifier, rate limiter, anti-cheat, API flows (auth, runs)

## M4 — Challenges, friends, leaderboards

- [x] Create challenge from a verified run (idempotent), get challenge, expiry
- [x] Challenge run: same seed, no continues, auto-friendship, attempt result (won/lost)
- [x] Friends: list (with best score + its height for the rival ghost) / add by code / remove
- [x] Leaderboards: friends all-time + weekly (Monday 00:00 UTC), verified runs only, partial indexes
- [x] Tests: duel end to end, own/expired challenge, symmetry, week boundary, flagged runs excluded

## M5 — Wallet, shop, skins, daily streak, account deletion

- [x] Wallet with `CHECK (balance >= 0)`, `SELECT … FOR UPDATE`, append-only ledger (trigger),
      unique `(user_id, idempotency_key)`
- [x] Run coins, rewarded "double coins" (once per run), shop purchase (Idempotency-Key), equip
- [x] Daily streak with escalating rewards (25 → 200, day 7 holds), UTC days
- [x] Account deletion with cascades; tokens of deleted users stop working immediately
- [x] Tests: idempotent purchase retry, rules, concurrent claims / purchases / same-key credits on
      real separate connections, DB-level constraints

## M6 — Web + deployment

- [x] `GET /c/{code}` landing page (OG tags, en/es, Open in app, store links with Play referrer)
- [x] `/.well-known/apple-app-site-association`, `/.well-known/assetlinks.json` from env vars
- [x] `/privacy`, `/terms` (en/es), `/app-ads.txt`, generated OG image
- [x] `backend/railway.json` (Dockerfile, `alembic upgrade head` pre-deploy, `/health` check)
- [x] Tests: landing page (preview tags, Spanish, escaping, expired, unknown), well-known files
- [x] End-to-end smoke run against the Docker image (guest → run → challenge → duel → daily)

## M7 — Unity: core game loop + visuals

- [x] Project from the Unity 6 Universal 3D template settings, `com.stackit.game`, asmdefs
- [~] Game loop: fixed 60 Hz ticks driving `StackSim`, recorded input events, interpolated view
- [~] Stack view with physics debris, isometric camera rig, gradient sky shifting with height
- [~] Neon look: emissive blocks, URP bloom / vignette / tonemapping volume built in code
- [~] Hype layer: pitch-rising perfects, combo punch, shake, chromatic pulse, particles, haptics,
      slow-motion + NEW RECORD on game over, rival ghost + "YOU PASSED @name!"
- [x] Localization: `strings.csv` (en/es) → Localization "UI" table importer; completeness test
- [~] Procedural audio (no audio assets)

## M8 — Unity: services + UI

- [~] `ApiClient` (typed, snake_case, JWT renewal, retry + backoff), `AuthService`, `GameApi`
- [x] Pure client logic with tests: retry policy, offline run queue, deep link parsing, install
      referrer, ad frequency policy, rival tracker, manifest generation, CSV
- [~] Deep links (Universal / App Links, custom scheme, Install Referrer, typed code on iOS)
- [~] Screens: home, HUD, game over (continue / results), challenge intro, code entry, friends,
      leaderboard, shop, daily reward, settings (link account, language, delete account)

## M9 — Unity: recorder, share, ads

- [~] Replay buffer: GPU capture → 720p → async readback → native H.264 ring buffer, end card
- [~] iOS: VideoToolbox + AVAssetWriter, share sheet, haptics, Sign in with Apple / Google
- [~] Android: MediaCodec + MediaMuxer, FileProvider share, haptics, Install Referrer,
      Credential Manager Google sign-in, generated manifest (App Links + FileProvider)
- [~] AdMob adapter: UMP consent → ATT → init; rewarded continue / double coins, capped
      interstitials, menu-only banners; Google test IDs in `AdsConfig`

## M10 — Final review

- [x] Full backend test + lint run; C# tests; compile checks
- [x] Docs match code (env vars, endpoints, decisions)
- [x] `SETUP.md`

## Verification status

What has actually been run, and what has not:

| Area | How it was verified |
|---|---|
| Backend (all of it) | 115 pytest tests on real PostgreSQL, ruff, `alembic check`, Docker smoke run |
| Simulation parity | Python and C# both replay the 14 golden vectors (`pytest`, `dotnet test`) |
| Client logic (StackIt.Client) | 64 NUnit tests via `dotnet test` |
| Unity runtime scripts | Compiled with `dotnet build unity/DotNetTests/CompileCheck/Game` against the editor's UnityEngine assemblies and the real uGUI, TextMeshPro and Input System sources |
| Unity editor scripts | Compiled against `UnityEditor.dll` (except `StringsImporter.cs`) |
| URP / Localization integration files | API names checked against the package sources; not compiled (packages too heavy to build outside Unity) |
| AdMob adapter, iOS `.mm`, Android `.java` | Not compiled: the Ads plugin, Xcode and Android build modules are not installed here |
| Gameplay feel, visuals, native recording/sharing | Not run: needs a licensed Unity editor and devices |
