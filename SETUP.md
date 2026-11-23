# Milli — Setup Guide

From a fresh clone to both stores. Follow the sections in order the first time.
Decisions behind the setup are in [docs/DECISIONS.md](docs/DECISIONS.md); what has and hasn't
been verified yet is at the bottom of [docs/PLAN.md](docs/PLAN.md).

Placeholders used below:

| Placeholder | Example | Where it is set |
|---|---|---|
| `<BASE_URL>` | `https://stackit-production.up.railway.app` | `PUBLIC_BASE_URL` (server) + `AppConfig.apiBaseUrl` (Unity) |
| `<HOST>` | `stackit-production.up.railway.app` | derived from `<BASE_URL>` |
| `<BUNDLE_ID>` | `game.milli.com` | `APP_BUNDLE_ID` (server) + `AppConfig.bundleId` (Unity) |

---

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Unity Hub + **Unity 6** | 6000.5.10f1 (or newer 6.x) | Add modules: **Android Build Support** (with OpenJDK + Android SDK & NDK) and **iOS Build Support**. Sign in to Unity Hub and activate a license (Personal is fine). |
| Unity Build Automation | Cloud service | Builds iOS on Unity's Macs, so you need no Mac or Xcode (section 9). |
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

Product metrics (first-party telemetry, `docs/PLAN.md` ▸ "Measurement findings"):

```sh
cd backend
uv run python -m scripts.metrics apply          # once, and whenever scripts/metrics.sql changes
uv run python -m scripts.metrics report --days 14
uv run python -m scripts.economy_model          # the run / coin / shop-price model
```

Against production, set `DATABASE_URL` to Railway's **public** Postgres URL (Postgres service ▸
*Connect* ▸ public network) for that command only.

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
| `APP_BUNDLE_ID` | yes | `game.milli.com` | Android package / iOS bundle ID in well-known files and store links. |
| `APP_URL_SCHEME` | no | `milli` | "Open in app" button on landing pages. |
| `GOOGLE_CLIENT_IDS` | for Google sign-in | `123-web.apps.googleusercontent.com,123-ios.apps.googleusercontent.com` | Accepted `aud` of Google ID tokens (web client ID used by Android + iOS client ID). |
| `APPLE_CLIENT_IDS` | no | defaults to `APP_BUNDLE_ID` | Accepted `aud` of Apple identity tokens. |
| `APPLE_TEAM_ID` | for iOS | `ABCDE12345` | `apple-app-site-association`, and revoking Apple tokens. |
| `APPLE_KEY_ID` | for iOS | `ABC123DEFG` | The Sign in with Apple key (9.3). |
| `APPLE_PRIVATE_KEY` | for iOS | the text of that `.p8` file | Ends a player's Apple sign-in when they delete their account. Without it, sign-in still works but nothing is revoked. |
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

1. *APIs & Services → OAuth consent screen*: External, app name "Milli", support email,
   privacy policy `<BASE_URL>/privacy`, terms `<BASE_URL>/terms`. Publish it.
2. *Credentials → Create credentials → OAuth client ID*, three times:
   - **Web application** → this is the **server client ID**. Android's Credential Manager issues
     ID tokens for this audience.
   - **Android** → package `<BUNDLE_ID>` + **SHA-1** of each signing certificate (debug/upload
     keystore and, after the first Play upload, the Play App Signing key from
     *Play Console → Test and release → App integrity*).
   - **iOS** → bundle ID `<BUNDLE_ID>`.
3. Server: `GOOGLE_CLIENT_IDS=<web client id>,<ios client id>`.
   To check the server has it, after deploying: a made-up token must be refused as a token
   (`401 invalid_identity_token`), not with `503 provider_not_configured`, which means
   `GOOGLE_CLIENT_IDS` is empty:

   ```sh
   curl -s -X POST <BASE_URL>/v1/auth/google/login -H "Content-Type: application/json" \
     -d '{"id_token": "not-a-real-token-but-long-enough"}'
   ```
4. Unity: `Assets/_Project/Resources/StackIt/AppConfig.asset` → `googleWebClientId` (web) and
   `googleIosClientId` (iOS). The iOS build hook writes `GIDClientID` and the reversed-client-ID
   URL scheme into `Info.plist`.

### Sign in with Apple (iOS)

1. Apple Developer → *Certificates, IDs & Profiles → Identifiers* → your App ID `<BUNDLE_ID>` →
   enable **Sign In with Apple** and **Associated Domains**.
2. The iOS build hook adds both capabilities to the Xcode project. `APPLE_CLIENT_IDS` defaults to
   the bundle ID. The server also needs a Sign in with Apple key (9.3), to revoke tokens when an
   account is deleted.
3. App Review requires Sign in with Apple whenever Google sign-in is offered on iOS; both are
   wired.

## 5. AdMob

1. Create an AdMob account, then **two apps** (Android + iOS, "not yet published" is fine).
   Note each **App ID** (`ca-app-pub-…~…`).
2. Per app, create three ad units: **Rewarded** (continue / double coins), **Interstitial**,
   **Banner**.
3. **Install the Google Mobile Ads Unity plugin.** Do this even before you have an AdMob account:
   the project is built around it, and the test IDs work without an account. The plugin needs
   **EDM4U** (External Dependency Manager for Unity), which also resolves our own native
   dependencies (`Assets/_Project/Editor/StackItDependencies.xml`). Pick **one** way:
   - **Package Manager (recommended):**
     1. *Edit ▸ Project Settings ▸ Package Manager ▸ Scoped Registries ▸ +*: Name `OpenUPM`,
        URL `https://package.openupm.com`, Scope(s) `com.google.ads.mobile` → *Save*.
     2. *Window ▸ Package Manager ▸ + ▸ Install package by name…* → `com.google.ads.mobile` →
        *Install*. It pulls in `com.unity.external-dependency-manager` (EDM4U, from Unity's own
        registry) automatically.
   - **.unitypackage:** download `GoogleMobileAds-v<version>.unitypackage` from
     https://github.com/googleads/googleads-mobile-unity/releases → *Assets ▸ Import Package ▸
     Custom Package…* → import everything. It creates `Assets/GoogleMobileAds/` and offers to add
     EDM4U through the Package Manager (accept). If EDM4U is still missing afterwards: *Window ▸
     Package Manager ▸ + ▸ Install package by name…* → `com.unity.external-dependency-manager`.

   Have **only one** EDM4U: either the `com.unity.external-dependency-manager` package or an
   `Assets/ExternalDependencyManager/` folder, never both (duplicate menus and resolver errors).
   Once EDM4U is in, the menu *Assets ▸ External Dependency Manager* exists.
4. *Assets ▸ Google Mobile Ads ▸ Settings…*: paste both **App IDs** (Android and iOS).
   **An Android build without an App ID crashes at launch.** No AdMob account yet? Use Google's
   public sample App IDs until you have one: Android `ca-app-pub-3940256099942544~3347511713`,
   iOS `ca-app-pub-3940256099942544~1458002511`. Leave the other settings at their defaults
   (Android SDK: *Standard*). The app only initializes the SDK after the consent flow (UMP), so
   there is nothing to delay by hand.
5. Run **Stack IT ▸ Setup Project** again: it detects the plugin and adds the `STACKIT_ADMOB`
   define, which compiles `Assets/_Project/AdMob/AdMobAdsService.cs`. Then resolve the Android
   libraries (section 8.2, steps 3–5).
6. **Keep the test IDs in the project.** `Assets/_Project/Resources/StackIt/AdsConfig.asset`
   and the Google Mobile Ads settings hold Google's public test IDs, and stay that way. Release
   builds get the real ones at build time, never from the repo: Android from the
   `ADMOB_ANDROID_*` variables through *Stack IT ▸ Build Android Release* (8.7), iOS from the
   cloud build's variables (9.11). Testing with real ads on your own device risks the account;
   add your device as a test device in AdMob instead.
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
   Sora 700/600 and Lexend Deca 500/600, instanced and subset to Latin + Latin-1 (Spanish included),
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

Section 8 walks through everything Android: dependencies, running on your phone, the keystore,
the release bundle and Google Play.

### iOS build

Section 9 walks through everything iOS, with no Mac: signing from Windows, Unity Build Automation,
TestFlight and the App Store.

