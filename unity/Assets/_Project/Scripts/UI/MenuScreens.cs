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

        public FriendsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Friends", Theme.Night)
        {
            Header(L.T("friends.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 28);

            Image card = UiKit.Panel(body, "MyCode", Theme.Card, rounded: true);
            UiKit.Column(card.rectTransform, 12, new RectOffset(40, 40, 32, 32));
            UiKit.Label(card.transform, L.T("friends.your_code"), 38, Theme.Lavender);
            _myCode = UiKit.Display(card.transform, "—", 96, Theme.Amber);
            _myCode.characterSpacing = 12;
            UiKit.Button(card.transform, L.T("friends.share_code"), ButtonStyle.Secondary, ShareCode, 120);

            RectTransform addRow = UiKit.Rect("Add", body);
            UiKit.Size(addRow, height: 130);
            UiKit.Row(addRow, 20);
            _input = UiKit.Input(addRow, L.T("friends.code_placeholder"), 12, TMP_InputField.CharacterValidation.Alphanumeric);
            UiKit.Size((RectTransform)_input.transform, flexibleWidth: 3);
            Button add = UiKit.Button(addRow, L.T("friends.add"), ButtonStyle.Primary, Add, 130);
            UiKit.Size((RectTransform)add.transform, flexibleWidth: 1);

            _list = UiKit.ScrollList(body, 16);
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
                UiKit.Label(_list, L.T("friends.empty"), 42, Theme.Lavender);
                return;
            }
            foreach (Friend friend in friends) Row(friend);
        }

        private void Row(Friend friend)
        {
            Image row = UiKit.Panel(_list, "Friend", new Color(1, 1, 1, 0.06f), rounded: true);
            UiKit.Size(row.rectTransform, height: 130);
            HorizontalLayoutGroup layout = UiKit.Row(row.rectTransform, 16);
            layout.padding = new RectOffset(40, 16, 0, 0);
            TextMeshProUGUI name = UiKit.Label(row.transform, friend.DisplayName, 46, Theme.Moon, TextAlignmentOptions.Left, true);
            UiKit.Size(name.rectTransform, flexibleWidth: 3);
            TextMeshProUGUI best = UiKit.Label(row.transform, friend.BestScore?.ToString() ?? "–", 46, Theme.Amber, TextAlignmentOptions.Right, true);
            UiKit.Size(best.rectTransform, flexibleWidth: 1);
            Button remove = UiKit.Button(row.transform, "×", ButtonStyle.Ghost, () =>
                Ctx.Router.Confirm(L.T("friends.remove_confirm", friend.DisplayName), L.T("friends.remove"),
                    () => Guard(RemoveThenReload(friend.UserId))), 110);
            UiKit.Size((RectTransform)remove.transform, width: 110);
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
        private readonly Button _allTime;
        private readonly Button _weekly;
        private bool _showWeekly;

        public LeaderboardScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Leaderboard", Theme.Night)
        {
            Header(L.T("ranks.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 24);
            RectTransform tabs = UiKit.Rect("Tabs", body);
            UiKit.Size(tabs, height: 120);
            UiKit.Row(tabs, 16);
            _allTime = UiKit.Button(tabs, L.T("ranks.all_time"), ButtonStyle.Secondary, () => Select(false), 120);
            _weekly = UiKit.Button(tabs, L.T("ranks.weekly"), ButtonStyle.Secondary, () => Select(true), 120);
            _note = UiKit.Label(body, "", 34, Theme.Lavender);
            _list = UiKit.ScrollList(body, 12);
            UiKit.Size((RectTransform)_list.parent, height: 1100).flexibleHeight = 1;
        }

        protected override void OnShow() => Select(_showWeekly);

        private void Select(bool weekly)
        {
            _showWeekly = weekly;
            _allTime.GetComponent<Image>().color = weekly ? new Color(1, 1, 1, 0.12f) : Theme.Magenta;
            _weekly.GetComponent<Image>().color = weekly ? Theme.Magenta : new Color(1, 1, 1, 0.12f);
            _note.text = weekly ? L.T("ranks.weekly_note") : "";
            Guard(Load(weekly));
        }

        private async System.Threading.Tasks.Task Load(bool weekly)
        {
            Leaderboard board = await Ctx.Api.LeaderboardAsync(weekly);
            if (weekly != _showWeekly) return; // user switched tabs meanwhile
            UiKit.Clear(_list);
            if (board.Entries.Count <= 1)
            {
                UiKit.Label(_list, L.T("ranks.empty"), 42, Theme.Lavender);
            }
            foreach (LeaderboardEntry entry in board.Entries)
            {
                Image row = UiKit.Panel(_list, "Entry", entry.IsMe ? new Color(1f, 0.24f, 0.6f, 0.22f) : new Color(1, 1, 1, 0.06f), rounded: true);
                UiKit.Size(row.rectTransform, height: 120);
                UiKit.Row(row.rectTransform, 12).padding = new RectOffset(36, 40, 0, 0);
                TextMeshProUGUI rank = UiKit.Label(row.transform, "#" + entry.Rank, 44, entry.Rank == 1 ? Theme.Amber : Theme.Lavender, TextAlignmentOptions.Left, true);
                UiKit.Size(rank.rectTransform, flexibleWidth: 1);
                TextMeshProUGUI name = UiKit.Label(row.transform, entry.IsMe ? L.T("ranks.you", entry.DisplayName) : entry.DisplayName, 46, Theme.Moon, TextAlignmentOptions.Left, entry.IsMe);
                UiKit.Size(name.rectTransform, flexibleWidth: 4);
                TextMeshProUGUI score = UiKit.Label(row.transform, entry.Score.ToString(), 48, Theme.Amber, TextAlignmentOptions.Right, true);
                UiKit.Size(score.rectTransform, flexibleWidth: 1.5f);
            }
        }
    }

    public sealed class ShopScreen : UiScreen
    {
        private readonly RectTransform _grid;
        private readonly TextMeshProUGUI _balance;
        private readonly Button _blocks;
        private readonly Button _themes;
        private string _kind = "block";
        private Shop _shop;

        public ShopScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Shop", Theme.Night)
        {
            Header(L.T("shop.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 24);
            _balance = UiKit.Label(body, "", 52, Theme.Amber, TextAlignmentOptions.Center, true);
            RectTransform tabs = UiKit.Rect("Tabs", body);
            UiKit.Size(tabs, height: 120);
            UiKit.Row(tabs, 16);
            _blocks = UiKit.Button(tabs, L.T("shop.blocks"), ButtonStyle.Secondary, () => Select("block"), 120);
            _themes = UiKit.Button(tabs, L.T("shop.themes"), ButtonStyle.Secondary, () => Select("theme"), 120);

            Image viewport = UiKit.Panel(body, "Grid", new Color(0, 0, 0, 0));
            UiKit.Size(viewport.rectTransform, height: 1150).flexibleHeight = 1;
            _grid = UiKit.Stretch(UiKit.Rect("Items", viewport.transform));
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(470, 330);
            grid.spacing = new Vector2(24, 24);
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
            _blocks.GetComponent<Image>().color = _kind == "block" ? Theme.Magenta : new Color(1, 1, 1, 0.12f);
            _themes.GetComponent<Image>().color = _kind == "theme" ? Theme.Magenta : new Color(1, 1, 1, 0.12f);
            if (_shop == null) return;
            _balance.text = L.T("shop.balance", _shop.Balance.ToString("N0"));
            UiKit.Clear(_grid);
            foreach (ShopItem item in _shop.Items)
                if (item.Kind == _kind) Card(item);
        }

        private void Card(ShopItem item)
        {
            Image card = UiKit.Panel(_grid, item.SkinId, item.Equipped ? new Color(1f, 0.24f, 0.6f, 0.25f) : Theme.Card, rounded: true);
            UiKit.Column(card.rectTransform, 10, new RectOffset(24, 24, 24, 24), TextAnchor.MiddleCenter);

            // Swatch: the first blocks of the skin, or the theme's sky.
            RectTransform swatch = UiKit.Rect("Swatch", card.transform);
            UiKit.Size(swatch, height: 120);
            UiKit.Row(swatch, 6);
            for (int i = 0; i < 6; i++)
            {
                Color color;
                if (item.Kind == "block") color = Palette.Block(item.SkinId, i * 3 + 1);
                else
                {
                    Palette.Sky(item.SkinId, i / 5f, out Color top, out Color bottom);
                    color = Color.Lerp(bottom, top, 0.5f);
                }
                UiKit.Panel(swatch, "c", color, rounded: true);
            }

            UiKit.Label(card.transform, L.T("skin." + item.SkinId), 42, Theme.Moon, TextAlignmentOptions.Center, true);
            string action = item.Equipped ? L.T("shop.equipped")
                : item.Owned ? L.T("shop.equip")
                : L.T("shop.buy", item.Price);
            ButtonStyle style = item.Equipped ? ButtonStyle.Ghost : item.Owned ? ButtonStyle.Secondary : ButtonStyle.Primary;
            Button button = UiKit.Button(card.transform, action, style, () => OnItem(item), 100);
            button.interactable = !item.Equipped;
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
            Ctx.Audio.Record();
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

        public DailyRewardScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Daily", Theme.Night)
        {
            Header(L.T("daily.title"), () => Ctx.Router.Back());
            RectTransform body = Body();
            UiKit.Column(body, 40, new RectOffset(0, 0, 60, 0));
            _title = UiKit.Label(body, "", 54, Theme.Moon, TextAlignmentOptions.Center, true);
            _days = UiKit.Rect("Days", body);
            UiKit.Size(_days, height: 260);
            UiKit.Row(_days, 12);
            UiKit.Label(body, L.T("daily.rule"), 38, Theme.Lavender);
            _claim = UiKit.Button(body, L.T("daily.claim"), ButtonStyle.Primary, Claim, 170);
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
                bool today = day == cycleDay;
                Image cell = UiKit.Panel(_days, "Day" + day,
                    today ? new Color(1f, 0.72f, 0.3f, 0.3f) : done ? new Color(1, 1, 1, 0.16f) : new Color(1, 1, 1, 0.06f), rounded: true);
                UiKit.Column(cell.rectTransform, 4, new RectOffset(4, 4, 24, 24), TextAnchor.MiddleCenter);
                UiKit.Label(cell.transform, L.T("daily.day", day), 30, Theme.Lavender);
                UiKit.Label(cell.transform, Rewards[day - 1].ToString(), 46, done ? Theme.Lavender : Theme.Amber, TextAlignmentOptions.Center, true);
            }
            _claim.interactable = !_status.ClaimedToday;
        }

        private void Claim() => Guard(ClaimAsync());

        private async System.Threading.Tasks.Task ClaimAsync()
        {
            _claim.interactable = false;
            // Key per calendar day: double taps and retries resolve to one claim.
            _status = await Ctx.Api.ClaimDailyAsync("daily-" + DateTime.UtcNow.ToString("yyyyMMdd"));
            Ctx.Audio.Record();
            Ctx.Haptics.Play(HapticKind.Success);
            Ctx.Router.Toast(L.T("daily.claimed", _status.ClaimedAmount ?? 0));
            Render();
        }
    }

    public sealed class SettingsScreen : UiScreen
    {
        private readonly TMP_InputField _name;
        private readonly RectTransform _account;

        public SettingsScreen(UiContext ctx, Transform parent) : base(ctx, parent, "Settings", Theme.Night)
        {
            Header(L.T("settings.title"), () => Ctx.Router.Back());
            RectTransform body = UiKit.ScrollList(Body(), 22);

            UiKit.Label(body, L.T("settings.name"), 36, Theme.Lavender, TextAlignmentOptions.Left);
            RectTransform nameRow = UiKit.Rect("NameRow", body);
            UiKit.Size(nameRow, height: 130);
            UiKit.Row(nameRow, 16);
            _name = UiKit.Input(nameRow, L.T("settings.name_placeholder"), 20);
            UiKit.Size((RectTransform)_name.transform, flexibleWidth: 3);
            UiKit.Size((RectTransform)UiKit.Button(nameRow, L.T("settings.save"), ButtonStyle.Secondary, Rename, 130).transform, flexibleWidth: 1);

            UiKit.Toggle(body, L.T("settings.sound"), ctx.Save.SoundOn, on => { ctx.Save.SoundOn = on; ctx.Audio.Enabled = on; });
            UiKit.Toggle(body, L.T("settings.haptics"), ctx.Save.HapticsOn, on => { ctx.Save.HapticsOn = on; ctx.Haptics.Enabled = on; });

            UiKit.Label(body, L.T("settings.language"), 36, Theme.Lavender, TextAlignmentOptions.Left);
            RectTransform lang = UiKit.Rect("Lang", body);
            UiKit.Size(lang, height: 120);
            UiKit.Row(lang, 16);
            UiKit.Button(lang, "English", ctx.Localizer.Language == "en" ? ButtonStyle.Primary : ButtonStyle.Secondary, () => SetLanguage("en"), 120);
            UiKit.Button(lang, "Español", ctx.Localizer.Language == "es" ? ButtonStyle.Primary : ButtonStyle.Secondary, () => SetLanguage("es"), 120);

            UiKit.Label(body, L.T("settings.account"), 36, Theme.Lavender, TextAlignmentOptions.Left);
            _account = UiKit.Rect("Account", body);
            UiKit.Column(_account, 16);
            var fitter = _account.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (ctx.Ads.PrivacyOptionsRequired)
                UiKit.Button(body, L.T("settings.privacy_choices"), ButtonStyle.Secondary, () => ctx.Ads.ShowPrivacyOptions(), 120);
            UiKit.Button(body, L.T("settings.privacy"), ButtonStyle.Ghost, () => Application.OpenURL(ctx.Config.apiBaseUrl + "/privacy?lang=" + ctx.Localizer.Language), 110);
            UiKit.Button(body, L.T("settings.terms"), ButtonStyle.Ghost, () => Application.OpenURL(ctx.Config.apiBaseUrl + "/terms?lang=" + ctx.Localizer.Language), 110);
            UiKit.Spacer(body, 30);
            Button delete = UiKit.Button(body, L.T("settings.delete"), ButtonStyle.Ghost, () =>
                ctx.Router.Confirm(L.T("settings.delete_confirm"), L.T("settings.delete_yes"), () => ctx.Flow.DeleteAccount()), 110);
            delete.GetComponentInChildren<TextMeshProUGUI>().color = Theme.Magenta;
            UiKit.Label(body, "Stack IT " + Application.version, 30, Theme.Lavender);
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
                    UiKit.Label(_account, L.T("settings.linked_" + id), 42, Theme.Cyan, TextAlignmentOptions.Left, true);
                    continue;
                }
                UiKit.Button(_account, L.T("settings.link_" + id), ButtonStyle.Secondary, () => Guard(Link(provider)), 120);
            }
            if (_account.childCount == 0) UiKit.Label(_account, L.T("settings.guest_note"), 36, Theme.Lavender, TextAlignmentOptions.Left);
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
            Ctx.Save.Language = code;
            Ctx.Localizer.SetLanguage(code); // the App rebuilds the UI on Changed
        }
    }
}
