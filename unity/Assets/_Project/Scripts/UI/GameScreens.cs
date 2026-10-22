using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Core;
using StackIt.Core.Simulation;
using StackIt.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StackIt.UI
{
    public sealed class HomeScreen : UiScreen
    {
        private readonly TextMeshProUGUI _best;
        private readonly TextMeshProUGUI _coins;
        private readonly TextMeshProUGUI _pending;
        private readonly Image _dailyDot;

        public UiScreen Friends, Leaderboard, Shop, Settings, Daily, CodeEntry;

        public HomeScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Home", new Color(0, 0, 0, 0))
        {
            // Top bar: daily reward (left), coins (right).
            RectTransform top = UiKit.Rect("Top", Safe);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(-2 * Theme.Gutter, 140);
            top.anchoredPosition = new Vector2(0, -Theme.Gutter);
            UiKit.Row(top, 24).childForceExpandWidth = false;

            Button daily = UiKit.Button(top, L.T("home.daily"), ButtonStyle.Secondary, () => Ctx.Router.Show(Daily), 120);
            UiKit.Size((RectTransform)daily.transform, width: 330);
            _dailyDot = UiKit.Panel(daily.transform, "Dot", Theme.Magenta, rounded: true);
            _dailyDot.rectTransform.anchorMin = _dailyDot.rectTransform.anchorMax = new Vector2(1, 1);
            _dailyDot.rectTransform.sizeDelta = new Vector2(34, 34);
            _dailyDot.rectTransform.anchoredPosition = new Vector2(-14, -14);

            UiKit.Size(UiKit.Rect("Fill", top), flexibleWidth: 1);
            Image coinChip = UiKit.Panel(top, "Coins", new Color(1f, 0.72f, 0.3f, 0.14f), rounded: true);
            UiKit.Size(coinChip.rectTransform, width: 330);
            _coins = UiKit.Label(coinChip.transform, "0", 50, Theme.Amber, TextAlignmentOptions.Center, true);
            UiKit.Stretch(_coins.rectTransform);

            // Center: logo, best, play.
            RectTransform center = UiKit.Rect("Center", Safe);
            center.anchorMin = new Vector2(0, 0.28f);
            center.anchorMax = new Vector2(1, 0.78f);
            center.offsetMin = new Vector2(Theme.Gutter * 2, 0);
            center.offsetMax = new Vector2(-Theme.Gutter * 2, 0);
            UiKit.Column(center, 28, null, TextAnchor.MiddleCenter);

            TextMeshProUGUI logo = UiKit.Display(center, "STACK IT", 190, Theme.Moon);
            logo.outlineColor = new Color(1f, 0.24f, 0.6f, 0.9f);
            logo.outlineWidth = 0.18f;
            UiKit.Size(logo.rectTransform, height: 220);
            _best = UiKit.Label(center, "", 50, Theme.Lavender, TextAlignmentOptions.Center, true);
            _best.characterSpacing = 6;
            UiKit.Size(_best.rectTransform, height: 70);
            UiKit.Spacer(center, 60);
            Button play = UiKit.Button(center, L.T("home.play"), ButtonStyle.Primary, () => Ctx.Flow.PlayNormal(), 190);
            play.GetComponentInChildren<TextMeshProUGUI>().fontSize = 72;
            UiKit.Button(center, L.T("home.have_code"), ButtonStyle.Ghost, () => Ctx.Router.Show(CodeEntry), 110);
            _pending = UiKit.Label(center, "", 36, Theme.Lavender);
            UiKit.Size(_pending.rectTransform, height: 50);

            // Bottom navigation, above the banner ad.
            RectTransform nav = UiKit.Rect("Nav", Safe);
            nav.anchorMin = new Vector2(0, 0);
            nav.anchorMax = new Vector2(1, 0);
            nav.pivot = new Vector2(0.5f, 0);
            nav.sizeDelta = new Vector2(-2 * Theme.Gutter, 140);
            nav.anchoredPosition = new Vector2(0, 190); // banner height + margin
            UiKit.Row(nav, 20);
            UiKit.Button(nav, L.T("home.friends"), ButtonStyle.Secondary, () => Ctx.Router.Show(Friends), 130);
            UiKit.Button(nav, L.T("home.ranks"), ButtonStyle.Secondary, () => Ctx.Router.Show(Leaderboard), 130);
            UiKit.Button(nav, L.T("home.shop"), ButtonStyle.Secondary, () => Ctx.Router.Show(Shop), 130);
            UiKit.Button(nav, L.T("home.settings"), ButtonStyle.Secondary, () => Ctx.Router.Show(Settings), 130);

            Ctx.Api.ProfileChanged += Render;
        }

        protected override void OnShow()
        {
            Render(Ctx.Api.Profile);
            RefreshDaily();
        }

        private async void RefreshDaily()
        {
            try
            {
                DailyStatus status = await Ctx.Api.DailyAsync();
                _dailyDot.gameObject.SetActive(!status.ClaimedToday);
            }
            catch (StackIt.Services.ApiException)
            {
                _dailyDot.gameObject.SetActive(false);
            }
        }

        private void Render(Profile profile)
        {
            int best = Mathf.Max(profile?.BestScore ?? 0, Ctx.Save.LocalBest);
            _best.text = best > 0 ? L.T("home.best", best) : L.T("home.tagline");
            _coins.text = (profile?.Coins ?? 0).ToString("N0");
            int pending = Ctx.Api.PendingSubmissions;
            _pending.text = pending > 0 ? L.T("home.pending", pending) : "";
        }
    }

    /// <summary>In-game HUD. The whole screen is the tap target, so there are no buttons to miss.</summary>
    public sealed class HudScreen : UiScreen
    {
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _combo;
        private readonly TextMeshProUGUI _rivalChip;
        private readonly TextMeshProUGUI _callout;
        private readonly TextMeshProUGUI _hint;

        public override bool AllowsBanner => false;

        public HudScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Hud", new Color(0, 0, 0, 0))
        {
            Root.GetComponent<Image>().raycastTarget = true; // tap catcher
            Root.gameObject.AddComponent<TapForwarder>().Hud = this;

            _score = UiKit.Display(Safe, "0", 200, Theme.Moon);
            Place(_score.rectTransform, 0.86f, 260);
            _rivalChip = UiKit.Label(Safe, "", 40, Theme.Cyan, TextAlignmentOptions.Center, true);
            Place(_rivalChip.rectTransform, 0.955f, 70);
            _combo = UiKit.Display(Safe, "", 76, Theme.Amber);
            Place(_combo.rectTransform, 0.74f, 110);
            _callout = UiKit.Display(Safe, "", 96, Theme.Cyan);
            Place(_callout.rectTransform, 0.62f, 240);
            _hint = UiKit.Label(Safe, L.T("hud.tap_hint"), 44, Theme.Lavender);
            Place(_hint.rectTransform, 0.18f, 80);

            ctx.Game.Placed += OnPlaced;
            ctx.Game.RivalProgress += OnRival;
        }

        private static void Place(RectTransform rt, float anchorY, float height)
        {
            rt.anchorMin = new Vector2(0, anchorY);
            rt.anchorMax = new Vector2(1, anchorY);
            rt.sizeDelta = new Vector2(-2 * Theme.Gutter, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private void OnTap() => Ctx.Game.Tap();

        protected override void OnShow()
        {
            RunSetup setup = Ctx.Game.Setup;
            _score.text = Ctx.Game.Score.ToString();
            _combo.text = "";
            _callout.text = "";
            _hint.gameObject.SetActive(Ctx.Game.Height == 0);
            _rivalChip.text = setup != null && setup.RivalScore > 0
                ? L.T("hud.rival", setup.RivalName, setup.RivalScore)
                : "";
        }

        private void OnPlaced(Step step)
        {
            _hint.gameObject.SetActive(false);
            _score.text = step.Score.ToString();
            Tween.Punch(_score.transform, step.Perfect ? 0.3f : 0.12f);
            if (step.Perfect)
            {
                _combo.text = step.Combo > 1 ? L.T("hud.perfect_combo", step.Combo) : L.T("hud.perfect");
                Tween.Punch(_combo.transform, 0.5f, 0.4f);
            }
            else
            {
                _combo.text = "";
            }
        }

        private void OnRival(RivalEvent e)
        {
            string name = Ctx.Game.Setup?.RivalName ?? "";
            _callout.text = e == RivalEvent.Passed ? L.T("hud.passed", name) : L.T("hud.almost", name);
            _callout.color = e == RivalEvent.Passed ? Theme.Cyan : Theme.Lavender;
            Tween.Punch(_callout.transform, 0.6f, 0.5f);
            Tween.Run(_callout, 2.2f, t => _callout.alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f);
        }

        private sealed class TapForwarder : MonoBehaviour, IPointerDownHandler
        {
            public HudScreen Hud;
            public void OnPointerDown(PointerEventData eventData) => Hud.OnTap();
        }
    }

    /// <summary>
    /// Two phases: first "keep going?" (rewarded continue) when allowed, then the results with the
    /// big "Challenge a friend" call to action.
    /// </summary>
    public sealed class GameOverScreen : UiScreen
    {
        private readonly RectTransform _continuePanel;
        private readonly RectTransform _resultPanel;
        private readonly TextMeshProUGUI _banner;
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _best;
        private readonly TextMeshProUGUI _coins;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _duel;
        private readonly Button _double;
        private readonly Button _challenge;

        public GameOverScreen(UiContext ctx, Transform parent) : base(ctx, parent, "GameOver", new Color(0.03f, 0.01f, 0.09f, 0.55f))
        {
            // Phase 1: continue offer.
            _continuePanel = UiKit.Stretch(UiKit.Rect("Continue", Safe), Theme.Gutter * 2, 0, Theme.Gutter * 2, 0);
            UiKit.Column(_continuePanel, 36, null, TextAnchor.MiddleCenter);
            UiKit.Label(_continuePanel, L.T("over.keep_going"), 84, Theme.Moon, TextAlignmentOptions.Center, true);
            UiKit.Button(_continuePanel, L.T("over.continue_ad"), ButtonStyle.Rival, () => Ctx.Flow.ContinueWithAd(), 170);
            UiKit.Button(_continuePanel, L.T("over.no_thanks"), ButtonStyle.Ghost, () => Ctx.Flow.DeclineContinue(), 120);

            // Phase 2: results.
            _resultPanel = UiKit.Stretch(UiKit.Rect("Results", Safe), Theme.Gutter * 1.5f, Theme.Gutter * 2, Theme.Gutter * 1.5f, Theme.Gutter);
            UiKit.Column(_resultPanel, 22, null, TextAnchor.MiddleCenter);
            _banner = UiKit.Display(_resultPanel, L.T("over.new_record"), 70, Theme.Amber);
            UiKit.Size(_banner.rectTransform, height: 100);
            _score = UiKit.Display(_resultPanel, "0", 240, Theme.Moon);
            UiKit.Size(_score.rectTransform, height: 270);
            _best = UiKit.Label(_resultPanel, "", 46, Theme.Lavender, TextAlignmentOptions.Center, true);
            _duel = UiKit.Label(_resultPanel, "", 50, Theme.Cyan, TextAlignmentOptions.Center, true);
            _coins = UiKit.Label(_resultPanel, "", 48, Theme.Amber, TextAlignmentOptions.Center, true);
            _status = UiKit.Label(_resultPanel, "", 36, Theme.Lavender);
            UiKit.Spacer(_resultPanel, 20);
            _challenge = UiKit.Button(_resultPanel, L.T("over.challenge"), ButtonStyle.Primary, () => Ctx.Flow.ShareChallenge(), 190);
            _challenge.GetComponentInChildren<TextMeshProUGUI>().fontSize = 60;
            _double = UiKit.Button(_resultPanel, L.T("over.double"), ButtonStyle.Secondary, () => Ctx.Flow.DoubleCoins(), 130);
            RectTransform row = UiKit.Rect("Row", _resultPanel);
            UiKit.Size(row, height: 130);
            UiKit.Row(row, 24);
            UiKit.Button(row, L.T("over.replay"), ButtonStyle.Secondary, () => Ctx.Flow.PlayNormal(), 130);
            UiKit.Button(row, L.T("over.home"), ButtonStyle.Ghost, () => Ctx.Flow.GoHome(), 130);
        }

        public override bool AllowsBanner => !_continuePanel.gameObject.activeSelf;

        public void ShowContinueOffer()
        {
            _continuePanel.gameObject.SetActive(true);
            _resultPanel.gameObject.SetActive(false);
        }

        /// <summary>Immediate results from the local simulation; the server result refines them.</summary>
        public void ShowLocalResult(int score, bool newRecord, int best, bool ranked)
        {
            _continuePanel.gameObject.SetActive(false);
            _resultPanel.gameObject.SetActive(true);
            _banner.gameObject.SetActive(newRecord);
            if (newRecord) Tween.Punch(_banner.transform, 0.6f, 0.6f);
            _score.text = score.ToString();
            _best.text = L.T("over.best", Mathf.Max(best, score));
            _coins.text = ranked ? L.T("over.coins", Economy.CoinsForRun(score)) : "";
            _status.text = ranked ? L.T("over.verifying") : L.T("over.unranked");
            _duel.text = "";
            _double.gameObject.SetActive(false);
            _challenge.interactable = false;
        }

        public void ShowServerResult(SubmitRunResponse result, bool rewardedReady)
        {
            if (result == null)
            {
                _status.text = L.T("over.queued");
                return;
            }
            bool verified = result.Status == "verified";
            _status.text = verified ? "" : L.T("over.not_verified");
            _best.text = L.T("over.best", result.BestScore);
            _coins.text = verified ? L.T("over.coins", result.CoinsEarned * (result.CoinsDoubled ? 2 : 1)) : "";
            _double.gameObject.SetActive(verified && rewardedReady && result.CoinsEarned > 0 && !result.CoinsDoubled);
            _challenge.interactable = verified;
            if (result.Challenge != null)
            {
                _duel.text = result.Challenge.Won
                    ? L.T("over.duel_won", result.Challenge.Rival.DisplayName, result.Challenge.Rival.Score)
                    : L.T("over.duel_lost", result.Challenge.Rival.DisplayName, result.Challenge.Rival.Score);
            }
        }

        public void ShowCoinsDoubled(int coins)
        {
            _coins.text = L.T("over.coins", coins);
            Tween.Punch(_coins.transform, 0.5f);
            _double.gameObject.SetActive(false);
        }

        public void SetSharing(bool busy)
        {
            _challenge.interactable = !busy;
            UiKit.SetText(_challenge, busy ? L.T("over.preparing_clip") : L.T("over.challenge"));
        }
    }

    public sealed class ChallengeIntroScreen : UiScreen
    {
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _note;
        private string _code;

        public ChallengeIntroScreen(UiContext ctx, Transform parent) : base(ctx, parent, "ChallengeIntro", Theme.Scrim)
        {
            RectTransform col = UiKit.Stretch(UiKit.Rect("Col", Safe), Theme.Gutter * 2, 0, Theme.Gutter * 2, 0);
            UiKit.Column(col, 30, null, TextAnchor.MiddleCenter);
            UiKit.Label(col, L.T("intro.eyebrow"), 40, Theme.Lavender, TextAlignmentOptions.Center, true).characterSpacing = 8;
            _title = UiKit.Label(col, "", 70, Theme.Moon, TextAlignmentOptions.Center, true);
            _score = UiKit.Display(col, "", 260, Theme.Cyan);
            UiKit.Size(_score.rectTransform, height: 290);
            _note = UiKit.Label(col, L.T("intro.rules"), 44, Theme.Lavender);
            UiKit.Spacer(col, 40);
            UiKit.Button(col, L.T("intro.accept"), ButtonStyle.Primary, () => Ctx.Flow.PlayChallenge(_code), 190);
            UiKit.Button(col, L.T("intro.later"), ButtonStyle.Ghost, () => Ctx.Flow.GoHome(), 120);
        }

        public void Bind(ChallengeInfo info)
        {
            _code = info.Code;
            _title.text = L.T("intro.title", info.CreatorName);
            _score.text = info.Score.ToString();
            _note.text = info.Expired ? L.T("intro.expired") : L.T("intro.rules");
        }
    }

    /// <summary>"Got a challenge code?" — the fallback when a link could not open the app directly.</summary>
    public sealed class CodeEntryScreen : UiScreen
    {
        private readonly TMP_InputField _input;

        public CodeEntryScreen(UiContext ctx, Transform parent) : base(ctx, parent, "CodeEntry", Theme.Night)
        {
            Header(L.T("code.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 32, new RectOffset(0, 0, 80, 0));
            UiKit.Label(body, L.T("code.body"), 46, Theme.Lavender);
            _input = UiKit.Input(body, L.T("code.placeholder"), 12, TMP_InputField.CharacterValidation.Alphanumeric);
            _input.onValidateInput = (text, index, c) => char.ToUpperInvariant(c);
            UiKit.Button(body, L.T("code.submit"), ButtonStyle.Primary, Submit);
        }

        protected override void OnShow()
        {
            _input.text = "";
            _input.ActivateInputField();
        }

        private void Submit()
        {
            string code = DeepLinks.NormalizeTypedCode(_input.text);
            if (code == null)
            {
                Ctx.Router.Toast(L.T("code.invalid"));
                return;
            }
            Ctx.Flow.OpenChallenge(code);
        }
    }
}
