using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Client.Theming;
using StackIt.Core.Simulation;
using StackIt.Game;
using StackIt.Integrations;
using StackIt.Services;
using StackIt.Services.Native;
using StackIt.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.Bootstrap
{
    /// <summary>
    /// Composition root and game flow. Creates every service by hand (no DI framework), builds the
    /// world and the UI in code, and sequences runs: play → miss → (continue) → results → share.
    /// Boots automatically after the first scene loads, so the scene itself can stay empty.
    /// </summary>
    public sealed class App : MonoBehaviour, IGameFlow
    {
        private const float SplashMinSeconds = 1.1f;
        private const float MusicPreviewSeconds = 10f;
        private const float LookFadeSeconds = 0.3f;

        private AppConfig _config;
        private AdsConfig _adsConfig;
        private PlayerPrefsStore _store;
        private SaveService _save;
        private ThemeService _theme;
        private PendingChallenges _pending;
        private SeenSkins _seen;
        private AuthService _auth;
        private GameApi _api;
        private IAdsService _ads;
        private IShareService _share;
        private IHaptics _haptics;
        private AudioService _audio;
        private MusicService _music;
        private RecorderService _recorder;
        private UnityLocalizer _localizer;
        private DeepLinkService _deepLinks;
        private AdPolicy _adPolicy;
        private TelemetryService _telemetry;

        private CameraRig _rig;
        private SkyBackdrop _sky;
        private StackView _view;
        private HypeFx _fx;
        private Mascot _mascot;
        private UrpPostFx _postFx;
        private GameController _game;

        private Canvas _canvas;
        private Canvas _overlay;
        private UiRouter _router;
        private SplashScreen _splash;
        private HomeScreen _home;
        private HudScreen _hud;
        private PauseScreen _pause;
        private GameOverScreen _gameOver;
        private ChallengeIntroScreen _intro;
        private CodeEntryScreen _codeEntry;

        private Task _signedIn = Task.CompletedTask;
        private List<Friend> _friends = new List<Friend>();
        private SubmitRunResponse _lastResult;
        private Task _finishing = Task.CompletedTask;
        private bool _lastRunWasRecord;
        private bool _starting;
        private bool _runOpen;
        private string _trialMusic; // a soundtrack unlocked for the next run by a rewarded ad
        private bool _continueOffered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<App>() != null) return;
            var go = new GameObject("App");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        private async void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _config = AppConfig.Load();
            _adsConfig = AdsConfig.Load();
            _store = new PlayerPrefsStore();
            _save = new SaveService(_store);
            _save.CountSession();
            _theme = new ThemeService(Look.Catalog, _store);
            Look.Init(_theme);
            Look.ReduceMotion = _save.ReduceMotion;
            _theme.Changed += ApplyLook;
            _pending = new PendingChallenges(_store);
            _seen = new SeenSkins(_store);

            _localizer = new UnityLocalizer();
            await _localizer.InitializeAsync(_save.Language);
            L.Localizer = _localizer;
            _localizer.Changed += RebuildUi;

            var client = new ApiClient(_config.apiBaseUrl);
            _auth = new AuthService(client, _store, new NativeSignInService(_config.googleWebClientId, _config.googleIosClientId));
            _api = new GameApi(client, new OfflineRunQueue(_store), _save);
            _api.ProfileChanged += _ => ApplyLook(_theme.Palette);
            _telemetry = gameObject.AddComponent<TelemetryService>();
            _telemetry.Init(_api, _store, () => _auth.IsSignedIn, SessionProps);
            client.Failed += (path, e) =>
            {
                if (path.StartsWith("/v1/events")) return; // never report the reporter
                _telemetry.Track(TelemetryEvents.ApiError, ("endpoint", TelemetryText.PathTemplate(path)), ("status", e.Status),
                    ("code", e.Code));
            };

            _ads = _adsConfig.adsEnabled ? AdsServiceRegistry.Create() : new NoAdsService();
            _ads.AdEvent += _telemetry.OnAdEvent;
            _share = new NativeShareService(_config.facebookAppId);
            _haptics = new NativeHaptics { Enabled = _save.HapticsOn };
            _audio = gameObject.AddComponent<AudioService>();
            _audio.Enabled = _save.SoundOn;
            _music = gameObject.AddComponent<MusicService>();
            _music.Enabled = _save.MusicOn;
            _music.Equip(_api.Profile?.EquippedMusic); // the cached profile, until the server's arrives
            _api.ProfileChanged += p => _music.Equip(p?.EquippedMusic);
            _recorder = gameObject.AddComponent<RecorderService>();
            _recorder.EndCardFactory = card => new EndCardRenderer(card);
            _adPolicy = new AdPolicy(_adsConfig.interstitialMinRuns, _adsConfig.interstitialMinSeconds);

            BuildWorld();
            EventSystemFactory.Create();
            UiKit.Pressed = () =>
            {
                _audio.Click();
                _haptics.Play(HapticKind.Light);
            };
            BuildUi();
            _router.Reset(_splash);
            float splashUntil = Time.realtimeSinceStartup + SplashMinSeconds;

            _deepLinks = new DeepLinkService(_config.apiBaseUrl, _config.urlScheme);
            _deepLinks.ChallengeReceived += code => OpenChallenge(code, "link");

            _signedIn = SignInAndSync();
            // Home once signed in (or offline), but never before the splash has had its moment.
            await Task.WhenAny(_signedIn, Task.Delay(2500));
            while (Time.realtimeSinceStartup < splashUntil) await Task.Yield();
            _router.Reset(_home);
            _telemetry.MarkLaunched(Time.realtimeSinceStartup);
            _music.Home();
            _deepLinks.Start(firstLaunch: _save.IsFirstSession);
            await _signedIn;
            _ = InitAdsAsync();

            // iOS has no install referrer: on first launch, offer to type the code from the link.
            if (_save.IsFirstSession && !_save.SeenCodePrompt && Application.platform == RuntimePlatform.IPhonePlayer
                && _router.Current == _home)
            {
                _save.SeenCodePrompt = true;
                _router.Show(_codeEntry);
            }
        }

        private async Task SignInAndSync()
        {
            try
            {
                await _auth.EnsureSignedInAsync();
                await _api.RefreshProfileAsync();
                await _api.FlushQueueAsync();
                _friends = await _api.FriendsAsync();
                _api.ReserveNextRun();
            }
            catch (ApiException e)
            {
                Debug.Log("Starting offline: " + e.Code);
            }
        }

        private async Task InitAdsAsync()
        {
            try
            {
                await _ads.InitializeAsync();
                if (_router.Current != null && _router.Current.AllowsBanner) _ads.ShowBanner();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Ads unavailable: " + e.Message);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && _api != null && _auth.IsSignedIn) _ = _api.FlushQueueAsync();
            // Leaving the app mid-run pauses it rather than letting the block run away.
            if (!focused && _game != null && _game.Phase == RunPhase.Playing && !_game.Paused) Pause();
        }

        // --- world & UI --------------------------------------------------------------------------

        private void BuildWorld()
        {
            _rig = CameraRig.Create();
            _rig.Camera.cullingMask &= ~(1 << EndCardRenderer.Layer);
            DontDestroyOnLoad(_rig.gameObject);
            _sky = SkyBackdrop.Create(_rig.Camera);

            _postFx = new GameObject("PostFx").AddComponent<UrpPostFx>();
            DontDestroyOnLoad(_postFx.gameObject);
            _postFx.Configure(_rig.Camera);

            var world = new GameObject("World");
            DontDestroyOnLoad(world);
            _view = world.AddComponent<StackView>();
            _fx = world.AddComponent<HypeFx>();
            _mascot = world.AddComponent<Mascot>();
            _mascot.ReduceMotion = _save.ReduceMotion;
            _fx.Init(_rig, _postFx, _haptics, _audio);
            _fx.ReduceMotion = _save.ReduceMotion;
            RivalGhostView ghost = RivalGhostView.Create(world.transform);
            _game = world.AddComponent<GameController>();
            _game.Init(_view, _fx, _rig, _sky, ghost);
            _game.Missed += OnMissed;
            _game.Placed += step =>
            {
                float width = Mathf.Min(step.Block.Width, step.Block.Depth) / (float)SimConstants.BlockSize;
                _music.Progress(step.N, width);
                _mascot.Placed(step.Perfect, step.Combo, step.N, width);
                // Clip highlights (H3): a perfect streak counts more the longer it gets.
                if (step.Perfect && step.Combo >= 5) _recorder.Highlight(ClipMoments.StreakWeight * step.Combo / 5f);
            };
            _game.RivalProgress += e =>
            {
                if (e == RivalEvent.Passed) _recorder.Highlight(ClipMoments.RivalWeight);
            };
            _game.NearMissed += near => _recorder.Highlight(
                near.Kind == NearMissKind.AlmostSaved ? ClipMoments.AlmostSavedWeight : ClipMoments.AlmostPerfectWeight);

            _view.ResetStack(new StackSim(0).Top);
            ApplyLook(_theme.Palette);
            _view.SetAttract(true);
            _rig.SetHome(true, snap: true);
        }

        private void BuildUi()
        {
            _canvas = UiKit.CreateCanvas("UI", 10);
            _overlay = UiKit.CreateCanvas("Overlay", 100);
            _router = new UiRouter(_overlay.transform, _ads);
            _router.Shown += screen => _telemetry.Track(TelemetryEvents.ScreenView, ("screen", screen.GetType().Name));
            var ctx = new UiContext
            {
                Config = _config,
                Api = _api,
                Auth = _auth,
                Save = _save,
                Ads = _ads,
                Audio = _audio,
                Haptics = _haptics,
                Share = _share,
                DeepLinks = _deepLinks,
                Localizer = _localizer,
                Theme = _theme,
                Pending = _pending,
                Seen = _seen,
                Game = _game,
                Flow = this,
                Router = _router,
                Telemetry = _telemetry,
            };
            Transform root = _canvas.transform;
            _splash = new SplashScreen(ctx, root);
            _home = new HomeScreen(ctx, root, reserveBanner: _adsConfig.adsEnabled);
            _hud = new HudScreen(ctx, root);
            _pause = new PauseScreen(ctx, root);
            _gameOver = new GameOverScreen(ctx, root);
            _intro = new ChallengeIntroScreen(ctx, root);
            _codeEntry = new CodeEntryScreen(ctx, root);
            _home.Friends = new FriendsScreen(ctx, root);
            _home.Leaderboard = new LeaderboardScreen(ctx, root);
            _home.Shop = new ShopScreen(ctx, root);
            _home.Settings = new SettingsScreen(ctx, root);
            _home.Daily = new DailyRewardScreen(ctx, root);
            _home.Missions = new MissionsScreen(ctx, root);
            _home.CodeEntry = _codeEntry;
        }

        /// <summary>Language switch: rebuild every screen with the new strings.</summary>
        private void RebuildUi()
        {
            Destroy(_canvas.gameObject);
            Destroy(_overlay.gameObject);
            BuildUi();
            _router.Reset(_home);
        }

        /// <summary>
        /// Pushes the look (and the equipped skin and sky) into the world: blocks, sky, post FX, the
        /// rival ghost and the effects. The UI follows by itself through the themed components.
        /// Visual only: never touches the simulation, so switching mid-run is safe.
        /// </summary>
        private void ApplyLook(ThemePalette look)
        {
            _sky.SetLook(look, _api?.Profile?.EquippedTheme);
            _view.SetLook(look, _api?.Profile?.EquippedBlockSkin, _sky.Horizon);
            _postFx.SetLook(look.Number(T.BloomIntensity), look.Number(T.BloomThreshold), look.Number(T.BloomScatter),
                look.Number(T.ChromaticPulse));
            _fx.SetLook(look);
            _mascot.SetLook(look);
            _game.Ghost.SetColor(Palette.ToColor(look.Color(T.Accent2)), look.Number(T.BlockEmission) + 0.3f);
        }

        // --- settings (IGameFlow) ----------------------------------------------------------------

        /// <summary>Switches the look with a 300 ms cross-fade from a snapshot of the old one.</summary>
        public void SetLook(ThemeId look)
        {
            if (look == _theme.Current) return;
            StartCoroutine(CrossFadeTo(look));
        }

        private IEnumerator CrossFadeTo(ThemeId look)
        {
            yield return new WaitForEndOfFrame();
            Texture2D snapshot = ScreenCapture.CaptureScreenshotAsTexture();
            var cover = new GameObject("LookFade", typeof(RectTransform));
            cover.transform.SetParent(_overlay.transform, false);
            UiKit.Stretch((RectTransform)cover.transform);
            var image = cover.AddComponent<RawImage>();
            image.texture = snapshot;
            image.raycastTarget = true; // no taps during the fade
            IgnoreTheming.Mark(image, "snapshot of the previous look");
            var group = cover.AddComponent<CanvasGroup>();

            _theme.Set(look);
            _telemetry.Track(TelemetryEvents.SettingChanged, ("setting", "look"), ("value", look.ToString()));
            _haptics.Play(HapticKind.Light);
            Tween.Fade(group, 0f, LookFadeSeconds, () =>
            {
                Destroy(cover);
                Destroy(snapshot);
            });
        }

        public void SetReduceMotion(bool on)
        {
            _save.ReduceMotion = on;
            _telemetry.Track(TelemetryEvents.SettingChanged, ("setting", "reduce_motion"), ("value", on));
            Look.ReduceMotion = on;
            _fx.ReduceMotion = on;
            _mascot.ReduceMotion = on;
        }

        public void SetMusic(bool on)
        {
            _save.MusicOn = on;
            _telemetry.Track(TelemetryEvents.SettingChanged, ("setting", "music"), ("value", on));
            _music.Enabled = on;
            if (!on) return;
            if (_game.Phase == RunPhase.Playing) _music.StartRun();
            else _music.Home();
        }

        public void PreviewMusic(string styleId)
        {
            _music.Audition(styleId, MusicPreviewSeconds);
            _telemetry.Track(TelemetryEvents.MusicPreview, ("item_id", styleId), ("seconds", MusicPreviewSeconds));
        }

        public void EndMusicPreview() => _music.EndAudition();

        /// <summary>Watch a rewarded ad, then play one run with a soundtrack the player doesn't own.</summary>
        public async void TryMusic(string styleId)
        {
            _telemetry.Track(TelemetryEvents.OfferAccepted, ("placement", AdPlacements.MusicTrial));
            _music.EndAudition();
            bool rewarded = await ShowRewarded(AdPlacements.MusicTrial);
            _adPolicy.RecordRewardedShown(Time.realtimeSinceStartup);
            if (!rewarded)
            {
                _router.Toast(L.T("flow.no_ad"));
                return;
            }
            _trialMusic = styleId;
            PlayNormal();
        }

        // --- game flow (IGameFlow) ---------------------------------------------------------------

        public async void PlayNormal()
        {
            if (_starting) return;
            _starting = true;
            try
            {
                _ = EndPendingRun(); // its submission (possibly a minute of retries offline) finishes in the background
                await MaybeInterstitial();
                StartRunResponse run = await _api.TakeReservedRunAsync();
                int best = BestScore();
                Friend rival = NextFriendToBeat(best);
                var setup = new RunSetup
                {
                    Seed = run != null ? (uint)run.Seed : (uint)Random.Range(int.MinValue, int.MaxValue),
                    RunId = run?.RunId,
                    ContinuesAllowed = run?.ContinuesAllowed ?? true,
                    PersonalBest = best,
                    RivalName = rival?.DisplayName,
                    RivalHeight = rival?.BestPlaced ?? 0,
                    RivalScore = rival?.BestScore ?? 0,
                };
                StartRun(setup);
                if (run != null) _api.ReserveNextRun();
            }
            finally
            {
                _starting = false;
            }
        }

        /// <summary>A normal (ranked) run with a chosen friend as the ghost to beat.</summary>
        public async void PlayAgainst(string rivalName, int rivalScore, int rivalHeight)
        {
            if (_starting) return;
            _starting = true;
            try
            {
                StartRunResponse run = await _api.TakeReservedRunAsync();
                StartRun(new RunSetup
                {
                    Seed = run != null ? (uint)run.Seed : (uint)Random.Range(int.MinValue, int.MaxValue),
                    RunId = run?.RunId,
                    ContinuesAllowed = run?.ContinuesAllowed ?? true,
                    PersonalBest = BestScore(),
                    RivalName = rivalName,
                    RivalHeight = rivalHeight,
                    RivalScore = rivalScore,
                });
                if (run != null) _api.ReserveNextRun();
            }
            finally
            {
                _starting = false;
            }
        }

        public async void PlayChallenge(string code)
        {
            if (_starting) return;
            _starting = true;
            try
            {
                await _signedIn;
                StartRunResponse run = await _api.StartChallengeRunAsync(code);
                _pending.Remove(code);
                StartRun(new RunSetup
                {
                    Seed = (uint)run.Seed,
                    RunId = run.RunId,
                    ContinuesAllowed = run.ContinuesAllowed,
                    ChallengeCode = run.ChallengeCode,
                    RivalName = run.Rival?.DisplayName,
                    RivalHeight = run.Rival?.Placed ?? 0,
                    RivalScore = run.Rival?.Score ?? 0,
                    PersonalBest = BestScore(),
                });
            }
            catch (ApiException e)
            {
                _router.Toast(e.IsOffline ? L.T("flow.challenge_offline") : Errors.Describe(e));
            }
            finally
            {
                _starting = false;
            }
        }

        public async void PlayDaily()
        {
            if (_starting) return;
            _starting = true;
            try
            {
                _ = EndPendingRun();
                await _signedIn;
                StartRunResponse run = await _api.StartDailyRunAsync();
                StartRun(new RunSetup
                {
                    Seed = (uint)run.Seed,
                    RunId = run.RunId,
                    ContinuesAllowed = run.ContinuesAllowed,
                    PersonalBest = BestScore(),
                    DailyNumber = run.DailyNumber ?? DailyTower.Number(System.DateTime.UtcNow),
                });
            }
            catch (ApiException e)
            {
                _router.Toast(e.IsOffline ? L.T("flow.daily_offline") : Errors.Describe(e));
            }
            finally
            {
                _starting = false;
            }
        }

        /// <summary>
        /// The emoji result (H2) on the clipboard, for WhatsApp and other chats where a video is
        /// heavy. A verified run also gets its duel link, like the Challenge button.
        /// </summary>
        public async void CopyResult()
        {
            await EndPendingRun();
            _telemetry.Track(TelemetryEvents.ShareTap, ("source", "copy_result"));
            string text = ResultText.Build(_game.Setup?.DailyNumber ?? 0, _game.Score, _game.Outcomes);
            if (_lastResult != null && _lastResult.Status == "verified")
            {
                try
                {
                    ChallengeInfo challenge = await _api.CreateChallengeAsync(_lastResult.RunId);
                    text += '\n' + challenge.Url;
                }
                catch (ApiException)
                {
                    // Offline or refused: the result alone is still worth sharing.
                }
            }
            GUIUtility.systemCopyBuffer = text;
            _router.Toast(L.T("flow.result_copied"));
        }

        public async void OpenChallenge(string code, string via)
        {
            _telemetry.Track(TelemetryEvents.ChallengeOpened, ("via", via));
            try
            {
                await _signedIn;
                ChallengeInfo info = await _api.GetChallengeAsync(code);
                if (info.IsOwn)
                {
                    _router.Toast(L.T("error.own_challenge"));
                    return;
                }
                _pending.Add(info.Code, info.CreatorName, info.Score);
                _intro.Bind(info);
                _router.Show(_intro);
            }
            catch (ApiException e)
            {
                _router.Toast(e.IsOffline ? L.T("flow.challenge_offline") : Errors.Describe(e));
            }
        }

        private void StartRun(RunSetup setup)
        {
            _lastResult = null;
            ApplyLook(_theme.Palette);
            _view.SetAttract(false);
            _rig.SetHome(false);
            _game.Begin(setup);
            _runOpen = true;
            _telemetry.RunStarted(setup.RunId, setup.ChallengeCode != null);
            _router.Reset(_hud);
            _recorder.BeginRun();
            _mascot.ResetMood();
            // A soundtrack tried with a rewarded ad lasts one run.
            _music.Override(_trialMusic);
            _trialMusic = null;
            _music.StartRun();
        }

        public void Pause()
        {
            if (_game.Phase != RunPhase.Playing) return;
            _game.Pause();
            _music.Pause(true);
            _router.Show(_pause, remember: false);
        }

        public void Resume()
        {
            _game.Resume();
            _music.Pause(false);
            _router.Reset(_hud);
        }

        /// <summary>From the pause menu: abandon this run (unsubmitted) and start a fresh one.</summary>
        public void Restart()
        {
            TrackRunEnd("restart");
            _game.Resume();
            _game.Idle();
            _recorder.EndRun();
            PlayNormal();
        }

        private void OnMissed()
        {
            _rig.SetZoomOut(1f);
            if (_game.CanContinue && _ads.RewardedReady)
            {
                _gameOver.ShowContinueOffer(_game.Score, _game.IsNewRecord, _game.Setup.PersonalBest);
                _router.Show(_gameOver, remember: false);
                _continueOffered = true;
                _telemetry.Track(TelemetryEvents.OfferShown, ("placement", AdPlacements.Continue));
                _music.GameOver();
            }
            else
            {
                _finishing = FinishRun();
            }
        }

        public async void ContinueWithAd()
        {
            _continueOffered = false;
            _telemetry.Track(TelemetryEvents.OfferAccepted, ("placement", AdPlacements.Continue));
            bool rewarded = await ShowRewarded(AdPlacements.Continue);
            _adPolicy.RecordRewardedShown(Time.realtimeSinceStartup);
            if (rewarded && _game.Continue())
            {
                _router.Reset(_hud);
                _music.StartRun();
                return;
            }
            if (!rewarded) _router.Toast(L.T("flow.no_ad"));
            _finishing = FinishRun();
        }

        public void DeclineContinue() => _finishing = FinishRun();

        /// <summary>Ends a run still waiting on "Continue" (another button on the results was pressed).</summary>
        private Task EndPendingRun()
        {
            if (_game.Phase == RunPhase.AwaitingContinue) _finishing = FinishRun();
            return _finishing;
        }

        private async Task FinishRun()
        {
            if (!_game.Finish()) return;
            if (_continueOffered)
            {
                _continueOffered = false;
                _telemetry.Track(TelemetryEvents.OfferDeclined, ("placement", AdPlacements.Continue));
            }
            TrackRunEnd("miss");
            _recorder.EndRun();
            _adPolicy.RecordRunEnded();
            _music.GameOver();
            _mascot.GameOver();

            RunSetup setup = _game.Setup;
            int score = _game.Score;
            int previousBest = setup.PersonalBest;
            _lastRunWasRecord = _game.IsNewRecord;
            _save.LocalBest = score;

            bool ranked = setup.RunId != null;
            _gameOver.ShowLocalResult(score, _lastRunWasRecord, previousBest, ranked);
            _router.Show(_gameOver, remember: false);
            if (!ranked) return;

            try
            {
                SubmitRunResponse result = await _api.SubmitRunAsync(setup.RunId, _game.Events, score); // null: queued offline
                if (_game.Setup != setup) return; // the next run already started
                _lastResult = result;
                bool rewardedReady = _ads.RewardedReady;
                _gameOver.ShowServerResult(result, rewardedReady);
                if (AdPolicy.OffersDoubleCoins(result, rewardedReady))
                    _telemetry.Track(TelemetryEvents.OfferShown, ("placement", AdPlacements.DoubleCoins));
            }
            catch (ApiException e)
            {
                if (_game.Setup != setup) return;
                _gameOver.ShowServerResult(new SubmitRunResponse { Status = "rejected", Reason = e.Code }, false);
            }
        }

        public async void DoubleCoins()
        {
            if (_lastResult == null) return;
            _telemetry.Track(TelemetryEvents.OfferAccepted, ("placement", AdPlacements.DoubleCoins));
            bool rewarded = await ShowRewarded(AdPlacements.DoubleCoins);
            _adPolicy.RecordRewardedShown(Time.realtimeSinceStartup);
            if (!rewarded)
            {
                _router.Toast(L.T("flow.no_ad"));
                return;
            }
            try
            {
                await _api.DoubleCoinsAsync(_lastResult.RunId);
                _lastResult.CoinsDoubled = true;
                _gameOver.ShowCoinsDoubled(_lastResult.CoinsEarned * 2);
            }
            catch (ApiException e)
            {
                _router.Toast(Errors.Describe(e));
            }
        }

        /// <summary>The viral loop: duel link + auto-recorded clip with an end card, via the share sheet.</summary>
        public void ShareChallenge() => ShareChallengeTo(ShareTarget.System);

        public async void ShareChallengeTo(ShareTarget target)
        {
            await EndPendingRun(); // pressed while "Continue" was still on offer: the run ends here
            string source = target == ShareTarget.TikTok ? "tiktok" : target == ShareTarget.InstagramStory ? "instagram" : "end_card";
            _telemetry.Track(TelemetryEvents.ShareTap, ("source", source));
            if (_lastResult == null || _lastResult.Status != "verified")
            {
                _router.Toast(L.T("flow.share_needs_verified"));
                return;
            }
            _gameOver.SetSharing(true);
            try
            {
                ChallengeInfo challenge = await _api.CreateChallengeAsync(_lastResult.RunId);
                string clip = null;
                if (_recorder.IsSupported)
                {
                    clip = await _recorder.ExportClipAsync(new EndCard(challenge.Score, L.T("share.headline"), challenge.Code));
                    _telemetry.Track(TelemetryEvents.ClipExported, ("ok", clip != null));
                }
                _share.ShareTo(target, L.T("share.text", challenge.Score, challenge.Code), challenge.Url, clip);
            }
            catch (ApiException e)
            {
                _router.Toast(Errors.Describe(e));
            }
            finally
            {
                _gameOver.SetSharing(false);
            }
        }

        public async void GoHome()
        {
            _ = EndPendingRun();
            await MaybeInterstitial();
            TrackRunEnd("quit"); // left from the pause menu mid-run (a finished run was already reported)
            // A clean cut to the base while Home fades in (the tower is gone).
            _game.Resume();
            _game.Idle();
            _view.ResetStack(new StackSim(0).Top);
            ApplyLook(_theme.Palette);
            _view.SetAttract(true);
            _rig.SetZoomOut(0f, snap: true);
            _rig.Follow(0f, snap: true);
            _rig.SetHome(true, snap: true);
            _sky.SetHeight(0);
            _game.Ghost.Hide();
            _router.Reset(_home);
            _music.Override(null); // a trial soundtrack ends with its run
            _mascot.ResetMood();
            _music.Home();
            if (_auth.IsSignedIn) _ = RefreshFriends();
        }

        public async void DeleteAccount()
        {
            try
            {
                await _api.DeleteAccountAsync();
            }
            catch (ApiException e)
            {
                _router.Toast(Errors.Describe(e));
                return;
            }
            _auth.ForgetDevice();
            _save.ClearAccountData();
            _friends.Clear();
            _router.Toast(L.T("flow.account_deleted"));
            _signedIn = SignInAndSync(); // a fresh guest account
            await _signedIn;
            _router.Reset(_home);
        }

        // --- helpers -----------------------------------------------------------------------------

        private async Task MaybeInterstitial()
        {
            if (_router.Current != _gameOver) return; // only between runs, never from menus
            if (!_adPolicy.ShouldShowInterstitial(_save.IsFirstSession, _lastRunWasRecord, Time.realtimeSinceStartup)) return;
            _telemetry.InFullScreenAd = true;
            try
            {
                await _ads.ShowInterstitialAsync(AdPlacements.BetweenRuns);
            }
            finally
            {
                _telemetry.InFullScreenAd = false;
            }
            _adPolicy.RecordInterstitialShown(Time.realtimeSinceStartup);
        }

        /// <summary>A rewarded ad; the app pause it causes isn't "leaving the app" for session telemetry.</summary>
        private async Task<bool> ShowRewarded(string placement)
        {
            _telemetry.InFullScreenAd = true;
            try
            {
                return await _ads.ShowRewardedAsync(placement);
            }
            finally
            {
                _telemetry.InFullScreenAd = false;
            }
        }

        private void TrackRunEnd(string reason)
        {
            if (!_runOpen) return;
            _runOpen = false;
            _telemetry.Track(TelemetryEvents.RunEnd, ("run_id", _game.Setup?.RunId), ("end_reason", reason), ("score", _game.Score),
                ("placed", _game.Height), ("continues", _game.ContinuesUsed), ("music", _music.StyleId));
        }

        private (string Key, object Value)[] SessionProps() => new (string Key, object Value)[]
        {
            ("os", SystemInfo.operatingSystem),
            ("device_ram_mb", SystemInfo.systemMemorySize),
            ("language", _localizer?.Language),
            ("look", _theme.Current.ToString()),
        };

        private int BestScore() => Mathf.Max(_api.Profile?.BestScore ?? 0, _save.LocalBest);

        /// <summary>The friend just above the player's best: the ghost to chase in normal runs.</summary>
        private Friend NextFriendToBeat(int best)
        {
            Friend next = null;
            foreach (Friend f in _friends)
            {
                if (f.BestScore == null || f.BestPlaced == null || f.BestScore <= best) continue;
                if (next == null || f.BestScore < next.BestScore) next = f;
            }
            return next;
        }

        private async Task RefreshFriends()
        {
            try
            {
                _friends = await _api.FriendsAsync();
            }
            catch (ApiException)
            {
                // keep the previous list
            }
        }

        private void OnDestroy() => _deepLinks?.Dispose();
    }
}
