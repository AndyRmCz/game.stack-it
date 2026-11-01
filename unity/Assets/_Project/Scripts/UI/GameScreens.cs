using System.Collections.Generic;
using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Client.Theming;
using StackIt.Core;
using StackIt.Core.Simulation;
using StackIt.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>Brief branded loading screen: the logo and three blocks dropping into a stack.</summary>
    public sealed class SplashScreen : UiScreen
    {
        private readonly IsoTower _tower;

        public override bool AllowsBanner => false;

        public SplashScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Splash", ScreenBackground.Gradient)
        {
            RectTransform column = UiKit.Pin(UiKit.Rect("Column", Safe), new Vector2(0.5f, 0.5f), new Vector2(320, 300), Vector2.zero);
            column.pivot = new Vector2(0.5f, 0.5f);
            UiKit.Column(column, 18, null, TextAnchor.MiddleCenter);
            RectTransform holder = UiKit.Rect("Tower", column);
            UiKit.Size(holder, height: 150);
            _tower = holder.gameObject.AddComponent<IsoTower>();
            _tower.raycastTarget = false;
            _tower.Footprint = 0.42f;
            var themed = holder.gameObject.AddComponent<ThemedIsoTower>();
            themed.Target = _tower;
            themed.Blocks = 3;
            themed.WithMoving = false;
            themed.Refresh();
            TextMeshProUGUI logo = UiKit.Text(column, "milli", TextRole.Logo);
            UiKit.Size(logo.rectTransform, height: 70);
            TextEffect.Add(logo, TextEffectKind.LogoGlow);
        }

        protected override void OnShow()
        {
            // The three blocks drop in one after another.
            for (int i = 0; i < 3; i++) _tower.SetDrop(i, 6f);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float delay = 0.12f + 0.16f * i;
                Tween.Run(new object(), delay + 0.38f, t =>
                {
                    if (_tower == null) return;
                    float local = Mathf.Clamp01((t * (delay + 0.38f) - delay) / 0.38f);
                    _tower.SetDrop(index, 6f * (1f - Tween.EaseOutBack(local)));
                });
            }
        }
    }

    /// <summary>
    /// The reference screen. Gift (dot when the daily reward is ready) and coins on top, the logo
    /// and best score, the tower as the hero (the game world behind this see-through screen), the
    /// breathing Play button, the code pill and the floating bottom navigation.
    /// </summary>
    public sealed class HomeScreen : UiScreen
    {
        private readonly TextMeshProUGUI _best;
        private readonly TextMeshProUGUI _coins;
        private readonly TextMeshProUGUI _pending;
        private readonly RoundedRect _giftDot;
        private readonly RoundedRect _friendsDot;
        private readonly RoundedRect _shopDot;

        public UiScreen Friends, Leaderboard, Shop, Settings, Daily, CodeEntry, Missions;
        private readonly RoundedRect _missionsDot;

        public HomeScreen(UiContext ctx, Transform parent, bool reserveBanner) : base(ctx, parent, "Home", ScreenBackground.None)
        {
            // Top bar: gift (left), coins (right).
            RectTransform top = TopBar(Safe);
            Button gift = UiKit.IconButton(top, Icons.Gift, () => Ctx.Router.Show(Daily));
            UiKit.Pin((RectTransform)gift.transform, new Vector2(0, 0.5f), new Vector2(Ui.IconButton, Ui.IconButton), Vector2.zero);
            _giftDot = UiKit.Dot(gift.transform);
            Button missions = UiKit.IconButton(top, Icons.Bolt, () => Ctx.Router.Show(Missions));
            UiKit.Pin((RectTransform)missions.transform, new Vector2(0, 0.5f), new Vector2(Ui.IconButton, Ui.IconButton),
                new Vector2(Ui.IconButton + 10f, 0f));
            _missionsDot = UiKit.Dot(missions.transform);
            _coins = UiKit.CoinPill(top);
            var coins = (RectTransform)_coins.transform.parent;
            coins.anchorMin = coins.anchorMax = coins.pivot = new Vector2(1, 0.5f);
            coins.anchoredPosition = Vector2.zero;

            // Logo and best score.
            RectTransform title = UiKit.Pin(UiKit.Rect("Title", Safe), new Vector2(0.5f, 1f), new Vector2(350, 110),
                new Vector2(0, -(Ui.TopGap + Ui.IconButton + 40)));
            UiKit.Column(title, 6, null, TextAnchor.UpperCenter);
            TextMeshProUGUI logo = UiKit.Text(title, "milli", TextRole.Logo);
            UiKit.Size(logo.rectTransform, height: 68);
            TextEffect.Add(logo, TextEffectKind.LogoGlow);
            _best = UiKit.Text(title, "", TextRole.Overline, T.TextSecondary);
            UiKit.Size(_best.rectTransform, height: 18);
            _pending = UiKit.Text(title, "", TextRole.Caption, T.TextSecondary);
            UiKit.Size(_pending.rectTransform, height: 16);

            // Bottom up: navigation (above the banner's reserved height), code pill, Play.
            float y = Ui.BottomGap + (reserveBanner ? Ui.Banner : 0f);
            RectTransform nav = UiKit.NavBar(Safe);
            UiKit.Band(nav, 0f, Ui.NavHeight, pivotY: 0f).anchoredPosition = new Vector2(0, y);
            _friendsDot = DotOn(UiKit.NavItem(nav, Icons.Friends, L.T("home.friends"), () => Ctx.Router.Show(Friends)));
            UiKit.NavItem(nav, Icons.Ranks, L.T("home.ranks"), () => Ctx.Router.Show(Leaderboard));
            _shopDot = DotOn(UiKit.NavItem(nav, Icons.Shop, L.T("home.shop"), () => Ctx.Router.Show(Shop)));
            UiKit.NavItem(nav, Icons.Settings, L.T("home.settings"), () => Ctx.Router.Show(Settings));
            y += Ui.NavHeight + 22f;

            RectTransform code = UiKit.Band(UiKit.Rect("Code", Safe), 0f, Ui.PillHeight, pivotY: 0f);
            code.anchoredPosition = new Vector2(0, y);
            UiKit.Column(code, 0, null, TextAnchor.MiddleCenter);
            UiKit.CenteredPill(code, L.T("home.have_code"), Icons.Ticket, () => Ctx.Router.Show(CodeEntry));
            y += Ui.PillHeight + 18f;

            RectTransform play = UiKit.Band(UiKit.Rect("Play", Safe), 0f, Ui.PrimaryHeight, pivotY: 0f);
            play.anchoredPosition = new Vector2(0, y);
            UiKit.Column(play, 0);
            UiKit.Primary(play, L.T("home.play"), Icons.Play, () => Ctx.Flow.PlayNormal(), breathe: true);

            Ctx.Api.ProfileChanged += Render;
            Ctx.Pending.Changed += () => UiKit.ShowDot(_friendsDot, Ctx.Pending.Any);
        }

        /// <summary>The bar under the safe area's top: 48 tall, inside the side padding.</summary>
        public static RectTransform TopBar(RectTransform safe)
        {
            RectTransform top = UiKit.Rect("Top", safe);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(-2 * Ui.Side, Ui.IconButton);
            top.anchoredPosition = new Vector2(0, -Ui.TopGap);
            return top;
        }

        private static RoundedRect DotOn(Button navItem) => UiKit.Dot(navItem.transform.Find("Icon"));

        protected override void OnShow()
        {
            Render(Ctx.Api.Profile);
            UiKit.ShowDot(_friendsDot, Ctx.Pending.Any);
            RefreshDaily();
            RefreshShop();
            RefreshMissions();
        }

        /// <summary>The missions dot: a finished mission or the chest is waiting to be claimed.</summary>
        private async void RefreshMissions()
        {
            try
            {
                MissionBoard board = await Ctx.Api.MissionsAsync();
                UiKit.ShowDot(_missionsDot, board.Claimable);
            }
            catch (StackIt.Services.ApiException)
            {
                UiKit.ShowDot(_missionsDot, false); // offline: no dot
            }
        }

        private async void RefreshDaily()
        {
            try
            {
                DailyStatus status = await Ctx.Api.DailyAsync();
                UiKit.ShowDot(_giftDot, !status.ClaimedToday);
            }
            catch (StackIt.Services.ApiException)
            {
                UiKit.ShowDot(_giftDot, false);
            }
        }

        private async void RefreshShop()
        {
            try
            {
                Shop shop = await Ctx.Api.ShopAsync();
                UiKit.ShowDot(_shopDot, Ctx.Seen.HasNew(ShopScreen.Entries(shop), shop.Balance));
            }
            catch (StackIt.Services.ApiException)
            {
                UiKit.ShowDot(_shopDot, false);
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

    /// <summary>
    /// In-game HUD: the whole screen is the tap target. Pause (top-left), the score (top centre,
    /// glowing in Neon), the combo pill that punches in on each perfect, the rival's marker (an
    /// accent2 line and a chip at the rival's height), the "You passed" banner and the near-miss
    /// callout: bare gold arcade lettering (no background) that slams in over the tower.
    /// </summary>
    public sealed class HudScreen : UiScreen
    {
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _combo;
        private readonly RectTransform _banner;
        private readonly TextMeshProUGUI _bannerText;
        private readonly TextMeshProUGUI _nearMiss;
        private readonly TextMeshProUGUI _hint;
        private readonly RivalMarker _rival;

        public override bool AllowsBanner => false;

        public HudScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Hud", ScreenBackground.None)
        {
            var catcher = Root.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            IgnoreTheming.Mark(catcher, "invisible full-screen tap catcher");
            Root.gameObject.AddComponent<TapForwarder>().Hud = this;

            _rival = RivalMarker.Create(Safe, ctx);

            RectTransform top = HomeScreen.TopBar(Safe);
            Button pause = UiKit.IconButton(top, Icons.Pause, () => Ctx.Flow.Pause());
            UiKit.Pin((RectTransform)pause.transform, new Vector2(0, 0.5f), new Vector2(Ui.IconButton, Ui.IconButton), Vector2.zero);

            _score = UiKit.Text(Safe, "0", TextRole.BigNumber, T.TextPrimary, size: 72f);
            UiKit.Pin(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(240, 90), new Vector2(0, -(Ui.TopGap + 40)));
            _score.rectTransform.pivot = new Vector2(0.5f, 1f);
            TextEffect.Add(_score, TextEffectKind.LogoGlow);

            _combo = UiKit.Chip(Safe, "", T.Accent, T.AccentOn, 32f, Icons.Star);
            var combo = (RectTransform)_combo.transform.parent;
            UiKit.Pin(combo, new Vector2(0.5f, 1f), new Vector2(120, 32), new Vector2(0, -(Ui.TopGap + 136)));
            combo.pivot = new Vector2(0.5f, 0.5f);

            RoundedRect banner = UiKit.Solid(Safe, "Passed", T.Accent2, 18f, glow: T.Accent2Glow);
            _banner = banner.rectTransform;
            UiKit.Pin(_banner, new Vector2(0.5f, 1f), new Vector2(320, 44), new Vector2(0, -(Ui.TopGap + 186)));
            _banner.pivot = new Vector2(0.5f, 0.5f);
            _bannerText = UiKit.Text(_banner, "", TextRole.Title, T.Accent2On, size: 20f);
            _bannerText.fontStyle = FontStyles.UpperCase;
            UiKit.Stretch(_bannerText.rectTransform, 12, 0, 12, 0);
            UiKit.OneLine(_bannerText, 12f);

            // No token: TextEffect paints the gold gradient, outline and shadow.
            _nearMiss = UiKit.Text(Safe, "", TextRole.BigNumber, null, size: 34f);
            _nearMiss.fontStyle = FontStyles.Italic; // not upper case: "mm" must stay a unit
            _nearMiss.characterSpacing = 3f;
            UiKit.Pin(_nearMiss.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(360, 64), Vector2.zero);
            _nearMiss.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiKit.OneLine(_nearMiss, 20f);
            TextEffect.Add(_nearMiss, TextEffectKind.ArcadeGold);

            _hint = UiKit.Text(Safe, L.T("hud.tap_hint"), TextRole.Body, T.TextPrimary);
            UiKit.Band(_hint.rectTransform, 0.16f, 40);

            ctx.Game.Placed += OnPlaced;
            ctx.Game.RivalProgress += OnRival;
            ctx.Game.NearMissed += OnNearMiss;
        }

        private void OnTap() => Ctx.Game.Tap();

        protected override void OnShow()
        {
            RunSetup setup = Ctx.Game.Setup;
            _score.text = Ctx.Game.Score.ToString();
            _combo.transform.parent.gameObject.SetActive(false);
            _banner.gameObject.SetActive(false);
            _nearMiss.gameObject.SetActive(false);
            _hint.gameObject.SetActive(Ctx.Game.Height == 0);
            bool rival = setup != null && setup.RivalHeight > 0 && Ctx.Game.Height < setup.RivalHeight;
            _rival.Show(rival ? setup.RivalName : null, setup?.RivalHeight ?? 0, setup?.RivalScore ?? 0);
        }

        private void OnPlaced(Step step)
        {
            _hint.gameObject.SetActive(false);
            _score.text = step.Score.ToString();
            if (!Look.ReduceMotion) Tween.Punch(_score.transform, step.Perfect ? 0.16f : 0.08f, 0.3f);
            GameObject combo = _combo.transform.parent.gameObject;
            if (step.Perfect)
            {
                combo.SetActive(true);
                _combo.text = step.Combo > 1 ? L.T("hud.perfect_combo", step.Combo) : L.T("hud.perfect");
                Tween.Punch(combo.transform, Look.ReduceMotion ? 0f : 0.22f, 0.35f);
            }
            else
            {
                combo.SetActive(false);
            }
        }

        private void OnRival(RivalEvent e)
        {
            string name = Ctx.Game.Setup?.RivalName ?? "";
            ShowBanner(e == RivalEvent.Passed ? L.T("hud.passed", name) : L.T("hud.almost", name));
            if (e == RivalEvent.Passed) _rival.Show(null, 0, 0);
        }

        /// <summary>"0.4 mm from perfect" as the gold callout; "Missed by 0.4 mm" as a toast, which stays over the results.</summary>
        private void OnNearMiss(NearMiss near)
        {
            string mm = near.Millimetres.ToString("0.0");
            if (near.Kind == NearMissKind.AlmostPerfect) ShowNearMiss(L.T("hud.almost_perfect", $"<size=145%>{mm}</size>"));
            else Ctx.Router.Toast(L.T("hud.missed_by", mm));
        }

        /// <summary>Arcade timing: slams in oversized, settles with a bounce, holds, then floats up and fades.</summary>
        private void ShowNearMiss(string text)
        {
            _nearMiss.text = text;
            RectTransform rt = _nearMiss.rectTransform;
            rt.gameObject.SetActive(true);
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            bool still = Look.ReduceMotion;
            Tween.Run(group, 1.5f, t =>
            {
                float s = still ? 1f : Mathf.LerpUnclamped(1.9f, 1f, Tween.EaseOutBack(Mathf.Clamp01(t / 0.14f)));
                float leave = Mathf.Clamp01((t - 0.72f) / 0.28f);
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = new Vector2(0f, still ? 0f : 28f * leave);
                group.alpha = 1f - leave;
            }, () => { if (rt != null) rt.gameObject.SetActive(false); });
        }

        private void ShowBanner(string text)
        {
            _bannerText.text = text;
            _banner.gameObject.SetActive(true);
            if (!Look.ReduceMotion) Tween.Punch(_banner, 0.2f, 0.4f);
            var group = _banner.GetComponent<CanvasGroup>();
            if (group == null) group = _banner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            Tween.Run(group, 2.4f, t => group.alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f,
                () => { if (_banner != null) _banner.gameObject.SetActive(false); });
        }

        private sealed class TapForwarder : MonoBehaviour, IPointerDownHandler
        {
            public HudScreen Hud;
            public void OnPointerDown(PointerEventData eventData) => Hud.OnTap();
        }
    }

    /// <summary>The rival's height on screen: an accent2 line across and a chip with their avatar and score.</summary>
    public sealed class RivalMarker : MonoBehaviour
    {
        private RectTransform _space;
        private RectTransform _line;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _initial;
        private int _height;

        public static RivalMarker Create(RectTransform safe, UiContext ctx)
        {
            RectTransform root = UiKit.Stretch(UiKit.Rect("Rival", safe));
            var marker = root.gameObject.AddComponent<RivalMarker>();
            marker._space = safe;
            RoundedRect line = UiKit.Solid(root, "Line", T.Accent2, 1f, alpha: 0.85f, glow: T.Accent2Glow);
            marker._line = line.rectTransform;
            marker._line.anchorMin = new Vector2(0, 0);
            marker._line.anchorMax = new Vector2(1, 0);
            marker._line.pivot = new Vector2(0.5f, 0.5f);
            marker._line.sizeDelta = new Vector2(0, 2);

            RoundedRect chip = UiKit.Solid(marker._line, "Chip", T.Accent2, 15f, glow: T.Accent2Glow);
            UiKit.Pin(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(150, 30), new Vector2(-Ui.Side, 0));
            HorizontalLayoutGroup row = UiKit.Row(chip.rectTransform, 6);
            row.padding = new RectOffset(4, 10, 3, 3);
            row.childForceExpandWidth = false;
            marker._initial = UiKit.Avatar(chip.transform, "?", 24f).GetComponentInChildren<TextMeshProUGUI>();
            marker._label = UiKit.Text(chip.transform, "", TextRole.Pill, T.Accent2On, TextAlignmentOptions.Left, 12f);
            UiKit.Size(marker._label.rectTransform, flexibleWidth: 1);
            UiKit.OneLine(marker._label, 9f);
            root.gameObject.SetActive(false);
            return marker;
        }

        public void Show(string name, int height, int score)
        {
            _height = height;
            bool visible = !string.IsNullOrEmpty(name) && height > 0;
            gameObject.SetActive(visible);
            if (!visible) return;
            _label.text = L.T("hud.rival", name, score);
            _initial.text = name.Substring(0, 1).ToUpperInvariant();
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // The top surface of the rival's highest block.
            var world = new Vector3(0f, StackView.Y(_height) + StackView.BlockHeight * 0.5f, 0f);
            Vector3 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, screen, null, out Vector2 local);
            float y = local.y - _space.rect.yMin;
            _line.anchoredPosition = new Vector2(0f, y);
            _line.gameObject.SetActive(screen.z > 0f && y > 0f && y < _space.rect.height);
        }
    }

    /// <summary>Pause card: Resume, Restart, Home, the look switch, sound and haptics.</summary>
    public sealed class PauseScreen : UiScreen
    {
        public override bool AllowsBanner => false;

        public PauseScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Pause", ScreenBackground.Dim)
        {
            RectTransform card = UiKit.Surface(Safe, "Card", SurfaceKind.Raised, Ui.SheetRadius).rectTransform;
            card.anchorMin = new Vector2(0, 0.5f);
            card.anchorMax = new Vector2(1, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(-2 * Ui.Side, 400);
            UiKit.Column(card, 14, new RectOffset(20, 20, 24, 24), TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Size(UiKit.Text(card, L.T("pause.title"), TextRole.Title).rectTransform, height: 36);
            UiKit.Primary(card, L.T("pause.resume"), Icons.Play, () => Ctx.Flow.Resume());
            RectTransform pills = UiKit.Rect("Pills", card);
            UiKit.Size(pills, height: Ui.PillHeight);
            HorizontalLayoutGroup row = UiKit.Row(pills, 12);
            row.childForceExpandWidth = false;
            UiKit.Pill(pills, L.T("pause.restart"), Icons.Replay, () => Ctx.Flow.Restart());
            UiKit.Pill(pills, L.T("over.home"), Icons.Home, () => Ctx.Flow.GoHome());

            UiKit.Spacer(card, 4);
            SettingsScreen.LookSwitch(card, ctx);
            UiKit.Toggle(card, Icons.Sound, L.T("settings.sound"), ctx.Save.SoundOn, on => { ctx.Save.SoundOn = on; ctx.Audio.Enabled = on; });
            UiKit.Toggle(card, Icons.Haptics, L.T("settings.haptics"), ctx.Save.HapticsOn, on => { ctx.Save.HapticsOn = on; ctx.Haptics.Enabled = on; });
        }
    }

    /// <summary>
    /// Results sheet under the camera's pull-back over the whole tower: Blocky perched on its edge
    /// reacting to the run, score, best, coins, the challenge result, the breathing "Challenge a
    /// friend" (the viral loop gets the accent; the share sheet already lists TikTok, Instagram
    /// and chats, so it is the only share button) and the pills: Play again, Continue (once per
    /// run), Double coins, Rematch.
    /// </summary>
    public sealed class GameOverScreen : UiScreen
    {
        private readonly RectTransform _sheet;
        private readonly RectTransform _record;
        private readonly TextMeshProUGUI _best;
        private readonly TextMeshProUGUI _score;
        private readonly RectTransform _coinsRow;
        private readonly TextMeshProUGUI _coins;
        private readonly RectTransform _duel;
        private readonly TextMeshProUGUI _youScore;
        private readonly TextMeshProUGUI _rivalName;
        private readonly TextMeshProUGUI _rivalScore;
        private readonly TextMeshProUGUI _duelResult;
        private readonly TextMeshProUGUI _status;
        private readonly Button _challenge;
        private readonly Button _continue;
        private readonly Button _double;
        private readonly Button _rematch;
        private readonly Blocky _blocky;
        private int _shownCoins;
        private bool _awaitingContinue;
        private bool _newRecord;

        public GameOverScreen(UiContext ctx, Transform parent) : base(ctx, parent, "GameOver", ScreenBackground.None)
        {
            RectTransform top = HomeScreen.TopBar(Safe);
            Button home = UiKit.IconButton(top, Icons.Home, () => Ctx.Flow.GoHome());
            UiKit.Pin((RectTransform)home.transform, new Vector2(0, 0.5f), new Vector2(Ui.IconButton, Ui.IconButton), Vector2.zero);

            _sheet = UiRouter.BottomSheet(Safe);
            _blocky = Blocky.Perch(_sheet);

            _record = UiKit.Solid(_sheet, "Record", T.Accent2, 14f, glow: T.Accent2Glow).rectTransform;
            UiKit.Size(_record, height: 28);
            var recordFit = _record.gameObject.AddComponent<HorizontalLayoutGroup>();
            recordFit.padding = new RectOffset(14, 14, 0, 0);
            recordFit.childAlignment = TextAnchor.MiddleCenter;
            recordFit.childForceExpandWidth = false;
            recordFit.childForceExpandHeight = false;
            recordFit.spacing = 6;
            recordFit.childControlWidth = true; // the label hugs its text, next to the star
            UiKit.Icon(_record, Icons.Star, 16f, T.Accent2On, filled: true);
            UiKit.Text(_record, L.T("over.new_record"), TextRole.Overline, T.Accent2On, TextAlignmentOptions.Left);
            Centre(_record, 28);

            _best = UiKit.Text(_sheet, "", TextRole.Overline, T.TextSecondary);
            UiKit.Size(_best.rectTransform, height: 18);
            _score = UiKit.Text(_sheet, "0", TextRole.BigNumber, T.TextPrimary, size: 88f);
            UiKit.Size(_score.rectTransform, height: 96);

            _coinsRow = UiKit.Rect("Coins", _sheet);
            HorizontalLayoutGroup coinsRow = UiKit.Row(_coinsRow, 8);
            coinsRow.childForceExpandWidth = false;
            coinsRow.childForceExpandHeight = false;
            UiKit.Coin(_coinsRow, 22f);
            _coins = UiKit.Text(_coinsRow, "", TextRole.Coin);
            Centre(_coinsRow, 26);

            _duel = UiKit.Rect("Duel", _sheet);
            UiKit.Column(_duel, 6);
            RectTransform scores = UiKit.Rect("Scores", _duel);
            UiKit.Size(scores, height: 60);
            UiKit.Row(scores, 12);
            _youScore = Side(scores, L.T("over.you"), out _);
            _rivalScore = Side(scores, "", out _rivalName);
            _duelResult = UiKit.Text(_duel, "", TextRole.Body, T.TextPrimary);
            UiKit.Size(_duelResult.rectTransform, height: 22);

            _status = UiKit.Text(_sheet, "", TextRole.Caption, T.TextSecondary);
            UiKit.Size(_status.rectTransform, height: 16);

            _challenge = UiKit.Primary(_sheet, L.T("over.challenge"), Icons.Share, () => Ctx.Flow.ShareChallenge(), breathe: true);

            RectTransform pills = UiKit.Rect("Pills", _sheet);
            UiKit.Size(pills, height: Ui.PillHeight);
            HorizontalLayoutGroup row = UiKit.Row(pills, 10);
            row.childForceExpandWidth = false;
            UiKit.Pill(pills, L.T("over.replay"), Icons.Replay, () => Ctx.Flow.PlayNormal());
            _continue = UiKit.Pill(pills, L.T("over.continue"), Icons.VideoAd, () => Ctx.Flow.ContinueWithAd());
            _rematch = UiKit.Pill(pills, L.T("over.rematch"), Icons.Challenge, Rematch);
            RectTransform more = UiKit.Rect("More", _sheet);
            UiKit.Size(more, height: Ui.PillHeight);
            HorizontalLayoutGroup moreRow = UiKit.Row(more, 10);
            moreRow.childForceExpandWidth = false;
            _double = UiKit.Pill(more, L.T("over.double_short"), Icons.VideoAd, () => Ctx.Flow.DoubleCoins());
        }

        /// <summary>Centres a hugging child on its own line of the sheet's column.</summary>
        private static void Centre(RectTransform child, float height)
        {
            child.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform line = UiKit.Rect(child.name + "Line", child.parent);
            line.SetSiblingIndex(child.GetSiblingIndex());
            child.SetParent(line, false);
            UiKit.Size(line, height: height);
            HorizontalLayoutGroup row = UiKit.Row(line, 0);
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
        }

        private static TextMeshProUGUI Side(RectTransform parent, string name, out TextMeshProUGUI nameLabel)
        {
            RectTransform col = UiKit.Rect("Side", parent);
            UiKit.Column(col, 0, null, TextAnchor.MiddleCenter);
            nameLabel = UiKit.Text(col, name, TextRole.Overline, T.TextSecondary);
            UiKit.OneLine(nameLabel, 9f);
            UiKit.Size(nameLabel.rectTransform, height: 18);
            TextMeshProUGUI score = UiKit.Text(col, "", TextRole.BigNumber, T.TextPrimary, size: 36f);
            UiKit.Size(score.rectTransform, height: 40);
            return score;
        }

        public override bool AllowsBanner => !_awaitingContinue;

        /// <summary>The block fell and a rewarded continue is possible: results so far, plus Continue.</summary>
        public void ShowContinueOffer(int score, bool newRecord, int best)
        {
            _awaitingContinue = true;
            Fill(score, newRecord, best, ranked: true, challengeCode: Ctx.Game.Setup?.ChallengeCode);
            _status.text = L.T("over.keep_going");
            _continue.gameObject.SetActive(true);
            _challenge.interactable = true; // pressing it ends the run, then shares
            _blocky.SetMood(MascotMood.Rally);
        }

        /// <summary>Immediate results from the local simulation; the server result refines them.</summary>
        public void ShowLocalResult(int score, bool newRecord, int best, bool ranked)
        {
            _awaitingContinue = false;
            Fill(score, newRecord, best, ranked, Ctx.Game.Setup?.ChallengeCode);
            _status.text = ranked ? L.T("over.verifying") : L.T("over.unranked");
            _challenge.interactable = false;
        }

        private void Fill(int score, bool newRecord, int best, bool ranked, string challengeCode)
        {
            _newRecord = newRecord;
            _blocky.SetMood(MascotMoods.ForResult(newRecord, duelWon: null));
            _record.parent.gameObject.SetActive(newRecord);
            if (newRecord && !Look.ReduceMotion) Tween.Punch(_record, 0.25f, 0.5f);
            CountUp(_score, score, null);
            _best.text = L.T("over.best", Mathf.Max(best, score));
            _shownCoins = 0;
            SetCoins(ranked ? Economy.CoinsForRun(score) : 0);
            _duel.gameObject.SetActive(false);
            _continue.gameObject.SetActive(false);
            _double.transform.parent.gameObject.SetActive(false);
            _rematch.gameObject.SetActive(!string.IsNullOrEmpty(challengeCode));
            UiKit.SetLabel(_challenge, L.T("over.challenge"));
            UiRouter.Rise(_sheet);
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
            SetCoins(verified ? result.CoinsEarned * (result.CoinsDoubled ? 2 : 1) : 0);
            _double.transform.parent.gameObject.SetActive(AdPolicy.OffersDoubleCoins(result, rewardedReady));
            _challenge.interactable = verified;
            if (result.Challenge != null) ShowDuel(result);
        }

        /// <summary>Challenge runs: both scores side by side and who won.</summary>
        private void ShowDuel(SubmitRunResponse result)
        {
            Rival rival = result.Challenge.Rival;
            _duel.gameObject.SetActive(true);
            _youScore.text = (result.Score ?? 0).ToString();
            _rivalName.text = "@" + rival.DisplayName;
            _rivalScore.text = rival.Score.ToString();
            _duelResult.text = result.Challenge.Won ? L.T("over.duel_won", rival.DisplayName, rival.Score) : L.T("over.duel_lost", rival.DisplayName, rival.Score);
            ThemedGraphic.Bind(_duelResult, result.Challenge.Won ? T.Success : T.TextPrimary);
            _rematch.gameObject.SetActive(true);
            _blocky.SetMood(MascotMoods.ForResult(_newRecord, result.Challenge.Won));
        }

        private void Rematch()
        {
            string code = Ctx.Game.Setup?.ChallengeCode;
            if (!string.IsNullOrEmpty(code)) Ctx.Flow.PlayChallenge(code);
        }

        public void ShowCoinsDoubled(int coins)
        {
            SetCoins(coins);
            _double.transform.parent.gameObject.SetActive(false);
        }

        public void SetSharing(bool busy)
        {
            _challenge.interactable = !busy;
            UiKit.SetLabel(_challenge, busy ? L.T("over.preparing_clip") : L.T("over.challenge"));
        }

        private void SetCoins(int coins)
        {
            _coinsRow.parent.gameObject.SetActive(coins > 0);
            if (coins <= 0) return;
            int from = _shownCoins;
            _shownCoins = coins;
            TextMeshProUGUI label = _coins;
            Tween.Run(label, 0.6f, t =>
            {
                if (label != null) label.text = L.T("over.coins", Mathf.RoundToInt(Mathf.Lerp(from, coins, Tween.EaseOutCubic(t))));
            }, () =>
            {
                if (label == null) return;
                label.text = L.T("over.coins", coins);
                if (!Look.ReduceMotion) Tween.Punch(label.transform.parent, 0.18f, 0.3f);
            });
        }

        /// <summary>Counts up to the number, with a short punch on the last tick.</summary>
        private static void CountUp(TextMeshProUGUI label, int value, System.Action done)
        {
            Tween.Run(label, Mathf.Clamp(value * 0.03f, 0.4f, 1.2f), t =>
            {
                if (label != null) label.text = Mathf.RoundToInt(value * Tween.EaseOutCubic(t)).ToString();
            }, () =>
            {
                if (label == null) return;
                label.text = value.ToString();
                if (!Look.ReduceMotion) Tween.Punch(label.transform, 0.12f, 0.28f);
                done?.Invoke();
            });
        }
    }

    /// <summary>Opened from a deep link or a code: who challenges you and the score to beat.</summary>
    public sealed class ChallengeIntroScreen : UiScreen
    {
        private readonly RectTransform _avatar;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _note;
        private string _code;

        public override bool AllowsBanner => false;

        public ChallengeIntroScreen(UiContext ctx, Transform parent) : base(ctx, parent, "ChallengeIntro", ScreenBackground.Gradient)
        {
            RectTransform column = UiKit.Stretch(UiKit.Rect("Column", Safe), Ui.Side, 0, Ui.Side, Ui.BottomGap);
            UiKit.Column(column, 12, new RectOffset(0, 0, 80, 0), TextAnchor.UpperCenter);
            _avatar = UiKit.Rect("AvatarLine", column);
            UiKit.Size(_avatar, height: 76);
            HorizontalLayoutGroup avatarRow = UiKit.Row(_avatar, 18);
            avatarRow.childForceExpandWidth = false;
            avatarRow.childControlWidth = true;
            _title = UiKit.Text(column, "", TextRole.Title);
            UiKit.Size(_title.rectTransform, height: 36);
            UiKit.OneLine(_title, 18f);
            UiKit.Spacer(column, 16);
            UiKit.Size(UiKit.Text(column, L.T("intro.score_to_beat"), TextRole.Overline, T.TextSecondary).rectTransform, height: 18);
            _score = UiKit.Text(column, "", TextRole.BigNumber, T.TextPrimary, size: 96f);
            UiKit.Size(_score.rectTransform, height: 104);
            TextEffect.Add(_score, TextEffectKind.LogoGlow);
            _note = UiKit.Text(column, L.T("intro.rules"), TextRole.Body, T.TextSecondary);
            UiKit.Flex(column);
            UiKit.Primary(column, L.T("intro.accept"), Icons.Play, () => Ctx.Flow.PlayChallenge(_code), breathe: true);
            UiKit.CenteredPill(column, L.T("intro.later"), Icons.Close, () => Ctx.Flow.GoHome());
        }

        public void Bind(ChallengeInfo info)
        {
            _code = info.Code;
            UiKit.Clear(_avatar);
            UiKit.Avatar(_avatar, info.CreatorName, 72f);
            Blocky.Create(_avatar, 52f).SetMood(MascotMood.Rally); // squares up to the rival
            _title.text = L.T("intro.title", info.CreatorName);
            _score.text = info.Score.ToString();
            _note.text = info.Expired ? L.T("intro.expired") : L.T("intro.rules");
        }
    }

    /// <summary>"Got a challenge code?": a bottom sheet with a large code field.</summary>
    public sealed class CodeEntryScreen : UiScreen
    {
        private readonly TMP_InputField _input;
        private readonly RectTransform _sheet;

        public override bool AllowsBanner => false;

        public CodeEntryScreen(UiContext ctx, Transform parent) : base(ctx, parent, "CodeEntry", ScreenBackground.Dim)
        {
            Root.GetComponentInChildren<RoundedRect>().gameObject.AddComponent<Button>().onClick.AddListener(() => Ctx.Router.Back());
            _sheet = UiRouter.BottomSheet(Safe);
            UiKit.Size(UiKit.Text(_sheet, L.T("code.title"), TextRole.Title).rectTransform, height: 36);
            UiKit.Text(_sheet, L.T("code.body"), TextRole.Body, T.TextSecondary);
            _input = UiKit.Input(_sheet, L.T("code.placeholder"), DeepLinks.ChallengeCodeLength, TMP_InputField.CharacterValidation.Alphanumeric, big: true);
            _input.onValidateInput = (text, index, c) => char.ToUpperInvariant(c);
            UiKit.Primary(_sheet, L.T("code.submit"), Icons.Play, Submit);
        }

        protected override void OnShow()
        {
            _input.text = "";
            _input.ActivateInputField();
            UiRouter.Rise(_sheet);
        }

        private void Submit()
        {
            string code = DeepLinks.NormalizeTypedCode(_input.text);
            if (code == null)
            {
                Ctx.Router.Toast(L.T("code.invalid"));
                return;
            }
            Ctx.Flow.OpenChallenge(code, "code");
        }
    }
}
