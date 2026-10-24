using System;
using System.Collections.Generic;
using System.Globalization;
using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Client.Theming;
using StackIt.Game;
using StackIt.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>Empty, offline and error states: an outline icon, one friendly line and one action.</summary>
    public static class States
    {
        public static RectTransform Show(Transform parent, string glyph, string message, string action, string actionIcon, Action onAction)
        {
            RectTransform box = UiKit.Rect("State", parent);
            UiKit.Column(box, 12, new RectOffset(16, 16, 32, 16), TextAnchor.UpperCenter);
            UiKit.Icon(box, glyph, 40f, T.TextSecondary);
            UiKit.Text(box, message, TextRole.Body, T.TextSecondary);
            if (onAction != null) UiKit.CenteredPill(box, action, actionIcon, () => onAction());
            return box;
        }

        /// <summary>Offline vs. any other failure, with Retry.</summary>
        public static void Failed(Transform parent, ApiException e, Action retry) =>
            Show(parent, e.IsOffline ? Icons.Offline : Icons.Error, Errors.Describe(e), L.T("common.retry"), Icons.Refresh, retry);
    }

    public sealed class FriendsScreen : UiScreen
    {
        private readonly TextMeshProUGUI _myCode;
        private readonly TMP_InputField _input;
        private readonly RectTransform _list;

        public FriendsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Friends", ScreenBackground.Gradient)
        {
            Header(L.T("friends.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, Ui.RowGap);

            // Your code: big, copyable, shareable.
            RectTransform card = UiKit.Card(body, "MyCode");
            UiKit.Column(card, 8, new RectOffset(16, 16, 16, 16), TextAnchor.UpperCenter);
            UiKit.Size(UiKit.Text(card, L.T("friends.your_code"), TextRole.Overline, T.TextSecondary).rectTransform, height: 18);
            RectTransform codeRow = UiKit.Rect("CodeRow", card);
            UiKit.Size(codeRow, height: Ui.IconButton);
            HorizontalLayoutGroup codeLayout = UiKit.Row(codeRow, 10);
            codeLayout.childForceExpandWidth = false;
            _myCode = UiKit.Text(codeRow, "—", TextRole.BigNumber, T.TextPrimary, size: 34f);
            _myCode.characterSpacing = 12f;
            UiKit.IconButton(codeRow, Icons.Copy, CopyCode);
            UiKit.CenteredPill(card, L.T("friends.share_code"), Icons.Share, ShareCode);

            // Add by code.
            RectTransform addRow = UiKit.Rect("Add", body);
            UiKit.Size(addRow, height: 48);
            HorizontalLayoutGroup add = UiKit.Row(addRow, 10);
            add.childForceExpandWidth = false;
            _input = UiKit.Input(addRow, L.T("friends.code_placeholder"), 12, TMP_InputField.CharacterValidation.Alphanumeric);
            UiKit.Size((RectTransform)_input.transform, flexibleWidth: 1);
            UiKit.Pill(addRow, L.T("friends.add"), Icons.AddFriend, Add);

            _list = UiKit.ScrollList(body, Ui.RowGap);
            UiKit.Size((RectTransform)_list.parent, height: 300).flexibleHeight = 1;
        }

        protected override void OnShow()
        {
            _myCode.text = Ctx.Api.Profile?.FriendCode ?? "—";
            Load();
        }

        private async void Load()
        {
            UiKit.Clear(_list);
            Pending();
            try
            {
                List<Friend> friends = await Ctx.Api.FriendsAsync();
                if (friends.Count == 0)
                {
                    States.Show(_list, Icons.Invite, L.T("friends.empty"), L.T("friends.invite"), Icons.Share, ShareCode);
                    return;
                }
                foreach (Friend friend in friends) Row(friend);
            }
            catch (ApiException e)
            {
                States.Failed(_list, e, Load);
            }
        }

        /// <summary>Challenges opened but not played yet, with the accent2 dot.</summary>
        private void Pending()
        {
            if (!Ctx.Pending.Any) return;
            TextMeshProUGUI heading = UiKit.Text(_list, L.T("friends.pending"), TextRole.Overline, T.TextSecondary, TextAlignmentOptions.Left);
            UiKit.Size(heading.rectTransform, height: 20);
            foreach (PendingChallenge c in Ctx.Pending.Items)
            {
                RectTransform row = UiKit.ListRow(_list);
                RoundedRect dot = UiKit.Solid(row, "Dot", T.Accent2, Ui.Dot / 2f, glow: T.Accent2Glow);
                UiKit.Size(dot.rectTransform, Ui.Dot, Ui.Dot);
                UiKit.Grow(UiKit.OneLine(UiKit.Text(row, L.T("friends.pending_row", c.CreatorName, c.Score), TextRole.Body, T.TextPrimary,
                    TextAlignmentOptions.Left), 12f));
                string code = c.Code;
                ChipButton(row, L.T("friends.play"), Icons.Play, () => Ctx.Flow.OpenChallenge(code, "friends"));
            }
        }

        private void Row(Friend friend)
        {
            RectTransform row = UiKit.ListRow(_list);
            UiKit.Avatar(row, friend.DisplayName, 40f);
            RectTransform text = UiKit.Rect("Text", row);
            UiKit.Column(text, 0, null, TextAnchor.MiddleLeft);
            UiKit.Size(text, flexibleWidth: 1);
            UiKit.OneLine(UiKit.Text(text, friend.DisplayName, TextRole.Body, T.TextPrimary, TextAlignmentOptions.Left), 12f);
            UiKit.Text(text, friend.BestScore != null ? L.T("home.best", friend.BestScore) : "—", TextRole.Caption, T.TextSecondary, TextAlignmentOptions.Left);
            if (friend.BestScore != null && friend.BestPlaced != null)
                ChipButton(row, L.T("friends.challenge"), Icons.Challenge,
                    () => Ctx.Flow.PlayAgainst(friend.DisplayName, friend.BestScore.Value, friend.BestPlaced.Value));
            Button remove = UiKit.IconButton(row, Icons.Close, () =>
                Ctx.Router.Confirm(L.T("friends.remove_confirm", friend.DisplayName), L.T("friends.remove"),
                    () => Guard(RemoveThenReload(friend.UserId)), destructive: true));
            UiKit.Size((RectTransform)remove.transform, 40, 40);
        }

        /// <summary>A small accent chip that is also a button.</summary>
        public static Button ChipButton(Transform parent, string text, string icon, Action onClick)
        {
            TextMeshProUGUI label = UiKit.Chip(parent, text, T.Accent, T.AccentOn, 30f, icon);
            var chip = (RectTransform)label.transform.parent;
            chip.GetComponent<RoundedRect>().raycastTarget = true;
            var button = chip.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            chip.gameObject.AddComponent<Pressable>().Button = button;
            return button;
        }

        private async System.Threading.Tasks.Task RemoveThenReload(string userId)
        {
            await Ctx.Api.RemoveFriendAsync(userId);
            Load();
        }

        private void Add() => Guard(AddAsync());

        private async System.Threading.Tasks.Task AddAsync()
        {
            string code = _input.text.Trim().ToUpperInvariant();
            if (code.Length < 4) return;
            Friend friend = await Ctx.Api.AddFriendAsync(code);
            _input.text = "";
            Ctx.Router.Toast(L.T("friends.added", friend.DisplayName));
            Load();
        }

        private void CopyCode()
        {
            string code = Ctx.Api.Profile?.FriendCode;
            if (code == null) return;
            GUIUtility.systemCopyBuffer = code;
            Ctx.Router.Toast(L.T("friends.copied"));
        }

        private void ShareCode()
        {
            string code = Ctx.Api.Profile?.FriendCode;
            if (code == null) return;
            Ctx.Telemetry.Track(TelemetryEvents.ShareTap, ("source", "friends"));
            Ctx.Share.Share(L.T("friends.share_text", code), Ctx.Config.apiBaseUrl, null);
        }
    }

    public sealed class LeaderboardScreen : UiScreen
    {
        private readonly RectTransform _list;
        private readonly RectTransform _mine;
        private readonly TextMeshProUGUI _countdown;
        private static readonly string[] Periods = { "weekly", "all_time", "daily" };

        private readonly Action<int> _selectTab;
        private string _period = "weekly";

        public LeaderboardScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Leaderboard", ScreenBackground.Gradient)
        {
            Header(L.T("ranks.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, Ui.RowGap);
            _selectTab = UiKit.Segmented(body, new[] { L.T("ranks.weekly"), L.T("ranks.all_time"), L.T("ranks.daily") },
                i => Select(Periods[i]));
            _countdown = UiKit.Text(body, "", TextRole.Overline, T.TextSecondary);
            UiKit.Size(_countdown.rectTransform, height: 18);
            _list = UiKit.ScrollList(body, Ui.RowGap);
            UiKit.Size((RectTransform)_list.parent, height: 300).flexibleHeight = 1;
            _mine = UiKit.Rect("Mine", body); // the player's own row, pinned under the list
            UiKit.Column(_mine, 0);
        }

        protected override void OnShow() => Select(_period);

        private void Select(string period)
        {
            _period = period;
            _selectTab(Array.IndexOf(Periods, period));
            _countdown.gameObject.SetActive(period != "all_time");
            Load(period);
        }

        private async void Load(string period)
        {
            bool weekly = period == "weekly";
            UiKit.Clear(_list);
            UiKit.Clear(_mine);
            Leaderboard board;
            try
            {
                board = await Ctx.Api.LeaderboardAsync(period);
            }
            catch (ApiException e)
            {
                States.Failed(_list, e, () => Load(period));
                return;
            }
            if (period != _period) return; // switched tabs meanwhile
            if (period == "daily")
            {
                int today = DailyTower.Number(DateTime.UtcNow);
                _countdown.text = L.T("ranks.daily_title", today);
                UiKit.CenteredPill(_list, L.T("ranks.play_daily", today), Icons.Play, () => Ctx.Flow.PlayDaily());
            }
            if (weekly && DateTime.TryParse(board.Since, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime since))
                _countdown.text = L.T("ranks.resets_in", WeeklyReset.Short(WeeklyReset.Remaining(since, DateTime.UtcNow)));
            if (board.Entries.Count <= 1 && period != "daily")
                States.Show(_list, Icons.Trophy, L.T("ranks.empty"), L.T("friends.invite"), Icons.Share, () => Ctx.Router.Back());
            foreach (LeaderboardEntry entry in board.Entries)
            {
                Entry(_list, entry);
                if (entry.IsMe) Entry(_mine, entry);
            }
        }

        private static void Entry(Transform parent, LeaderboardEntry entry)
        {
            RectTransform row;
            if (entry.IsMe)
            {
                row = UiKit.Surface(parent, "Me", SurfaceKind.Highlight, Ui.CardRadius).rectTransform;
                UiKit.Size(row, height: 60);
                HorizontalLayoutGroup layout = UiKit.Row(row, 12);
                layout.padding = new RectOffset((int)Ui.CardPad, (int)Ui.CardPad, 8, 8);
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            else
            {
                row = UiKit.ListRow(parent, 60);
            }
            if (entry.Rank <= 3) UiKit.Medal(row, entry.Rank);
            else UiKit.Size(UiKit.Text(row, entry.Rank.ToString(), TextRole.Pill, T.TextSecondary).rectTransform, 32, 32);
            UiKit.Grow(UiKit.OneLine(UiKit.Text(row, entry.IsMe ? L.T("ranks.you", entry.DisplayName) : entry.DisplayName,
                TextRole.Body, T.TextPrimary, TextAlignmentOptions.Left), 12f));
            TextMeshProUGUI score = UiKit.Text(row, entry.Score.ToString(), TextRole.BigNumber, T.TextPrimary, TextAlignmentOptions.Right, 24f);
            UiKit.Size(score.rectTransform, width: 70);
        }
    }

    public sealed class ShopScreen : UiScreen
    {
        private static readonly string[] Kinds = { "block", "theme", "music" };

        private readonly RectTransform _grid;
        private readonly TextMeshProUGUI _balance;
        private readonly Action<int> _selectTab;
        private string _kind = "block";
        private Shop _shop;
        private bool _trialOffered; // offer_shown once per visit, not per render

        public ShopScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Shop", ScreenBackground.Gradient)
        {
            Header(L.T("shop.title"), () => Ctx.Router.Back());
            _balance = UiKit.CoinPill(HomeScreen.TopBar(Safe));
            var coins = (RectTransform)_balance.transform.parent;
            coins.anchorMin = coins.anchorMax = coins.pivot = new Vector2(1, 0.5f);

            RectTransform body = Body();
            UiKit.Column(body, Ui.RowGap);
            _selectTab = UiKit.Segmented(body, new[] { L.T("shop.blocks"), L.T("shop.themes"), L.T("shop.music") }, i => Select(Kinds[i]));
            _grid = UiKit.ScrollList(body, Ui.RowGap);
            UiKit.Size((RectTransform)_grid.parent, height: 300).flexibleHeight = 1;
        }

        /// <summary>The shop as (id, price, owned) for the "new affordable skin" dot.</summary>
        public static IEnumerable<(string Id, int Price, bool Owned)> Entries(Shop shop)
        {
            foreach (ShopItem item in shop.Items) yield return (item.SkinId, item.Price, item.Owned);
        }

        protected override void OnShow()
        {
            _trialOffered = false;
            Reload();
        }

        protected override void OnHide() => Ctx.Flow.EndMusicPreview();

        private async void Reload()
        {
            try
            {
                _shop = await Ctx.Api.ShopAsync();
            }
            catch (ApiException e)
            {
                UiKit.Clear(_grid);
                States.Failed(_grid, e, Reload);
                return;
            }
            Ctx.Seen.MarkSeen(Entries(_shop), _shop.Balance);
            Ctx.Telemetry.Track(TelemetryEvents.ShopView, ("balance", _shop.Balance));
            Render();
        }

        private void Select(string kind)
        {
            _kind = kind;
            Render();
        }

        private void Render()
        {
            _selectTab(Array.IndexOf(Kinds, _kind));
            if (_shop == null) return;
            _balance.text = _shop.Balance.ToString("N0");
            UiKit.Clear(_grid);
            RectTransform line = null;
            int column = 0;
            foreach (ShopItem item in _shop.Items)
            {
                if (item.Kind != _kind) continue;
                if (column == 0)
                {
                    line = UiKit.Rect("Line", _grid);
                    UiKit.Size(line, height: 236);
                    UiKit.Row(line, Ui.RowGap);
                }
                Card(line, item);
                column = (column + 1) % 2;
            }
            if (column == 1) UiKit.Rect("Empty", line); // keep the last card at half width
        }

        private void Card(RectTransform line, ShopItem item)
        {
            RectTransform card = UiKit.Card(line, item.SkinId);
            UiKit.Column(card, 8, new RectOffset(12, 12, 12, 12), TextAnchor.UpperCenter);
            var tap = card.gameObject.AddComponent<Button>();
            card.GetComponent<RoundedRect>().raycastTarget = true;
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => OnItem(item, card));

            if (item.Kind == "music") MusicPreview(card, item);
            else Preview(card, item);
            TextMeshProUGUI name = UiKit.Text(card, L.T("skin." + item.SkinId), TextRole.Body, T.TextPrimary);
            UiKit.OneLine(name, 12f);
            UiKit.Size(name.rectTransform, height: 22);

            RectTransform state = UiKit.Rect("State", card);
            UiKit.Size(state, height: 32);
            HorizontalLayoutGroup row = UiKit.Row(state, 0);
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            if (item.Equipped)
            {
                // Accent check badge: what you are using now.
                RoundedRect badge = UiKit.Solid(card, "Equipped", T.Accent, 14f, glow: T.AccentGlow);
                UiKit.Pin(badge.rectTransform, new Vector2(1, 1), new Vector2(28, 28), new Vector2(-8, -8));
                UiKit.IgnoreLayout(badge);
                UiKit.Stretch(UiKit.Icon(badge.transform, Icons.Check, 18f, T.AccentOn).rectTransform);
                UiKit.Text(state, L.T("shop.equipped"), TextRole.Pill, T.TextSecondary);
            }
            else if (item.Owned)
            {
                UiKit.Pill(state, L.T("shop.equip"), Icons.Check, () => OnItem(item, card));
            }
            else
            {
                bool affordable = _shop.Balance >= item.Price;
                RoundedRect price = affordable
                    ? UiKit.Solid(state, "Price", T.Accent, 16f, glow: T.AccentGlow)
                    : UiKit.Surface(state, "Price", SurfaceKind.Sunken, 16f);
                HorizontalLayoutGroup p = UiKit.Row(price.rectTransform, 6);
                p.padding = new RectOffset(6, 12, 0, 0);
                p.childForceExpandWidth = false;
                p.childForceExpandHeight = false;
                UiKit.Coin(price.transform, 20f);
                UiKit.Text(price.transform, item.Price.ToString("N0"), TextRole.Coin, affordable ? T.AccentOn : T.TextPrimary, size: 15f);
                price.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        /// <summary>A live mini tower: the skin's colours (block skins) or the theme's sky (themes).</summary>
        private void Preview(RectTransform card, ShopItem item)
        {
            RectTransform frame;
            if (item.Kind == "theme" && !Palette.FollowsLook(item.SkinId))
            {
                frame = UiKit.Rect("Sky", card);
                var sky = frame.gameObject.AddComponent<GradientBackground>();
                Palette.Sky(item.SkinId, 12f, out Color top, out Color bottom);
                sky.SetStops(top, Color.Lerp(top, bottom, 0.5f), bottom, 0.5f);
                IgnoreTheming.Mark(sky, "skin preview: the theme's own sky");
            }
            else if (item.Kind == "theme")
            {
                frame = UiKit.Rect("Sky", card);
                var sky = frame.gameObject.AddComponent<GradientBackground>();
                var themed = frame.gameObject.AddComponent<ThemedGradientBackground>();
                themed.Target = sky;
                themed.Refresh();
            }
            else
            {
                frame = UiKit.Surface(card, "Stage", SurfaceKind.Sunken, 14f).rectTransform;
            }
            UiKit.Size(frame, height: 130);

            RectTransform holder = UiKit.Stretch(UiKit.Rect("Tower", frame), 8, 10, 8, 10);
            var tower = holder.gameObject.AddComponent<IsoTower>();
            tower.raycastTarget = false;
            tower.Footprint = 0.5f;
            var blocks = new List<IsoTower.BlockFaces>();
            string skin = item.Kind == "block" ? item.SkinId : null;
            for (int i = 0; i < 3; i++) blocks.Add(IsoTower.FromTop(Palette.BlockTop(skin, i * 4, Look.P)));
            tower.SetBlocks(blocks);
            Color[] moving = Look.Colors(T.BlockMoving);
            tower.SetMoving(new IsoTower.BlockFaces { Top = moving[0], Left = moving[1], Right = moving[2] }, true);
            IgnoreTheming.Mark(tower, "skin preview");
            holder.gameObject.AddComponent<TowerSlide>().Tower = tower;
        }

        /// <summary>A soundtrack: its note icon, a listen button and, if not owned, "try it for a run" with an ad.</summary>
        private void MusicPreview(RectTransform card, ShopItem item)
        {
            RectTransform frame = UiKit.Surface(card, "Stage", SurfaceKind.Sunken, 14f).rectTransform;
            UiKit.Size(frame, height: 130);
            UiKit.Column(frame, 10, new RectOffset(8, 8, 12, 12), TextAnchor.MiddleCenter);
            UiKit.Size(UiKit.Icon(frame, Icons.Music, 40f, T.Accent).rectTransform, height: 44);

            RectTransform buttons = UiKit.Rect("Buttons", frame);
            UiKit.Size(buttons, height: 48);
            HorizontalLayoutGroup row = UiKit.Row(buttons, 12);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandWidth = false;
            UiKit.IconButton(buttons, Icons.Play, () => Ctx.Flow.PreviewMusic(item.SkinId));
            if (item.Owned || Ctx.Ads == null || !Ctx.Ads.RewardedReady) return;
            UiKit.IconButton(buttons, Icons.VideoAd, () => Ctx.Flow.TryMusic(item.SkinId));
            if (_trialOffered) return;
            _trialOffered = true;
            Ctx.Telemetry.Track(TelemetryEvents.OfferShown, ("placement", AdPlacements.MusicTrial));
        }

        private void OnItem(ShopItem item, RectTransform card)
        {
            if (item.Equipped) return;
            if (item.Owned)
            {
                Guard(Equip(item.SkinId));
                return;
            }
            if (_shop.Balance < item.Price)
            {
                Ctx.Telemetry.Track(TelemetryEvents.ItemUnaffordableTap, ("item_id", item.SkinId), ("price", item.Price),
                    ("balance", _shop.Balance));
                Shake(card);
                Ctx.Haptics.Play(HapticKind.Medium);
                Ctx.Router.Toast(L.T("error.insufficient_funds"));
                return;
            }
            Ctx.Telemetry.Track(TelemetryEvents.ItemPreview, ("item_id", item.SkinId), ("price", item.Price),
                ("balance", _shop.Balance), ("owned", false));
            Ctx.Router.Confirm(L.T("shop.confirm", L.T("skin." + item.SkinId), item.Price), L.T("shop.buy", item.Price),
                () => Guard(Buy(item.SkinId)));
        }

        private static void Shake(RectTransform card)
        {
            if (Look.ReduceMotion) return;
            Vector2 rest = card.anchoredPosition;
            Tween.Run(card, 0.36f, t =>
            {
                if (card != null) card.anchoredPosition = rest + new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * 8f * (1f - t), 0f);
            }, () => { if (card != null) card.anchoredPosition = rest; });
        }

        private async System.Threading.Tasks.Task Buy(string skinId)
        {
            // One key per purchase intent: a retried request can never charge twice.
            _shop = await Ctx.Api.PurchaseAsync(skinId, Guid.NewGuid().ToString("N"));
            Ctx.Audio.Reward();
            Render();
        }

        private async System.Threading.Tasks.Task Equip(string skinId)
        {
            _shop = await Ctx.Api.EquipAsync(skinId);
            Render();
        }
    }

    /// <summary>
    /// The day's three missions (easy, medium, hard) and the chest for finishing all three.
    /// Progress comes from the server (the day's verified runs); "Claim" pays coins once.
    /// </summary>
    public sealed class MissionsScreen : UiScreen
    {
        private readonly RectTransform _list;
        private MissionBoard _missions;

        public MissionsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Missions", ScreenBackground.Gradient)
        {
            Header(L.T("missions.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, Ui.RowGap);
            TextMeshProUGUI note = UiKit.Text(body, L.T("missions.note"), TextRole.Caption, T.TextSecondary);
            UiKit.Size(note.rectTransform, height: 18);
            _list = UiKit.ScrollList(body, Ui.RowGap);
            UiKit.Size((RectTransform)_list.parent, height: 300).flexibleHeight = 1;
        }

        protected override void OnShow() => Reload();

        private async void Reload()
        {
            try
            {
                _missions = await Ctx.Api.MissionsAsync();
            }
            catch (ApiException e)
            {
                UiKit.Clear(_list);
                States.Failed(_list, e, Reload);
                return;
            }
            Render();
        }

        private void Render()
        {
            UiKit.Clear(_list);
            foreach (Mission m in _missions.Missions)
                Row(L.T("mission." + m.Kind, m.Target), L.T("missions.progress", m.Progress, m.Target), m.Reward, m.Done, m.Claimed, m.Slot,
                    m.Kind == "daily" ? (Action)(() => Ctx.Flow.PlayDaily()) : null);
            MissionChest chest = _missions.Chest;
            if (chest != null)
                Row(L.T("missions.chest"), L.T("missions.chest_note"), chest.Reward, chest.Ready, chest.Claimed, MissionBoard.ChestSlot);
        }

        private void Row(string title, string detail, int reward, bool done, bool claimed, int slot, Action play = null)
        {
            RectTransform card = UiKit.Card(_list, "Mission" + slot);
            UiKit.Size(card, height: 76);
            HorizontalLayoutGroup row = UiKit.Row(card, 12);
            row.padding = new RectOffset(16, 12, 10, 10);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childForceExpandWidth = false;

            RectTransform text = UiKit.Rect("Text", card);
            UiKit.Column(text, 2, null, TextAnchor.MiddleLeft);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            UiKit.Size(UiKit.OneLine(UiKit.Text(text, title, TextRole.Body, T.TextPrimary, TextAlignmentOptions.Left), 12f).rectTransform, height: 24);
            UiKit.Size(UiKit.Text(text, detail, TextRole.Caption, T.TextSecondary, TextAlignmentOptions.Left).rectTransform, height: 18);

            if (claimed)
                UiKit.Icon(card, Icons.Check, 26f, T.Accent);
            else if (done)
                UiKit.Pill(card, L.T("missions.claim", reward), Icons.Coin, () => Guard(Claim(slot)));
            else if (play != null)
                UiKit.Pill(card, L.T("missions.play"), Icons.Play, () => play());
            else
                UiKit.Chip(card, "+" + reward, T.SurfaceSunken, T.TextSecondary, 30f, Icons.Coin);
        }

        private async System.Threading.Tasks.Task Claim(int slot)
        {
            _missions = await Ctx.Api.ClaimMissionAsync(slot);
            Ctx.Audio.Reward();
            Ctx.Haptics.Play(HapticKind.Success);
            Ctx.Telemetry.Track(TelemetryEvents.MissionClaimed, ("slot", slot),
                ("kind", slot == MissionBoard.ChestSlot ? "chest" : _missions.Missions[slot].Kind));
            Render();
        }
    }

    /// <summary>Slides an IsoTower's top block back and forth (the shop's "live" previews).</summary>
    public sealed class TowerSlide : MonoBehaviour
    {
        public IsoTower Tower;
        private float _phase;

        private void OnEnable() => _phase = UnityEngine.Random.value * 6f;

        private void Update()
        {
            if (Tower == null || Look.ReduceMotion) return;
            Tower.SetSlide(Mathf.Sin(Time.unscaledTime * 1.6f + _phase));
        }
    }

    /// <summary>
    /// Daily reward modal: the streak as an overline, a 7-day grid (claimed days checked, today in the
    /// accent with its glow, future days dimmed) and Claim.
    /// </summary>
    public sealed class DailyRewardScreen : UiScreen
    {
        private static readonly int[] Rewards = { 25, 35, 50, 70, 90, 120, 200 }; // mirrors DAILY_REWARDS
        private const float TileHeight = 96f;

        private readonly TextMeshProUGUI _streak;
        private readonly TextMeshProUGUI _title;
        private readonly RectTransform _days;
        private readonly Button _claim;
        private readonly RectTransform _card;
        private DailyStatus _status;

        public override bool AllowsBanner => false;

        public DailyRewardScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Daily", ScreenBackground.Dim)
        {
            _card = UiKit.Surface(Safe, "Modal", SurfaceKind.Raised, Ui.SheetRadius).rectTransform;
            _card.anchorMin = new Vector2(0, 0.5f);
            _card.anchorMax = new Vector2(1, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(-2 * Ui.Side, 400);
            UiKit.Column(_card, 14, new RectOffset(16, 16, 20, 20), TextAnchor.UpperCenter);
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button close = UiKit.IconButton(_card, Icons.Close, () => Ctx.Router.Back());
            UiKit.IgnoreLayout(close);
            UiKit.Pin((RectTransform)close.transform, new Vector2(1, 1), new Vector2(Ui.IconButton, Ui.IconButton), new Vector2(-10, -10));

            UiKit.Size(UiKit.Text(_card, L.T("daily.title"), TextRole.Title).rectTransform, height: 34);
            _streak = UiKit.Text(_card, "", TextRole.Overline, T.TextSecondary);
            UiKit.Size(_streak.rectTransform, height: 18);
            _title = UiKit.Text(_card, "", TextRole.Body, T.TextPrimary);
            _days = UiKit.Rect("Days", _card);
            UiKit.Column(_days, 10);
            UiKit.Text(_card, L.T("daily.rule"), TextRole.Caption, T.TextSecondary);
            _claim = UiKit.Primary(_card, L.T("daily.claim"), Icons.Gift, Claim);
        }

        protected override void OnShow()
        {
            Load();
            UiRouter.Rise(_card, 0f);
        }

        private async void Load()
        {
            try
            {
                _status = await Ctx.Api.DailyAsync();
                Render();
            }
            catch (ApiException e)
            {
                Ctx.Router.Toast(Errors.Describe(e));
            }
        }

        private void Render()
        {
            bool claimed = _status.ClaimedToday;
            int nextDay = claimed ? _status.Streak : _status.Streak + 1;
            int cycleDay = Mathf.Min(nextDay, Rewards.Length);
            _streak.text = L.T("daily.streak", _status.Streak);
            _title.text = claimed ? L.T("daily.come_back") : L.T("daily.ready", _status.NextReward);

            // Days 1-4 on top; 5, 6 and the big day 7 (two tiles wide) below.
            UiKit.Clear(_days);
            RectTransform row = null;
            for (int day = 1; day <= Rewards.Length; day++)
            {
                if (day == 1 || day == 5)
                {
                    row = UiKit.Rect("Week", _days);
                    UiKit.Size(row, height: TileHeight);
                    UiKit.Row(row, 10);
                }
                bool done = day < cycleDay || (claimed && day == cycleDay);
                bool today = day == cycleDay && !claimed;
                RectTransform tile = Tile(row, day, done, today);
                // Equal shares whatever the text, so both rows line up; day 7 takes two plus the gap.
                bool big = day == Rewards.Length;
                UiKit.Size(tile, width: big ? 10 : 0, flexibleWidth: big ? 2 : 1);
            }
            _claim.gameObject.SetActive(!claimed);
            _claim.interactable = true;
        }

        private static RectTransform Tile(RectTransform row, int day, bool done, bool today)
        {
            RoundedRect surface = today
                ? UiKit.Solid(row, "Day" + day, T.Accent, 16f, glow: T.AccentGlow)
                : UiKit.Surface(row, "Day" + day, done ? SurfaceKind.Card : SurfaceKind.Sunken, 16f);
            RectTransform tile = surface.rectTransform;
            UiKit.Column(tile, 2, new RectOffset(4, 4, 10, 8), TextAnchor.MiddleCenter);
            string ink = today ? T.AccentOn : done ? T.Success : T.TextSecondary;
            UiKit.Size(UiKit.Text(tile, L.T("daily.day", day), TextRole.Caption, ink).rectTransform, height: 16);
            if (done)
            {
                UiKit.Icon(tile, Icons.Check, 26f, T.Success);
            }
            else
            {
                RectTransform coin = UiKit.Rect("CoinLine", tile);
                UiKit.Size(coin, height: 26);
                HorizontalLayoutGroup r = UiKit.Row(coin, 0);
                r.childForceExpandWidth = false;
                r.childForceExpandHeight = false; // keep the coin round
                r.childControlWidth = true;
                if (day == Rewards.Length) UiKit.Icon(coin, Icons.Gift, 26f, ink, filled: today);
                else UiKit.Coin(coin, 20f);
            }
            UiKit.Size(UiKit.Text(tile, Rewards[day - 1].ToString(), TextRole.Coin, today ? T.AccentOn : T.TextPrimary).rectTransform, height: 22);
            if (!today && !done) tile.gameObject.AddComponent<CanvasGroup>().alpha = 0.7f; // future days dimmed
            return tile;
        }

        private async void Claim()
        {
            _claim.interactable = false;
            try
            {
                // Key per calendar day: double taps and retries resolve to one claim.
                _status = await Ctx.Api.ClaimDailyAsync("daily-" + DateTime.UtcNow.ToString("yyyyMMdd"));
            }
            catch (ApiException e)
            {
                _claim.interactable = true;
                Ctx.Router.Toast(Errors.Describe(e));
                return;
            }
            Ctx.Audio.Reward();
            Ctx.Haptics.Play(HapticKind.Success);
            Ctx.Router.Toast(L.T("daily.claimed", _status.ClaimedAmount ?? 0));
            Render();
        }
    }

    /// <summary>
    /// Grouped cards: Look (Pop / Neon), motion, sound, music and haptics, language, account, legal,
    /// ad choices and Delete account.
    /// </summary>
    public sealed class SettingsScreen : UiScreen
    {
        private readonly TMP_InputField _name;
        private readonly RectTransform _account;

        public SettingsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Settings", ScreenBackground.Gradient)
        {
            Header(L.T("settings.title"), () => Ctx.Router.Back());
            RectTransform body = UiKit.ScrollList(Body(), Ui.RowGap);

            LookSwitch(body, ctx);

            RectTransform prefs = Group(body);
            UiKit.Toggle(prefs, Icons.Motion, L.T("settings.reduce_motion"), ctx.Save.ReduceMotion, on => ctx.Flow.SetReduceMotion(on));
            UiKit.Toggle(prefs, Icons.Sound, L.T("settings.sound"), ctx.Save.SoundOn, on => { ctx.Save.SoundOn = on; ctx.Audio.Enabled = on; ctx.Telemetry.Track(TelemetryEvents.SettingChanged, ("setting", "sound"), ("value", on)); });
            UiKit.Toggle(prefs, Icons.Music, L.T("settings.music"), ctx.Save.MusicOn, on => ctx.Flow.SetMusic(on));
            UiKit.Toggle(prefs, Icons.Haptics, L.T("settings.haptics"), ctx.Save.HapticsOn, on => { ctx.Save.HapticsOn = on; ctx.Haptics.Enabled = on; ctx.Telemetry.Track(TelemetryEvents.SettingChanged, ("setting", "haptics"), ("value", on)); });

            Overline(body, L.T("settings.language"));
            Action<int> lang = UiKit.Segmented(body, new[] { "English", "Español" }, i => SetLanguage(i == 0 ? "en" : "es"));
            lang(ctx.Localizer.Language == "es" ? 1 : 0);

            Overline(body, L.T("settings.account"));
            RectTransform account = Group(body);
            RectTransform nameRow = UiKit.Rect("NameRow", account);
            UiKit.Size(nameRow, height: 48);
            HorizontalLayoutGroup row = UiKit.Row(nameRow, 10);
            row.childForceExpandWidth = false;
            _name = UiKit.Input(nameRow, L.T("settings.name_placeholder"), 20);
            UiKit.Size((RectTransform)_name.transform, flexibleWidth: 1);
            UiKit.Pill(nameRow, L.T("settings.save"), Icons.Check, Rename);
            _account = UiKit.Rect("Providers", account);
            UiKit.Column(_account, 8);

            RectTransform legal = Group(body);
            if (ctx.Ads.PrivacyOptionsRequired)
                LinkRow(legal, Icons.Privacy, L.T("settings.privacy_choices"), () => ctx.Ads.ShowPrivacyOptions());
            LinkRow(legal, Icons.Privacy, L.T("settings.privacy"), () => Application.OpenURL(ctx.Config.apiBaseUrl + "/privacy?lang=" + ctx.Localizer.Language));
            LinkRow(legal, Icons.Terms, L.T("settings.terms"), () => Application.OpenURL(ctx.Config.apiBaseUrl + "/terms?lang=" + ctx.Localizer.Language));

            RectTransform danger = Group(body);
            LinkRow(danger, Icons.Delete, L.T("settings.delete"), () =>
                ctx.Router.Confirm(L.T("settings.delete_confirm"), L.T("settings.delete_yes"), () => ctx.Flow.DeleteAccount(), destructive: true),
                T.Danger);
            UiKit.Size(UiKit.Text(body, "Stack IT " + Application.version, TextRole.Caption, T.TextSecondary).rectTransform, height: 24);
        }

        /// <summary>
        /// "Look": two tiles previewing Pop (pastel) and Neon (dark with a teal glow). The previews
        /// show their own look whatever the current one is; the selected tile wears the accent ring.
        /// </summary>
        public static void LookSwitch(Transform parent, UiContext ctx)
        {
            Overline(parent, L.T("settings.look"));
            RectTransform row = UiKit.Rect("Look", parent);
            UiKit.Size(row, height: 92);
            UiKit.Row(row, Ui.RowGap);
            var rings = new List<(ThemeId Id, ThemedSurface Ring)>();
            foreach (ThemePalette look in Look.Catalog.All)
            {
                ThemeId id = look.Id;
                RoundedRect tile = UiKit.Shape(row, "Tile " + id, 16f);
                tile.raycastTarget = true;
                ThemedSurface ring = ThemedSurface.Bind(tile, SurfaceKind.Solid, T.Accent);
                ring.FillAlpha = 0f;
                ring.RingToken = T.SecondaryBorder; // bright in both looks
                rings.Add((id, ring));

                RectTransform swatch = UiKit.Stretch(UiKit.Rect("Swatch", tile.transform), 4, 4, 4, 30);
                var sky = swatch.gameObject.AddComponent<GradientBackground>();
                Rgba[] stops = look.Colors(T.BgGradient);
                sky.SetStops(Look.ToColor(stops[0]), Look.ToColor(stops[1]), Look.ToColor(stops[2]), 0.5f);
                IgnoreTheming.Mark(sky, "look preview");
                RoundedRect glow = UiKit.Shape(swatch, "Glow", 0f);
                UiKit.Pin(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Glow centre = look.Glow(T.BgCenterGlow);
                glow.SetShadow(Look.ToColor(centre.Color), 40f, 0f);
                IgnoreTheming.Mark(glow, "look preview");
                RectTransform towerRect = UiKit.Stretch(UiKit.Rect("Tower", swatch), 0, 6, 0, 6);
                var tower = towerRect.gameObject.AddComponent<IsoTower>();
                tower.Footprint = 0.3f;
                Rgba[] faces = look.Colors(T.BlockBase);
                Rgba[] moving = look.Colors(T.BlockMoving);
                tower.SetBlocks(new[] { new IsoTower.BlockFaces { Top = Look.ToColor(faces[0]), Left = Look.ToColor(faces[1]), Right = Look.ToColor(faces[2]) } });
                tower.SetMoving(new IsoTower.BlockFaces { Top = Look.ToColor(moving[0]), Left = Look.ToColor(moving[1]), Right = Look.ToColor(moving[2]) }, true);
                IgnoreTheming.Mark(tower, "look preview");

                TextMeshProUGUI label = UiKit.Text(tile.transform, L.T("look." + ThemeCatalog.Key(id)), TextRole.Pill, T.TextPrimary);
                UiKit.Pin(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(120, 26), new Vector2(0, 2));
                label.rectTransform.pivot = new Vector2(0.5f, 0f);

                var button = tile.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    UiKit.Pressed?.Invoke();
                    ctx.Flow.SetLook(id);
                });
                tile.gameObject.AddComponent<Pressable>().Button = button;
            }
            void Mark(ThemePalette current)
            {
                foreach ((ThemeId id, ThemedSurface ring) in rings)
                {
                    if (ring == null) continue;
                    ring.RingWidth = id == current.Id ? 2.5f : 0f;
                    ring.Refresh();
                }
            }
            Mark(Look.P);
            Look.Changed += Mark;
        }

        private static void Overline(Transform parent, string text)
        {
            TextMeshProUGUI label = UiKit.Text(parent, text, TextRole.Overline, T.TextSecondary, TextAlignmentOptions.Left);
            UiKit.Size(label.rectTransform, height: 22);
        }

        /// <summary>A card holding a few rows.</summary>
        private static RectTransform Group(RectTransform parent)
        {
            RectTransform card = UiKit.Card(parent, "Group");
            UiKit.Column(card, 4, new RectOffset((int)Ui.CardPad, (int)Ui.CardPad, 8, 8));
            return card;
        }

        /// <summary>A tappable row: icon, label, chevron.</summary>
        private static void LinkRow(Transform group, string icon, string text, Action onClick, string token = T.TextPrimary)
        {
            RectTransform row = UiKit.Rect("Link", group);
            UiKit.Size(row, height: 48);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            IgnoreTheming.Mark(hit, "invisible tap area");
            HorizontalLayoutGroup layout = UiKit.Row(row, 12);
            layout.childForceExpandWidth = false;
            UiKit.Icon(row, icon, Ui.IconSize, token == T.TextPrimary ? T.TextSecondary : token);
            UiKit.Grow(UiKit.Text(row, text, TextRole.Body, token, TextAlignmentOptions.Left));
            UiKit.Icon(row, Icons.Chevron, 18f, T.TextSecondary);
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.AddListener(() => onClick());
            row.gameObject.AddComponent<Pressable>().Button = button;
        }

        protected override void OnShow()
        {
            _name.text = Ctx.Api.Profile?.DisplayName ?? "";
            RenderAccount();
        }

        private void RenderAccount()
        {
            UiKit.Clear(_account);
            List<string> providers = Ctx.Api.Profile?.Providers ?? new List<string>();
            foreach (SignInProvider provider in new[] { SignInProvider.Apple, SignInProvider.Google })
            {
                if (!Ctx.Auth.CanUse(provider)) continue;
                string id = provider == SignInProvider.Google ? "google" : "apple";
                if (providers.Contains(id))
                {
                    TextMeshProUGUI linked = UiKit.Text(_account, UiKit.WithIcon(Icons.Check, L.T("settings.linked_" + id)), TextRole.Body, T.Success,
                        TextAlignmentOptions.Left);
                    UiKit.Size(linked.rectTransform, height: 36);
                    continue;
                }
                RectTransform line = UiKit.Rect("Link", _account);
                UiKit.Size(line, height: Ui.PillHeight);
                HorizontalLayoutGroup row = UiKit.Row(line, 0);
                row.childForceExpandWidth = false;
                row.childAlignment = TextAnchor.MiddleLeft;
                UiKit.Pill(line, L.T("settings.link_" + id), Icons.Link, () => Guard(Link(provider)));
            }
            if (_account.childCount == 0)
                UiKit.Text(_account, L.T("settings.guest_note"), TextRole.Caption, T.TextSecondary, TextAlignmentOptions.Left);
        }

        private async System.Threading.Tasks.Task Link(SignInProvider provider)
        {
            try
            {
                await Ctx.Auth.LinkAsync(provider);
            }
            catch (ApiException e) when (e.Code == "identity_in_use")
            {
                // That Google/Apple account already has progress: offer to switch to it.
                Ctx.Router.Confirm(L.T("settings.switch_account"), L.T("settings.switch"), () => Guard(Switch(provider)));
                return;
            }
            await Ctx.Api.RefreshProfileAsync();
            Ctx.Router.Toast(L.T("settings.linked_toast"));
            RenderAccount();
        }

        private async System.Threading.Tasks.Task Switch(SignInProvider provider)
        {
            await Ctx.Auth.SwitchToProviderAccountAsync(provider);
            Ctx.Save.ClearAccountData();
            await Ctx.Api.RefreshProfileAsync();
            RenderAccount();
        }

        private void Rename() => Guard(RenameAsync());

        private async System.Threading.Tasks.Task RenameAsync()
        {
            await Ctx.Api.RenameAsync(_name.text.Trim());
            Ctx.Router.Toast(L.T("settings.saved"));
        }

        private void SetLanguage(string code)
        {
            if (code == Ctx.Localizer.Language) return;
            Ctx.Save.Language = code;
            Ctx.Telemetry.Track(TelemetryEvents.SettingChanged, ("setting", "language"), ("value", code));
            Ctx.Localizer.SetLanguage(code); // the App rebuilds the UI on Changed
        }
    }
}
