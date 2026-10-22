# Stack IT

Neon stack-and-snap mobile game (Unity 6, iOS + Android) with a FastAPI + PostgreSQL backend.
Every run is re-simulated on the server from its seed and tap timings, so scores can't be forged.
Friends duel on the exact same block sequence through deep links and auto-recorded clips.

| Path | Contents |
|---|---|
| `backend/` | FastAPI API (`api → services → repositories → clients`, pure `domain/`), Alembic, tests, Railway config |
| `unity/` | Unity project. Pure C# simulation and client logic in `Assets/_Project/Scripts/{Core,Client}`; game, services and UI in `Scripts/`; native plugins in `Assets/Plugins` |
| `unity/DotNetTests/` | Runs the shared C# tests (incl. simulation vectors) with `dotnet test`; compile checks against the Unity assemblies |
| `shared/` | [Simulation spec](shared/SIMULATION.md) and golden vectors shared by Python and C# |
| `docs/` | [Plan + verification status](docs/PLAN.md), [decisions](docs/DECISIONS.md) |

**Start here: [SETUP.md](SETUP.md)** (local dev, Railway, sign-in, AdMob, deep links, builds, stores).

## Quick commands

```sh
docker compose up -d db
cd backend && uv sync && uv run alembic upgrade head
uv run uvicorn app.main:app --reload           # http://localhost:8000/health
uv run pytest && uv run ruff check .           # backend tests (real Postgres) + lint
dotnet test ../unity/DotNetTests/Tests/StackIt.Tests.csproj   # C# simulation + client logic
```

## API (v1)

| Area | Endpoints |
|---|---|
| Auth | `POST /v1/auth/guest`, `POST /v1/auth/{google,apple}/login`, `POST /v1/auth/{google,apple}/link`, `POST /v1/auth/refresh`, `POST /v1/auth/logout` |
| Profile | `GET/PATCH/DELETE /v1/me` |
| Runs | `POST /v1/runs` (seed), `POST /v1/runs/{id}/submit` (replay-verified), `POST /v1/runs/{id}/double-coins` |
| Social | `POST /v1/challenges`, `GET /v1/challenges/{code}`, `GET/POST /v1/friends`, `DELETE /v1/friends/{id}`, `GET /v1/leaderboards/friends?period=all_time|weekly` |
| Economy | `GET /v1/wallet`, `GET /v1/shop`, `POST /v1/shop/purchase`, `POST /v1/skins/equip`, `GET /v1/daily`, `POST /v1/daily/claim` |
| Web | `/c/{code}`, `/.well-known/apple-app-site-association`, `/.well-known/assetlinks.json`, `/privacy`, `/terms`, `/delete-account`, `/app-ads.txt`, `/health` |

Interactive docs at `/docs` while running locally.
