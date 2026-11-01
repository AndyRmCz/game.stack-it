# Milli launch checklist

What's left after the rename from Stack IT to **Milli** (package `game.milli.com`, link scheme
`milli://`). The code, web pages, icon and store art are done; these steps happen in Unity, on
the phone and in the Google / Railway / AdMob dashboards. Work top to bottom: the Railway
domain and the package must be final **before the first Play upload**.

Production keys (real AdMob IDs) are backed up outside the repo in
`D:\An_ Documentos\VS Code\ValrdIQ\stackit-prod-keys.txt`. The repo keeps Google's test ad IDs
for every testing build.

## 1. Railway (api service ▸ Variables, then apply the changes)

- [ ] `APP_BUNDLE_ID=game.milli.com`
- [ ] `APP_URL_SCHEME=milli`
- [ ] `ADMOB_PUBLISHER_ID=pub-1305259028857953` (fixes the `/app-ads.txt` 404)
- [ ] Redeploy the new code: `cd backend`, then `railway up`. The web pages say "Milli" only after this.
- [ ] **Recommended now:** the domain `gamestack-it-production.up.railway.app` still says "stack-it",
      and players see it in every challenge link. Change it in *Settings ▸ Networking*
      (for example `milli-game`), then:
  - [ ] set `PUBLIC_BASE_URL` to the new `https://…up.railway.app`
  - [ ] set `AppConfig.apiBaseUrl` in Unity to the same address (no trailing slash)
  - [ ] update the Google consent screen links (section 3)
- [ ] Check in a browser: `<BASE_URL>/health`, `/privacy`, `/terms`, `/app-ads.txt`,
      `/.well-known/assetlinks.json` (the package should read `game.milli.com`).

## 2. Unity

- [ ] Click into the Unity window so it reloads the changed files.
- [ ] Run **Stack IT ▸ Setup Project** (the editor menu keeps its old name). It sets Product
      Name "Milli", package `game.milli.com`, the iOS URL scheme and the new icon.
- [ ] *Edit ▸ Project Settings ▸ Player*: check Product Name **Milli** and Package Name **game.milli.com**.
- [ ] *Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force Resolve* (it caches the old package name).
- [ ] Check *AdsConfig* and *Assets ▸ Google Mobile Ads ▸ Settings* still show the test IDs
      (`ca-app-pub-3940256099942544…`).
- [ ] Uninstall "Stack IT" from the phone: the new package installs as a separate app.
- [ ] *Player ▸ Android ▸ Publishing Settings*: keystore password, alias `upload`, key password
      (Unity forgets them every time it closes).
- [ ] Bundle Version Code `1`, untick **Development Build**, tick **Build App Bundle**, build `Milli.aab`.

## 3. Google Cloud (console.cloud.google.com, project of the app)

- [ ] *Google Auth Platform ▸ Branding*: app name **Milli**; privacy `<BASE_URL>/privacy`,
      terms `<BASE_URL>/terms`; authorized domain = your Railway host.
- [ ] *Clients*: create a **new Android client**, package `game.milli.com` + the upload key SHA-1.
      Delete the old `com.stackit.game` one.
- [ ] Nothing to change for the **web client**: `googleWebClientId` stays the same.
- [ ] After the first Play upload: a **second Android client** with the *App signing key* SHA-1
      (Play Console ▸ Test and release ▸ App integrity).

## 4. AdMob

- [ ] Rename both apps to **Milli** (*Apps ▸ App settings*). The App IDs don't change.
- [ ] *Privacy & messaging*: check the GDPR message names Milli and links `<BASE_URL>/privacy`.
- [ ] After the app is live: link the Android app to its Play listing.

## 5. Play Console

- [ ] *Create app*: name **Milli: Stack Tower Duel**, English (US), Game, Free, declarations.
- [ ] Short description, e.g. "Stack blocks to the millimetre. Duel your friends with one link."
- [ ] App icon: `store/icon-512.png`
- [ ] Feature graphic: `store/feature-graphic.png`
- [ ] Screenshots for internal and closed testing: the mockups in `store/screenshots-en/` and
      `store/screenshots-es/` (drawn by `scripts.generate_screen_mockups` from the game's layout).
      They're illustrations: replace them with real captures before production, as below.
- [ ] Real screenshots, 4 to 6, portrait, taken on the phone (power + volume down):
  - [ ] a run in progress
  - [ ] a near miss with the gold "0.4 mm from perfect" callout
  - [ ] a duel / challenge
  - [ ] the daily tower
  - [ ] Neon and Pop
  - [ ] Frame them (Play refuses phone captures longer than 2:1). From `backend/`:
        `uv run python -m scripts.generate_store_art --screens "<folder>"` (add `--lang es` for Spanish),
        then upload `store/screenshots-en/`.
- [ ] Dashboard "Set up your app" answers: SETUP.md section 8.10.
- [ ] *Internal testing*: tester list, *Create new release*, **Use Google-generated key**, upload `Milli.aab`.
- [ ] Then section 3's second Android client, and on Railway
      `ANDROID_SHA256_FINGERPRINTS=<upload SHA-256>,<Play SHA-256>`.
- [ ] *Closed testing*: 12+ testers opted in for 14 days in a row, at least one update, then
      *Apply for production*.

## 6. Before the production release

- [ ] Paste the real AdMob App IDs and ad units back from the backup file, build, then switch back to the test IDs.
- [ ] Production DB migrations ran on Railway (they run on every deploy: check the deploy log).