## 8. Android and Google Play, step by step

Written for a first Google Play release. Follow it top to bottom. Expect about **three weeks**
from creating the developer account to being live:

- identity verification takes a few days;
- new personal accounts must then run a **closed test with at least 12 testers for 14 days in a
  row** before Google lets them publish (8.11);
- each review can take hours to a few days.

Start 8.8 (the developer account) early, so the waiting overlaps with your own testing.

Play Console menus get renamed from time to time. If a path below doesn't match, use the search
box at the top of Play Console, or the checklist on the app's **Dashboard**, which links to every
task.

### 8.1 What you need

- **Unity Android modules.** Unity Hub ▸ *Installs* ▸ ⚙ next to your Unity version ▸ *Add
  modules* ▸ **Android Build Support**, with **OpenJDK** and **Android SDK & NDK Tools** ticked.
  You don't need Android Studio: Unity bundles the SDK, `adb` and `keytool`.
- **An Android phone** with Android 8.0 or newer (the app's minimum is API 26), and a USB cable
  that carries data. Some cables only charge.
- **A backend the phone can reach:** your laptop on the same Wi-Fi (8.4) or Railway (section 3).
  A release build needs the HTTPS Railway URL.
- **For Google Play:**
  - a Google account that will own the app for good (not a throwaway);
  - a credit or debit card (not prepaid) for the US$25 fee;
  - a government ID in your legal name;
  - an Android phone for device verification;
  - 12 or more people with Android phones for the closed test.

The bundled tools live under the Unity install. In PowerShell, set a shortcut once per window and
use it in the commands below:

```powershell
$U = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PlaybackEngines\AndroidPlayer"
& "$U\SDK\platform-tools\adb.exe" version      # adb
& "$U\OpenJDK\bin\keytool.exe" -help           # keytool
```

### 8.2 Native dependencies (EDM4U + Google Mobile Ads)

1. Install the Google Mobile Ads plugin and EDM4U: section 5, step 3. To get EDM4U alone (no
   ads plugin yet): *Window ▸ Package Manager ▸ + ▸ Install package by name…* →
   `com.unity.external-dependency-manager`.
2. Set the AdMob App IDs (section 5, step 4) and run **Stack IT ▸ Setup Project**.
3. *File ▸ Build Profiles* ▸ **Android** ▸ *Switch Platform*. The first switch reimports
   everything and can take several minutes.
4. *Assets ▸ External Dependency Manager ▸ Android Resolver ▸ **Resolve***. It downloads the
   Android libraries and writes them into the Gradle templates in `Assets/Plugins/Android/`.
   Run it again every time you add or update a plugin. If things look stale, use *Force Resolve*.
5. **Check it worked.** Open `Assets/Plugins/Android/mainTemplate.gradle`. Between
   `// Android Resolver Dependencies Start` and `End` you should see both groups:
   - ours: `androidx.core`, `androidx.credentials` (two lines), `installreferrer`, `googleid`;
   - Google's: `play-services-ads`, `user-messaging-platform`, `constraintlayout`,
     `lifecycle-process`, `fragment`.

   If Google's are missing, the resolver ran before the ads plugin was imported: Resolve again.
6. Commit what this generated, so other machines build the same:
   - `Packages/manifest.json` and `packages-lock.json`
   - `Assets/GoogleMobileAds/` (if you used the .unitypackage)
   - the new files in `Assets/Plugins/Android/` and `Assets/Plugins/iOS/`
   - `ProjectSettings/AndroidResolverDependencies.xml` and `ExternalDependencyManager*`
   - `Assets/Settings/Build Profiles/`
7. **Turn off the Ads plugin's pre-build resolve.** *Assets ▸ Google Mobile Ads ▸ Settings…* ▸
   *Android settings* ▸ untick **Enable Gradle build pre-processor**. Otherwise every build
   resolves again synchronously, and that can hang the editor forever in *Finishing import*
   (8.13). Everything that step does is already in place: minimum API ≥ 24 (we use 26), the
   Gradle templates, Jetifier, and the resolve you just ran. EDM's background auto-resolution
   keeps the libraries current; after adding or updating a plugin, run *Resolve* yourself
   (step 4) before building.

### 8.3 Player settings to check once

*Edit ▸ Project Settings ▸ Player* ▸ Android tab (the robot icon). **Stack IT ▸ Setup Project**
already sets these: package name `<BUNDLE_ID>`, IL2CPP, ARM64 + ARMv7, minimum API 26,
portrait, and the icon. Check the rest:

| Setting | Where | Value | Why |
|---|---|---|---|
| **Version** | Other Settings ▸ Identification | `0.1.0`, then whatever you like | What users see. |
| **Bundle Version Code** | Other Settings ▸ Identification | `1`, then **+1 for every upload** | Play rejects an upload whose code isn't higher than every earlier upload, on any track. |
| **Target API Level** | Other Settings ▸ Identification | *Automatic (highest installed)*, or *API level 36* or higher | Since 31 Aug 2026, Play requires new apps and updates to target API 36 (Android 16). Google raises this every August. |
| **Target Architectures** | Other Settings ▸ Configuration | ARM64 ticked (ARMv7 too) | Play requires 64-bit. |
| **Allow downloads over HTTP** | Other Settings ▸ Configuration | *Allowed in development builds* | Only to test against your laptop over plain `http://` (8.4). Release builds stay HTTPS-only. |

### 8.4 Run it on your phone (development build against your laptop)

**Prepare the phone (once):**

1. *Settings ▸ About phone* ▸ tap **Build number** 7 times, then enter your PIN. "You are now a
   developer". On Samsung it's under *About phone ▸ Software information*.
2. *Settings ▸ System ▸ Developer options* ▸ turn on **USB debugging**.
3. Plug the phone in. If it asks what the USB connection is for, choose *File transfer*. Accept
   **Allow USB debugging?** and tick *Always allow from this computer*.
4. Check from PowerShell: `& "$U\SDK\platform-tools\adb.exe" devices` should list your phone as
   `<serial>   device`.
   - `unauthorized`: accept the prompt on the phone.
   - Empty list: try another cable or USB port. Samsung phones also need the Samsung USB driver
     on Windows.

**Make the laptop's backend reachable:**

1. From the repo root: `docker compose up -d --build`. The API listens on port 8000.
2. Find the laptop's address: run `ipconfig` and look under the **Wi-Fi** adapter for
   *IPv4 Address*, for example `192.168.1.17`. This is `<LAN IP>`. The phone must be on the
   same Wi-Fi; guest networks usually block devices from seeing each other.
3. Allow port 8000 through the Windows firewall (PowerShell **as administrator**, once):
   ```powershell
   New-NetFirewallRule -DisplayName "Stack IT API (dev)" -Direction Inbound -Protocol TCP -LocalPort 8000 -Action Allow -Profile Private
   ```
   The Wi-Fi network must be set to *Private*: *Settings ▸ Network & internet ▸ Wi-Fi ▸
   <your network> ▸ Network profile type*.
4. **Test from the phone's browser first:** `http://<LAN IP>:8000/health` should show
   `{"status":"ok",…}`. If the browser can't load it, the app can't either.
5. `Assets/_Project/Resources/StackIt/AppConfig.asset` ▸ `apiBaseUrl` = `http://<LAN IP>:8000`.
   Edit it **in Unity** (select the asset in the Project window and change it in the Inspector),
   then *File ▸ Save Project*. If you edit the file in a text editor while Unity is open, Unity
   can write its own copy back over yours, and the build ships the old URL. This is a local
   setting: don't commit it. Set it back before a release build (8.7). Your router can give the
   laptop a different LAN IP another day: check `ipconfig` again if the app suddenly goes
   offline.
   Challenge links shared from this build still say `localhost`, because the server builds them
   from `PUBLIC_BASE_URL` in `docker-compose.yml`.

**Build and run:**

1. *File ▸ Build Profiles* ▸ Android:
   - tick **Development Build**;
   - untick **Build App Bundle (Google Play)**;
   - set **Run Device** to your phone.
2. Click **Build And Run** and save as `unity/StackIt-dev.apk` (APKs there are git-ignored). The
   first build takes 10–30 minutes while IL2CPP compiles and Gradle downloads; later builds are
   much faster. The app opens on the phone by itself.
3. To see the logs: `& "$U\SDK\platform-tools\adb.exe" logcat -s Unity`. A healthy start has no
   `Starting offline` line.

### 8.5 Create your upload keystore (once, keep it forever)

Android apps are signed. Google Play uses **two keys**:

- **Upload key (yours).** It signs the files you upload, and Google checks that each upload comes
  from you.
- **App signing key (Google's).** Google creates and keeps it the first time you upload (*Play App
  Signing*), and uses it to sign what users actually install.

If you lose the upload key, Play Console can reset it (*App integrity ▸ Request upload key
reset*), but that takes days. Back it up properly.

1. *Edit ▸ Project Settings ▸ Player* ▸ Android ▸ **Publishing Settings** ▸ **Keystore
   Manager…**
2. *Keystore… ▸ Create New ▸ Anywhere…* and save it **outside the repo**, for example
   `D:\Keys\stackit-upload.keystore`.
3. Enter a **keystore password** twice.
4. **New Key Values:**
   - *Alias*: `upload`
   - *Password*: a key password (it may be the same as the keystore's)
   - *Validity (years)*: `50`. Google requires it to be valid until at least 2033.
   - Name, organization and country: your real details. Users never see them.
5. Click **Add Key**. If Unity asks whether to use the new keystore for the project, say yes.
6. Back in **Publishing Settings**, fill in:
   - tick **Custom Keystore**;
   - the keystore path and its password;
   - the alias `upload` and its password.
7. **Unity doesn't remember these passwords** after you close the editor. If a build fails with a
   keystore or signing error after reopening Unity, enter them again here.
8. **Back up** the `.keystore` file, both passwords and the alias in a password manager, plus a
   second copy somewhere off this laptop. `*.keystore` and `*.jks` are git-ignored; keep the file
   out of the repo anyway.

Once the custom keystore is set, development builds are signed with it too. That's why Google
sign-in on the builds you install yourself uses the upload key's fingerprint (8.6).

### 8.6 Fingerprints (SHA-1 / SHA-256): what goes where

Print your upload key's fingerprints:

```powershell
& "$U\OpenJDK\bin\keytool.exe" -list -v -keystore "D:\Keys\stackit-upload.keystore" -alias upload
```

The output shows `SHA1: AA:BB:…` and `SHA256: CC:DD:…`. The **app signing key's** fingerprints
only exist after your first upload (8.11). Find them in Play Console ▸ *Test and release ▸ Setup
▸ App integrity* ▸ *Play app signing* ▸ *App signing key certificate*.

| Fingerprint | Goes to | So that |
|---|---|---|
| Upload key **SHA-1** | Google Cloud ▸ an **Android** OAuth client, package `<BUNDLE_ID>` (section 4) | Google sign-in works on builds you install with Build And Run |
| App signing key **SHA-1** | a **second** Android OAuth client with the same package name | Google sign-in works on builds installed from Google Play |
| Both **SHA-256**, upload key first, comma-separated | Railway ▸ `ANDROID_SHA256_FINGERPRINTS` (section 3) | Challenge links open the app (App Links, section 6) |

Each Android OAuth client holds one SHA-1, so make one client per key. The **web** client ID
(`AppConfig.googleWebClientId`) stays the same for both.

### 8.7 Build the release bundle (.aab)

Google Play takes **Android App Bundles** (`.aab`), not APKs.

1. `AppConfig.asset` ▸ `apiBaseUrl` = `<BASE_URL>`: your `https://` Railway URL, not the LAN
   address.
2. `AdsConfig.asset` keeps Google's **test** IDs, with `debugForceEeaConsent` off.
3. **Bump Bundle Version Code** (8.3), and Version if you like. Play refuses a code twice.
4. Build it one of two ways:
   - **With test ads**, for internal and closed testing: *File ▸ Build Profiles* ▸ Android,
     untick **Development Build**, tick **Build App Bundle (Google Play)**, click **Build** and
     save it in `unity/Builds/`.
   - **With real ads**, for the release that goes to production. Once, in PowerShell, set the
     four IDs from your AdMob Android app (they're in the backup file next to the keystore):

     ```powershell
     setx ADMOB_ANDROID_APP_ID "ca-app-pub-XXXXXXXXXXXXXXXX~XXXXXXXXXX"
     setx ADMOB_ANDROID_REWARDED "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX"
     setx ADMOB_ANDROID_INTERSTITIAL "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX"
     setx ADMOB_ANDROID_BANNER "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX"
     ```

     Close Unity and Unity Hub and open them again, so they see the variables. Enter the
     keystore passwords (8.3), then *Stack IT ▸ Build Android Release*. It refuses to build if
     an ID is missing, is one of Google's test IDs, or is in the wrong place (an app ID, with
     `~`, where an ad unit, with `/`, goes). It builds the release bundle with the real IDs,
     then puts the test IDs back, so `git status` shows no ad settings changed. Only this menu
     reads the variables: every other build keeps the test ads.
5. Builds are git-ignored wherever you save them (`*.aab`).
6. Play Console may warn that the bundle "contains native code" without **debug symbols**. That's
   a warning, not a blocker. To get readable crash reports, set the Android build profile's
   *Debug Symbols* option to include symbols in the bundle and rebuild. The label varies between
   Unity 6 versions.

### 8.8 Google Play developer account (once)

1. Go to https://play.google.com/console/signup and sign in with the Google account that will
   own the app.
2. **Account type:**
   - **Personal**: you as an individual. Pick this unless you have a registered company. Personal
     accounts must pass the closed-test requirement (8.11) before their first public release.
   - **Organization**: needs a company and a D-U-N-S number, and verification takes longer.
3. Fill in:
   - the public **developer name** shown on the store;
   - a contact email and phone number;
   - your **legal name and address** exactly as on your ID.
4. Pay the **one-time US$25** fee. Prepaid cards aren't accepted.
5. **Verify your identity** with a government ID. This can take a few days. If the details turn
   out invalid, the fee isn't refunded, so type them carefully.
6. **Verify an Android device.** Install the *Google Play Console* app on an Android phone, sign
   in with the same account, and follow the prompt.
7. Confirm the email and phone codes when asked.

### 8.9 Create the app

Play Console ▸ *Home* ▸ **Create app**:

- **App name**: `Milli: Stack Tower Duel` (30 characters max; you can change it later).
- **Default language**: English (United States). You add the Spanish listing later, under store
  listing translations.
- **App or game**: *Game*.
- **Free or paid**: *Free*. A free app can never become paid.
- Tick the declarations (Developer Program Policies, US export laws) → **Create app**.

You don't type the package name here. **Your first upload fixes it forever** (`<BUNDLE_ID>`,
`game.milli.com` by default). If Play says that name is already taken, choose another one, for
example `com.<yourname>.milli`:

1. Set it in `AppConfig.asset` ▸ `bundleId` and in Railway ▸ `APP_BUNDLE_ID`.
2. Run **Stack IT ▸ Setup Project**.
3. Update the Google OAuth clients to the new package name.

### 8.10 Dashboard: "Set up your app"

The app's **Dashboard** lists every required task, and each opens its page. Most live under
*Policy and programs ▸ App content* and *Grow users ▸ Store presence*. Answers for Milli:

| Task | Answer |
|---|---|
| **Privacy policy** | `<BASE_URL>/privacy` |
| **App access** | *All functionality is available without special access*: guest accounts, no login wall. |
| **Ads** | *Yes, my app contains ads.* |
| **Content rating** | Start the IARC questionnaire with your email; category *Game*. Milli has no violence, sexual content, gambling, drugs or bad language. Players see each other's **display names** on leaderboards, friends and challenges, but there is **no chat, voice or photo sharing**. There are no real-money purchases: skins cost in-game coins. Answer the user-interaction questions with those facts. |
| **Target audience** | Tick **13–15, 16–17, 18+**. Don't include under-13: that brings the Families policy and certified-ads-only rules. If asked whether the app could unintentionally appeal to children, answer honestly. The look is bright and casual, so expect a follow-up question. |
| **Data safety** | See the table below. |
| **Advertising ID** | *Yes*, the app uses the advertising ID, for **Advertising or marketing**. The Google Mobile Ads SDK adds the `AD_ID` permission. |
| **Government apps** | No. |
| **Financial features** | *My app doesn't provide any financial features.* |
| **Health** | No health features. |
| **News app** | No. |
| **Store settings** | Category **Casual**. Contact email = `SUPPORT_EMAIL`. Website = `<BASE_URL>`, which AdMob uses to find `app-ads.txt`. |
| **Main store listing** | See the store listing table below. |

**Data safety.** It covers our backend **and** the AdMob SDK. Check Google's own list for the
SDK's part: https://developers.google.com/admob/android/privacy/play-data-disclosure.

| Question / data type | Answer | Purpose | Notes |
|---|---|---|---|
| Collects or shares user data? | Yes | | |
| Encrypted in transit | Yes | | HTTPS everywhere |
| Users can request deletion | Yes | | In-app (Settings) and `<BASE_URL>/delete-account` |
| Device or other IDs | Collected, **shared** (with Google for ads) | Advertising, app functionality, fraud prevention | Advertising ID (AdMob); random device ID for the guest account |
| App activity: app interactions / other actions | Collected | App functionality, analytics, advertising | Runs and scores; ad interactions (AdMob) |
| App info and performance: diagnostics | Collected | Analytics, advertising | AdMob SDK diagnostics |
| Approximate location | Collected, **shared** | Advertising | AdMob derives it from the IP address |
| Personal info: name | Collected | App functionality | The display name the player picks |
| Personal info: email | Collected, only when Google/Apple is linked | Account management | |
| **Account deletion URL** (separate question) | `<BASE_URL>/delete-account` | | |

**Store listing assets:**

| Asset | Requirement | Where to get it |
|---|---|---|
| App name | ≤ 30 characters | `Milli: Stack Tower Duel` |
| Short description | ≤ 80 characters | For example: "Stack blocks, beat your friends' scores, one tap at a time." |
| Full description | ≤ 4000 characters | What the game is, duels and challenge links, the two looks, free with ads |
| **App icon** | 512 × 512 PNG, 32-bit, ≤ 1 MB | `store/icon-512.png` (see below) |
| **Feature graphic** | Exactly 1024 × 500, JPG or 24-bit PNG (no transparency) | `store/feature-graphic.png` |
| **Phone screenshots** | 2–8 images, JPG/PNG, each side 320–3840 px | Take them on the phone (power + volume down), portrait: a run, a near miss ("0.4 mm from perfect"), a duel, the daily tower, the two looks. Then frame them (below): most phones capture 20:9, and Play refuses anything longer than 2:1. |

`backend/scripts/generate_store_art.py` draws the logo (`store/logo-neon.png`, `logo-pop.png`), the icon and the feature graphic, and frames screenshots under a caption on a 1080 × 1920 card:

```sh
cd backend
uv run python -m scripts.generate_store_art --screens "D:\path\to\phone-screenshots"          # store/screenshots-en/
uv run python -m scripts.generate_store_art --screens "D:\path\to\phone-screenshots" --lang es # store/screenshots-es/
```

Blocky sits on each card's phone, reacting to the caption (`POSES` in the script). Every Blocky in
the art is a still baked from the game's own glass and faces, in `backend/scripts/blocky/`. After
changing Blocky's look or faces, bake them again, then run the art scripts:

```sh
cd unity
python Tools/blocky_stills.py   # needs numpy, Pillow and the .NET SDK
cd ../backend
uv run python -m scripts.generate_app_icons && uv run python -m scripts.generate_store_art
```

### 8.11 Testing tracks, then production

| Track | Who | Review | Use it for |
|---|---|---|---|
| **Internal testing** | Up to 100 testers you list by email | None: available within minutes | Your first upload and quick checks |
| **Closed testing** | Testers you list by email or Google Group | Yes | The required 12-tester / 14-day test |
| Open testing | Anyone with the link | Yes | Optional |
| **Production** | Everyone | Yes | The public release |

**Internal testing (your first upload):**

1. *Test and release ▸ Testing ▸ **Internal testing*** ▸ *Testers* tab ▸ *Create email list*.
   Add the Gmail addresses of your testers, yourself included, then *Save*. Copy the
   **"Join on the web"** link.
2. *Releases* tab ▸ **Create new release**.
3. The first time, Play asks how to sign the app: choose **Use Google-generated key**. This is
   Play App Signing (8.5).
4. Upload `StackIt.aab`. The release name fills itself in (the version). Add release notes, for
   example "First test build".
5. Click *Next*. Fix any **errors**; **warnings** (debug symbols, for example) can stay.
6. Click *Save and publish*, or *Start rollout to Internal testing*.
7. Testers open the join link on their phone, signed in with the listed Google account, tap
   *Accept*, then *Download it on Google Play*. It can take a few minutes to show up.
8. **Uninstall your Build And Run copy first.** It's signed with the upload key and Play's copy
   with the app signing key; Android refuses to install one over the other ("App not
   installed").
9. Now that the app signing key exists, finish 8.6:
   - add the second Android OAuth client in Google Cloud;
   - set `ANDROID_SHA256_FINGERPRINTS` on Railway;
   - check `curl https://<HOST>/.well-known/assetlinks.json`.

**Closed testing (required for new personal accounts):**

1. *Test and release ▸ Testing ▸ **Closed testing*** ▸ use the default *Closed testing* track or
   *Create track*.
2. *Countries / regions*: add the countries where your testers are.
3. *Testers*: an email list or a Google Group. You need **at least 12 people opted in for 14 days
   in a row**. Someone who opts out and back in restarts the count. Recruit 15–20 people for a
   margin: friends and family with Android phones.
4. *Create new release* ▸ *Add from library* (reuse the internal build) or upload a newer
   `.aab` ▸ *Next* ▸ *Save*.
5. *Publishing overview* ▸ **Send changes for review**. The first review of a new account can
   take several days.
6. Once approved, send testers the join link. They opt in, install and **actually play**.
7. During the 14 days:
   - collect feedback (a short form or group chat is enough);
   - ship at least one update (bump Bundle Version Code, upload to the same track).

   The production application asks about both.
8. After 14 days, the Dashboard shows **Apply for production**. The form has three parts:
   - your closed test: how you recruited testers, whether they used every feature, and what they
     said and how you collected it;
   - your game: audience, what makes it worth playing, and expected installs in the first year;
   - your readiness: what you changed after testing, and why you think it's ready.

   Answer in your own words. Google replies after review, which can take several days.

**Production:**

1. Build the production bundle:
   - real AdMob unit IDs in `AdsConfig`;
   - a higher Bundle Version Code;
   - `apiBaseUrl` = `<BASE_URL>`.
2. *Test and release ▸ **Production*** ▸ *Countries / regions* ▸ add the countries to launch in.
3. *Create new release* ▸ upload the `.aab` (or *Add from library*) ▸ release notes.
4. Optionally use a **staged rollout**, for example 20 %: only part of your users get it first.
   Watch *Monitor and improve ▸ Android vitals* (crashes and ANRs) for a day or two, then raise
   it to 100 %.
5. *Publishing overview* ▸ **Send changes for review**. Turn on **Managed publishing** if you want
   to choose the moment an approved release goes live.
6. After it's live:
   - in AdMob, link the Android app to its Play listing (*Apps ▸ App settings ▸ Link to app
     store*), so `app-ads.txt` gets verified (section 5, step 8);
   - test a challenge link on a phone that doesn't have the app yet: the Play Store should open,
     and after install the challenge should load (install referrer, section 6).

### 8.12 Every update after that

1. Bump **Bundle Version Code** (and Version).
2. Build the `.aab` (8.7) and upload it to Internal testing. A bundle you'll **promote to
   production** must be the real-ads one (*Build Android Release*): promoting ships the very
   same bundle, test ads included.
3. Check it on your phone, then promote it: on the release, *Promote release ▸ Production*, or
   create a new production release from the library.
4. Every August Google raises the required target API level. Update Unity (or the Android SDK it
   uses) and rebuild before the deadline Play Console shows.

### 8.12b Sharing straight to TikTok and Instagram Stories (optional)

The results screen has **TikTok** and **Instagram Stories** buttons next to Challenge. Until the
keys below are set, both fall back to the normal share sheet, which is safe to ship.

1. **Instagram Stories** (Android and iOS):
   1. At [developers.facebook.com](https://developers.facebook.com) create an app.
   2. Copy its **App ID**.
   3. In Unity, select `Assets/_Project/Resources/StackIt/AppConfig` and paste the App ID into
      *Facebook App ID*.
   4. iOS only: add `instagram-stories` to `LSApplicationQueriesSchemes` in `BuildHooks.cs`. (The
      code opens Instagram without checking first, so it works without this, but Apple recommends
      declaring it.)
2. **TikTok:**
   - On Android, the button already opens TikTok directly with the challenge card.
   - Adding TikTok's own hashtag and green-screen mode needs TikTok's **Share Kit SDK**:
     1. Register at [developers.tiktok.com](https://developers.tiktok.com).
     2. Add the Share Kit product.
     3. Put the **client key** into AppConfig's *TikTok Client Key*.
     4. Ask for the SDK integration: it isn't added yet, and the key is unused until it is.
   - On iOS, TikTok uses the share sheet until then.

### 8.13 Troubleshooting

| Symptom | Cause and fix |
|---|---|
| No *Assets ▸ External Dependency Manager* menu | EDM4U isn't installed: 8.2, step 1. |
| Build never finishes; Unity's title bar says *Importing … Finishing import (busy for …)*; `Logs/Editor.log` stops at `Resolving Android Gradle dependencies.` | The Ads plugin's pre-build step resolves dependencies synchronously and deadlocks with the asset import. End Unity in Task Manager, reopen it, then do 8.2 step 7 and build again. |
| Log says `Starting offline: offline` | Nothing answered at `apiBaseUrl`. Check the port (8000), that Docker is up, the firewall rule and Wi-Fi profile (8.4), and test `/health` in the phone's browser. |
| App closes right after launch; logcat mentions the Google Mobile Ads **application ID** | Set the Android App ID: *Assets ▸ Google Mobile Ads ▸ Settings…* (section 5, step 4). |
| Gradle error: `Duplicate class …` | Two copies of a library. *Force Resolve*, and make sure there's only one EDM4U (section 5, step 3). |
| Gradle error: `2 files found with path 'META-INF/kotlinx_coroutines_core.version'` | *Assets ▸ Google Mobile Ads ▸ Settings…* ▸ enable *kotlinx.coroutines packaging option*. |
| Build fails with a keystore or signing error after reopening Unity | Unity forgot the passwords: re-enter them in *Publishing Settings* (8.5, step 7). |
| `adb devices` is empty or `unauthorized` | 8.4, "Prepare the phone", step 4. |
| "App not installed" when installing from Play | A sideloaded build with another signature is on the phone: uninstall it first. |
| Play: version code already used | Bump *Bundle Version Code* (8.3). |
| Play: target API level too low | *Target API Level* = Automatic or 36+ (8.3). |
| Play: package name already in use | Choose another package name (8.9). |
| Google sign-in works on your own build but not from Play, or the other way round | That build's signing key SHA-1 isn't in Google Cloud: add the matching Android OAuth client (8.6). |
| Challenge links open the browser, not the app | `assetlinks.json` lacks that build's SHA-256, or the app isn't installed from that signature (8.6, section 6). |

## 9. iOS and the App Store, step by step

Written for a first App Store release from a Windows PC, with **no Mac**. Unity Build Automation
builds the app on Unity's Mac machines from your GitHub repository, and a script uploads the result
to Apple. Follow this section top to bottom.

Plan on **one to three weeks**. Apple's enrollment can take a few days, your first builds will
probably need fixes, and each review can take a day or more. Apple has no equivalent of Google's
12-tester closed test.

Three things are different from Android, and they shape everything below:

- **Only what's pushed to GitHub gets built.** Local changes on your PC don't exist for the cloud.
- **TestFlight is your only way onto an iPhone.** There's no cable and no live log, so every try
  costs a build and its Mac minutes.
- **No iOS build of Milli has been made yet.** The native code in `Assets/Plugins/iOS/` has never
  been compiled, and neither the cloud setup nor the upload script has run on Unity's machines.
  Expect fixes in 9.8.

App Store Connect and Unity Dashboard menus get renamed from time to time. If a path below doesn't
match, use their search. Apple's rules below were checked in November 2026; before uploading, read
[Upcoming Requirements](https://developer.apple.com/news/upcoming-requirements/).

### 9.1 What you need

| What | Notes |
|---|---|
| **Apple Developer Program** | US$99 per year (9.2). A free account can't do this: the app uses Sign in with Apple and Associated Domains. |
| **Unity Build Automation** | Part of Unity Cloud, with the Unity account you already use (9.7). |
| **The GitHub repository** | Unity reads it with a token you create (9.7). |
| **OpenSSL** | Already on your PC: use **Git Bash**, not PowerShell, for the commands below. |
| **An iPhone** | iOS 15 or newer, with Apple's **TestFlight** app installed. |
| **The backend on Railway** | Section 3. The iPhone build talks to your HTTPS `<BASE_URL>`. |

**What the builds cost.** Unity's free tier includes [100 Mac build minutes a
month](https://unity.com/products/pricing-updates). After that it's pay-as-you-go: Unity's pricing
listed Standard Mac at [US$0.07 per minute](https://unity.com/products/gaming-services/pricing), so
check the current rate. Past the free minutes you need a card on file, or builds stop. A first
IL2CPP build is slow, so expect it to use a large share of the free minutes. Turn **auto-build
off** (9.7), so that a push doesn't spend minutes by accident.

You don't need Xcode, CocoaPods or a macOS install: Unity's machines have them.

### 9.2 Join the Apple Developer Program (once)

1. Turn on **two-factor authentication** on the Apple account that will own the app.
2. Go to https://developer.apple.com/programs/enroll/ and sign in.
3. Choose the entity type:
   - **Individual**: you. The App Store shows **your legal name** as the seller.
   - **Organization**: needs a legal entity and a free D-U-N-S number, and takes longer. The
     company's name is shown instead.
4. Pay **US$99 per year** (local prices vary). If the membership lapses, the app leaves the store.
5. Wait for the confirmation email. It's often quick, but it can take days.
6. Note your **Team ID**: https://developer.apple.com ▸ *Account* ▸ *Membership details*.

### 9.3 App ID, Sign in with Apple key and deep links

**The App ID:**

1. https://developer.apple.com/account ▸ *Certificates, IDs & Profiles* ▸ *Identifiers* ▸ **+** ▸
   *App IDs* ▸ *App*.
2. *Description*: `Milli`. *Bundle ID*: **Explicit**, `<BUNDLE_ID>` (`game.milli.com`). If Apple
   says it's taken, follow 8.9's steps for a new ID.
3. Under *Capabilities*, tick **Associated Domains** and **Sign In with Apple**. *Register*.

**The Sign in with Apple key.** The server uses it to end a player's Apple sign-in when they delete
their account, which [Apple asks of apps](https://developer.apple.com/help/app-review/guideline-reference/5-1-1-account-deletion).

1. *Certificates, IDs & Profiles* ▸ *Keys* ▸ **+**.
2. *Name*: `Milli Sign in with Apple`. Tick **Sign in with Apple** ▸ *Configure* ▸ choose your App
   ID ▸ *Save* ▸ *Continue* ▸ *Register*.
3. **Download the `.p8` file. Apple only lets you do this once.** Keep it outside the repo, with a
   backup. Note the **Key ID**.

**On Railway** (section 3):

1. Set `APPLE_TEAM_ID`, `APPLE_KEY_ID` and `APPLE_PRIVATE_KEY`. For the last one, paste the text of
   the `.p8` file.
2. Check that `APP_BUNDLE_ID` and `APP_URL_SCHEME` are set.
3. Deploy. The migration that stores Apple's token runs by itself.
4. Check the file Apple reads for challenge links:
   ```sh
   curl -i https://<HOST>/.well-known/apple-app-site-association
   # 200, appIDs ["<TEAM_ID>.<BUNDLE_ID>"]
   ```

### 9.4 Google sign-in and AdMob for iOS

1. **Google sign-in** (section 4):
   - create the **iOS** OAuth client with bundle ID `<BUNDLE_ID>`;
   - add its ID to Railway's `GOOGLE_CLIENT_IDS`, after the web client's;
   - paste it into `AppConfig.asset` ▸ `googleIosClientId`, which is empty today, and commit it.
     It isn't a secret.
2. **AdMob** (section 5): create the **iOS app** and its three ad units, and publish the **IDFA
   explainer**. Keep the IDs for 9.11: test builds use Google's test IDs, and the real ones never
   go in the repo.

### 9.5 Create the signing files on Windows

Apple needs two files to trust a build: a **certificate** (`.p12`, it proves it's you) and a
**provisioning profile** (`.mobileprovision`, it says which app that certificate may sign). The
usual instructions assume a Mac. These steps do the same with OpenSSL, in **Git Bash**:

1. Make a folder **outside the repo**, and create your private key and a request for Apple. Put your
   own email, name and country code in the second command:
   ```sh
   mkdir -p /d/Keys/milli-ios && cd /d/Keys/milli-ios
   MSYS_NO_PATHCONV=1 openssl req -new -newkey rsa:2048 -nodes -keyout milli-dist.key -out milli-dist.csr \
     -subj "/emailAddress=you@example.com/CN=Your Name/C=MX"
   ```
   Keep `MSYS_NO_PATHCONV=1`: without it, Git Bash rewrites the `/emailAddress…` part as a Windows
   path and the command fails.
2. In the browser: *Certificates, IDs & Profiles* ▸ *Certificates* ▸ **+** ▸ **Apple Distribution**
   ▸ *Continue*. Upload `milli-dist.csr`, then **download** `distribution.cer` into the same folder.
3. Turn it into a `.p12`. It asks you to choose a password, which you'll need in 9.7:
   ```sh
   openssl x509 -inform DER -in distribution.cer -out distribution.pem
   openssl pkcs12 -export -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg sha1 \
     -inkey milli-dist.key -in distribution.pem -out milli-dist.p12
   ```
   Keep the algorithm flags: macOS can refuse newer OpenSSL formats, and the build runs on macOS.
4. In the browser: *Profiles* ▸ **+** ▸ under *Distribution*, **App Store Connect** ▸ *Continue*.
   Choose your App ID, then your certificate. *Name*: `Milli App Store`. *Generate*, then
   **download** the `.mobileprovision` file.
5. Back up the whole folder somewhere private. `milli-dist.key` and `.p12` are as sensitive as the
   Android keystore. `*.p8`, `*.p12` and `*.mobileprovision` are git-ignored, but keep them out of
   the repo anyway.

If you change the App ID's capabilities later, generate the profile again and upload the new one.
Distribution certificates expire after about a year: repeat steps 1–4 when yours does.

### 9.6 Create the app in App Store Connect

At https://appstoreconnect.apple.com:

1. *Apps* ▸ **+** ▸ *New App*:
   - *Platforms*: iOS;
   - *Name*: `Milli: Stack Tower Duel` (30 characters max, and it must be free on the store);
   - *Primary language*: English (U.S.);
   - *Bundle ID*: `<BUNDLE_ID>`;
   - *SKU*: `milli-ios`, for your records only;
   - *User access*: Full Access.
2. Open *App Information* and note the **Apple ID**: digits only. This is `ASC_APP_ID` in 9.7.
3. **Create the API key that uploads builds.** *Users and Access* ▸ *Integrations* ▸ *App Store
   Connect API* ▸ *Team Keys* ▸ **+**. *Name*: `Milli uploads`. *Access*: **App Manager**.
   *Generate*.
4. **Download the `.p8` file (once only).** Note the **Key ID** and the **Issuer ID** at the top of
   the page. This is a different key from the one in 9.3.
5. Turn the key into one line for 9.7, in Git Bash:
   ```sh
   base64 -w0 AuthKey_XXXXXXXXXX.p8
   ```
6. **EU trader status** (*Business*, or the prompt on your first submission). [Everyone has to
   declare it](https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/),
   even if you don't sell in the EU. Without it, the app isn't shown there. Traders have their
   address, phone and email **shown publicly** in the EU. An app that earns from ads may well count
   as a trader, so read Apple's page before you answer. If you're an individual, use a contact
   address you're happy to publish.
7. **Agreements:** a free app needs only the standard agreement. You don't need the Paid Apps
   Agreement or banking details here: ad income comes through AdMob, and Milli sells nothing for
   real money.

### 9.7 Set up Unity Build Automation

**Push your work first.** Merge to `main` and push. Build Automation builds what's on GitHub.

**Connect the project:**

1. Go to https://cloud.unity.com and sign in with your Unity account. Create a project called
   `Milli` if you don't have one.
2. Open *DevOps* ▸ *Build Automation* and start the setup.
3. *Source control*: choose **GitHub**, [create a token](https://docs.unity.com/en-us/build-automation/get-started-with-build-automation/connect-your-version-control-system)
   (the `repo` scope for a private repository), and pick this repository.

**Create the [build target](https://docs.unity.com/en-us/build-automation/basic-build-configuration/overview):**

| Setting | Value | Why |
|---|---|---|
| **Platform** | iOS | |
| **Target name** | `iOS App Store` | |
| **Branch** | `main` | |
| **Project subfolder** | `unity` | The Unity project isn't at the repo's root. |
| **Unity version** | Auto-detect (reads `ProjectVersion.txt`) | If 6000.5.10f1 isn't offered, choose the closest 6000.5. |
| **Builder OS** | macOS Tahoe | |
| **Xcode version** | **26.5.0**, or the [newest 26.x listed](https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions) | Required for iOS targets. Apple [needs Xcode 26 or later](https://developer.apple.com/news/upcoming-requirements/). Avoid 27 for now: [Google warns](https://developers.google.com/admob/ios/quick-start) that its builds fail with CocoaPods. |
| **Bundle ID** | `<BUNDLE_ID>` | |
| **Auto-build** | **Off** | Each build spends Mac minutes. |

**Credentials:** upload `milli-dist.p12`, its password, and the `.mobileprovision` file (9.5).

**Advanced settings**, on the build target:

| Setting | Value | What it does |
|---|---|---|
| **Pre-Export Method** | `CloudBuildSetup.PreExport` | Makes the font atlases and string tables, sets iPhone-only, numbers the build. Without it, text is missing. |
| **Post-Build Script** | `unity/Tools/upload-to-testflight.sh` | Uploads the build to Apple. |

**Environment variables**, on the build target:

| Variable | Value |
|---|---|
| `ASC_APP_ID` | The Apple ID from 9.6 |
| `ASC_KEY_ID` | The upload key's Key ID |
| `ASC_ISSUER_ID` | The Issuer ID |
| `ASC_KEY_BASE64` | The one line from 9.6, step 5 |

**You don't need a Team ID, version bump or build number by hand.** The credentials carry the team,
and `CloudBuildSetup` gives each build Build Automation's own number, which only goes up. The
**Version** the player sees comes from Player Settings, shared with Android.

### 9.8 Run the first build

1. Start a build of `iOS App Store` from the dashboard.
2. Open its **log**. The steps run in this order, so the place it stops tells you what broke:

   | Stage | If it fails here |
   |---|---|
   | Clone and Unity import | The repo or subfolder setting, or the Unity version |
   | `CloudBuildSetup.PreExport` | Fonts or strings. Search the log for `Stack IT`. |
   | Unity export, IL2CPP | C# that only compiles for iOS |
   | CocoaPods, then Xcode | Native code or signing. See 9.14. |
   | Post-build script | The upload. Search the log for `App Store Connect`. |

3. **Compile errors in `StackItNative.mm` or `StackItSignIn.mm` are likely** on this first build,
   because nothing has compiled them yet. Fix them in `Assets/Plugins/iOS/` on your PC, push, and
   build again. The Xcode project is regenerated each time, so `BuildHooks.cs` and
   `Assets/Plugins/iOS/` are the only places fixes last.
4. A good build ends with `Uploaded to App Store Connect`. Apple then processes it: usually minutes,
   sometimes longer. An email tells you if Apple's checks found a problem (9.14).

### 9.9 Test on TestFlight

1. App Store Connect ▸ your app ▸ *TestFlight*.
2. **Internal testing:** create a group and add people from your team, up to 100. There's no review,
   and they get the build as soon as it's processed. Add yourself.
3. On the iPhone, open **TestFlight**, accept the invitation and install Milli.

Go through this on the phone. Each item is something only a real iPhone can prove:

- [ ] The game starts and plays smoothly, and Blocky looks right (`docs/TODO-PHONE-ONLINE.md`).
- [ ] **Text** is everywhere, in English and Spanish, and never a raw key like `blocky.new_best.0`.
- [ ] **Online:** coins and the leaderboard load. If not, the app is offline: check `apiBaseUrl` and
      open `<BASE_URL>/health` in Safari.
- [ ] **Consent:** on a fresh install the consent form appears if required, then the tracking
      prompt, once.
- [ ] **Test ads** load: rewarded continue, interstitial, banner.
- [ ] **Sign in with Apple** and **Google**, including linking to a guest account.
- [ ] **Sharing:** the challenge card opens the share sheet, and **Save Image** works.
- [ ] **Challenge link:** paste `<BASE_URL>/c/<CODE>` into Notes and tap it. The app opens with that
      challenge. Apple's CDN can delay this by hours after a change to `APPLE_TEAM_ID`.
- [ ] **Haptics** on a perfect drop.
- [ ] **Delete account** with Apple linked. On Railway, the logs must not show `Could not revoke`,
      and Milli disappears from *Settings ▸ your name ▸ Sign-In & Security ▸ Sign in with Apple*.

Without a live log, the backend's logs on Railway are your best view of what the app did.

**External testing** (optional): friends who aren't on your team, up to 10,000, by email or a public
link. The first build of each version needs a short **Beta App Review**. Fill in *Test Information*
(feedback email, what to test). Builds expire after 90 days.

### 9.10 What the project already handles

So you don't have to redo these by hand. They're in the code, and 9.9 is where you prove them:

| Requirement | Where |
|---|---|
| Associated Domains, Sign in with Apple, tracking text | `BuildHooks.cs` |
| **Save Image** text (`NSPhotoLibraryAddUsageDescription`), so sharing doesn't crash | `BuildHooks.cs` |
| Encryption answer (`ITSAppUsesNonExemptEncryption` = false), so builds don't wait on it | `BuildHooks.cs` |
| iPhone only: no iPad screenshots or layout | `ProjectSetup.cs`, run by the pre-export method |
| Apple's token revoked on account deletion | `backend/app/clients/apple_tokens.py` |
| App Store screenshot size | `generate_store_art.py --app-store` (9.11) |

**The encryption answer is your legal statement to Apple.** It says that Milli only encrypts through
HTTPS, which [normally counts as exempt](https://developer.apple.com/forums/thread/765911). If you
ever add your own encryption, change it in `BuildHooks.cs`.

**What Sign in with Apple doesn't cover yet.** Tokens are revoked for players who signed in with a
build containing this code, and only while the Railway key is set. Apple also suggests a
server-to-server notification endpoint for changes on Apple's side, which isn't built.

### 9.11 Listing, privacy and submission

**Build the release version.** Add the real AdMob IDs to the build target's **Environment
variables** (they never go in the repo), then start a build:

| Variable | Value |
|---|---|
| `ADMOB_IOS_APP_ID` | The iOS App ID (`ca-app-pub-…~…`) |
| `ADMOB_IOS_REWARDED` | The rewarded unit ID |
| `ADMOB_IOS_INTERSTITIAL` | The interstitial unit ID |
| `ADMOB_IOS_BANNER` | The banner unit ID |

With `ADMOB_IOS_APP_ID` set, the build fails if any of the other three is missing, so a release
can't ship with test ads. Remove the variables to go back to test builds. Don't tap your own real
ads: add your iPhone as a test device in AdMob.

**Version page** (your app ▸ *App Store* ▸ the version):

| Field | Value |
|---|---|
| **Screenshots** | At least one set, see below |
| **Promotional text** | ≤ 170 characters; you can change it without a new build |
| **Description** | ≤ 4000 characters: the game, duels and challenge links, the two looks, free with ads |
| **Keywords** | ≤ 100 characters, comma-separated: `stack,tower,blocks,duel,arcade,one tap` |
| **Support URL** | `<BASE_URL>/privacy`, which shows the support email |
| **Marketing URL** | Optional: `<BASE_URL>` |
| **Copyright** | `2026 <your legal name>` |
| **Build** | Choose the release build |

**Screenshots.** [Apple needs exact sizes](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/),
so the Play cards in `store/screenshots-*` won't upload. One iPhone set of 1 to 10 images is enough.
Take them on any iPhone (side button + volume up), put them in a folder on your PC, then:

```sh
cd backend
uv run python -m scripts.generate_store_art --screens "D:\path\to\iphone-screenshots" --app-store           # store/screenshots-ios-en/
uv run python -m scripts.generate_store_art --screens "D:\path\to\iphone-screenshots" --app-store --lang es # store/screenshots-ios-es/
```

These cards are 1320 × 2868, the 6.9-inch size. Show the same moments as Google Play: a run, a near
miss, a duel, the daily tower, the two looks. Add the Spanish listing too (*Add Language* at the top
of the version page).

**App Information:**

| Field | Value |
|---|---|
| **Subtitle** | ≤ 30 characters, for example `One tap. Stack to the sky.` |
| **Category** | Primary *Games* ▸ *Casual* |
| **Content rights** | Answer honestly. Milli's art and music are your own, and its fonts are open-licensed. |
| **Age rating** | Fill in the [questionnaire](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions), then accept the result (4+, 9+, 13+, 16+ or 18+). The facts: no violence, sexual content, gambling, drugs or bad language; **ads** are shown; players see each other's display names on leaderboards, friends and challenges; **no chat, voice or photo sharing**. It now includes questions about social features, so answer those with the same facts. |

**Pricing and Availability:** *Free*. Under countries, **remove China mainland**: games there need a
government licence.

**App Privacy** ▸ *Privacy Policy URL*: `<BASE_URL>/privacy`. It must match the Android Data safety
form (8.10). Check Google's list for the SDK's part:
https://developers.google.com/admob/ios/privacy/data-disclosure.

| Data type | Used for | Linked to the user | Tracking |
|---|---|---|---|
| Identifiers ▸ **Device ID** | Third-party advertising | Yes | **Yes** |
| Identifiers ▸ **User ID** | App functionality | Yes | No |
| Usage Data ▸ **Advertising Data** | Third-party advertising | Yes | **Yes** |
| Usage Data ▸ **Product Interaction** | App functionality, analytics | Yes | No |
| Location ▸ **Coarse Location** | Third-party advertising (AdMob, from the IP address) | No | **Yes** |
| User Content ▸ **Gameplay Content** | App functionality, analytics | Yes | No |
| Diagnostics ▸ **Performance Data** | Analytics, third-party advertising | No | No |
| Contact Info ▸ **Name** | App functionality (the display name) | Yes | No |
| Contact Info ▸ **Email Address** | App functionality, only if Apple or Google is linked | Yes | No |

**App Review Information:**

- *Sign-in required*: **no**.
- *Contact*: your name, phone and email.
- *Notes*, ready to paste:

  > Milli needs no login: you play as a guest from the first launch. Sign in with Apple and Google
  > are optional, to keep progress across devices (Settings). Account deletion is in Settings and
  > at <BASE_URL>/delete-account. There are no real-money purchases: skins cost in-game coins. Ads
  > are served by Google AdMob; the tracking prompt appears on first launch, after the consent form
  > where one is required. To test a challenge, tap the challenge link on Home and enter a code
  > from Share on the results screen.

**Submit:**

1. *Version Release*: choose **Manually release this version** for your first launch, so you pick
   the moment.
2. *Add for Review* ▸ *Submit*.
3. Review usually takes a day or two. A rejection arrives with a reason in *App Review*. Fix it, run
   a new build if the code changed, and resubmit.
4. Once approved, press **Release**.

### 9.12 After release

1. Copy the app's address, for example `https://apps.apple.com/app/id1234567890`. The number is the
   Apple ID from 9.6.
2. Set Railway's `IOS_APP_STORE_URL`. Landing pages then offer the App Store, and iPhones without
   the app go there.
3. Put the same address in `AppConfig.asset` ▸ `iosAppStoreUrl`, and commit it for the next build.
4. In AdMob, link the iOS app to its store listing, so `app-ads.txt` gets verified (section 5,
   step 8).
5. On an iPhone without the app, open a challenge link: the App Store should open. iOS has no
   install referrer, so after installing, the player types the code (section 6).

### 9.13 Every update after that

1. Raise **Version** in *Player Settings* and push to `main`. Apple needs a new version number for
   each release; TestFlight builds within a version can share it.
2. Remove the `ADMOB_IOS_*` variables, build, and test on TestFlight.
3. Add the variables back and build the release.
4. Create the new version on the App Store page, choose the build, and submit.
5. Once a year, Apple raises the required Xcode (usually in spring). Watch
   [Upcoming Requirements](https://developer.apple.com/news/upcoming-requirements/), and choose a
   newer Xcode on the build target. [Unity lists the versions it offers](https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions),
   and removes each after about 15 months.
6. Renew the certificate and profile when they expire (9.5).

CocoaPods' central repository [goes read-only on 2 December
2026](https://blog.cocoapods.org/CocoaPods-Specs-Repo/). Existing builds keep working, but pods
there get no new versions. Before updating the Ads SDK, check that the release you want is still
installable this way.

### 9.14 Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Build can't find the project | *Project subfolder* must be `unity` (9.7). |
| `Pre-Export Method` not found | The name must be `CloudBuildSetup.PreExport`, and `unity/Assets/Editor/CloudBuildSetup.cs` must be on the branch you build. |
| Post-build script not found | Check that `unity/Tools/upload-to-testflight.sh` is on the branch. The path starts at the repo's root. |
| Build fails with `The ad unit … is from another AdMob account than the App ID …` | `AdsConfig` has real iOS units but the build would keep Google's test App ID, so no ad would ever load. Set all four `ADMOB_IOS_*` variables (9.11). |
| Log says `ASC_… is not set: skipping the upload` | Set the variables on the build target (9.7). The build itself worked. |
| Log says `No .ipa found` | The build made no `.ipa`, or Unity changed where it puts it. Read the Xcode stage above it. |
| `The upload to App Store Connect failed.` | Read the `altool` lines above it. A bad key shows as an authentication error: check `ASC_KEY_ID`, `ASC_ISSUER_ID`, and that `ASC_KEY_BASE64` is one line. Apple needs the app to exist first (9.6), and `ASC_APP_ID` is digits only. |
| `Provisioning profile … doesn't match the entitlements` or lacks a capability | Tick the capability on the App ID (9.3), generate the profile again (9.5), and upload the new one. It must be an **App Store Connect** profile for `<BUNDLE_ID>`. |
| `.p12` password or certificate errors | Upload the `.p12`, not the `.cer`, and make it with the flags in 9.5. Its certificate must be the one chosen in the profile. |
| CocoaPods fails | Read the `pod install` error in the log. The pods come from `StackItDependencies.xml` and the Ads plugin. |
| `Undefined symbol` or a missing header for `GIDSignIn` | The pods didn't reach the Xcode build. Read the CocoaPods stage. |
| Compile error in a `.mm` file | Fix it in `Assets/Plugins/iOS/` and push (9.8). |
| Text is missing or shows raw keys like `blocky.new_best.0` | The string tables didn't reach the build. Check that the pre-export method ran, then turn on **Addressables** in the build target's advanced settings. |
| App closes at launch | Without a live log, compare with the last good build. A common cause is a bad AdMob App ID: check `ADMOB_IOS_APP_ID`. |
| The app stays offline | `apiBaseUrl` in `AppConfig.asset` must be `<BASE_URL>` on the branch you built. Open `<BASE_URL>/health` in the iPhone's Safari. |
| Google sign-in fails or doesn't return to the app | `googleIosClientId` is empty or wrong in the build, or the ID isn't in `GOOGLE_CLIENT_IDS` (9.4). |
| Sign in with Apple fails on the server | `APP_BUNDLE_ID` doesn't match the app, or `APPLE_CLIENT_IDS` is set wrongly (section 4). |
| Railway logs show `Could not redeem` or `Could not revoke` | Sign-in and deletion still worked, but Apple refused. Check `APPLE_TEAM_ID`, `APPLE_KEY_ID`, and that `APPLE_PRIVATE_KEY` is the key from 9.3, not the upload key from 9.6. |
| Tracking prompt never appears | It only shows once per install. Delete the app and try again, and check *Settings ▸ Privacy & Security ▸ Tracking ▸ Allow Apps to Request to Track*. |
| Challenge links open Safari | `APPLE_TEAM_ID` or the bundle ID is wrong in the association file, the build's `applinks:` host doesn't match, or Apple's CDN hasn't caught up (section 6). A link tapped inside a page on the same domain never opens the app. |
| Upload refused: the SDK or Xcode is too old | Choose Xcode 26 or later on the build target (9.7). |
| Upload refused about iPad multitasking or orientations | The pre-export method sets iPhone only. Check that it ran. |
| Upload refused: the build number was already used | Build Automation's number only goes up per target, so a **new** target starts low. Keep using the same target. |
| Email `ITMS-91061`, missing privacy manifest | An SDK in the app has none. Update that SDK, or ask its maker. |
| Build stuck on *Missing Compliance* | Answer the question in TestFlight, and check that the build has `BuildHooks.cs`'s change (9.10). |
| Screenshot size refused | Use the `--app-store` cards (9.11). |
| Builds stopped | The free minutes ran out and there's no card on file (9.1). |

## 10. Moving to a custom domain later

1. Railway → Service → Settings → Networking → *Custom Domain* → add `play.yourdomain.com` and
   create the CNAME record it shows.
2. Change **one** server variable: `PUBLIC_BASE_URL=https://play.yourdomain.com`.
3. Change **one** client value: `AppConfig.apiBaseUrl`. The Android intent filters and the iOS
   `applinks:` entitlement are derived from it at build time. Ship new builds.
4. Old links (`*.up.railway.app/c/…`) keep resolving as long as the Railway domain stays attached.
   Only the new domain opens the app directly.
5. Update store listings (website, privacy policy URL), the Google OAuth consent screen links and
   the AdMob app-ads.txt website.

## 11. Pre-launch checklist

- [ ] `ENVIRONMENT=production`, strong `JWT_SECRET`, `SUPPORT_EMAIL` set; `/health` is green
- [ ] Railway Postgres backups enabled
- [ ] Legal texts (`backend/app/web/legal.py`) reviewed by someone qualified for your countries
- [ ] Production bundle built with *Stack IT ▸ Build Android Release* (real ads, 8.7);
      the project itself keeps the test IDs; `debugForceEeaConsent` off
- [ ] GDPR message and IDFA explainer published in AdMob; `/app-ads.txt` served
- [ ] `apple-app-site-association` and `assetlinks.json` validated; link opens the app on
      both platforms
- [ ] Google OAuth clients include the **Play App Signing** SHA-1; consent screen published
- [ ] Sign in with Apple and Google tested on device, including "switch account"
- [ ] Challenge share (the end card image + link) tested on a low-end Android and an older iPhone
- [ ] Offline run → reconnect → run is verified (offline queue)
- [ ] Account deletion tested end to end; `/delete-account` reachable
- [ ] Spanish pass on every screen (device language and Settings switch)
- [ ] Unity EditMode tests green in the editor; CI green (`backend`, `client-logic`)
- [ ] Generated `.meta` files committed; keystore backed up outside the repo
- [ ] Android: Bundle Version Code bumped, Target API ≥ 36, `apiBaseUrl` is the HTTPS
      `<BASE_URL>`, `mainTemplate.gradle` lists `play-services-ads` (8.2)
- [ ] Google Play: closed test done (12+ testers, 14 days), production access granted, Dashboard
      tasks all green (8.10–8.11)
- [ ] iOS: built with Xcode 26+, the 9.9 phone checks pass on a TestFlight build, and the release
      build has the `ADMOB_IOS_*` variables set
- [ ] iOS: `APPLE_KEY_ID` and `APPLE_PRIVATE_KEY` set on Railway; deleting an account with Apple
      linked removes Milli from the iPhone's Sign in with Apple list
- [ ] iOS: signing key, `.p12` and both `.p8` keys backed up outside the repo
- [ ] App Store: EU trader status declared, App Privacy matches Google's Data safety form, age
      rating done, `IOS_APP_STORE_URL` set after release
- [ ] Store listings: screenshots, icon, data safety / privacy labels, content rating
