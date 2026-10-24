using System;
using System.Collections.Generic;
using StackIt.Bootstrap;
using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Game;
using StackIt.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>Actions screens can trigger. Implemented by the composition root (App).</summary>
    public interface IGameFlow
    {
        void PlayNormal();
        void PlayAgainst(string rivalName, int rivalScore, int rivalHeight);
        void PlayChallenge(string code);
        /// <summary>Today's Daily Tower.</summary>
        void PlayDaily();
        /// <summary>Copies the last run's emoji result (and a duel link, if verified) for chats.</summary>
        void CopyResult();
        /// <param name="via">Where it came from, for telemetry: "link", "code" or "friends".</param>
        void OpenChallenge(string code, string via);
        void ContinueWithAd();
        void DeclineContinue();
        void DoubleCoins();
        void ShareChallenge();
        /// <summary>The challenge clip straight into TikTok or Instagram Stories (H5).</summary>
        void ShareChallengeTo(ShareTarget target);
        void Pause();
        void Resume();
        void Restart();
        void GoHome();
        void DeleteAccount();
        void SetLook(ThemeId look);
        void SetReduceMotion(bool on);
        void SetMusic(bool on);
        /// <summary>Plays a soundtrack from the shop for a few seconds.</summary>
        void PreviewMusic(string styleId);
        void EndMusicPreview();
        /// <summary>A rewarded ad, then one run with a soundtrack the player doesn't own.</summary>
        void TryMusic(string styleId);
    }

    /// <summary>What a screen may use. Built once by the App.</summary>
    public sealed class UiContext
    {
        public AppConfig Config;
        public GameApi Api;
        public AuthService Auth;
        public SaveService Save;
        public IAdsService Ads;
        public AudioService Audio;
        public IHaptics Haptics;
        public IShareService Share;
        public DeepLinkService DeepLinks;
        public ILocalizer Localizer;
        public IThemeService Theme;
        public PendingChallenges Pending;
        public SeenSkins Seen;
        public GameController Game;
        public IGameFlow Flow;
        public UiRouter Router;
        public ITelemetry Telemetry = NullTelemetry.Instance;
    }

    public enum ScreenBackground
    {
        Gradient, // the look's background (menus)
        None,     // see-through: the game world shows behind (Home, HUD, Game Over)
        Dim,      // overlay.dim over the game (pause, dialogs)
    }

    public abstract class UiScreen
    {
        private const float ShowSeconds = 0.22f;
        private const float Rise = 16f;

        protected readonly UiContext Ctx;
        public readonly RectTransform Root;
        protected readonly RectTransform Safe;
        private readonly CanvasGroup _group;

        /// <summary>Menu screens show the banner ad; gameplay screens never do.</summary>
        public virtual bool AllowsBanner => true;

        protected UiScreen(UiContext ctx, Transform parent, string name, ScreenBackground background)
        {
            Ctx = ctx;
            Root = UiKit.Stretch(UiKit.Rect(name, parent));
            if (background == ScreenBackground.Gradient) UiKit.Background(Root);
            else if (background == ScreenBackground.Dim) UiKit.Dim(Root);
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            Safe = UiKit.SafeArea(Root);
            Root.gameObject.SetActive(false);
        }

        public bool Visible => Root.gameObject.activeSelf;

        public void Show()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            _group.alpha = 0f;
            Tween.Fade(_group, 1f, ShowSeconds);
            // The incoming screen fades in and slides up 16 pt (a plain fade with Reduce motion).
            RectTransform safe = Safe;
            float from = Look.ReduceMotion ? 0f : -Rise;
            Tween.Run(safe, ShowSeconds, t =>
            {
                if (safe != null) safe.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, 0f, Tween.EaseOutCubic(t)));
            });
            OnShow();
        }

        public void Hide()
        {
            if (!Visible) return;
            OnHide();
            Root.gameObject.SetActive(false);
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>Top bar: back icon button on the left, the screen title centred.</summary>
        protected RectTransform Header(string title, Action back)
        {
            RectTransform bar = UiKit.Rect("Header", Safe);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, Ui.IconButton);
            bar.anchoredPosition = new Vector2(0, -Ui.TopGap);

            Button backButton = UiKit.IconButton(bar, Icons.Back, () => back());
            UiKit.Pin((RectTransform)backButton.transform, new Vector2(0, 0.5f), new Vector2(Ui.IconButton, Ui.IconButton), new Vector2(Ui.Side, 0));

            TextMeshProUGUI label = UiKit.Text(bar, title, TextRole.Title);
            UiKit.Stretch(label.rectTransform, Ui.Side + Ui.IconButton + 8, 0, Ui.Side + Ui.IconButton + 8, 0);
            UiKit.OneLine(label, 18);
            return bar;
        }

        /// <summary>Content area under the header, inside the side and bottom padding.</summary>
        protected RectTransform Body(float top = Ui.TopGap + Ui.IconButton + 20f, float bottom = Ui.BottomGap)
        {
            return UiKit.Stretch(UiKit.Rect("Body", Safe), Ui.Side, top, Ui.Side, bottom);
        }

        protected async void Guard(System.Threading.Tasks.Task task)
        {
            try
            {
                await task;
            }
            catch (ApiException e)
            {
                Ctx.Router.Toast(Errors.Describe(e));
            }
        }
    }

    /// <summary>Maps API error codes to friendly, localized messages.</summary>
    public static class Errors
    {
        public static string Describe(ApiException e)
        {
            if (e.IsOffline) return L.T("error.offline");
            string key = "error." + e.Code;
            string text = L.T(key);
            return text == key ? L.T("error.generic") : text;
        }
    }

    /// <summary>One screen at a time, plus overlays (toast, confirmation sheet, theme cross-fade).</summary>
    public sealed class UiRouter
    {
        private readonly Stack<UiScreen> _history = new Stack<UiScreen>();
        private readonly IAdsService _ads;
        private readonly RectTransform _toast;
        private readonly TextMeshProUGUI _toastText;
        private readonly CanvasGroup _toastGroup;
        private readonly RectTransform _sheet;
        private readonly RectTransform _sheetCard;
        private readonly TextMeshProUGUI _sheetText;
        private readonly RectTransform _sheetActions;
        private Action _onConfirm;

        public UiScreen Current { get; private set; }

        /// <summary>A screen came to the front (telemetry's screen_view).</summary>
        public event Action<UiScreen> Shown;

        public UiRouter(Transform overlayParent, IAdsService ads)
        {
            _ads = ads;
            RectTransform safe = UiKit.SafeArea(overlayParent);

            // Toast: a themed surface near the top.
            RoundedRect toast = UiKit.Surface(safe, "Toast", SurfaceKind.Toast, Ui.SheetRadius);
            _toast = toast.rectTransform;
            UiKit.Band(_toast, 1f, 56, pivotY: 1f);
            _toast.anchoredPosition = new Vector2(0, -(Ui.TopGap + Ui.IconButton + 12));
            _toastText = UiKit.Text(_toast, "", TextRole.Body, T.ToastText);
            UiKit.Stretch(_toastText.rectTransform, 18, 6, 18, 6);
            _toastText.enableAutoSizing = true;
            _toastText.fontSizeMin = 12;
            _toastText.fontSizeMax = 16;
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            // Confirmation bottom sheet over a dim layer.
            _sheet = UiKit.Stretch(UiKit.Rect("Sheet", overlayParent));
            RoundedRect dim = UiKit.Dim(_sheet);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Dismiss);
            _sheetCard = BottomSheet(_sheet);
            _sheetText = UiKit.Text(_sheetCard, "", TextRole.Body);
            _sheetText.fontSize = 18;
            _sheetActions = UiKit.Rect("Actions", _sheetCard);
            UiKit.Column(_sheetActions, Ui.RowGap);
            _sheet.gameObject.AddComponent<CanvasGroup>();
            _sheet.gameObject.SetActive(false);
        }

        /// <summary>A card pinned to the bottom (radius 22) that grows with its content.</summary>
        public static RectTransform BottomSheet(RectTransform parent)
        {
            RectTransform card = UiKit.Surface(parent, "Card", SurfaceKind.Raised, Ui.SheetRadius).rectTransform;
            card.anchorMin = new Vector2(0, 0);
            card.anchorMax = new Vector2(1, 0);
            card.pivot = new Vector2(0.5f, 0);
            card.sizeDelta = new Vector2(-2 * 8f, 300);
            card.anchoredPosition = new Vector2(0, 8f);
            UiKit.Column(card, 16, new RectOffset(20, 20, 24, 24 + 20), TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        /// <summary>Slides a bottom sheet up from below the screen.</summary>
        public static void Rise(RectTransform sheet, float restY = 8f)
        {
            if (Look.ReduceMotion)
            {
                sheet.anchoredPosition = new Vector2(0f, restY);
                return;
            }
            Tween.Run(sheet, 0.32f, t =>
            {
                if (sheet != null) sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(restY - 420f, restY, Tween.EaseOutCubic(t)));
            });
        }

        public void Show(UiScreen screen, bool remember = true)
        {
            if (Current == screen) return;
            if (Current != null)
            {
                if (remember) _history.Push(Current);
                Current.Hide();
            }
            Current = screen;
            screen.Show();
            Shown?.Invoke(screen);
            if (screen.AllowsBanner) _ads.ShowBanner();
            else _ads.HideBanner();
        }

        /// <summary>Replaces the whole stack (e.g. returning home).</summary>
        public void Reset(UiScreen screen)
        {
            _history.Clear();
            if (Current != null && Current != screen) Current.Hide();
            Current = null;
            Show(screen, remember: false);
        }

        public void Back()
        {
            if (_history.Count == 0) return;
            UiScreen previous = _history.Pop();
            Current?.Hide();
            Current = previous;
            previous.Show();
            Shown?.Invoke(previous);
            if (previous.AllowsBanner) _ads.ShowBanner();
            else _ads.HideBanner();
        }

        public void Toast(string text, float seconds = 2.4f)
        {
            _toastText.text = text;
            _toast.SetAsLastSibling();
            _toastGroup.alpha = 0f;
            float restY = _toast.anchoredPosition.y;
            Tween.Run(_toastGroup, seconds, t =>
            {
                float shown = seconds * t;
                float inK = Tween.EaseOutCubic(Mathf.Clamp01(shown / 0.22f));
                _toastGroup.alpha = t < 0.85f ? inK : 1f - (t - 0.85f) / 0.15f;
                _toast.anchoredPosition = new Vector2(0f, restY + (Look.ReduceMotion ? 0f : Mathf.Lerp(12f, 0f, inK)));
            }, () => _toast.anchoredPosition = new Vector2(0f, restY));
        }

        /// <summary>
        /// Confirmation sheet: the message, one confirming action (danger-coloured for destructive
        /// actions) and "Cancel".
        /// </summary>
        public void Confirm(string text, string confirmLabel, Action onConfirm, bool destructive = false)
        {
            _sheetText.text = text;
            UiKit.Clear(_sheetActions);
            UiKit.Primary(_sheetActions, confirmLabel, destructive ? Icons.Delete : Icons.Check, () =>
            {
                Dismiss();
                _onConfirm?.Invoke();
            }, kind: destructive ? ButtonKind.Danger : ButtonKind.Primary);
            UiKit.CenteredPill(_sheetActions, L.T("common.cancel"), Icons.Close, Dismiss);
            _onConfirm = onConfirm;
            _sheet.gameObject.SetActive(true);
            _sheet.SetAsLastSibling();
            var group = _sheet.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            Tween.Fade(group, 1f, 0.2f);
            Rise(_sheetCard);
        }

        private void Dismiss() => _sheet.gameObject.SetActive(false);
    }
}
