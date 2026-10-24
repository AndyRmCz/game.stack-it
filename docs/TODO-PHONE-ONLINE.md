# To do: get the phone build online

Pending as of 2026-10-23. The development build installed on the phone, but it starts offline:
it can't reach the backend running on the laptop. Three things block it, and all three need
fixing. The full walkthrough is in [SETUP.md](../SETUP.md), section 8.4.

## Why it's offline

| # | Problem | Found |
|---|---|---|
| 1 | The build may have shipped `apiBaseUrl = http://localhost:8000`, so the phone calls itself. | `AppConfig.asset` on disk said `localhost` while the text editor showed `192.168.1.16`, and the file changed after the build. |
| 2 | Android blocks plain `http://`. | *Allow downloads over HTTP* is off (`insecureHttpOption: 0`). |
| 3 | The Windows firewall drops the phone's requests. | The Wi-Fi `IZZI-80C2` is set to **Public**; the port 8000 rule only covers **Private**. |

The backend itself was fine: `http://192.168.1.16:8000/health` returned 200 from the laptop.

## Steps

1. **Check the laptop's current LAN IP.** It changed from `.17` to `.16` once already.
   ```powershell
   ipconfig
   ```
   Look under the Wi-Fi adapter for *IPv4 Address*. Below, `<LAN IP>` means that value.
2. **Start the backend** from the repo root, if it isn't running:
   ```powershell
   docker compose up -d --build
   ```
3. **Set the Wi-Fi to Private:** Windows *Settings ▸ Network & internet ▸ Wi-Fi ▸ IZZI-80C2 ▸
   Network profile type* ▸ **Private**.
4. **Firewall rule** for port 8000. It already exists ("Stack IT API (dev)", Private). Only if
   it's missing, run in PowerShell as administrator:
   ```powershell
   New-NetFirewallRule -DisplayName "Stack IT API (dev)" -Direction Inbound -Protocol TCP -LocalPort 8000 -Action Allow -Profile Private
   ```
5. **Test from the phone's browser:** `http://<LAN IP>:8000/health` should show
   `{"status":"ok",…}`. Don't go on until this works; if the browser can't reach it, the app
   can't either.
6. **Set the URL in Unity, not in a text editor.**
   1. If the file is open in VS Code with unsaved changes, close it without saving.
   2. In Unity, select `Assets/_Project/Resources/StackIt/AppConfig` in the Project window.
   3. In the Inspector, set `apiBaseUrl` = `http://<LAN IP>:8000`.
   4. *File ▸ Save Project*.
7. **Allow HTTP in development builds:** *Edit ▸ Project Settings ▸ Player ▸ Android ▸ Other
   Settings ▸ Configuration ▸ Allow downloads over HTTP* ▸ **Allowed in development builds**.
8. **Plug in the phone** and accept the USB debugging prompt. Check it shows up:
   ```powershell
   $U = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PlaybackEngines\AndroidPlayer"
   & "$U\SDK\platform-tools\adb.exe" devices
   ```
9. **Build And Run again.** *File ▸ Build Profiles ▸ Android*: Development Build ticked, Build
   App Bundle unticked. It reuses the compiled code, so it should be much faster than the first
   build.
10. **Check the log:**
    ```powershell
    & "$U\SDK\platform-tools\adb.exe" logcat -s Unity
    ```
    A healthy start has no `Starting offline` line.

## Also pending from the same session

- [ ] Ads plugin: *Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force Resolve*, then
      check that `Assets/Plugins/Android/mainTemplate.gradle` lists `play-services-ads` and
      `user-messaging-platform` (SETUP.md 8.2, step 5).
- [ ] *Assets ▸ Google Mobile Ads ▸ Settings…* ▸ untick **Enable Gradle build pre-processor**.
      With it on, the build can hang forever in "Finishing import" (SETUP.md 8.2, step 7).
- [ ] Optional, for faster test builds: untick **ARMv7** under *Target Architectures*, and set
      *IL2CPP Code Generation* to *Faster (smaller) builds*.
- [ ] Commit the plugin and EDM files: `Packages/manifest.json`, `packages-lock.json`,
      `Assets/GoogleMobileAds/`, the new files in `Assets/Plugins/Android/` and
      `Assets/Plugins/iOS/`, `ProjectSettings/AndroidResolverDependencies.xml`,
      `ProjectSettings/ExternalDependencyManager*`, `Assets/Settings/Build Profiles/`.
      **Not** the local `AppConfig.asset` URL.
