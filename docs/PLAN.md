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

## Later versions (ideas, not started)

Ideas for after the first release. Each has a first recommendation and the questions to settle
before building it. **Do 1 first**: without measurements we can't tell whether 2–5 help or
hurt.

**Money rule (2026-10-23):** income comes from **ads only**. There will never be real-money
purchases: no IAP, coin packs, subscriptions or paid "remove ads". Everything in the shop is
bought with earned coins, priced "not too accessible, but not too expensive"
(`DECISIONS.md` ▸ Monetization).

### 1. Measure first (prerequisite)

- **2026-10-23:** research, the economy model and the design, in **"Measurement findings"**
  below.
- **2026-10-24:** first-party telemetry built (findings ▸ J–K), and the unused IAP and
  analytics packages removed.
- **Next:** ship a build, let real players generate 1–2 weeks of data, then set targets (M)
  and revisit the shop prices (E) with real coin rates.

### 2. Viral hooks: research what's trending

Research done 2026-10-24; findings and recommendations in **"Viral hooks findings"** below.
**Build order decided:** H6 + H4 → H1 + H2 → H3 + H5 (findings ▸ V2). Start once all the
research is done.

### 3. More dynamism: surprises and animation

Literal **jump scares** clash with the calm, premium look, and with an audience that includes
13-year-olds. Casual players tend to punish them with uninstalls and low ratings.
**Recommendation:** surprises that are exciting, not frightening. If we still want scares, make
them an opt-in seasonal event (a Halloween look with a mild "boo" block), never the default.

The juice that also makes clip moments now lives in the viral hooks (findings ▸ V2), so it isn't
listed twice:
- **H6** (perfect streak) takes the rising chime, the fanfares and the zoom punch on perfects.
- **H4** (near miss and game over) takes slow motion on near misses, the tower collapse and
  confetti on a new record.

The ideas left here, roughly cheapest first:

- **Height milestones change the world.** The sky already moves from dawn to night as the tower
  grows. What's missing: something new every 25 blocks (clouds, then space), with parallax props
  passing by (birds, planes, satellites). The `SkyBackdrop` and `block.ramp` colour stops are the
  place to hook this in.
- **Fever mode** after N perfects in a row: blocks glow, music jumps a layer, coins double for
  10 seconds. It builds on H6's streak and needs the music to have layers (every soundtrack style
  keeps them, soundtracks plan ▸ S6).
- **Tower events** that change how a run feels:
  - a gust of wind sways the moving block;
  - a "golden block" worth bonus coins;
  - a speed burst;
  - a "giant block" breather.

  Telegraph each one for about a second so it feels fair. It must also stay deterministic: the
  simulation's seed has to drive it, or replay verification breaks (bump `SIM_VERSION` on both
  sides).
- **Character or mascot** reacting at the base of the tower: cheers, nervous at narrow blocks.
  It gives clips a face. ✅ *Built 2026-10-24 as a placeholder:* "Blocky" (`Mascot.cs`).
  - A little bevelled block with eyes, in the look's second accent, drawn with the blocks' own
    shader; no art files.
  - It breathes, hops on perfects (higher on fanfares), shivers on thin blocks, looks up as the
    tower grows, and droops at game over.
  - Still with Reduce Motion.
  - **Not yet seen in Unity:** its spot beside the base may need adjusting.
- *Not built (owner, 2026-10-24):* fever mode and tower events. Both change the simulation the
  server replays (a `SIM_VERSION` bump).

### 4. Music genres in the shop

Goal: buy a soundtrack (metal, K-pop, lo-fi, synthwave, reggaeton…) that replaces the default
music.

**Decision (owner, 2026-10-24):** original soundtracks **built in code, like today's music**,
with no AI-generated or licensed audio, to avoid legal risk. Realistic instruments are
best-effort; a stylized sound is acceptable. Genres approved: synthwave, lo-fi, reggaeton, K-pop
style, metal. Plan in **"Soundtracks plan"** below. Short version:
- Each genre is a music style with its own synthesized instruments and score.
- It keeps today's tempo rise and layers.
- The current music stays the free default.
- Coins only (money rule above).

### 5. Longer sessions, so more ad views feel fair

- Ad views should follow from players choosing to stay, not from more ad slots. Play's
  disruptive-ads policy and AdMob's placement rules penalize surprise or too-frequent
  interstitials. Players also churn when ads outnumber fun.
- **Recommendation:** make "one more run" stronger and give players reasons to *choose*
  rewarded ads.
