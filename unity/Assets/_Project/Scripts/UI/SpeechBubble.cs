using System;
using StackIt.Client.Theming;
using StackIt.Game;
using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// What Blocky says: a short line left of it, tail pointing back, that hugs its text and leaves
    /// on its own after a beat long enough to read. Never takes a tap.
    /// </summary>
    public sealed class SpeechBubble
    {
        private const float MaxWidth = 216f, PadX = 14f, PadY = 9f, TailSize = 13f;
        private const float BaseSeconds = 2.2f, SecondsPerCharacter = 0.05f;

        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly TextMeshProUGUI _text;

        /// <param name="owner">The bubble hangs off its left edge, at <paramref name="height"/> (0..1) of it.</param>
        public SpeechBubble(RectTransform owner, float height)
        {
            _root = UiKit.Rect("Bubble", owner);
            _root.anchorMin = _root.anchorMax = new Vector2(0f, height);
            _root.pivot = new Vector2(1f, 0.5f);
            _root.anchoredPosition = new Vector2(-TailSize * 0.5f, 0f);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            RectTransform tail = UiKit.Surface(_root, "Tail", SurfaceKind.Toast, 3f).rectTransform;
            tail.anchorMin = tail.anchorMax = tail.pivot = new Vector2(1f, 0.5f);
            tail.sizeDelta = new Vector2(TailSize, TailSize);
            tail.anchoredPosition = new Vector2(TailSize * 0.3f, 0f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);

            RectTransform face = UiKit.Stretch(UiKit.Surface(_root, "Face", SurfaceKind.Toast, 16f).rectTransform);
            _text = UiKit.Text(face, "", TextRole.Body, T.ToastText, size: 14f);
            UiKit.Stretch(_text.rectTransform, (int)PadX, (int)PadY, (int)PadX, (int)PadY);
            _root.gameObject.SetActive(false);
        }

        public void Say(string line, Action done = null)
        {
            Stop();
            _text.text = line;
            Vector2 fit = _text.GetPreferredValues(line, MaxWidth - 2f * PadX, 0f);
            _root.sizeDelta = new Vector2(Mathf.Min(fit.x + 1f, MaxWidth - 2f * PadX) + 2f * PadX, fit.y + 2f * PadY);
            _root.localScale = Vector3.one;
            _root.gameObject.SetActive(true);
            _group.alpha = 0f;
            Tween.Fade(_group, 1f, 0.16f);
            if (!Look.ReduceMotion) Tween.PopIn(_root, 0.26f);
            Tween.Run(_text, BaseSeconds + SecondsPerCharacter * line.Length, _ => { }, () => Leave(done));
        }

        public void Hide()
        {
            Stop();
            _root.gameObject.SetActive(false);
        }

        private void Leave(Action done)
        {
            if (_root == null) return;
            Tween.Fade(_group, 0f, 0.2f, () =>
            {
                if (_root == null) return;
                _root.gameObject.SetActive(false);
                done?.Invoke();
            });
        }

        private void Stop()
        {
            Tween.Kill(_text);
            Tween.Kill(_group);
            Tween.Kill(_root);
        }
    }
}
