using System.Collections.Generic;
using System.Threading.Tasks;
using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Core.Simulation;
using StackIt.Game;
using StackIt.Integrations;
using StackIt.Services;
using StackIt.Services.Native;
using StackIt.UI;
using UnityEngine;

namespace StackIt.Bootstrap
{
    /// <summary>
    /// Composition root and game flow. Creates every service by hand (no DI framework), builds the
    /// world and the UI in code, and sequences runs: play → miss → (continue) → results → share.
    /// Boots automatically after the first scene loads, so the scene itself can stay empty.
    /// </summary>
    public sealed class App : MonoBehaviour, IGameFlow
    {
        private AppConfig _config;
        private AdsConfig _adsConfig;
        private PlayerPrefsStore _store;
        private SaveService _save;
        private AuthService _auth;
        private GameApi _api;
        private IAdsService _ads;
        private IShareService _share;
        private IHaptics _haptics;
        private AudioService _audio;
        private RecorderService _recorder;
        private UnityLocalizer _localizer;
        private DeepLinkService _deepLinks;
        private AdPolicy _adPolicy;

        private CameraRig _rig;
        private SkyBackdrop _sky;
        private StackView _view;
        private GameController _game;

        private Canvas _canvas;
        private Canvas _overlay;
        private UiRouter _router;
        private HomeScreen _home;
        private HudScreen _hud;
        private GameOverScreen _gameOver;
        private ChallengeIntroScreen _intro;
        private CodeEntryScreen _codeEntry;

        private Task _signedIn = Task.CompletedTask;
        private List<Friend> _friends = new List<Friend>();
        private SubmitRunResponse _lastResult;
        private bool _lastRunWasRecord;
        private bool _starting;

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

            _localizer = new UnityLocalizer();
            await _localizer.InitializeAsync(_save.Language);
            L.Localizer = _localizer;
            _localizer.Changed += RebuildUi;

            var client = new ApiClient(_config.apiBaseUrl);
            _auth = new AuthService(client, _store, new NativeSignInService(_config.googleWebClientId, _config.googleIosClientId));
            _api = new GameApi(client, new OfflineRunQueue(_store), _save);
            _api.ProfileChanged += _ => ApplyCosmetics();

            _ads = _adsConfig.adsEnabled ? AdsServiceRegistry.Create() : new NoAdsService();
            _share = new NativeShareService();
            _haptics = new NativeHaptics { Enabled = _save.HapticsOn };
            _audio = gameObject.AddComponent<AudioService>();
            _audio.Enabled = _save.SoundOn;
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
            _router.Reset(_home);

            _deepLinks = new DeepLinkService(_config.apiBaseUrl, _config.urlScheme);
            _deepLinks.ChallengeReceived += OpenChallenge;
            _deepLinks.Start(firstLaunch: _save.IsFirstSession);

            _signedIn = SignInAndSync();
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
        }

        // --- world & UI --------------------------------------------------------------------------

        private void BuildWorld()
        {
            _rig = CameraRig.Create();
            _rig.Camera.cullingMask &= ~(1 << EndCardRenderer.Layer);
            DontDestroyOnLoad(_rig.gameObject);
            _sky = SkyBackdrop.Create(_rig.Camera);

            // Soft warm key light from the upper left; the sky provides the ambient fill (SkyBackdrop),
            // so the three visible faces of each block read as light, mid and shade.
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.95f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.transform.rotation = Quaternion.Euler(58f, -20f, 0f);
            light.shadows = LightShadows.None;
            DontDestroyOnLoad(light.gameObject);

            var postFx = new GameObject("PostFx").AddComponent<UrpPostFx>();
            DontDestroyOnLoad(postFx.gameObject);
            postFx.Configure(_rig.Camera);

            var world = new GameObject("World");
            DontDestroyOnLoad(world);
            _view = world.AddComponent<StackView>();
            var fx = world.AddComponent<HypeFx>();
            fx.Init(_rig, postFx, _haptics, _audio);
            RivalGhostView ghost = RivalGhostView.Create(world.transform);
            _game = world.AddComponent<GameController>();
            _game.Init(_view, fx, _rig, _sky, ghost);
            _game.Missed += OnMissed;

            ApplyCosmetics();
            _view.ResetStack(new StackSim(0).Top);
            _rig.SetHome(true, snap: true);
        }

