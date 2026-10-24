using System;
using System.Collections.Generic;
using NUnit.Framework;
using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Game;
using StackIt.Services;
using StackIt.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StackIt.Tests
{
    /// <summary>
    /// The UI is built in code (no prefabs), so this builds every screen and checks that each
    /// graphic takes its colour from the look: a themed component on it, a themed owner
    /// (<see cref="ThemedPart"/>), or an explicit <see cref="IgnoreTheming"/> (hit areas, previews).
    /// A graphic with none of these would keep whatever colour it was built with in both looks.
    /// </summary>
    public class NoHardcodedColorsTests
    {
        private sealed class Store : IKeyValueStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string GetString(string key) => _values.TryGetValue(key, out string v) ? v : null;
            public void SetString(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        private sealed class Localizer : ILocalizer
        {
            public string Language => "en";
            public event Action Changed { add { } remove { } }
            public string Get(string key) => key;
            public void SetLanguage(string code) { }
        }

        private sealed class Flow : IGameFlow
        {
            public void PlayNormal() { }
            public void PlayAgainst(string rivalName, int rivalScore, int rivalHeight) { }
            public void PlayChallenge(string code) { }
            public void PlayDaily() { }
            public void CopyResult() { }
            public void OpenChallenge(string code, string via) { }
            public void ContinueWithAd() { }
            public void DeclineContinue() { }
            public void DoubleCoins() { }
            public void ShareChallenge() { }
            public void ShareChallengeTo(ShareTarget target) { }
            public void Pause() { }
            public void Resume() { }
            public void Restart() { }
            public void GoHome() { }
            public void DeleteAccount() { }
            public void SetLook(ThemeId look) { }
            public void SetReduceMotion(bool on) { }
            public void SetMusic(bool on) { }
            public void PreviewMusic(string styleId) { }
            public void EndMusicPreview() { }
            public void TryMusic(string styleId) { }
        }

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void EveryGraphicOnEveryScreenFollowsTheLook()
        {
            var store = new Store();
            var save = new SaveService(store);
            var theme = new ThemeService(Look.Catalog, store);
            Look.Init(theme);
            var gameGo = new GameObject("Game");
            _created.Add(gameGo);
            Canvas canvas = UiKit.CreateCanvas("UI", 10);
            _created.Add(canvas.gameObject);
            var ctx = new UiContext
            {
                Config = Resources.Load<StackIt.Bootstrap.AppConfig>(StackIt.Bootstrap.AppConfig.ResourcePath),
                Save = save,
                Ads = new NoAdsService(),
                Localizer = new Localizer(),
                Theme = theme,
                Pending = new PendingChallenges(store),
                Seen = new SeenSkins(store),
                Game = gameGo.AddComponent<GameController>(),
                Flow = new Flow(),
                Api = new GameApi(new ApiClient("http://127.0.0.1:9"), new OfflineRunQueue(store), save),
            };
            ctx.Router = new UiRouter(canvas.transform, ctx.Ads);
            Transform root = canvas.transform;

            var screens = new Func<UiScreen>[]
            {
                () => new SplashScreen(ctx, root),
                () => new HomeScreen(ctx, root, true),
                () => new HudScreen(ctx, root),
                () => new PauseScreen(ctx, root),
                () => new GameOverScreen(ctx, root),
                () => new ChallengeIntroScreen(ctx, root),
                () => new CodeEntryScreen(ctx, root),
                () => new FriendsScreen(ctx, root),
                () => new LeaderboardScreen(ctx, root),
                () => new ShopScreen(ctx, root),
                () => new DailyRewardScreen(ctx, root),
                () => new SettingsScreen(ctx, root),
            };
            foreach (Func<UiScreen> build in screens) build();

            var unthemed = new List<string>();
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_SubMeshUI || graphic is TMP_SelectionCaret) continue; // owned by their label/field
                GameObject go = graphic.gameObject;
                if (go.GetComponent<Themed>() != null || go.GetComponent<ThemedPart>() != null || go.GetComponent<IgnoreTheming>() != null)
                    continue;
                unthemed.Add(Path(graphic.transform));
            }
            CollectionAssert.IsEmpty(unthemed);
        }

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
