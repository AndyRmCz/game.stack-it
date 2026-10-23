# Stack IT — Setup Guide

From a fresh clone to both stores. Follow the sections in order the first time.
Decisions behind the setup are in [docs/DECISIONS.md](docs/DECISIONS.md); what has and hasn't
been verified yet is at the bottom of [docs/PLAN.md](docs/PLAN.md).

Placeholders used below:

| Placeholder | Example | Where it is set |
|---|---|---|
| `<BASE_URL>` | `https://stackit-production.up.railway.app` | `PUBLIC_BASE_URL` (server) + `AppConfig.apiBaseUrl` (Unity) |
| `<HOST>` | `stackit-production.up.railway.app` | derived from `<BASE_URL>` |
| `<BUNDLE_ID>` | `com.stackit.game` | `APP_BUNDLE_ID` (server) + `AppConfig.bundleId` (Unity) |

---

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Unity Hub + **Unity 6** | 6000.5.10f1 (or newer 6.x) | Add modules: **Android Build Support** (with OpenJDK + Android SDK & NDK) and **iOS Build Support**. Sign in to Unity Hub and activate a license (Personal is fine). |
| Xcode | 16+ (macOS only) | iOS builds and TestFlight. Install CocoaPods: `sudo gem install cocoapods`. |
| Android Studio / JDK | Optional | Unity's bundled OpenJDK and SDK are enough; Android Studio helps with `adb` and logcat. |
| Python | 3.12 | Backend. |
| uv | 0.12+ | `pip install uv` or https://docs.astral.sh/uv/ |
| Docker Desktop | current | Local PostgreSQL. |
| .NET SDK | 6.0+ | Runs the C# simulation tests without Unity (`dotnet test`). |
| Railway CLI | current | `npm i -g @railway/cli`, then `railway login`. Optional: the dashboard works too. |

Accounts you will need: Railway, Google Cloud, Apple Developer Program, Google Play Console,
AdMob.

## 2. Backend locally

```sh
docker compose up -d db                       # PostgreSQL 17, creates `stackit` and `stackit_test`
cd backend
uv sync
uv run alembic upgrade head
uv run uvicorn app.main:app --reload          # http://localhost:8000/health → {"status":"ok"}
```

Or everything in containers: `docker compose up --build`. If ports 8000/5432 are taken:
`API_PORT=8010 POSTGRES_PORT=5433 docker compose up --build` (and adjust URLs).

Tests and lint (the database container must be running):

```sh
cd backend
uv run pytest                                  # real PostgreSQL, schema built by the migrations
uv run ruff check . && uv run ruff format --check .
uv run alembic check                           # models and migrations agree (needs a migrated DB)
```

