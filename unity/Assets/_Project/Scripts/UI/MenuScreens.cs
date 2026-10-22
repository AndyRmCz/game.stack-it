using System;
using System.Collections.Generic;
using StackIt.Client.Api;
using StackIt.Core;
using StackIt.Game;
using StackIt.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    public sealed class FriendsScreen : UiScreen
    {
        private readonly TextMeshProUGUI _myCode;
        private readonly TMP_InputField _input;
        private readonly RectTransform _list;

        public FriendsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Friends", Theme.Enamel)
        {
            Header(L.T("friends.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 28);

            RectTransform card = UiKit.Card(body, "MyCode");
            UiKit.Column(card, 12, new RectOffset(48, 48, 40, 44));
            UiKit.Caps(card, L.T("friends.your_code"), Theme.InkSoft);
            _myCode = UiKit.Display(card, "—", 100, Theme.Ink);
            _myCode.characterSpacing = 10;
            UiKit.Size(_myCode.rectTransform, height: 120);
            UiKit.Button(card, L.T("friends.share_code"), ButtonStyle.Secondary, ShareCode, 120, Icons.Share);

            RectTransform addRow = UiKit.Rect("Add", body);
            UiKit.Size(addRow, height: 140);
            UiKit.Row(addRow, 20);
            _input = UiKit.Input(addRow, L.T("friends.code_placeholder"), 12, TMP_InputField.CharacterValidation.Alphanumeric);
            UiKit.Size((RectTransform)_input.transform, flexibleWidth: 3);
            Button add = UiKit.Button(addRow, L.T("friends.add"), ButtonStyle.Primary, Add, 140, Icons.AddFriend);
            UiKit.Size((RectTransform)add.transform, flexibleWidth: 1.4f);

            _list = UiKit.ScrollList(body, 20);
            UiKit.Size((RectTransform)_list.parent, height: 900).flexibleHeight = 1;
        }

        protected override void OnShow()
        {
            _myCode.text = Ctx.Api.Profile?.FriendCode ?? "—";
            Guard(Load());
        }

        private async System.Threading.Tasks.Task Load()
        {
            List<Friend> friends = await Ctx.Api.FriendsAsync();
            UiKit.Clear(_list);
            if (friends.Count == 0)
            {
                UiKit.Label(_list, L.T("friends.empty"), 40, Theme.InkSoft);
                return;
            }
            foreach (Friend friend in friends) Row(friend);
        }

        private void Row(Friend friend)
        {
            RectTransform row = UiKit.Card(_list, "Friend", radius: 36);
            UiKit.Size(row, height: 140);
            HorizontalLayoutGroup layout = UiKit.Row(row, 24);
            layout.padding = new RectOffset(28, 16, 22, 22);
            layout.childForceExpandWidth = false;
            string initial = string.IsNullOrEmpty(friend.DisplayName) ? "?" : friend.DisplayName.Substring(0, 1).ToUpperInvariant();
            UiKit.Badge(row, initial, Theme.WithAlpha(Theme.Pool, 0.14f), Theme.Pool, 96);
            TextMeshProUGUI name = UiKit.Label(row, friend.DisplayName, Theme.BodySize, Theme.Ink, TextAlignmentOptions.Left, bold: true);
            UiKit.OneLine(name, 30);
            UiKit.Size(name.rectTransform, flexibleWidth: 1);
            TextMeshProUGUI best = UiKit.Display(row, friend.BestScore?.ToString() ?? "–", 56, Theme.Ink);
            best.alignment = TextAlignmentOptions.Right;
            UiKit.Size(best.rectTransform, width: 150);
            Button remove = UiKit.IconButton(row, Icons.Close, ButtonStyle.Ghost, () =>
                Ctx.Router.Confirm(L.T("friends.remove_confirm", friend.DisplayName), L.T("friends.remove"),
                    () => Guard(RemoveThenReload(friend.UserId))), 96);
            remove.GetComponentInChildren<TextMeshProUGUI>().color = Theme.InkSoft;
        }

        private async System.Threading.Tasks.Task RemoveThenReload(string userId)
        {
            await Ctx.Api.RemoveFriendAsync(userId);
            await Load();
        }

        private void Add() => Guard(AddAsync());

        private async System.Threading.Tasks.Task AddAsync()
        {
            string code = _input.text.Trim().ToUpperInvariant();
            if (code.Length < 4) return;
            Friend friend = await Ctx.Api.AddFriendAsync(code);
            _input.text = "";
            Ctx.Router.Toast(L.T("friends.added", friend.DisplayName));
            await Load();
        }

        private void ShareCode()
        {
            string code = Ctx.Api.Profile?.FriendCode;
            if (code == null) return;
            Ctx.Share.Share(L.T("friends.share_text", code), Ctx.Config.apiBaseUrl, null);
        }
    }

    public sealed class LeaderboardScreen : UiScreen
    {
        private readonly RectTransform _list;
        private readonly TextMeshProUGUI _note;
        private readonly Action<int> _selectTab;
        private bool _showWeekly;

        public LeaderboardScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Leaderboard", Theme.Enamel)
        {
            Header(L.T("ranks.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 24);
            _selectTab = UiKit.Segmented(body, new[] { L.T("ranks.all_time"), L.T("ranks.weekly") }, i => Select(i == 1));
            _note = UiKit.Label(body, "", Theme.CaptionSize, Theme.InkSoft);
            _list = UiKit.ScrollList(body, 16);
            UiKit.Size((RectTransform)_list.parent, height: 1100).flexibleHeight = 1;
        }

        protected override void OnShow() => Select(_showWeekly);

        private void Select(bool weekly)
        {
            _showWeekly = weekly;
            _selectTab(weekly ? 1 : 0);
            _note.text = weekly ? L.T("ranks.weekly_note") : "";
            _note.gameObject.SetActive(weekly);
            Guard(Load(weekly));
        }

        private async System.Threading.Tasks.Task Load(bool weekly)
        {
            Leaderboard board = await Ctx.Api.LeaderboardAsync(weekly);
            if (weekly != _showWeekly) return; // user switched tabs meanwhile
            UiKit.Clear(_list);
            if (board.Entries.Count <= 1)
            {
                UiKit.Label(_list, L.T("ranks.empty"), 40, Theme.InkSoft);
            }
            foreach (LeaderboardEntry entry in board.Entries)
            {
                RectTransform row = UiKit.Card(_list, "Entry", entry.IsMe ? Theme.Hex(0xe3eeff) : Theme.Paper, radius: 36);
                UiKit.Size(row, height: 132);
                HorizontalLayoutGroup layout = UiKit.Row(row, 24);
                layout.padding = new RectOffset(24, 40, 18, 18);
                layout.childForceExpandWidth = false;
                bool podium = entry.Rank <= 3;
                UiKit.Badge(row, entry.Rank.ToString(), podium ? Theme.Sun : Theme.Enamel, podium ? Theme.Ink : Theme.InkSoft, 96);
                TextMeshProUGUI name = UiKit.Label(row, entry.IsMe ? L.T("ranks.you", entry.DisplayName) : entry.DisplayName,
                    Theme.BodySize, Theme.Ink, TextAlignmentOptions.Left, bold: true);
                UiKit.OneLine(name, 30);
                UiKit.Size(name.rectTransform, flexibleWidth: 1);
                TextMeshProUGUI score = UiKit.Display(row, entry.Score.ToString(), 60, entry.IsMe ? Theme.Pool : Theme.Ink);
                score.alignment = TextAlignmentOptions.Right;
                UiKit.Size(score.rectTransform, width: 180);
            }
        }
    }

    public sealed class ShopScreen : UiScreen
    {
        private const string CoinTint = "#D99A0B";

        private readonly RectTransform _grid;
        private readonly TextMeshProUGUI _balance;
        private readonly Action<int> _selectTab;
        private string _kind = "block";
        private Shop _shop;

        public ShopScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Shop", Theme.Enamel)
        {
            Header(L.T("shop.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 28);

            RectTransform balanceRow = UiKit.Rect("Balance", body);
            UiKit.Size(balanceRow, height: 100);
            UiKit.Row(balanceRow, 0).childForceExpandWidth = false;
            _balance = UiKit.Pill(balanceRow, "", Theme.Paper, Theme.Ink, 100);

            _selectTab = UiKit.Segmented(body, new[] { L.T("shop.blocks"), L.T("shop.themes") }, i => Select(i == 0 ? "block" : "theme"));

            RectTransform viewport = UiKit.Rect("Grid", body);
            UiKit.Size(viewport, height: 1150).flexibleHeight = 1;
            _grid = UiKit.Stretch(UiKit.Rect("Items", viewport));
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(472, 420);
            grid.spacing = new Vector2(24, 28);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        protected override void OnShow() => Guard(Reload());

        private async System.Threading.Tasks.Task Reload()
        {
            _shop = await Ctx.Api.ShopAsync();
            Render();
        }

        private void Select(string kind)
        {
            _kind = kind;
            Render();
        }

        private void Render()
        {
            _selectTab(_kind == "block" ? 0 : 1);
            if (_shop == null) return;
            _balance.text = UiKit.WithIcon($"<color={CoinTint}>{Icons.Coin}</color>", _shop.Balance.ToString("N0"));
            UiKit.Clear(_grid);
            foreach (ShopItem item in _shop.Items)
                if (item.Kind == _kind) Card(item);
        }

        private void Card(ShopItem item)
        {
            RectTransform card = UiKit.Card(_grid, item.SkinId, radius: 44);
            UiKit.Column(card, 14, new RectOffset(28, 28, 28, 24), TextAnchor.UpperCenter);

            Swatch(card, item);
            TextMeshProUGUI name = UiKit.Label(card, L.T("skin." + item.SkinId), 40, Theme.Ink, TextAlignmentOptions.Center, bold: true);
            UiKit.OneLine(name, 28);
            UiKit.Size(name.rectTransform, height: 56);

            if (item.Equipped)
            {
                TextMeshProUGUI equipped = UiKit.Label(card, UiKit.WithIcon(Icons.Check, L.T("shop.equipped")), 40, Theme.Mint, TextAlignmentOptions.Center, bold: true);
                UiKit.Size(equipped.rectTransform, height: 104);
            }
            else if (item.Owned)
                UiKit.Button(card, L.T("shop.equip"), ButtonStyle.Secondary, () => OnItem(item), 104);
            else
                UiKit.Button(card, item.Price.ToString("N0"), ButtonStyle.Reward, () => OnItem(item), 104, Icons.Coin);
        }

        /// <summary>A mini tower of the skin's first blocks, or a slice of the theme's sky.</summary>
        private static void Swatch(RectTransform card, ShopItem item)
        {
            RectTransform swatch = UiKit.Rect("Swatch", card);
            UiKit.Size(swatch, height: 180);
            if (item.Kind == "block")
            {
                VerticalLayoutGroup column = UiKit.Column(swatch, 6, new RectOffset(40, 40, 8, 8), TextAnchor.LowerCenter);
                column.childForceExpandHeight = true;
                for (int i = 5; i >= 0; i--) // drawn top to bottom
                    UiKit.Panel(swatch, "Block", Palette.Block(item.SkinId, i * 3 + 1), rounded: true, radius: 10);
                return;
            }
            Image frame = UiKit.Panel(swatch, "Sky", Color.white, rounded: true, radius: 28);
            UiKit.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>(); // clips the bands to the rounded corners
            VerticalLayoutGroup bands = UiKit.Column(frame.rectTransform, 0);
            bands.childForceExpandHeight = true;
            for (int i = 0; i < 6; i++)
            {
                Palette.Sky(item.SkinId, 12f, out Color top, out Color bottom);
                UiKit.Panel(frame.transform, "Band", Color.Lerp(top, bottom, i / 5f));
            }
        }

        private void OnItem(ShopItem item)
        {
            if (item.Owned)
            {
                Guard(Equip(item.SkinId));
                return;
            }
            if (_shop.Balance < item.Price)
            {
                Ctx.Router.Toast(L.T("error.insufficient_funds"));
                return;
            }
            Ctx.Router.Confirm(L.T("shop.confirm", L.T("skin." + item.SkinId), item.Price), L.T("shop.buy", item.Price),
                () => Guard(Buy(item.SkinId)));
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

    public sealed class DailyRewardScreen : UiScreen
    {
        private static readonly int[] Rewards = { 25, 35, 50, 70, 90, 120, 200 }; // mirrors DAILY_REWARDS
        private readonly RectTransform _days;
        private readonly TextMeshProUGUI _title;
        private readonly Button _claim;
        private DailyStatus _status;

        public DailyRewardScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Daily", Theme.Enamel)
        {
            Header(L.T("daily.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 40, new RectOffset(0, 0, 40, 0));
            _title = UiKit.Display(body, "", 64, Theme.Ink);
            _title.textWrappingMode = TextWrappingModes.Normal;
            _days = UiKit.Rect("Days", body);
            UiKit.Size(_days, height: 230);
            UiKit.Row(_days, 12);
            UiKit.Label(body, L.T("daily.rule"), Theme.SmallSize, Theme.InkSoft);
            _claim = UiKit.Button(body, L.T("daily.claim"), ButtonStyle.Reward, Claim, 170, Icons.Gift);
        }

        protected override void OnShow() => Guard(Load());

        private async System.Threading.Tasks.Task Load()
        {
            _status = await Ctx.Api.DailyAsync();
            Render();
        }

        private void Render()
        {
            int nextDay = _status.ClaimedToday ? _status.Streak : _status.Streak + 1;
            _title.text = _status.ClaimedToday ? L.T("daily.come_back") : L.T("daily.ready", _status.NextReward);
            UiKit.Clear(_days);
            for (int day = 1; day <= Rewards.Length; day++)
            {
                int cycleDay = Mathf.Min(nextDay, Rewards.Length);
                bool done = day < cycleDay || (_status.ClaimedToday && day == cycleDay);
                bool today = day == cycleDay && !_status.ClaimedToday;
                RectTransform cell = UiKit.Card(_days, "Day" + day, today ? Theme.Sun : Theme.Paper, radius: 28, shadow: today);
                UiKit.Column(cell, 6, new RectOffset(4, 4, 22, 22), TextAnchor.MiddleCenter);
                UiKit.Label(cell, L.T("daily.day", day), 26, today ? Theme.Ink : Theme.InkSoft, TextAlignmentOptions.Center, bold: true);
                if (done) UiKit.Icon(cell, Icons.Check, 56, Theme.Mint);
                else UiKit.Display(cell, Rewards[day - 1].ToString(), 44, Theme.Ink);
            }
            _claim.interactable = !_status.ClaimedToday;
        }

        private void Claim() => Guard(ClaimAsync());

        private async System.Threading.Tasks.Task ClaimAsync()
        {
            _claim.interactable = false;
            // Key per calendar day: double taps and retries resolve to one claim.
            _status = await Ctx.Api.ClaimDailyAsync("daily-" + DateTime.UtcNow.ToString("yyyyMMdd"));
            Ctx.Audio.Reward();
            Ctx.Haptics.Play(HapticKind.Success);
            Ctx.Router.Toast(L.T("daily.claimed", _status.ClaimedAmount ?? 0));
            Render();
        }
    }

    public sealed class SettingsScreen : UiScreen
    {
        private readonly TMP_InputField _name;
        private readonly RectTransform _account;

        public SettingsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Settings", Theme.Enamel)
        {
            Header(L.T("settings.title"), () => Ctx.Router.Back());
            RectTransform body = UiKit.ScrollList(Body(), 20);

            UiKit.Caps(body, L.T("settings.name"), Theme.InkSoft, align: TextAlignmentOptions.Left);
            RectTransform nameRow = UiKit.Rect("NameRow", body);
            UiKit.Size(nameRow, height: 140);
            UiKit.Row(nameRow, 16);
            _name = UiKit.Input(nameRow, L.T("settings.name_placeholder"), 20);
            UiKit.Size((RectTransform)_name.transform, flexibleWidth: 3);
            UiKit.Size((RectTransform)UiKit.Button(nameRow, L.T("settings.save"), ButtonStyle.Primary, Rename, 140).transform, flexibleWidth: 1.2f);

            UiKit.Spacer(body, 12);
            RectTransform prefs = Group(body);
            UiKit.Toggle(prefs, Icons.Sound, L.T("settings.sound"), ctx.Save.SoundOn, on => { ctx.Save.SoundOn = on; ctx.Audio.Enabled = on; });
            UiKit.Toggle(prefs, Icons.Haptics, L.T("settings.haptics"), ctx.Save.HapticsOn, on => { ctx.Save.HapticsOn = on; ctx.Haptics.Enabled = on; });

            UiKit.Spacer(body, 12);
            UiKit.Caps(body, L.T("settings.language"), Theme.InkSoft, align: TextAlignmentOptions.Left);
            Action<int> lang = UiKit.Segmented(body, new[] { "English", "Español" }, i => SetLanguage(i == 0 ? "en" : "es"));
            lang(ctx.Localizer.Language == "es" ? 1 : 0);

            UiKit.Spacer(body, 12);
            UiKit.Caps(body, L.T("settings.account"), Theme.InkSoft, align: TextAlignmentOptions.Left);
            _account = UiKit.Rect("Account", body);
            UiKit.Column(_account, 16);

            UiKit.Spacer(body, 12);
            if (ctx.Ads.PrivacyOptionsRequired)
                UiKit.Button(body, L.T("settings.privacy_choices"), ButtonStyle.Secondary, () => ctx.Ads.ShowPrivacyOptions(), 120);
            UiKit.Button(body, L.T("settings.privacy"), ButtonStyle.Ghost, () => Application.OpenURL(ctx.Config.apiBaseUrl + "/privacy?lang=" + ctx.Localizer.Language), 100);
            UiKit.Button(body, L.T("settings.terms"), ButtonStyle.Ghost, () => Application.OpenURL(ctx.Config.apiBaseUrl + "/terms?lang=" + ctx.Localizer.Language), 100);
            UiKit.Spacer(body, 20);
            UiKit.Button(body, L.T("settings.delete"), ButtonStyle.Danger, () =>
                ctx.Router.Confirm(L.T("settings.delete_confirm"), L.T("settings.delete_yes"), () => ctx.Flow.DeleteAccount()), 120, Icons.Delete);
            UiKit.Label(body, "Stack IT " + Application.version, Theme.CaptionSize, Theme.InkSoft);
        }

        /// <summary>A paper card holding a few rows.</summary>
        private static RectTransform Group(RectTransform parent)
        {
            // The parent column sizes it from its own layout group; no fitter needed.
            RectTransform card = UiKit.Card(parent, "Group");
            UiKit.Column(card, 0, new RectOffset(40, 32, 12, 12));
            return card;
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
                    UiKit.Label(_account, UiKit.WithIcon(Icons.Check, L.T("settings.linked_" + id)), Theme.BodySize, Theme.Mint, TextAlignmentOptions.Left, bold: true);
                    continue;
                }
                UiKit.Button(_account, L.T("settings.link_" + id), ButtonStyle.Secondary, () => Guard(Link(provider)), 130, Icons.Link);
            }
            if (_account.childCount == 0) UiKit.Label(_account, L.T("settings.guest_note"), Theme.SmallSize, Theme.InkSoft, TextAlignmentOptions.Left);
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
            Ctx.Localizer.SetLanguage(code); // the App rebuilds the UI on Changed
        }
    }
}