- **Reasons to keep playing:**
  - **Missions:** 3 rotating daily goals ("10 perfects", "reach 50", "beat a friend"), each
    paying coins. Complete all 3 for a chest. ✅ *Built 2026-10-24:*
    - Three a day per player (easy, medium, hard), from five kinds: runs, perfects, reach N in
      one run, N perfects in a row, play the Daily.
    - Provisional rewards: 20 / 30 / 45 coins, plus a 50-coin chest (145 a day at most).
    - Progress is read from the day's verified runs; each claim is a ledger entry keyed by day
      and slot, so it pays once. No new table.
    - A Missions button on Home's top bar shows a dot when something can be claimed.
    - XP and levels wait for real retention data (owner's choice).
  - **Player level / XP:** every run fills a bar, and levels unlock skins, soundtracks and
    backgrounds. This is the long-term reason to return.
  - **Instant restart:** *already built:* "Play again" on the game-over screen starts the next
    run in one tap, on a seed reserved one run ahead, so it never waits for the network. An
    interstitial can come first only under the existing rule: at least 3 runs and 90 s apart,
    never in the first session, never after a new record.
  - **Weekly tower event:** a special seed everyone plays, with its own leaderboard. It shares
    its machinery with H1, the Daily Tower (viral hooks findings ▸ V2). *Today* there's only the
    weekly friends leaderboard.
  - **Streaks:** *already built:* the daily gift is a 7-day streak calendar paying 25 → 200
    coins, with the big reward on day 7 (`DailyRewardScreen`, backend `daily_streaks`).
- **More rewarded moments the player picks:**
  - double mission rewards;
  - a free chest every few hours;
  - try a locked soundtrack (*built 2026-10-24*) or skin for one run;
  - a second continue at a higher price.
- **Tune with data (item 1):**
  - raise interstitial frequency only if D7 retention holds;
  - track ads per session together with retention, never alone.
- **Success target to set once we have a baseline:** for example, sessions from N to 1.5×N
  runs with D7 retention flat or better.

### Suggested order

1 measure → 3 milestones and fever, and 5 missions / instant restart
→ 2 viral hooks in their build order (H6 + H4 → H1 + H2 → H3 + H5) → 4 soundtracks
(built in code, steps in "Soundtracks plan" ▸ S11). No IAP step: see the money rule.

## Viral hooks findings (2026-10-24)

Research for idea 2. **Sourced** = from a cited source (see Sources at the end of this
section); **assumption** = our reasoning, to be checked with the telemetry. Source quality is
uneven: most "TikTok trends" material is vendor blogs without methodology. Where only blogs
said it, it's flagged as such.

### V1. What the evidence says

| # | Finding | Strength |
|---|---|---|
| 1 | **Short, instantly readable moments win.** Viewers decide in ~3 s whether to keep watching; single-moment clips (one clutch play in 15 s) beat montages; the sweet spot is 15–30 s; strong emotion (excitement, laughter, shock) drives shares. | Many blogs agree; no primary data. The 3-second hook rule also appears in TikTok's own ad guidance, as relayed by an ad-creative guide. |
| 2 | **Ad hooks:** put the game's own visuals and motion in the first frames; no logo intro (logo on the end card); a weak first second can't be rescued later. | Ad-creative guides, consistent with each other. |
| 3 | **Wordle's spread came from its share format.** A spoiler-free grid of colored squares, the same puzzle for everyone each day (so results are comparable), and a share button added after players began posting emoji grids by hand. It went viral once the button shipped. Its creator: a random word per player "wouldn't have caught on". | Journalism (Smithsonian, AOL/NYT coverage) and an academic's comment. Strongest evidence found. |
| 4 | **Satisfying chain reactions and physics spread** (Suika Game: one merge triggers several; the "pop" sound; easy to learn, hard to master). Its developer reported sales up "more than 50,000 times" after it went viral. | Explainers; the sales figure is a developer claim relayed second-hand. |
| 5 | **Failure and rage moments travel.** The most-shared streamer clips are often unflattering moments, and rage reactions became TikTok sound memes. | Kotaku analysis plus meme sites; general, not specific to mobile casual games. |
| 6 | **The biggest casual hit right now is calm.** Block Blast! was the #1 game worldwide by downloads in 2025 and in Q1 2026 (Sensor Tower). Its publisher credits a simple core mechanic, low-pressure pacing and satisfying progression; no source ties it to TikTok. | Sensor Tower via press releases and TechCrunch. Supports our calm, premium look. |
| 7 | **Discovery channels:** in a 2024 US survey (1,009 players), YouTube was the top source for finding new games (64 %), with TikTok second (38 %). TikTok-commissioned studies claim much higher numbers. | The survey is the more neutral number. Clips must work on Shorts and Reels, not only TikTok. |
| 8 | **Game feel ("juice")** such as short screen shake (0.1–0.3 s, eased) and slow motion makes feedback satisfying, but it should echo the core gameplay; juice for its own sake hurts. | "Juice it or lose it" (Jonasson & Purho) and follow-up talks and articles. |
| 9 | **Direct-share SDKs exist.** TikTok Share Kit (Android/iOS) posts a video straight into TikTok's editor, auto-adds the app's hashtag, and has a **green-screen** format (the player films a reaction over the clip); it needs a TikTok developer `client_key`. Instagram "Sharing to Stories" takes a video (H.264, ≤ 20 s, 1080p) plus a sticker layer through an Android intent or iOS URL scheme, and needs a Facebook App ID. | Official TikTok and Meta developer docs. |

**What Stack IT already has (measured, from code):**
- **Clip:** the recorder keeps the **last 15 s** of the run at 720 × 1280 and 30 fps, plus a 2 s end card (logo, big score, "Can you beat me?", challenge code). That's 17 s, which fits Instagram's 20 s limit and TikTok's aspect-ratio range.
- **Hype effects** (`HypeFx`):
  - `Perfect(combo)`: a chime that climbs the pentatonic scale up to two octaves
    (`Feel.PerfectSemitones`), growing rings, a brightness pulse, and stronger haptics from 5.
  - `Missed`: a shake.
  - `RivalPassed`: a burst.
  - `NewRecord`: slow motion.
  - Sliced pieces and the falling block drop with physics; the tower itself stays standing.
- **Duels:** duel links with an install referrer, and friends' ghosts during runs.
- **Missing:** near-miss detection, a text share format, a daily shared seed, and direct share targets.

### V2. Recommendations (ranked: impact ÷ effort)

| | Hook | Why (finding) | Effort | Fit |
|---|---|---|---|---|
| **H1** | **Daily Tower.** One seed per UTC day, the same for everyone. Unlimited attempts, but only the best counts on a daily leaderboard. | Wordle's "same puzzle for everyone" made results comparable and worth sharing (3). The simulation is already deterministic and the server verifies every run, so a shared seed is cheap and cheat-proof. | Low–medium: a server-side seed of the day, a daily board, a Home entry | Excellent |
| **H2** | **Spoiler-free text share** for chats: `Stack IT · Daily #142 · 87` + one emoji row per 10 blocks (🟪 perfect streak / 🟦 clean / 🟧 sliced) + the duel link. | Wordle's grid (3). It works in WhatsApp and SMS, where a 17 s video is heavy, and shows how the run went without spoiling anything. | Low: build the string from the run's steps; a "Copy result" button next to Share | Excellent |
| **H3** | **Clip starts on its best moment.** Choose the clip window around the run's highlight instead of always the last 15 s: the near-miss at the end, a perfect streak, or passing a friend. Overlay a hook caption in the first second ("1 mm from perfect", "Passed Ana!"). | 3-second hook and single-moment clips (1, 2). Fail and near-miss moments travel (5). | Medium: the recorder already buffers; tag frames with events, export a sub-range, draw the caption | Very good |
| **H4** | **Near miss and game over.** When a miss or a slice was razor-thin, play brief slow motion and show "missed by 0.4 mm". At game over the tower itself collapses. A new record adds a confetti burst on top of its existing slow motion. Off with Reduce Motion. *(Absorbs idea 3's slow motion on near misses, collapse and confetti.)* | Emotional peak plus physics satisfaction (1, 4, 8). It gives H3 its best moments. | Low–medium: the simulation knows the offset; `StackView` already gives debris physics bodies | Very good |
| **H5** | **Direct share targets:** TikTok (Share Kit, auto hashtag, green-screen "react to my run"), Instagram Stories (clip plus a sticker with the challenge code), then the system sheet for everything else, including YouTube Shorts. | Fewer steps from "wow" to posted (9); covers YouTube, the top discovery channel (7). | Medium–high: native code on both platforms, TikTok and Facebook app registration, store review notes | Good, after H1–H4 |
| **H6** | **Perfect streak payoff.** Already built: the chime climbs the G pentatonic scale, the rings grow, and a brightness pulse plays. To add: a fanfare and a bigger ring burst at 5, 10 and 20, a small camera zoom punch on each perfect, and a streak counter that pulses. *(Absorbs idea 3's perfect streak and zoom punch.)* | Chain-reaction satisfaction (4) and juice that echoes the core mechanic (8). The music is already in G pentatonic (`MusicPlan`). | Low: half of it exists | Very good |

**Not recommended:**
- Fake or misleading "fail" ads, the hyper-casual UA trope. They hurt ratings and violate store and ad policies.
- Jump scares (idea 3).
- Chasing short-lived meme sounds inside the game. Those belong in creator posts, not in the app.

**Build order (decided 2026-10-24).** Start once the research for all the ideas is done:

1. **H6 + H4:** cheap juice that creates clip-worthy moments. ✅ *Built 2026-10-24:*
   - **H6:** fanfares at 5, 10, 20 and every 10 after, with a wider two-colour ring burst and a
     firm haptic; a camera zoom punch on every perfect. The "Perfect ×N" pill already existed.
   - **H4:** "0.4 mm from perfect" on the HUD banner; "Missed by 0.4 mm" as a toast with a
     beat of slow motion; the top 8 blocks topple when the run really ends (not while Continue
     is on offer); confetti on a new record.
   - **Logic:** `NearMiss` and `Feel.StreakFanfare`, tested. A block reads as 10 cm, so 300 sim
     units are 1 mm. "Almost perfect" means within 1.5 mm of the perfect window; "almost
     saved" means the block cleared the tower by under 5 mm.
   - **Reduce Motion** turns off the punch, the slow motion, the collapse and the confetti.
   - **Not yet seen in Unity or on a phone.**
2. **H1 + H2:** the daily shared challenge and its share format, the best-evidenced loop.
   ✅ *Built 2026-10-24:*
   - **Daily Tower:**
     - one seed per UTC day (an HMAC of the date with the server secret);
     - unlimited attempts, each a normal verified run that pays coins;
     - Daily #1 is 2026-10-24;
     - a Daily tab in Ranks with a "Play Daily #N" button (moved off Home on 2026-10-24: too
       busy), and a Play button on the "Play the Daily Tower" mission;
     - **friends only** (owner's choice), like the other boards.
   - **"Copy result":** the emoji grid (purple perfect, blue clean, orange sliced, ten per row,
     at most 6 rows), plus the duel link when the run is verified.
3. **H3 + H5:** the clip starts on the best moment, and players share it straight to TikTok and
   Instagram. ✅ *Built 2026-10-24:*
   - **H3:** the clip starts 1 s before the run's strongest highlight. Weights: a streak of 5+
     counts 1 per 5, passing a friend 3, almost perfect 1, the hair-thin miss 4. The clip is
     6–15 s long. The HUD's banners for that moment are in the frame, so they act as the
     caption. No native change was needed: the recorder already exported "the last N seconds".
   - **H5:** TikTok and Instagram Stories buttons on the results screen.
     - Android: TikTok opens directly; Stories uses Instagram's official intent.
     - iOS: Stories through its URL scheme; TikTok through the share sheet.
     - Keys are placeholders in AppConfig (SETUP.md 8.12b). TikTok's Share Kit SDK (hashtag,
       green screen) is not integrated.
     - The iOS code isn't compiled (needs Xcode); the Java compiles only in a Unity Android build.
   - **Not yet run in Unity or on a phone.**

### V3. How we'll know it works (uses the new telemetry)

- **Share rate:** `share_tap` per session, by `source` (add `copy_result` for H2, and the
  share target once H5 ships).
- **Viral loop:** challenges created → `challenge_opened` (`via` link / referrer) → new users
  whose first run is a duel → those users' D1 retention. Together these are our K-factor.
- **Daily Tower** (H1): daily players ÷ DAU, attempts per player, and next-day return of
  daily players compared with non-daily players.
- **Hook quality** (H3), outside the app: when we post clips ourselves, watch the platforms'
  3-second view rate and completion rate.
- **Telemetry to add when building:** `share_tap.source` values `copy_result` / `tiktok` /
  `instagram` / `system`, and `run_end.min_offset` (closest miss) for near-miss frequency.
  Both need an update to `shared/telemetry/events.json`.

### V4. Sources

- Smithsonian, why Wordle went viral: https://www.smithsonianmag.com/smart-news/heres-why-the-word-game-wordle-went-viral-180979439/
- CUNY, "The Wordle Effect": https://pressbooks.cuny.edu/inspire/part/the-wordle-effect-how-five-letters-changed-the-internet/
- AOL (NYT), Wordle brought tens of millions of users: https://www.aol.com/news/wordle-brought-tens-millions-users-150000203.html
- Business Wire, Block Blast! #1 downloads Q1 2026: https://www.businesswire.com/news/home/20260423998721/en/Block-Blast-Ends-Q1-2026-as-the-No.-1-Most-Downloaded-Mobile-Game-Worldwide
- TechCrunch, Block Blast! on Apple Arcade (2025's most downloaded game): https://techcrunch.com/2026/08/11/2025s-most-downloaded-game-block-blast-is-going-ad-free-on-apple-arcade/
- Game World Observer, Sensor Tower Q2 2025: https://gameworldobserver.com/2025/08/13/sensor-tower-in-the-second-quarter-of-2025-apps-surpassed-mobile-games-in-iap-revenue-for-the-first-time
- AFK Gaming, Suika Game: https://afkgaming.com/gaming/general/what-is-the-suika-game-why-has-it-garnered-momentum
- Kotaku, Twitch's most-watched clips: https://kotaku.com/twitchs-most-watched-clips-have-almost-nothing-to-do-wi-1842038158
- GameDev Reports, how people discover games (2024 survey): https://gamedevreports.substack.com/p/big-games-machine-how-people-discover
- TikTok for Business, Newzoo launch study: https://ads.tiktok.com/business/en-US/blog/journey-of-a-launch-newzoo
- TikTok for Developers, Share Kit: https://developers.tiktok.com/products/share-kit and green screen: https://developers.tiktok.com/docs/en/green-screen-kit
- Meta for Developers, Sharing to Stories: https://developers.facebook.com/docs/instagram-platform/sharing-to-stories
- Vidscape, mobile game ad hooks (first 3 seconds): https://vidscape.ai/blog/mobile-game-ad-hooks
- Viryze, TikTok gaming trends 2026 (vendor blog): https://viryze.com/blog/tiktok-gaming-trends-2026
- GameAnalytics, juice in game design: https://www.gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design
- Game Developer, mistakes that drain juice: https://gamedeveloper.com/design/6-mistakes-that-ll-drain-the-juice-out-of-your-game

## Soundtracks plan (2026-10-24)

Plan for idea 4. **Built on 2026-10-24**, except the parts S11 lists as left. Nobody has listened
to the styles yet: the next step is the listening loop (S7).

**Decision (owner, 2026-10-24):** build every soundtrack **in code, the way the current music is
built**. This avoids the legal risk of AI-generated or licensed audio. Realistic instruments are
best-effort: a stylized sound is acceptable where synthesis falls short. The AI route was
researched first and is kept for reference in S12.

**Sourced** = from a cited source (Sources at the end); **measured** = read from our code;
**assumption** = our reasoning, to check by listening. The legal notes are research, not legal
advice.

### S1. What we're making

- **One music style per genre:**
  - its own instruments, synthesized at start-up like today's;
  - its own score and tempo range;
  - the same layers that join as the tower grows.
- **Instrumental only.** Synthesized voices don't sound convincing, and a voice competes with the
  game's sounds (assumption).
- **Original:**
  - Our own chords, melodies and patterns.
  - No melody, hook or riff from a real song.
  - No artist names in the code or the shop.
- **The current music stays the free default** ("Classic").

### S2. What exists today (measured, from code)

- **`MusicService` + `MusicPlan`:**
  - Instruments are synthesized at start-up into short clips (marimba, pad, bass, shaker, kick,
    tick).
  - A 4-bar score in G pentatonic (G, Em, C, Dsus4) plays one 16th-note step at a time, scheduled
    on the audio clock with a 24-voice pool.
  - The tempo rises from 84 to 120 bpm as the tower grows and the block narrows.
  - Layers join at 10, 25 and 40 blocks, and a tension tick plays below 35 % width.
  - At game over the music slows down and fades; pause ducks it.
- **`MusicPlan` is pure C#,** tested under .NET; the synthesis lives in `MusicService` (Unity).
- **The perfect chime is in G pentatonic** (`Feel.PerfectSemitones`).
- **Shop:** the backend item kinds are `block` and `theme` (database check constraint `kind_known`);
  the user row has `equipped_block_skin` and `equipped_theme`.
- **Shared clips are silent** (`DECISIONS.md`).

### S3. Why build it in code

| | In code (chosen) | AI-generated audio (S12) |
|---|---|---|
| Service terms, training-data lawsuits | None | Depend on the tool; several are being sued |
| Who wrote it | Our code and our notes | Unclear: likely nobody owns raw AI output |
| Tempo rises with the tower | Yes, as today | No: a recording can't speed up without its pitch rising |
| The chime is in key | Always (we pick the key) | Only if the generated track lands in the right key |
| Size | About zero: synthesized on the phone | About 2 MB per pack |
| Realistic instruments | Best-effort (S4) | Good |

**Remaining legal care:**
- **Write original material.** Chord progressions and genre rhythms are shared by whole genres,
  but don't copy a song's melody, hook or riff, and don't name artists.
- **The reggaeton beat:** the "dembow" rhythm is the subject of a long US lawsuit (Steely &
  Clevie's "Fish Market" against reggaeton artists).
  - In July 2026 the judge sent the case toward a jury.
  - On 1 September 2026 he reversed that, ruling that the plaintiffs had not identified a
    copyrighted work of theirs containing the combination of elements they claimed.
  - We write our own pattern and use no samples. Check for later filings (an appeal) before
    release.
- **Ownership:** code and notes written with AI help raise the same "human author" question as
  AI audio (S12). It matters little here: the risk that counts is infringing someone else, and
  original code-built music keeps that low (assumption).

### S4. How close each genre can get (assumption, until we listen)

All styles are in **G major or E minor**. The chime's G pentatonic fits both, so it stays in tune
with no code change.

| Genre | Key, tempo | Instruments and how | Expected realism |
|---|---|---|---|
| **Synthwave** | E minor, 100–118 bpm | Detuned saw pads and leads through a resonant low-pass; a pulsing 8th-note bass; arpeggios; a gated, reverbed snare. | **High.** The genre is made of synthesizers. |
| **Lo-fi** | G major (7th chords), 72–88 bpm | FM electric piano; soft, swung drums; a round bass; vinyl crackle and tape hiss; a slow wobble in pitch; a dull low-pass on everything. | **High.** The dusty sound hides synthesis. |
| **Reggaeton** | E minor, 90–98 bpm | Our own dembow-style pattern on 808-style drums (kick, snare, rim, hats); a sub bass; a plucked lead (Karplus-Strong). | **Good.** 808 drums are synthesized by design. |
| **K-pop style** | G major / E minor, 112–128 bpm | Instrumental pop-dance: 808s and claps, a supersaw lead, a synth bass, plucks, risers into the busier layers. | **Good,** minus the vocals that define the genre. |
| **Metal** | E minor, 120–150 bpm | Power chords and palm-muted chugs from distorted, plucked-string synthesis (Extended Karplus-Strong through a waveshaper and a "cabinet" filter); a bass guitar the same way; synthesized double-kick drums and crash. | **Medium.** It will read as metal but sound stylized, closer to industrial or game metal than a real band. |

### S5. Synthesis toolkit

All of these are standard techniques that run in plain C#:

- **Subtractive:** saw, square and pulse waves, detuned in unison, through a resonant low-pass with
  envelopes. Used for synthwave and K-pop leads, pads and basses.
- **FM:** one sine modulating another. Used for electric piano, bells and punchy basses.
- **Plucked strings, Karplus-Strong** and its extended version (Jaffe & Smith, 1983): a short noise
  burst circulating through a delay line with a low-pass. Used for guitar, bass guitar and harp.
  Through distortion it gives the metal guitar.
- **808-style drums,** as the TR-808 itself made them:
  - kick: a sine sweeping down with a decay;
  - snare: two sine tones plus filtered noise;
  - hats: six square waves through a high-pass.
- **Textures:** vinyl crackle, tape hiss, wow and flutter (a slow pitch wobble), bit reduction.
- **Effects:** all baked into each instrument's clip in code: distortion, filters, and a small
  room (a few darkened echoes). As built, Unity's reverb and chorus filters are not used, so the
  WAV previews sound exactly like the game.
- **Variation:** 2–3 versions of each drum hit with different noise, played in turn, so repeats
  don't sound machine-gunned. *Not built yet:* one version per hit for now; add it if repeats
  sound mechanical when listening.

### S6. Code design

- **Synthesis moves to pure C#.**
  - Instruments become functions in `StackIt.Client` that return a `float[]`. `MusicService` only
    turns them into `AudioClip`s.
  - That makes them testable under .NET, and lets a preview tool render them outside Unity (S7).
- **`MusicStyle` data**, one per genre:
  - key, tempo range, chord progression;
  - the patterns per layer and the instruments;
  - the layer thresholds (default: `MusicPlan`'s 10 / 25 / 40 blocks and 35 % width).
- **The scheduler plays any style.**
  - `MusicService` keeps scheduling 16th-note steps.
  - Today's `MusicPlan` becomes the "Classic" style and must sound exactly as before (a test
    compares its notes).
- **Longer form.** Each style gets an A and a B section of 8 bars plus a drum fill every 4 bars,
  so a long session doesn't hear the same 4 bars on repeat.
- **Only the equipped style is synthesized:**
  - at start-up, or when the player equips it;
  - its start-up time is measured on the phone;
  - the voice pool grows from 24 only if a style needs it.
- **Game over and pause** behave as today: slow down and fade, duck.
- **Tests** (EditMode and .NET):
  - every note is in the style's key;
  - patterns fit the bar;
  - tempos stay in range;
  - layers join at the thresholds;
  - a rendered bar never clips (peak ≤ 1);
  - Classic is unchanged.

**As built (2026-10-24):**
- **Code:** `Scripts/Client/Music/`:
  - `Dsp` (oscillators, filters, plucked string, distortion, room);
  - `Kit` (the shared instruments);
  - `MusicStyle` and one class per style;
  - `MusicStyles` (the ids);
  - `MusicRenderer` (offline mix);
  - `VoicePool`.
- **`MusicService`:**
  - plays any style;
  - synthesizes a new style on a background thread and switches at the next bar line;
  - `Audition` plays a shop preview mid-run for 10 s;
  - `Override` plays a one-run trial.
- **Voice pool, a change Classic gets too:**
  - The 24 voices used to be reused in strict rotation, which cut long notes (pads) while still
    audible: 8 cuts in 4 bars of Classic at full intensity, found by the renderer.
  - `VoicePool` now takes a finished voice first, otherwise the one closest to finishing.
  - No audible cut is left in any style.
- **Genre layers:** a run starts with its beat (many runs end before 25 blocks), the bass joins
  at 10 blocks, the full drive at 25, and the tension layer plays while the block is thin.
  Classic keeps its own thresholds.
- **Loudness:** each style has a `Level` that matches Classic within 3 dB at Home and mid-run.
  The measure ignores the bass below ~120 Hz, which phone speakers can't play (tested).
- **Tuning:** every synthesized instrument is within 8 cents of G (tested; under 15 cents is
  inaudible against the chime).

### S7. Listening loop

Code can't judge its own sound, so the owner's ears decide. `unity/DotNetTests/MusicPreview`
renders each style to WAV files with the game's own code. From `unity/`:

```powershell
dotnet run -c Release --project DotNetTests/MusicPreview            # all styles
dotnet run -c Release --project DotNetTests/MusicPreview music_lofi # one style
```

Files land in `unity/DotNetTests/MusicPreview/out/` (git-ignored), named
`<style>-1-home.wav`, `-2-start`, `-3-30-blocks`, `-4-thin-block` and `-5-whole-run`. The whole
run climbs from 0 to 60 blocks, so the tempo rises and the layers join. The tool also prints
loudness, peak and voice cuts.
1. Listen on the PC and on the phone (speaker and headphones).
2. Give notes ("bass too loud", "guitar too fizzy").
3. Tweak and render again.
4. Then try the style in the game on the phone.

### S8. Backend and shop

- **Backend:**
  - A new item kind `music`: a migration extends the `kind_known` constraint.
  - An `equipped_music` column on the user, default `music_classic`.
  - Equip works like skins.
  - Item ids: `music_classic`, `music_synthwave`, `music_lofi`, `music_reggaeton`, `music_kpop`,
    `music_metal` (`classic` and `synthwave` were already taken by a block skin and a theme).
- **Shop:**
  - A Music tab.
  - A free 10-second listen, played live by the same code. It plays even with music turned off,
    because the player asked for it.
  - "Try it for one run" through a rewarded ad (idea 5): after the ad, a run starts at once with
    that soundtrack. The next run, or going Home, brings back the equipped one.
  - *Changed from the plan:* no list in Settings. Soundtracks are equipped in the shop, like
    skins and themes; Settings keeps the music on/off switch.
- **Prices (provisional):** coins only. Lo-fi 600, synthwave 800, reggaeton 1,000, K-pop style
  1,000, metal 1,200: 4,600 coins in all, a new sink for engaged players. Revisit them in the
  shop curve (findings ▸ E) once we have real coin rates.
- **Tests:** backend economy and equip tests; `StackIt.Tests.UI` for the new shop rows.

### S9. Measuring it

- **Already covered:** `item_preview`, `item_unaffordable_tap`, `setting_changed`, and the ad
  events by `placement`.
- **Added (2026-10-24)** in `shared/telemetry/events.json` and the backend copy:
  - a `music_preview` event (`item_id`, `seconds`);
  - a `music` property on `run_end`;
  - the `music_trial` placement for the try-it ad.
- **Questions to answer:**
  - Preview → purchase rate.
  - Do runs per session and session length change with a bought style vs Classic?
  - How often do players accept the rewarded "try" ad?

### S10. Clips and Content ID

- Shared clips are silent today.
- Since the music is our own, adding it to clips later (with H3 / H5) carries no third-party
  claim. A recognizable game sound can help clips spread.
- Still don't register it with Content ID: that would claim our own players' videos
  (assumption, from how Content ID works).

### S11. Steps

1. ✅ **Refactor, no new sound:**
   - move synthesis to pure C#;
   - add `MusicStyle`;
   - make today's music the Classic style, proven unchanged by tests.

   Exception: the voice pool fix, which only removes audible cuts.
2. ✅ **The preview tool** (S7).
3. ✅ **Styles,** all five written. **Left:** the owner's listening loop (S7), and tweaks from it.
4. ✅ **Backend item kind and equip; the shop's Music tab** with the listen button.
5. ✅ **Telemetry additions and the rewarded try.**
6. **Left before release:**
   - run the Unity tests in the editor;
   - try the shop tab and a trial run on a phone;
   - check FPS (`perf_sample`) and the time to switch styles on a phone;
   - migrate the production database;
   - release, then compare the metrics (S9).

### S12. If a genre falls short

In order of preference:
1. **Accept the stylized sound.** The owner's call (2026-10-24): fine if realism isn't possible.
2. **Real recorded notes under a public-domain (CC0) licence,** played by our sequencer. This is
   how the game already uses Kenney's CC0 sound effects.
   - Example: one guitar or drum hit per note.
   - Check each pack's licence file before use, and keep it in the repo.
   - It adds a few MB.
3. **AI-generated audio** (researched, not chosen):

   | Tool | Finding |
   |---|---|
   | **ElevenLabs Music** | Trained on licensed music. Its self-serve plans exclude "film, TV, and Studio Games" (undefined); Enterprise allows all uses. |
   | **Stable Audio 2.5** | "Fully licensed dataset" according to Stability; rights depend on the tier. |
   | **Lyria 2** (Google) | Instrumental 32.8 s clips with a SynthID watermark; whether Google's AI indemnity covers it is unconfirmed. |
   | **Suno** | Paid plans grant commercial use but no warranty that any copyright exists. UMG and Sony are still suing, and a German court found infringement. |
   | **Udio** | Downloads are disabled. |

   **Ownership:** raw AI output is likely unowned in the US (Copyright Office, 2025; the Supreme
   Court declined *Thaler* in 2026) and in Mexico (Supreme Court, Amparo Directo 6/2025).

### S13. Open questions

1. **Genres:** approved (2026-10-24): synthwave, lo-fi, reggaeton, K-pop style, metal. Is that
   build order OK?
2. **Listening:** who approves each style? Proposed: the owner, through the preview WAVs (S7).
3. **Prices:** after real coin data.
4. **Sound in clips (S10):** decide together with H3 / H5.

### Sources

Building in code:
- [Stanford CCRMA: Extended Karplus-Strong algorithm](https://ccrma.stanford.edu/realsimple/faust_strings/Extended_Karplus_Strong_Algorithm.html)
- [J. O. Smith: Karplus-Strong algorithms](https://ccrma.stanford.edu/~jos/pasp/Karplus_Strong_Algorithms.html)
- [J. O. Smith: Electric guitars (virtual instruments)](https://dsprelated.com/freebooks/pasp/Electric_Guitars.html)
- [Stanford CCRMA student project: distorted electric guitar with EKS](https://ccrma.stanford.edu/~hugo/220a/project/)
- [Wikipedia: Roland TR-808](https://Www.Wikipedia.org/wiki/TR-808)
- [Equipboard: the Roland TR-808 story](https://equipboard.com/posts/roland-tr-808-guide)
- [ModularGrid: SD808 module (808 snare circuit)](https://modulargrid.net/e/modules/view/54058)

Dembow case:
- [Music Business Worldwide: only a jury can decide (July 2026)](https://www.musicbusinessworldwide.com/only-a-jury-can-decide-reggaeton-dembow-copyright-case-targeting-bad-bunny-karol-g-and-150-artists-judge-rules/)
- [Complete Music Update: Bad Bunny asks the judge to reconsider](https://completemusicupdate.com/bad-bunny-asks-judge-to-reconsider-dembow-riddim-ruling-argues-steely-clevie-are-seeking-to-protect-a-frankenstein-work/)
- [Music Business Worldwide: Bad Bunny wins dembow ruling (September 2026)](https://www.musicbusinessworldwide.com/bad-bunny-wins-reggaeton-dembow-ruling/)

AI route (S12):
- [ElevenLabs: Eleven Music (plans, commercial use, stems)](https://elevenlabs.io/music)
- [ElevenLabs: Music API](https://elevenlabs.io/music-api)
- [AlternativeTo: Stable Audio 2.5](https://alternativeto.net/news/2025/9/stable-audio-2-5-from-stability-ai-delivers-faster-more-intricate-ai-generated-music/)
- [Google Cloud: Lyria on Vertex AI](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/music/generate-music)
- [Suno: update to downloads policy and Terms of Service](https://about.suno.com/blog/suno-updates-tos)
- [Music in Africa: Suno adjusts ownership terms](https://www.musicinafrica.net/magazine/suno-adjusts-ai-music-ownership-terms-after-warner-music-partnership)
- [The Next Web: Suno v6 licensed models](https://thenextweb.com/news/suno-v6-warner-bmg-believe-licensed-models)
- [Digital Music News: Suno and Universal settlement impasse](https://www.digitalmusicnews.com/2026/04/09/suno-universal-music-lawsuit-settlement-impasse/)
- [Digital Music News: UMG and Udio, open questions](https://www.digitalmusicnews.com/2025/11/03/umg-udio-vexing-questions-swirl/)
- [Akin Gump: Copyright Office AI report, part 2](https://www.akingump.com/en/insights/ai-law-and-regulation-tracker/us-copyright-office-releases-part-2-of-ai-report)
- [Decrypt: Supreme Court declines AI authorship case](https://decrypt.co/es/359761/tribunal-supremo-eeuu-derechos-autor-ia-autoria-humana-fallo/)
- [Pérez-Llorca: note on Amparo Directo 6/2025](https://www.perezllorca.com/es-mx/actualidad/nota-juridica/comentario-a-la-sentencia-dictada-en-el-amparo-directo-6-2025-por-la-segunda-sala-de-la-scjn-sobre-el-registro-de-obras-generadas-por-inteligencia-artificial-2/)

## Measurement findings (2026-10-23)

Research for step 1, recorded so later work starts from facts. Every number is labelled
**measured** (from code or a build), **modelled** (from the simulation below, built on stated
assumptions) or **external** (a published benchmark, see Sources).

### A. What we can already measure, with no new SDK

The backend stores most of what matters, keyed by `user_id`:

| Table | Holds | Gives us |
|---|---|---|
| `users.created_at` | Install / first-launch cohort | D1 / D7 / D30 retention (a returning user has runs on day N) |
| `runs` | Every run: `started_at`, `finished_at`, `status`, `verified_score`, `placed`, `perfects`, `max_combo`, `continues_used`, `coins_earned`, `coins_doubled`, `challenge_id`, and the full tap `events` | Runs per day; run length; score distribution; continue usage (= rewarded "continue" views); double-coins usage (= rewarded "double" views); duel share; abandoned runs (`status = 'started'` never finished); each player's real timing accuracy (for the model in D) |
| `wallet_transactions` (append-only ledger; kinds `run_reward`, `rewarded_double`, `daily_reward`, `purchase`) | Every coin in and out | Coins earned vs spent per day; time to first purchase; which items sell |
| `user_skins`, `daily_streaks` | Ownership; streaks | Item popularity; streak length |
| challenges / friends tables | Duels and friend codes | Viral loop: challenges created → opened → played |

### B. What we cannot see yet

- **App sessions.** The client counts launches (`LocalStore.SessionCount`) but never sends
  them; app opens without a run are invisible.
- **Screens and quit points.** We don't know which screen players leave from.
- **Interstitials and banners** shown, failed and clicked: no record anywhere. Rewarded views
  are visible only indirectly, through `continues_used` and `coins_doubled`.
- **Rewarded offers shown vs accepted** (opt-in rate). We know acceptances, not how often the
  offer appeared.
- **Ad revenue per user.** The installed Google Mobile Ads plugin (11.6.0) has `OnAdPaid` with an
  `AdValue` (value in micros, currency code, precision). **Measured** in the plugin DLLs. It
  gives an impression-level revenue estimate per ad, per player, inside our own data. AdMob's
  console stays the source of truth for the payout.
- **Shop intent:** views, previews, and taps on items the player can't afford yet.
- **Sharing:** clip exports and share taps (server-side we only see challenges created).
- **Technical health:** time from launch to Home, FPS, offline starts, API errors.

### C. Problems found in the current build (measured)

1. **The APK asks for the in-app purchase permission.** `aapt dump permissions` on
   `StackIt-dev.apk` lists `com.android.vending.BILLING` and bundles the Play Billing library.
   It comes from `com.unity.purchasing` 4.15.1 (it injects `com.android.billingclient:billing`).
   Nothing in our code uses it, and the money rule says it never will.
   **Recommendation:** remove `com.unity.purchasing`.
2. **Unused analytics packages:** `com.unity.analytics` 3.8.2 (legacy) pulls in
   `com.unity.services.analytics` 6.3.0 and `com.unity.services.core`. Unity Analytics is
   disabled in `UnityConnectSettings` and no script calls it, so they send nothing, but they
   add weight. **Recommendation:** remove them, unless we pick Unity Analytics in H.
3. **The rewarded "Continue" is worth little** (modelled): +2 to +4 points on a median run,
   because the tower keeps its narrowed width after the continue. A player who notices this
   stops accepting the offer.
4. **"Double coins" is worth little** (modelled): it doubles 3–14 coins, against shop prices of
   200–3000. Expect low opt-in until the reward means more.
5. **Size:** the development APK is 96 MB, both ABIs, with `libil2cpp.so` at 88 MB + 71 MB
   uncompressed. Measure the **release** AAB's download size in Play Console (*App bundle
   explorer*). Install size affects how many store visitors install, and dropping ARMv7 roughly
   halves the native code.
6. The APK also has `FOREGROUND_SERVICE` (from AndroidX WorkManager, pulled in by the ads SDK)
   and the `ACCESS_ADSERVICES_*` permissions (Privacy Sandbox, ads SDK). That's expected; keep
   them in mind for the Play Console permission declarations.

### D. Run model (modelled)

- **Method:** `backend/scripts/economy_model.py`. Re-run from `backend/` with
  `uv run python -m scripts.economy_model`; the default seed 1234 reproduces the tables in D
  and E. Tests: `tests/domain/test_economy_model.py`.
  - It plays 3,000 runs per row with the **real engine** (`backend/app/domain/sim/engine.py`).
  - The simulated player aims for the tick where the moving block crosses the centre on its
    first pass, and misses by a Gaussian timing error σ.
  - The engine decides placements, perfects, regrowth, combos and the miss.
  - "With continue" takes the single continue on the first miss.
- **The σ values are assumptions** (25 / 35 / 50 / 70 ms ≈ skilled / good / average / new).
  Calibrate after launch: a player's real `perfects / placed` maps onto σ using the "perfect
  rate" column.

| σ (ms) | Continue | Score p10 / p50 / p90 | Blocks placed (mean) | Perfect rate | Coins per run (mean / median) | Active seconds per run (mean) |
|---|---|---|---|---|---|---|
| 25 | no | 50 / 69 / 97 | 41.6 | 0.43 | 14.0 / 13 | 26.4 |
| 25 | yes | 52 / 73 / 100 | 43.4 | 0.44 | 14.6 / 14 | 29.3 |
| 35 | no | 30 / 43 / 59 | 29.6 | 0.33 | 8.4 / 8 | 20.1 |
| 35 | yes | 32 / 45 / 63 | 31.0 | 0.34 | 8.9 / 9 | 22.9 |
| 50 | no | 18 / 26 / 37 | 20.6 | 0.25 | 5.0 / 5 | 14.8 |
| 50 | yes | 20 / 28 / 40 | 21.9 | 0.26 | 5.4 / 5 | 17.7 |
| 70 | no | 11 / 17 / 25 | 14.5 | 0.18 | 3.1 / 3 | 11.0 |
| 70 | yes | 13 / 19 / 27 | 15.8 | 0.19 | 3.5 / 3 | 13.9 |

- **Runs are short:** 11–29 s of play. Add roughly 8–12 s for game over, results and restart,
  so one run cycle is about 25–40 s.
- **Interstitial pacing today:** the rule is ≥ 3 runs **and** ≥ 90 s since the last ad, none
  in the first session or after a record, and a rewarded ad resets the timer. With 25–40 s run
  cycles, that's about one interstitial every 2 minutes of play. This sits inside AdMob's
  rules (between runs, never mid-run, never back-to-back) and at the busier end of the ranges
  publishers quote (G).

### E. Economy and shop prices (modelled)

- **Today's rules:**
  - each run pays `score // 5`;
  - double coins (rewarded ad) ×2 for that run;
  - daily gift 25 / 35 / 50 / 70 / 90 / 120 / 200 over a 7-day streak (590 a week if claimed
    every day).
- **Prices:** 10 paid items totalling 9,400 coins.
  - blocks: glass 200, magma 400, aurora 600, circuit 900, prism 1500, gold 3000;
  - themes: midnight 300, sunset 500, vapor 800, matrix 1200.
- **Three player types.** Habits are assumptions; coin rates come from D.

| Player | Habits | Coins per day | First item | Gold, if saving only for it | Whole shop (cheapest first) |
|---|---|---|---|---|---|
| Casual | σ 50, 2 sessions × 6 runs, doubles 20 % of runs, claims the gift 60 % of days | ~120 | day 3 | day 32 | day 100 |
| Regular | σ 35, 3 × 8 runs, doubles 30 %, claims daily | ~340 | day 1 | day 9 | day 23 |
| Engaged | σ 25, 4 × 10 runs, doubles 40 %, claims daily | ~870 | day 1 | day 4 | day 11 |

What this means for "not too accessible, but not too expensive":

- **Coin income spreads about 7× between player types**, because coins follow score, and score
  grows with both skill (combo bonus) and play time. The engaged player owns everything in
  under two weeks and then has nothing to spend coins on. The casual player waits three days
  for the first item, and that player is the one most likely to leave in week 1. The daily
  gift is almost half of a casual player's income.
- **Proposed targets.** To confirm against real ledger data before changing any price:
  - **first item within the first day for everyone;**
  - then a new item every 3–5 days of regular play;
  - the top item at about 4–6 weeks for a regular player;
  - nobody finishes the shop in under ~4 weeks.
- **Levers, in order of preference:**
  1. A larger base reward per run, less tied to skill: coins from blocks placed, or a flat
     amount per run plus a smaller score share. This narrows the spread.
  2. A one-time starter reward or day-1 missions, so the first item lands in the first
     session.
  3. More coin sinks for engaged players: soundtracks (idea 4), more skins, cosmetic tiers.
  4. Only then, re-price the top items upwards.

  Any change to `coins_for_run` must change the backend **and** the C# mirror together.
- **Make rewarded offers worth an ad** (C3–C4). For example: "Continue" restores some width or
  a free perfect; "double coins" pays at least a fixed bonus. These are the cheapest way to
  raise rewarded views without adding interruptions.

### F. Ads today (measured, from code)

| Format | Where | Rule |
|---|---|---|
| Banner | Menu screens only (`AllowsBanner`; never in-run, never on the continue offer) | Hidden during play |
| Interstitial | After a run, when going back to Home or into a new run (`MaybeInterstitial`) | ≥ 3 runs and ≥ 90 s since the last ad (`AdsConfig`); never in the first session or after a new record |
| Rewarded "Continue" | Miss → offer; once per run; never in duels | Resets the interstitial timer |
| Rewarded "Double coins" | Results screen | Resets the interstitial timer |

AdMob's rules (external, AdMob policy page): interstitials only at natural breaks; never on app
launch or exit; never back-to-back; at most one per two user actions; preload so the ad never
appears late, over the next screen. Today's flow meets these.

### G. External benchmarks (use as rough ranges only)

Sources disagree, and most are vendor blogs; one (Playio) says outright that no neutral
benchmark exists for impressions per user. Use these to sanity-check, then set targets from our
own first weeks of data.

| Metric | Range quoted | Source |
|---|---|---|
| D1 retention, all mobile games | median ~22 % (2025 data); top quarter ~26–33 % | GameAnalytics 2025/2026 benchmarks |
| D7 retention, all mobile games | median ~3.4–4 % | GameAnalytics |
| Hyper-casual retention | D1 ~29–33 %, D7 ~6–12 %, D30 ~1.4–4 % (sources vary) | AppAgent / Sensor Tower, Appodeal 2025 |
| Session length | median games 5–6 min; top games 8–9 min | GameAnalytics 2025 |
| Ad-only ARPDAU, hyper-casual | ~$0.01–0.10 (most quotes $0.03–0.08) | AppFollow, NextApp, Playio |
| Interstitials | 4–6 per session ceiling with 2–3 min spacing; others say 1–2 per session | Playio / AdReact, Audiencelab |
| Rewarded | opt-in 40–70 %, completion 90 %+ (targets, not measurements) | AdReact, MonetizeMore |

### H. Tool choice

| Option | Pros | Cons |
|---|---|---|
| **First-party telemetry** (new `/v1/events` endpoint into Postgres) — **recommended** | Joins directly with runs, wallet and challenges by `user_id`. No new SDK, no new third party, no extra install size. Same auth and offline queue pattern we already have. Revenue per player via `OnAdPaid`. We own the definitions. | We write the dashboards (SQL views first; Metabase later if needed). We build A/B assignment ourselves (simple: a hash of `user_id`). |
| Firebase Analytics | Ready-made dashboards and cohorts; links with AdMob for revenue per user | Another SDK and pods (EDM), `google-services` files, data shared with Google for analytics, a consent-mode setup; can't join with our runs and wallet data |
| Unity Analytics (UGS) | The package is already half-installed | Another dashboard outside our data; needs a Unity Cloud project; same joining problem |
| GameAnalytics | Free, game-specific benchmarks | Third-party SDK and data sharing; same joining problem |

**Decision proposed:**
- first-party telemetry for player behaviour;
- the AdMob console for eCPM, fill rate and payout;
- no third-party analytics SDK for now.

### I. Metric definitions (so numbers mean the same thing every time)

- **Install cohort:** `users.created_at` date (UTC).
- **Active on day N:** at least one `session_start` (or, before telemetry ships, one run) on
  calendar day `created + N` (UTC).
- **DN retention:** active on day N ÷ cohort size. D1, D7, D30.
- **Session:** from app foreground to background or quit. A return within **30 s** continues
  the same session.
- **Session length:** foreground seconds in the session.
- **Runs per session:** runs started within the session.
- **DAU:** distinct users active that day.
- **Ads per session, per format:** `ad_shown` events ÷ sessions.
- **Rewarded opt-in:** `offer_accepted ÷ offer_shown`, per placement (continue, double).
- **Rewarded completion:** `ad_rewarded ÷ ad_shown` (rewarded only).
- **Interstitial fill:** `ad_shown ÷ ad_requested`.
- **ARPDAU (estimated):** Σ `ad_paid.value_micros` ÷ 10⁶ ÷ DAU. Reconcile against AdMob
  monthly.
- **Coins per day, earned vs spent:** from `wallet_transactions` by `kind`.
- **Time to first purchase:** first `purchase` minus `users.created_at`.
- **Quit point:** last `screen_view` before `session_end`.
- **Viral:** challenges created per DAU; challenge opens → installs → first duel played
  (install referrer and deep link events).

### J. Event list (as built, 2026-10-24)

The source of truth is **`shared/telemetry/events.json`**: event names and the props each one
may carry. The server rejects unknown events, undeclared props, non-scalar values, strings over
64 characters and numbers over 10¹². There are tests to keep the backend copy
(`backend/app/domain/telemetry_events.json`) and the C# names (`TelemetryEvents`) in sync with
it.

- **Per batch:** `app_version`, `platform` (`android`, `ios`, `editor`, `other`).
- **Per event:** a client-generated `id` (makes a re-sent batch harmless), `session_id`, and
  client time `at`.
- **Added by the server:** `user_id`, `received_at`, and `occurred_at`. `occurred_at` is the
  client time when it lies within 7 days back to 5 minutes ahead of server time; otherwise it's
  the server time.

| Event | Props | Sent from |
|---|---|---|
| `session_start` | `session_index`, `first_session`, `cold_start`, `launch_ms` (cold start: launch to Home), `os`, `device_ram_mb`, `language`, `look` | `TelemetryService` (`App` calls `MarkLaunched` when Home first shows) |
| `session_end` | `duration_s` (foreground only), `runs`, `ads`, `ended_by` (`timeout` = back after > 30 s; `relaunch` = the app had been killed, reported on the next launch) | `TelemetryService` |
| `screen_view` | `screen` (class name) | `UiRouter.Shown` |
| `run_start` | `run_id` (joins `runs`), `duel`, `offline` | `App.StartRun` |
| `run_end` | `run_id`, `end_reason` (`miss` / `restart` / `quit`), `score`, `placed`, `continues`, `music` (the soundtrack playing) | `App` |
| `offer_shown` / `offer_accepted` / `offer_declined` | `placement` (`continue` / `double` / `music_trial`) | `App` (the double-coins offer follows the results screen's own rule) |
| `ad_requested` / `ad_loaded` / `ad_load_failed` | `format`, `latency_ms` / `error_code` | AdMob adapter |
| `ad_shown` (impression) / `ad_clicked` / `ad_rewarded` / `ad_closed` | `format`, `placement` (`continue`, `double`, `between_runs`, `menu`, `music_trial`) | AdMob adapter; the editor's fake ads too |
| `ad_show_failed` | `format`, `placement`, `error_code` (`-1` = an ad was due but none was loaded) | AdMob adapter |
| `ad_paid` | `format`, `placement`, `value_micros`, `currency`, `precision`, `ad_source` | AdMob `OnAdPaid` |
| `shop_view` | `balance` | Shop |
| `item_preview` | `item_id`, `price`, `balance`, `owned` (the buy confirmation opened) | Shop |
| `item_unaffordable_tap` | `item_id`, `price`, `balance` | Shop |
| `music_preview` | `item_id`, `seconds` | Shop (the listen button) |
| `mission_claimed` | `slot` (0–2, 3 = chest), `kind` | Missions screen |
| `share_tap` | `source` (`end_card` / `friends` / `copy_result` / `tiktok` / `instagram`) | `App`, Friends |
| `clip_exported` | `ok` | `App` |
| `challenge_opened` | `via` (`link` / `code` / `friends`) | `App.OpenChallenge` |
| `setting_changed` | `setting` (`look`, `music`, `sound`, `haptics`, `reduce_motion`, `language`), `value` | `App`, Settings |
| `perf_sample` | `fps_median`, `fps_p5`, `seconds` (each time the app goes to the background) | `TelemetryService` |
| `api_error` | `endpoint` (ids removed: `/v1/runs/{id}/submit`), `status` (0 = offline), `code` | `ApiClient.Failed` |

Notes:
- **Ads don't break sessions.** On Android a full-screen ad pauses the app. `App` sets
  `InFullScreenAd` around every ad, so that pause doesn't count as leaving.
- **Banners refresh by themselves.** Later banner loads report `ad_loaded` without a new
  `ad_requested`, so measure banner fill in AdMob's console instead.

### K. Implementation (built 2026-10-24, except 4)

1. **Backend.**
   - Migration `148c2f165bbf` creates `client_events` (cascade-deleted with the account),
     indexed on (`name`, `occurred_at`) and (`user_id`, `occurred_at`).
   - `POST /v1/events` takes ≤ 100 events per batch, rate-limited to 30 per minute per IP,
     inserted with `ON CONFLICT (id) DO NOTHING`. It returns `{accepted, rejected}`.
   - Code: `app/domain/telemetry.py` (rules), `app/services/telemetry.py`,
     `app/repositories/telemetry.py`, `app/api/telemetry.py`.
   - Tests: `tests/domain/test_telemetry.py`, `tests/api/test_events.py`.
2. **Client.**
   - Engine-free logic in `Client/Telemetry.cs`: names, the persisted `TelemetryQueue`
     (capacity 500, oldest dropped), the `SessionTracker` (30 s rule, survives kills) and
     `FrameStats`. Tests: `Tests/EditMode/TelemetryTests.cs`.
   - `Services/TelemetryService.cs` serializes and queues events and flushes them every 30 s,
     on background and after launch, once signed in. It retries offline, 5xx, 429 and 401
     errors, and drops a batch on any other 4xx.
   - Hooks: `App`, `UiRouter.Shown`, Shop, Friends, Settings, `ApiClient.Failed`, and the AdMob
     adapter, compiled against the real plugin 11.6.0 DLLs.
3. **Reports.**
   - `backend/scripts/metrics.sql` creates views in a separate `metrics` schema: `activity`,
     `dau`, `retention`, `sessions`, `session_daily`, `ads_daily`, `offers_daily`,
     `revenue_daily`, `quit_points`, `runs_daily`, `player_accuracy`, `economy_daily`,
     `first_purchase`, `item_sales`.
   - Run `uv run python -m scripts.metrics apply`, then `report [--days N]`. The views read
     the live tables, so `apply` is only needed when `metrics.sql` changes.
   - Tests: `tests/services/test_metrics_views.py`.
   - To query production, point `DATABASE_URL` at Railway's public Postgres URL.
4. **Remote config: not built.** Later, for A/B tests:
   - `GET /v1/config` returns tunables per variant, with the variant chosen by a hash of
     `user_id`;
   - tunables: interstitial runs and seconds, rewarded values, coin rules, shop prices.
5. **Housekeeping.**
   - `com.unity.purchasing` and `com.unity.analytics` are removed from
     `Packages/manifest.json`; `com.unity.services.analytics` and `services.core` were only
     their dependencies.
   - Still to do: reopen Unity, rebuild, and check with `aapt dump permissions` that
     `com.android.vending.BILLING` is gone.

### L. Privacy and stores

- **What we store:** pseudonymous data tied to the game account. No advertising ID, no IP
  address, no precise location, no free text. Deleting the account deletes the events
  (tested).
- **Raw event retention: not built yet.** The plan is about 13 months, then aggregates only,
  which needs a scheduled purge. Until then events are kept while the account exists, which is
  what the privacy policy says.
- **Privacy policy** (`backend/app/web/legal.py`, EN + ES): a new "Improving the game" section
  lists what the telemetry records.
- **Play Data safety / App Store labels:** "App interactions" and "Diagnostics" are already
  listed for analytics in `SETUP.md` 8.10. Check again against the final event list.
- **Open legal question (not advice):** whether analytics events from EEA/UK players need
  consent, or can rely on legitimate interest. If consent is needed, gate the telemetry on
  the UMP consent result. Have it reviewed with the legal texts (pre-launch checklist).

### M. Open questions

- What session and retention targets do we commit to after the first 2 weeks of data?
- Should A/B tests wait until we have about 1,000 DAU? Smaller groups give noisy results.
- Keep ARMv7 for release (more old phones) or drop it (smaller download)?

### Sources

- AdMob, interstitial ad guidance: https://support.google.com/admob/answer/6201362
- AdMob, splash pages to avoid launch interstitials:
  https://blog.google/products/admob/using-splash-pages-to-avoid-unexpected-launch-interstitials/
- GameAnalytics 2025 mobile benchmarks (2024 data):
  https://investgame.net/wp-content/uploads/2025/02/2025-GameAnalytics-Mobile-Gaming-Benchmarks.pdf
  and summary https://gamedevreports.substack.com/p/gameanalytics-mobile-gaming-benchmarks
- Mobile retention benchmarks 2026:
  https://investgame.net/wp-content/uploads/2026/01/2026-01-20-Mobile_retention_benchmarks_2026.pdf
- Appodeal Mobile Casual Benchmarks Report 2025:
  https://files.gameindustrylibrary.com/documents/mobile-casual-benchmarks-report-2025.pdf
- AppAgent retention benchmarks: https://appagent.com/blog/mobile-game-retention-benchmarks/
- Playio ad revenue calculator inputs: https://blog.playio.co/mobile-game-ad-revenue-calculator
- AppFollow ad formats and eCPM: https://appfollow.io/blog/mobile-game-ads-formats-monetization
- NextApp ARPDAU guide: https://www.nextappinc.com/blog/mobile-game-monetization
- AdReact rewarded video guide: https://adreact.com/blog/rewarded-video-ads-mobile-game-monetization/
- MonetizeMore AdMob playbook: https://www.monetizemore.com/blog/admob-monetization/
- Audiencelab monetization: https://audiencelab.ai/blog/mobile-game-monetization-strategies
- Monetization Guy, ads vs UX: https://monetizationguy.com/articles/balancing-ad-revenue-and-user-experience-mobile-apps

## Verification status

What has actually been run, and what has not:

| Area | How it was verified |
|---|---|
| Backend (all of it) | 170 pytest tests on real PostgreSQL, ruff, `alembic check`, the soundtracks migration run up, down and up again on the local database (2026-10-24), Docker smoke run |
| Telemetry end to end | 2026-10-24: a real batch posted to the Docker API (6 accepted, 1 unknown event rejected), then read back through `scripts.metrics report` (sessions, offers, revenue). Not yet run from a device. |
| AdMob adapter | 2026-10-24: compiled with `STACKIT_ADMOB` against the installed Google Mobile Ads 11.6.0 DLLs (temporary project, 0 errors). Not run on a device. |
| Simulation parity | Python and C# both replay the 14 golden vectors (`pytest`, `dotnet test`) |
| Client logic (StackIt.Client) | 195 NUnit tests via `dotnet test` (2026-10-24, includes `TelemetryTests`, `MusicStyleTests`, `VoicePoolTests`) |
| Soundtracks | Rendered offline to WAV with the game's own code (`DotNetTests/MusicPreview`); tests check key, tuning (all within 8 cents), tempo, headroom, voice use and loudness against Classic. **Not yet heard by a person, and not yet played in Unity or on a phone.** |
| Unity runtime scripts | Compiled with `dotnet build unity/DotNetTests/CompileCheck/Game` against the editor's UnityEngine assemblies and the real uGUI, TextMeshPro and Input System sources |
| Unity editor scripts | Compiled against `UnityEditor.dll` (except `StringsImporter.cs`) |
| URP / Localization integration files | API names checked against the package sources; not compiled (packages too heavy to build outside Unity) |
| iOS `.mm` | Not compiled: needs Xcode |
| Android `.java` | Built into a development APK on 2026-10-23 (Unity Build And Run) |
| Gameplay feel, visuals, native recording/sharing | Not run: needs a licensed Unity editor and devices |