Cross-language simulation tests (the same golden vectors in C#):

```sh
dotnet test unity/DotNetTests/Tests/StackIt.Tests.csproj
```

Changing a game rule: edit both `backend/app/domain/sim/engine.py` and
`unity/Assets/_Project/Scripts/Core/Simulation/StackSim.cs`, bump `SIM_VERSION` in both, run
`uv run python scripts/generate_sim_vectors.py`, review the vector diff, and run both test suites.

Optional, with Unity installed: compile-check the Unity scripts without opening the editor:

```sh
dotnet build unity/DotNetTests/CompileCheck/Game -p:UnityEditorData="<Unity>/Editor/Data"
dotnet build unity/DotNetTests/CompileCheck/Editor -p:UnityEditorData="<Unity>/Editor/Data"
```

(The Input System project expects the package extracted to
`unity/DotNetTests/CompileCheck/.packages/inputsystem/package`; extract
`<Unity>/Editor/Data/Resources/PackageManager/Editor/com.unity.inputsystem-*.tgz` there.)

## 3. Deploy the backend to Railway

1. **Create the project.** Railway dashboard → New Project → *Deploy from GitHub repo* → this
   repository. (CLI: `railway init`, then `railway link`.)
2. **Add PostgreSQL.** In the project: *+ New* → *Database* → *PostgreSQL*.
3. **Point the service at the backend.** Service → Settings:
   - *Root Directory*: `/backend`
   - *Config-as-code / Railway config file*: `/backend/railway.json` (the config file path does
     not follow the root directory, so give the full path).
   `railway.json` builds the Dockerfile, runs `alembic upgrade head` as the **pre-deploy command**
   and gates the deploy on `GET /health`.
4. **Generate the domain.** Service → Settings → Networking → *Generate Domain*. This is
   `<BASE_URL>`.
5. **Set the variables** (Service → Variables). `DATABASE_URL` should reference the plugin:
   `${{Postgres.DATABASE_URL}}`. The app converts `postgresql://` to the async driver itself.

| Variable | Required | Example / default | Purpose |
|---|---|---|---|
| `ENVIRONMENT` | yes | `production` | Enables production checks (e.g. a real `JWT_SECRET`). |
| `DATABASE_URL` | yes | `${{Postgres.DATABASE_URL}}` | PostgreSQL connection. |
| `PUBLIC_BASE_URL` | yes | `<BASE_URL>` | Every public link: challenge URLs, OG tags, well-known files. |
| `JWT_SECRET` | yes | 48+ random chars: `python -c "import secrets; print(secrets.token_urlsafe(48))"` | Signs access tokens. Rotating it signs everybody out (refresh tokens keep working). |
| `APP_BUNDLE_ID` | yes | `com.stackit.game` | Android package / iOS bundle ID in well-known files and store links. |
| `APP_URL_SCHEME` | no | `stackit` | "Open in app" button on landing pages. |
| `GOOGLE_CLIENT_IDS` | for Google sign-in | `123-web.apps.googleusercontent.com,123-ios.apps.googleusercontent.com` | Accepted `aud` of Google ID tokens (web client ID used by Android + iOS client ID). |
| `APPLE_CLIENT_IDS` | no | defaults to `APP_BUNDLE_ID` | Accepted `aud` of Apple identity tokens. |
| `APPLE_TEAM_ID` | for iOS links | `ABCDE12345` | `apple-app-site-association`. |
| `ANDROID_SHA256_FINGERPRINTS` | for Android links | `AA:BB:…,CC:DD:…` | `assetlinks.json`: upload key **and** Play App Signing key. |
| `IOS_APP_STORE_URL` | after release | `https://apps.apple.com/app/id1234567890` | Landing page button (hidden while empty). |
| `ANDROID_PLAY_STORE_URL` | no | derived from `APP_BUNDLE_ID` | Landing page button; the challenge code is appended as install referrer. |
| `ADMOB_PUBLISHER_ID` | for ads | `pub-1234567890123456` | Served as `/app-ads.txt`. |
| `SUPPORT_EMAIL` | yes | `support@yourdomain.com` | Privacy policy, terms, account deletion page. |
| `CORS_ALLOWED_ORIGINS` | no | empty | Comma-separated; leave empty (native clients only). |
| `LOG_LEVEL` | no | `INFO` | JSON logs to stdout. |
| `ACCESS_TOKEN_TTL_SECONDS` | no | `900` | Access token lifetime. |
| `REFRESH_TOKEN_TTL_DAYS` | no | `60` | Refresh token lifetime. |
| `RUN_TTL_HOURS` | no | `48` | Started runs must be submitted within this window. |
| `CHALLENGE_TTL_DAYS` | no | `7` | Challenge link lifetime. |
| `RATE_LIMIT_ENABLED` | no | `true` | In-memory per-IP limits (one instance). |
| `PORT` | set by Railway | — | Port uvicorn listens on. |

6. **Deploy** (push to the tracked branch, or `railway up` from `backend/`). Migrations run before
   the new container receives traffic.
7. **Verify:**
   ```sh
   curl https://<HOST>/health                      # {"status":"ok","database":"ok"}
   curl https://<HOST>/privacy -o /dev/null -w "%{http_code}\n"
   ```

## 4. Sign-in providers

### Google Sign-In (Android + iOS)

In Google Cloud Console (one project for the app):

1. *APIs & Services → OAuth consent screen*: External, app name "Stack IT", support email,
   privacy policy `<BASE_URL>/privacy`, terms `<BASE_URL>/terms`. Publish it.
2. *Credentials → Create credentials → OAuth client ID*, three times:
   - **Web application** → this is the **server client ID**. Android's Credential Manager issues
     ID tokens for this audience.
   - **Android** → package `<BUNDLE_ID>` + **SHA-1** of each signing certificate (debug/upload
     keystore and, after the first Play upload, the Play App Signing key from
     *Play Console → Test and release → App integrity*).
   - **iOS** → bundle ID `<BUNDLE_ID>`.
3. Server: `GOOGLE_CLIENT_IDS=<web client id>,<ios client id>`.
4. Unity: `Assets/_Project/Resources/StackIt/AppConfig.asset` → `googleWebClientId` (web) and
   `googleIosClientId` (iOS). The iOS build hook writes `GIDClientID` and the reversed-client-ID
   URL scheme into `Info.plist`.

### Sign in with Apple (iOS)

1. Apple Developer → *Certificates, IDs & Profiles → Identifiers* → your App ID `<BUNDLE_ID>` →
   enable **Sign In with Apple** and **Associated Domains**.
2. The iOS build hook adds both capabilities to the Xcode project. Nothing to set on the server:
   `APPLE_CLIENT_IDS` defaults to the bundle ID.
3. App Review requires Sign in with Apple whenever Google sign-in is offered on iOS; both are
   wired.

## 5. AdMob

1. Create an AdMob account, then **two apps** (Android + iOS, "not yet published" is fine).
   Note each **App ID** (`ca-app-pub-…~…`).
2. Per app, create three ad units: **Rewarded** (continue / double coins), **Interstitial**,
   **Banner**.
3. **Install the Google Mobile Ads Unity plugin** (it also installs EDM4U, which resolves the
   native dependencies of `Assets/_Project/Editor/StackItDependencies.xml`): import the latest
   `.unitypackage` from https://github.com/googleads/googleads-mobile-unity/releases, or add it
   through OpenUPM (`com.google.ads.mobile`).
4. *Assets → Google Mobile Ads → Settings*: paste both **App IDs**. Leave "Delay app measurement"
   on (we initialize only after consent).
5. Run **Stack IT ▸ Setup Project** again: it detects the plugin and adds the `STACKIT_ADMOB`
   define, which compiles `Assets/_Project/AdMob/AdMobAdsService.cs`.
6. **Swap the test IDs.** `Assets/_Project/Resources/StackIt/AdsConfig.asset` ships with Google's
   public test units. Replace the six IDs with yours **only for release builds**. Testing with
   real ads on your own device risks the account; add your device as a test device in AdMob
   instead.
7. **Consent messages** (AdMob → *Privacy & messaging*):
   - **GDPR** message for EEA/UK/Switzerland. Link the privacy policy `<BASE_URL>/privacy`.
   - **IDFA explainer** (iOS), shown before the ATT prompt. The ATT text itself is set by the
     build hook (`NSUserTrackingUsageDescription`); edit it in `BuildHooks.cs` if needed.
   - To test the GDPR form outside the EEA, tick `AdsConfig.debugForceEeaConsent` (never in
     release builds).
8. **app-ads.txt**: set `ADMOB_PUBLISHER_ID` on Railway and check
   `curl https://<HOST>/app-ads.txt`. AdMob looks for it at the root of the **developer website**
   listed in each store listing, so use `<BASE_URL>` as that website (or your custom domain later).

Ad rules implemented in code: banners only on menu screens; interstitials only between runs, at
least 3 runs and 90 s apart, never in the first session and never right after a new record;
rewarded continue once per run and never in challenge duels.

## 6. Deep links (Universal Links / App Links)

1. **iOS**: set `APPLE_TEAM_ID` (Apple Developer → Membership). Check:
   ```sh
   curl -i https://<HOST>/.well-known/apple-app-site-association
   # 200, content-type application/json, appIDs ["<TEAM_ID>.<BUNDLE_ID>"], components "/c/*"
   ```
   Apple's CDN caches this file; changes can take hours to reach devices. The entitlement
   `applinks:<HOST>` is added by the build hook from `AppConfig.apiBaseUrl`.
2. **Android**: get the SHA-256 fingerprints of the **upload key** (`keytool -list -v -keystore
   your.keystore`) and of the **Play App Signing key** (Play Console → App integrity). Set
   `ANDROID_SHA256_FINGERPRINTS=<upload>,<play signing>`. Check:
   ```sh
   curl https://<HOST>/.well-known/assetlinks.json
   ```
   and Google's tester: https://developers.google.com/digital-asset-links/tools/generator.
   On a device after install: `adb shell pm get-app-links <BUNDLE_ID>` should show `verified`.
   The intent filters come from `Assets/Plugins/Android/StackItManifest.androidlib`, which is
   regenerated from `AppConfig` before every Android build (menu: *Stack IT ▸ Regenerate Android
   Manifest*).
3. **Fallbacks** when a link doesn't open the app:
   - Fresh Android installs: the Play Store link carries `referrer=challenge%3D<CODE>`; the app
     reads it with the Play Install Referrer API on first launch.
   - iOS has no install referrer: on first launch the app opens "Got a challenge code?", and Home
     always has that link.
4. Try it: open `<BASE_URL>/c/<CODE>` from Notes (iOS) or Messages/WhatsApp (Android). Tapping a
   link inside a browser tab with the same domain does **not** trigger Universal Links. That is
   iOS behaviour, and it is why the landing page has its own "Open in app" button.

## 7. Unity project

1. Unity Hub → *Add → Add project from disk* → select the `unity/` folder → open with Unity 6.
   The first import resolves packages (URP 17.5, uGUI/TMP, Input System, Localization,
   Newtonsoft, iOS ATT support) and generates `.meta` files. **Commit the generated `.meta` files.**
2. **Stack IT ▸ Setup Project** (safe to run again). It:
   - creates `Resources/StackIt/AppConfig.asset` and `AdsConfig.asset`
   - creates the URP materials
   - creates the empty `Scenes/Main.unity` and adds it to Build Settings
   - applies player settings (bundle ID, portrait, IL2CPP, min SDK 26 / iOS 15, the Neon app icon)
   - imports the TMP Essentials
   - copies the look tokens `shared/theme/themes.json` to `Resources/StackIt/themes.json`
   - bakes the TMP font atlases into `Resources/StackIt/Fonts` (generated, git-ignored)
   - imports `strings.csv` into the Localization "UI" table
   - toggles the AdMob define

   The editor also does the token copy, the font bake (when the fonts or the subset changed) and
   the strings import on its own when it loads or when `strings.csv` changes (`AutoSetup`).

   **Fonts** (all SIL OFL 1.1 or Apache 2.0, licences next to them in `Assets/_Project/Fonts`):
   Fredoka 700/600 and Nunito 700/800, instanced and subset to Latin + Latin-1 (Spanish included),
   and Material Symbols Rounded in an outline and a filled cut (only the icons in `UI/Fonts.cs`).
   To add an icon or characters, re-subset with fonttools (`docs/THEMING.md` has the command); the
   atlases rebuild by themselves.

   **Shaders**: `Resources/StackIt/Shaders/UIRoundedRect.shader` (every UI surface, border, shadow
   and glow) and `Block.shader` (the blocks' exact face colours) live in Resources, so builds always
   include them; nothing to set up.
3. Edit `AppConfig.asset`: `apiBaseUrl = <BASE_URL>` (or `http://<your LAN IP>:8000` for a
   device against your laptop), store URLs and Google client IDs.
4. Press Play in `Main.unity`. In the editor, rewarded ads "succeed" instantly so continue and
   double-coins can be tested.
5. Tests: *Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All*. `StackIt.Tests.EditMode` holds
   the same files `dotnet test` runs (golden vectors, tokens, contrast); `StackIt.Tests.UI` needs
   the engine (uGUI layout, every screen's graphics follow the look).
6. **Localization**: UI text lives in `Assets/_Project/Resources/StackIt/strings.csv` (`key,en,es`).
   The editor re-imports it when it changes (or run **Stack IT ▸ Import Strings**). Localization uses Addressables: keep
   *Edit ▸ Preferences ▸ Addressables ▸ Build Addressables on Player Build* enabled (default).

### Android build

1. *File ▸ Build Profiles* → Android → *Switch Platform*.
2. *Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Resolve* (androidx, Install
   Referrer, Credential Manager, Google ID).
3. *Player Settings ▸ Publishing Settings*: create a **keystore** (*Keystore Manager*). Keep it
   and its passwords safe, outside the repo (`*.keystore` / `*.jks` are git-ignored). Losing it
   means losing the ability to update the app.
4. Check *Build App Bundle (Google Play)*, then *Build* → `StackIt.aab`.
5. Quick device test: uncheck the bundle option and *Build And Run* an APK with a USB-connected
   device.

### iOS build

1. Switch platform to iOS → *Build* → choose a folder. EDM4U runs `pod install` (GoogleSignIn).
2. Open **`Unity-iPhone.xcworkspace`** (not `.xcodeproj`).
3. *Signing & Capabilities*: select your team; confirm **Associated Domains**
   (`applinks:<HOST>`) and **Sign in with Apple** are present (added by `BuildHooks.cs`).
4. Run on a device. The clip recorder, share sheet, haptics and sign-in need real hardware.
5. *Product ▸ Archive* → *Distribute App* → App Store Connect.

## 8. Publishing

### Google Play Console

1. Create the app (`<BUNDLE_ID>`), category Games › Casual, free, contains ads: **Yes**.
2. **App content**:
   - *Privacy policy*: `<BASE_URL>/privacy`
   - *Ads*: yes
   - *App access*: no special access needed (guest accounts)
   - *Target audience*: 13+ is simplest. Lower ages need families-policy ad settings.
   - *Content rating*: fill the IARC questionnaire (no violence, no gambling; user-generated
     content limited to display names).
   - *Data safety*:

     | Data | Collected | Purpose | Notes |
     |---|---|---|---|
     | Device or other IDs | yes | Advertising, app functionality | AdMob advertising ID; random device ID for the guest account |
     | App activity (gameplay) | yes | App functionality, fraud prevention | runs, scores |
     | Email (only if Google is linked) | yes | Account management | |
     | Encrypted in transit | yes | | HTTPS |
     | Deletion | in-app + `<BASE_URL>/delete-account` | | |

   - *Account deletion*: URL `<BASE_URL>/delete-account`.
3. Upload the AAB to **Internal testing**, add testers by email, and install from the opt-in link.
   Then copy the **Play App Signing SHA-256** into `ANDROID_SHA256_FINGERPRINTS`.
4. Store listing: set the website to `<BASE_URL>` (needed for app-ads.txt).

### App Store Connect

1. Create the app with `<BUNDLE_ID>` and a SKU.
2. **App Privacy** (nutrition labels):
   - *Identifiers*: Device ID, used for third-party advertising and **tracking**
   - *User ID*: app functionality
   - *Gameplay content* and *Product interaction*: app functionality and analytics
   - *Email address* (if Apple/Google linked): app functionality
   - *Advertising data*: third-party advertising
3. ATT: the `NSUserTrackingUsageDescription` text is set by the build hook, and the prompt appears
   after the UMP consent flow.
4. Upload an archive, then **TestFlight** → internal testers → external group (needs a short
   beta review).
5. Review notes: mention guest play (no login needed), and that the account deletion is in
   Settings.

## 9. Moving to a custom domain later

1. Railway → Service → Settings → Networking → *Custom Domain* → add `play.yourdomain.com` and
   create the CNAME record it shows.
2. Change **one** server variable: `PUBLIC_BASE_URL=https://play.yourdomain.com`.
3. Change **one** client value: `AppConfig.apiBaseUrl`. The Android intent filters and the iOS
   `applinks:` entitlement are derived from it at build time. Ship new builds.
4. Old links (`*.up.railway.app/c/…`) keep resolving as long as the Railway domain stays attached.
   Only the new domain opens the app directly.
5. Update store listings (website, privacy policy URL), the Google OAuth consent screen links and
   the AdMob app-ads.txt website.

## 10. Pre-launch checklist

- [ ] `ENVIRONMENT=production`, strong `JWT_SECRET`, `SUPPORT_EMAIL` set; `/health` is green
- [ ] Railway Postgres backups enabled
- [ ] Legal texts (`backend/app/web/legal.py`) reviewed by someone qualified for your countries
- [ ] Real AdMob unit IDs in `AdsConfig` for release; test IDs for internal builds;
      `debugForceEeaConsent` off
- [ ] GDPR message and IDFA explainer published in AdMob; `/app-ads.txt` served
- [ ] `apple-app-site-association` and `assetlinks.json` validated; link opens the app on
      both platforms
- [ ] Google OAuth clients include the **Play App Signing** SHA-1; consent screen published
- [ ] Sign in with Apple and Google tested on device, including "switch account"
- [ ] Clip export and share tested on a low-end Android and an older iPhone (check orientation:
      if a clip is upside down, toggle `RecorderService.invertFlip`)
- [ ] Offline run → reconnect → run is verified (offline queue)
- [ ] Account deletion tested end to end; `/delete-account` reachable
- [ ] Spanish pass on every screen (device language and Settings switch)
- [ ] Unity EditMode tests green in the editor; CI green (`backend`, `client-logic`)
- [ ] Generated `.meta` files committed; keystore backed up outside the repo
- [ ] Store listings: screenshots, icon, data safety / privacy labels, content rating
