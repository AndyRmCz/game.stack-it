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
    /// <summary>
    /// The tower is the hero: a small title above it, one Play button below, a glass dock with the
    /// four destinations at the bottom. Transparent, so the sky and the base block show through.
    /// </summary>
    public sealed class HomeScreen : UiScreen
    {
        private const string CoinTint = "#D99A0B";

        private readonly TextMeshProUGUI _best;
        private readonly TextMeshProUGUI _coins;
        private readonly TextMeshProUGUI _pending;
        private readonly Image _dailyDot;

        public UiScreen Friends, Leaderboard, Shop, Settings, Daily, CodeEntry;

        public HomeScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Home", new Color(0, 0, 0, 0))
        {
            // Top bar: daily gift (left), coins (right).
            RectTransform top = UiKit.Band(UiKit.Rect("Top", Safe), 1f, 120, pivotY: 1f);
            top.anchoredPosition = new Vector2(0, -40);
            HorizontalLayoutGroup row = UiKit.Row(top, 24);
            row.childForceExpandWidth = false;
            row.childControlWidth = true;

            Button daily = UiKit.IconButton(top, Icons.Gift, ButtonStyle.Secondary, () => Ctx.Router.Show(Daily), 120);
            _dailyDot = UiKit.Panel(daily.transform, "Dot", Theme.Tomato, rounded: true, radius: 16);
            _dailyDot.rectTransform.anchorMin = _dailyDot.rectTransform.anchorMax = new Vector2(1, 1);
            _dailyDot.rectTransform.sizeDelta = new Vector2(32, 32);
            _dailyDot.rectTransform.anchoredPosition = new Vector2(-10, -10);
            UiKit.Size(UiKit.Rect("Fill", top), flexibleWidth: 1);
            _coins = UiKit.Pill(top, "0", Theme.Paper, Theme.Ink, 108, $"<color={CoinTint}>{Icons.Coin}</color>");

            // Title, small, above the tower.
            RectTransform title = UiKit.Band(UiKit.Rect("Title", Safe), 0.79f, 300);
            UiKit.Column(title, 4, null, TextAnchor.MiddleCenter);
            TextMeshProUGUI logo = UiKit.Display(title, "STACK IT", 150, Theme.Ink);
            UiKit.OneLine(logo, 90);
            UiKit.Size(logo.rectTransform, height: 180);
            _best = UiKit.Caps(title, "", Theme.InkSoft, 36);
            UiKit.Size(_best.rectTransform, height: 60);

            // Play, below the tower.
            RectTransform play = UiKit.Band(UiKit.Rect("Play", Safe), 0.27f, 190, inset: Theme.Gutter * 2.5f);
            UiKit.Column(play, 0);
            UiKit.Button(play, L.T("home.play"), ButtonStyle.Primary, () => Ctx.Flow.PlayNormal(), 190, Icons.Play);

            RectTransform link = UiKit.Band(UiKit.Rect("Code", Safe), 0.195f, 100, inset: Theme.Gutter * 2f);
            UiKit.Column(link, 0);
            Button code = UiKit.Button(link, L.T("home.have_code"), ButtonStyle.Ghost, () => Ctx.Router.Show(CodeEntry), 100);
            code.GetComponentInChildren<TextMeshProUGUI>().color = Theme.InkSoft;
            _pending = UiKit.Label(Safe, "", 32, Theme.InkSoft);
            UiKit.Band(_pending.rectTransform, 0.155f, 50);

            // Dock, above the banner ad.
            RectTransform dock = UiKit.Card(Safe, "Dock", Theme.Glass, radius: 56);
            UiKit.Band(dock, 0f, 190, pivotY: 0f);
            dock.anchoredPosition = new Vector2(0, 190); // banner height + margin
            UiKit.Row(dock, 0).padding = new RectOffset(16, 16, 20, 16);
            UiKit.DockItem(dock, Icons.Friends, L.T("home.friends"), () => Ctx.Router.Show(Friends));
            UiKit.DockItem(dock, Icons.Ranks, L.T("home.ranks"), () => Ctx.Router.Show(Leaderboard));
            UiKit.DockItem(dock, Icons.Shop, L.T("home.shop"), () => Ctx.Router.Show(Shop));
            UiKit.DockItem(dock, Icons.Settings, L.T("home.settings"), () => Ctx.Router.Show(Settings));

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
            _coins.text = UiKit.WithIcon($"<color={CoinTint}>{Icons.Coin}</color>", (profile?.Coins ?? 0).ToString("N0"));
            int pending = Ctx.Api.PendingSubmissions;
            _pending.text = pending > 0 ? L.T("home.pending", pending) : "";
        }
    }

    /// <summary>In-game HUD. The whole screen is the tap target, so there are no buttons to miss.</summary>
    public sealed class HudScreen : UiScreen
    {
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _combo;
        private readonly TextMeshProUGUI _rival;
        private readonly TextMeshProUGUI _callout;
        private readonly TextMeshProUGUI _hint;

        public override bool AllowsBanner => false;

        public HudScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Hud", new Color(0, 0, 0, 0))
        {
            Root.GetComponent<Image>().raycastTarget = true; // tap catcher
            Root.gameObject.AddComponent<TapForwarder>().Hud = this;

            _rival = UiKit.Pill(Safe, "", Theme.Pool, Theme.Paper, 84, Icons.Trophy);
            Float(_rival, 0.955f);
            _score = UiKit.Display(Safe, "0", 230, Theme.Paper, shadow: true);
            UiKit.Band(_score.rectTransform, 0.85f, 260);
            _combo = UiKit.Pill(Safe, "", Theme.Paper, Theme.Ink, 88);
            Float(_combo, 0.755f);
            _callout = UiKit.Display(Safe, "", 76, Theme.Paper, shadow: true);
            UiKit.Band(_callout.rectTransform, 0.66f, 120);
            UiKit.OneLine(_callout, 44);
            _hint = UiKit.Label(Safe, L.T("hud.tap_hint"), 42, Theme.Paper, TextAlignmentOptions.Center, bold: true);
            _hint.gameObject.AddComponent<TextEffect>().Shadow = true;
            UiKit.Band(_hint.rectTransform, 0.2f, 80);

            ctx.Game.Placed += OnPlaced;
            ctx.Game.RivalProgress += OnRival;
        }

        /// <summary>Centres a free-floating pill at a vertical anchor.</summary>
        private static void Float(TextMeshProUGUI pill, float anchorY)
        {
            var bg = (RectTransform)pill.transform.parent;
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, anchorY);
            bg.pivot = new Vector2(0.5f, 0.5f);
            bg.anchoredPosition = Vector2.zero;
        }

        private static void ShowPill(TextMeshProUGUI pill, bool visible) => pill.transform.parent.gameObject.SetActive(visible);

        private void OnTap() => Ctx.Game.Tap();

        protected override void OnShow()
        {
            RunSetup setup = Ctx.Game.Setup;
            _score.text = Ctx.Game.Score.ToString();
            ShowPill(_combo, false);
            _callout.text = "";
            _hint.gameObject.SetActive(Ctx.Game.Height == 0);
            bool rival = setup != null && setup.RivalScore > 0;
            ShowPill(_rival, rival);
            if (rival) _rival.text = UiKit.WithIcon(Icons.Trophy, L.T("hud.rival", setup.RivalName, setup.RivalScore));
        }

        private void OnPlaced(Step step)
        {
            _hint.gameObject.SetActive(false);
            _score.text = step.Score.ToString();
            Tween.Punch(_score.transform, step.Perfect ? 0.16f : 0.08f, 0.3f);
            if (step.Perfect)
            {
                ShowPill(_combo, true);
                _combo.text = UiKit.WithIcon(Icons.Star, step.Combo > 1 ? L.T("hud.perfect_combo", step.Combo) : L.T("hud.perfect"));
                Transform pill = _combo.transform.parent;
                Tween.Punch(pill, 0.18f, 0.35f);
            }
            else
            {
                ShowPill(_combo, false);
            }
        }

        private void OnRival(RivalEvent e)
        {
            string name = Ctx.Game.Setup?.RivalName ?? "";
            _callout.text = e == RivalEvent.Passed ? L.T("hud.passed", name) : L.T("hud.almost", name);
            Tween.Punch(_callout.transform, 0.2f, 0.4f);
            Tween.Run(_callout, 2.4f, t => _callout.alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f);
            if (e == RivalEvent.Passed) ShowPill(_rival, false);
        }

        private sealed class TapForwarder : MonoBehaviour, IPointerDownHandler
        {
            public HudScreen Hud;
            public void OnPointerDown(PointerEventData eventData) => Hud.OnTap();
        }
    }

    /// <summary>
    /// Two phases: first "keep going?" (rewarded continue) when allowed, then the results sheet with
    /// the big "Challenge a friend" call to action. The sheet leaves the top of the screen to the
    /// camera pulling back over the whole tower.
    /// </summary>
    public sealed class GameOverScreen : UiScreen
    {
        private const string CoinTint = "#D99A0B";

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

        public GameOverScreen(UiContext ctx, Transform parent) : base(ctx, parent, "GameOver", new Color(0.086f, 0.129f, 0.243f, 0.12f))
        {
            // Phase 1: continue offer.
            _continuePanel = Sheet("Continue", new RectOffset(64, 64, 72, 56));
            UiKit.Display(_continuePanel, L.T("over.keep_going"), 76, Theme.Ink);
            UiKit.Spacer(_continuePanel, 8);
            UiKit.Button(_continuePanel, L.T("over.continue_ad"), ButtonStyle.Rival, () => Ctx.Flow.ContinueWithAd(), 170, Icons.Play);
            UiKit.Button(_continuePanel, L.T("over.no_thanks"), ButtonStyle.Ghost, () => Ctx.Flow.DeclineContinue(), 120);

            // Phase 2: results.
            _resultPanel = Sheet("Results", new RectOffset(64, 64, 64, 56));
            RectTransform bannerRow = UiKit.Rect("Banner", _resultPanel);
            HorizontalLayoutGroup bannerLayout = UiKit.Row(bannerRow, 0);
            bannerLayout.childForceExpandWidth = false;
            UiKit.Size(bannerRow, height: 84);
            _banner = UiKit.Pill(bannerRow, L.T("over.new_record"), Theme.Sun, Theme.Ink, 84, Icons.Star);
            _score = UiKit.Display(_resultPanel, "0", 210, Theme.Ink);
            UiKit.Size(_score.rectTransform, height: 220);
            _best = UiKit.Caps(_resultPanel, "", Theme.InkSoft, 34);
            _duel = UiKit.Label(_resultPanel, "", 44, Theme.Pool, TextAlignmentOptions.Center, bold: true);
            _coins = UiKit.Label(_resultPanel, "", 46, Theme.Ink, TextAlignmentOptions.Center, bold: true);
            _status = UiKit.Label(_resultPanel, "", 34, Theme.InkSoft);
            UiKit.Spacer(_resultPanel, 8);
            _challenge = UiKit.Button(_resultPanel, L.T("over.challenge"), ButtonStyle.Rival, () => Ctx.Flow.ShareChallenge(), 180, Icons.Share);
            _double = UiKit.Button(_resultPanel, L.T("over.double"), ButtonStyle.Reward, () => Ctx.Flow.DoubleCoins(), 130, Icons.Play);
            RectTransform row = UiKit.Rect("Row", _resultPanel);
            UiKit.Size(row, height: 140);
            UiKit.Row(row, 24);
            UiKit.Button(row, L.T("over.replay"), ButtonStyle.Primary, () => Ctx.Flow.PlayNormal(), 140, Icons.Replay);
            UiKit.Button(row, L.T("over.home"), ButtonStyle.Secondary, () => Ctx.Flow.GoHome(), 140, Icons.Home);
        }

        /// <summary>A paper sheet pinned to the bottom that grows with its content.</summary>
        private RectTransform Sheet(string name, RectOffset padding)
        {
            RectTransform sheet = UiKit.Card(Safe, name, radius: 64);
            sheet.anchorMin = new Vector2(0, 0);
            sheet.anchorMax = new Vector2(1, 0);
            sheet.pivot = new Vector2(0.5f, 0);
            sheet.sizeDelta = new Vector2(-2 * SheetMargin, 600);
            sheet.anchoredPosition = new Vector2(0, SheetMargin);
            UiKit.Column(sheet, 18, padding, TextAnchor.UpperCenter);
            sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return sheet;
        }

        private const float SheetMargin = 24f;

        private static void Rise(RectTransform sheet)
        {
            Tween.Run(sheet, 0.45f, t =>
            {
                if (sheet != null) sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(SheetMargin - 700f, SheetMargin, Tween.EaseOutCubic(t)));
            });
        }

        public override bool AllowsBanner => !_continuePanel.gameObject.activeSelf;

        public void ShowContinueOffer()
        {
            _continuePanel.gameObject.SetActive(true);
            _resultPanel.gameObject.SetActive(false);
            Rise(_continuePanel);
        }

        /// <summary>Immediate results from the local simulation; the server result refines them.</summary>
        public void ShowLocalResult(int score, bool newRecord, int best, bool ranked)
        {
            _continuePanel.gameObject.SetActive(false);
            _resultPanel.gameObject.SetActive(true);
            _banner.transform.parent.parent.gameObject.SetActive(newRecord);
            if (newRecord) Tween.Punch(_banner.transform.parent, 0.25f, 0.5f);
            _score.text = score.ToString();
            CountUp(score);
            _best.text = L.T("over.best", Mathf.Max(best, score));
            SetCoins(ranked ? Economy.CoinsForRun(score) : 0);
            _status.text = ranked ? L.T("over.verifying") : L.T("over.unranked");
            _status.gameObject.SetActive(true);
            _duel.gameObject.SetActive(false);
            _double.gameObject.SetActive(false);
            _challenge.interactable = false;
            Rise(_resultPanel);
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
            _status.gameObject.SetActive(!verified);
            _best.text = L.T("over.best", result.BestScore);
            SetCoins(verified ? result.CoinsEarned * (result.CoinsDoubled ? 2 : 1) : 0);
            _double.gameObject.SetActive(verified && rewardedReady && result.CoinsEarned > 0 && !result.CoinsDoubled);
            _challenge.interactable = verified;
            if (result.Challenge != null)
            {
                _duel.gameObject.SetActive(true);
                _duel.text = result.Challenge.Won
                    ? L.T("over.duel_won", result.Challenge.Rival.DisplayName, result.Challenge.Rival.Score)
                    : L.T("over.duel_lost", result.Challenge.Rival.DisplayName, result.Challenge.Rival.Score);
            }
        }

        public void ShowCoinsDoubled(int coins)
        {
            SetCoins(coins);
            Tween.Punch(_coins.transform, 0.25f);
            _double.gameObject.SetActive(false);
        }

        public void SetSharing(bool busy)
        {
            _challenge.interactable = !busy;
            UiKit.SetText(_challenge, busy ? L.T("over.preparing_clip") : L.T("over.challenge"), busy ? null : Icons.Share);
        }

        private void SetCoins(int coins)
        {
            _coins.gameObject.SetActive(coins > 0);
            _coins.text = UiKit.WithIcon($"<color={CoinTint}>{Icons.Coin}</color>", L.T("over.coins", coins));
        }

        /// <summary>The score counts up from zero: a small payoff for the run.</summary>
        private void CountUp(int score)
        {
            TextMeshProUGUI label = _score;
            Tween.Run(label, Mathf.Clamp(score * 0.03f, 0.4f, 1.2f), t =>
            {
                if (label != null) label.text = Mathf.RoundToInt(score * Tween.EaseOutCubic(t)).ToString();
            });
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
            RectTransform card = UiKit.Card(Safe, "Card", radius: 64);
            card.anchorMin = new Vector2(0, 0.5f);
            card.anchorMax = new Vector2(1, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(-2 * Theme.Gutter, 900);
            card.anchoredPosition = Vector2.zero;
            UiKit.Column(card, 20, new RectOffset(64, 64, 72, 56), TextAnchor.MiddleCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Caps(card, L.T("intro.eyebrow"), Theme.Pool, 34);
            _title = UiKit.Label(card, "", 52, Theme.Ink, TextAlignmentOptions.Center, bold: true);
            _score = UiKit.Display(card, "", 240, Theme.Pool);
            UiKit.Size(_score.rectTransform, height: 250);
            _note = UiKit.Label(card, L.T("intro.rules"), 38, Theme.InkSoft);
            UiKit.Spacer(card, 16);
            UiKit.Button(card, L.T("intro.accept"), ButtonStyle.Rival, () => Ctx.Flow.PlayChallenge(_code), 180, Icons.Play);
            UiKit.Button(card, L.T("intro.later"), ButtonStyle.Ghost, () => Ctx.Flow.GoHome(), 120);
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

        public CodeEntryScreen(UiContext ctx, Transform parent) : base(ctx, parent, "CodeEntry", Theme.Enamel)
        {
            Header(L.T("code.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 32, new RectOffset(0, 0, 60, 0));
            UiKit.Label(body, L.T("code.body"), Theme.BodySize, Theme.InkSoft);
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