- [ ] Before a release build: set `apiBaseUrl` back to the HTTPS `<BASE_URL>` (SETUP.md 8.7).

## Pending from the telemetry change (2026-10-24)

The telemetry work is staged (see `docs/PLAN.md`, "Measurement findings" ▸ K). These checks
could not be run here:

- [ ] **Run the Unity tests in the editor:** *Window ▸ General ▸ Test Runner ▸ EditMode ▸
      Run All*. They cover `StackIt.Tests.EditMode`, including the new `TelemetryTests`, and
      `StackIt.Tests.UI` (every screen built with the new `UiContext`). The scratch-copy run
      was stopped for low memory, so they haven't run since the change.
- [ ] **Let Unity re-resolve packages.** `com.unity.purchasing` and `com.unity.analytics` were
      removed from `Packages/manifest.json`. Focus the editor and wait for the import, then
      check the Console for errors.
- [ ] **Rebuild the APK and confirm the purchase permission is gone:**
      ```powershell
      $U = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PlaybackEngines\AndroidPlayer"
      & "$U\SDK\build-tools\36.0.0\aapt.exe" dump permissions unity\StackIt-dev.apk
      ```
      `com.android.vending.BILLING` should no longer be listed.
- [ ] **Check for a leftover Unity process** in Task Manager (a `Unity.exe` with no project
      window, from the stopped test run) and end it if it's there.
- [ ] **See telemetry arrive from the phone.** After the app is online (steps above), play a
      few runs, leave the app, then from `backend/` run:
      ```powershell
      uv run python -m scripts.metrics report --days 1
      ```
      Sessions, runs and screens should show up. The local database also holds a test
      account (`dev-telemetry-smoke-01`) with 6 smoke-test events; ignore or delete it.
- [ ] **Commit** the staged change (the commit message is in the conversation), then the plugin
      files together with `packages-lock.json` after Unity has re-resolved.

## Pending from the hooks, missions and mascot (2026-10-24)

- [ ] `uv run alembic upgrade head` in `backend/` (new: `runs.daily_date`).
- [ ] **On the phone, check:**
  - [ ] a perfect streak to 5 and 10: fanfare, burst, zoom punch;
  - [ ] a near miss: "Missed by … mm" and slow motion;
  - [ ] the tower toppling at game over, and confetti on a record;
  - [ ] Ranks ▸ Daily: the Play button and the board;
  - [ ] "Copy result" pasted into WhatsApp;
  - [ ] the clip starting near the best moment;
  - [ ] the TikTok / Stories buttons (they fall back to the sheet until the keys are set:
        SETUP.md 8.12b);
  - [ ] Missions: the dot, Claim, the chest;
  - [ ] Blocky's position and look in both looks.
- [ ] Commit the regenerated localization assets (`UI Shared Data.asset`, `UI_en.asset`,
      `UI_es.asset`) that Unity rebuilt from `strings.csv`.

## Pending from the soundtracks (2026-10-24)

Built and tested outside Unity (`docs/PLAN.md`, "Soundtracks plan"). Still to do:

- [ ] **Listen to the WAV previews** and send notes per style. From `unity/`:
      ```powershell
      dotnet run -c Release --project DotNetTests/MusicPreview
      ```
      The files are in `unity/DotNetTests/MusicPreview/out/`. Start with the `-5-whole-run` file
      of each style.
- [ ] **Migrate the local database** if Docker was rebuilt (`uv run alembic upgrade head` in
      `backend/`), and later production.
- [ ] **In the editor:** run the EditMode tests (they now include `MusicStyleTests` and
      `VoicePoolTests`). Unity re-imports `strings.csv` by itself.
- [ ] **On the phone:**
  - [ ] Shop ▸ Music: play the listen button.
  - [ ] Buy and equip a soundtrack; the music should switch at the next bar.
  - [ ] Try one with the ad button (test ads); the run should start with it, and Home should
        bring back the equipped one.
  - [ ] Watch for stutters when a style switches, and check the `perf_sample` FPS in the
        metrics report.
