using System;
using System.Collections.Generic;
using StackIt.Bootstrap;
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
        void PlayChallenge(string code);
        void OpenChallenge(string code);
        void ContinueWithAd();
        void DeclineContinue();
        void DoubleCoins();
        void ShareChallenge();
        void GoHome();
        void DeleteAccount();
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
        public GameController Game;
        public IGameFlow Flow;
        public UiRouter Router;
    }

    public abstract class UiScreen
    {
        protected readonly UiContext Ctx;
        public readonly RectTransform Root;
        protected readonly RectTransform Safe;
        private readonly CanvasGroup _group;

        /// <summary>Menu screens show the banner ad; gameplay screens never do.</summary>
        public virtual bool AllowsBanner => true;

        protected UiScreen(UiContext ctx, Transform parent, string name, Color background)
        {
            Ctx = ctx;
            Image bg = UiKit.Panel(parent, name, background);
            Root = UiKit.Stretch(bg.rectTransform);
            bg.raycastTarget = background.a > 0f;
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
            Tween.Fade(_group, 1f, 0.18f);
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

        protected RectTransform Header(string title, Action back)
        {
            RectTransform bar = UiKit.Rect("Header", Safe);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 170);
            bar.anchoredPosition = Vector2.zero;

            Button backButton = UiKit.Button(bar, "‹", ButtonStyle.Ghost, () => back(), 120);
            RectTransform b = (RectTransform)backButton.transform;
            b.anchorMin = b.anchorMax = new Vector2(0, 0.5f);
            b.pivot = new Vector2(0, 0.5f);
            b.sizeDelta = new Vector2(120, 120);
            b.anchoredPosition = new Vector2(Theme.Gutter, 0);

            TextMeshProUGUI label = UiKit.Label(bar, title, 64, Theme.Moon, TextAlignmentOptions.Center, true);
            UiKit.Stretch(label.rectTransform, 200, 0, 200, 0);
            return bar;
        }

        /// <summary>Content area under a header.</summary>
        protected RectTransform Body(float top = 190f)
        {
            return UiKit.Stretch(UiKit.Rect("Body", Safe), Theme.Gutter, top, Theme.Gutter, Theme.Gutter);
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

    /// <summary>One screen at a time, plus overlays (toast, confirm dialog).</summary>
    public sealed class UiRouter
    {
        private readonly Stack<UiScreen> _history = new Stack<UiScreen>();
        private readonly IAdsService _ads;
        private readonly TextMeshProUGUI _toast;
        private readonly CanvasGroup _toastGroup;
        private readonly RectTransform _dialog;
        private readonly TextMeshProUGUI _dialogText;
        private readonly Button _dialogConfirm;
        private Action _onConfirm;

        public UiScreen Current { get; private set; }

        public UiRouter(Transform overlayParent, IAdsService ads)
        {
            _ads = ads;
            Image toastBg = UiKit.Panel(overlayParent, "Toast", new Color(0.05f, 0.02f, 0.12f, 0.95f), rounded: true);
            RectTransform t = toastBg.rectTransform;
            t.anchorMin = new Vector2(0.06f, 0.82f);
            t.anchorMax = new Vector2(0.94f, 0.9f);
            t.offsetMin = t.offsetMax = Vector2.zero;
            toastBg.raycastTarget = false;
            _toast = UiKit.Label(t, "", 42, Theme.Moon);
            UiKit.Stretch(_toast.rectTransform, 36, 8, 36, 8);
            _toastGroup = t.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            Image scrim = UiKit.Panel(overlayParent, "Dialog", Theme.Scrim);
            _dialog = UiKit.Stretch(scrim.rectTransform);
            Image card = UiKit.Panel(_dialog, "Card", Theme.Card, rounded: true);
            RectTransform c = card.rectTransform;
            c.anchorMin = new Vector2(0.08f, 0.35f);
            c.anchorMax = new Vector2(0.92f, 0.65f);
            c.offsetMin = c.offsetMax = Vector2.zero;
            UiKit.Column(c, 36, new RectOffset(56, 56, 64, 56), TextAnchor.MiddleCenter);
            _dialogText = UiKit.Label(c, "", 46, Theme.Moon);
            _dialogConfirm = UiKit.Button(c, "", ButtonStyle.Primary, () =>
            {
                _dialog.gameObject.SetActive(false);
                _onConfirm?.Invoke();
            });
            UiKit.Button(c, "", ButtonStyle.Ghost, () => _dialog.gameObject.SetActive(false));
            _dialog.gameObject.SetActive(false);
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
            if (previous.AllowsBanner) _ads.ShowBanner();
            else _ads.HideBanner();
        }

        public void Toast(string text, float seconds = 2.4f)
        {
            _toast.text = text;
            _toastGroup.alpha = 1f;
            Tween.Run(_toastGroup, seconds, t => _toastGroup.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f);
        }

        public void Confirm(string text, string confirmLabel, Action onConfirm)
        {
            _dialogText.text = text;
            UiKit.SetText(_dialogConfirm, confirmLabel);
            _dialog.GetComponentsInChildren<Button>()[1].GetComponentInChildren<TextMeshProUGUI>().text = L.T("common.cancel");
            _onConfirm = onConfirm;
            _dialog.gameObject.SetActive(true);
            _dialog.SetAsLastSibling();
        }
    }
}
