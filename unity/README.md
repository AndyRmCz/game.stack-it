# unity/

Unity 6 (URP) project for iOS and Android. Open this folder with Unity Hub and run
**Stack IT ▸ Setup Project** once (see [SETUP.md](../SETUP.md), section 7).

```
Assets/
  _Project/
    Scripts/
      Core/         StackIt.Core: deterministic simulation, PCG32, economy mirror (no UnityEngine)
      Client/       StackIt.Client: API DTOs, retry policy, offline queue, deep links, ad policy,
                    rival tracker, strings CSV, manifest generation (no UnityEngine, unit tested)
      Game/         game loop (GameController), stack view, camera, sky, hype effects, rival ghost
      Services/     ApiClient, AuthService, GameApi, audio, deep links, native bridges
      UI/           code-built uGUI + TMP screens (UiKit, router, all screens, challenge card)
      Integrations/ URP post-processing, Localization, Input System event system
      Bootstrap/    App (composition root + game flow), AppConfig / AdsConfig
      Editor/       Setup Project, Import Strings, build hooks (Android manifest, iOS capabilities)
    AdMob/          AdMob adapter (Assembly-CSharp, compiled only with the plugin installed)
    Editor/         EDM4U native dependencies
    Resources/      strings.csv (+ config and materials created by Setup Project)
    Tests/EditMode/ NUnit tests shared with DotNetTests (golden vectors, client logic)
  Plugins/iOS       share, haptics, Sign in with Apple/Google (.mm)
  Plugins/Android   share, haptics, install referrer, Credential Manager (.java)
                    + StackItManifest.androidlib (App Links, FileProvider)
  Settings/         URP assets from the Universal 3D template
DotNetTests/        dotnet test runner + compile checks (outside Assets, ignored by Unity)
```

The scene is empty on purpose: `App` boots after the first scene loads and builds the world and
UI in code.