        private void BuildUi()
        {
            _canvas = UiKit.CreateCanvas("UI", 10);
            _overlay = UiKit.CreateCanvas("Overlay", 100);
            _router = new UiRouter(_overlay.transform, _ads);
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
                Game = _game,
                Flow = this,
                Router = _router,
            };
            Transform root = _canvas.transform;
            _home = new HomeScreen(ctx, root);
            _hud = new HudScreen(ctx, root);
            _gameOver = new GameOverScreen(ctx, root);
            _intro = new ChallengeIntroScreen(ctx, root);
            _codeEntry = new CodeEntryScreen(ctx, root);
            _home.Friends = new FriendsScreen(ctx, root);
            _home.Leaderboard = new LeaderboardScreen(ctx, root);
            _home.Shop = new ShopScreen(ctx, root);
            _home.Settings = new SettingsScreen(ctx, root);
            _home.Daily = new DailyRewardScreen(ctx, root);
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

        private void ApplyCosmetics()
        {
            if (_game != null && _game.Phase == RunPhase.Playing) return; // never mid-run
            _view.SetSkin(_api?.Profile?.EquippedBlockSkin);
            _sky.SetTheme(_api?.Profile?.EquippedTheme);
            _view.SetHorizon(_sky.Horizon);
        }

        // --- game flow (IGameFlow) ---------------------------------------------------------------

        public async void PlayNormal()
        {
            if (_starting) return;
            _starting = true;
            try
            {
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

        public async void PlayChallenge(string code)
        {
            if (_starting) return;
            _starting = true;
            try
            {
                await _signedIn;
                StartRunResponse run = await _api.StartChallengeRunAsync(code);
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

        public async void OpenChallenge(string code)
        {
            try
            {
                await _signedIn;
                ChallengeInfo info = await _api.GetChallengeAsync(code);
                if (info.IsOwn)
                {
                    _router.Toast(L.T("error.own_challenge"));
                    return;
                }
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
            ApplyCosmetics();
            _rig.SetHome(false);
            _game.Begin(setup);
            _router.Reset(_hud);
            _recorder.BeginRun();
        }

        private void OnMissed()
        {
            if (_game.CanContinue && _ads.RewardedReady)
            {
                _gameOver.ShowContinueOffer();
                _router.Show(_gameOver, remember: false);
            }
            else
            {
                FinishRun();
            }
        }

        public async void ContinueWithAd()
        {
            bool rewarded = await _ads.ShowRewardedAsync();
            _adPolicy.RecordRewardedShown(Time.realtimeSinceStartup);
            if (rewarded && _game.Continue())
            {
                _router.Reset(_hud);
                return;
            }
            if (!rewarded) _router.Toast(L.T("flow.no_ad"));
            FinishRun();
        }

        public void DeclineContinue() => FinishRun();

        private async void FinishRun()
        {
            if (!_game.Finish()) return;
            _recorder.EndRun();
            _adPolicy.RecordRunEnded();

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
                _lastResult = await _api.SubmitRunAsync(setup.RunId, _game.Events, score);
                _gameOver.ShowServerResult(_lastResult, _ads.RewardedReady);
            }
            catch (ApiException e)
            {
                _gameOver.ShowServerResult(new SubmitRunResponse { Status = "rejected", Reason = e.Code }, false);
            }
        }

        public async void DoubleCoins()
        {
            if (_lastResult == null) return;
            bool rewarded = await _ads.ShowRewardedAsync();
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
        public async void ShareChallenge()
        {
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
                    clip = await _recorder.ExportClipAsync(new EndCard(challenge.Score, L.T("share.headline"), challenge.Code));
                _share.Share(L.T("share.text", challenge.Score, challenge.Code), challenge.Url, clip);
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
            await MaybeInterstitial();
            // A clean cut to the base block while Home fades in (the tower is gone).
            _view.ResetStack(new StackSim(0).Top);
            _rig.SetZoomOut(0f, snap: true);
            _rig.Follow(0f, snap: true);
            _rig.SetHome(true, snap: true);
            _sky.SetHeight(0);
            _game.Ghost.Hide();
            _router.Reset(_home);
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
            await _ads.ShowInterstitialAsync();
            _adPolicy.RecordInterstitialShown(Time.realtimeSinceStartup);
        }

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
