using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Game;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// Blocky in 2D, front on as in the store art: an accent2 block with a lighter top face and
    /// big eyes, reacting with a <see cref="MascotMood"/>. Cheer hops under the gold slab, Rally
    /// squints and bounces, Happy bobs. Still with Reduce motion, and always on the challenge card
    /// (one rendered frame). Eyes keep the 3D mascot's fixed white and ink in every look.
    /// </summary>
    public sealed class Blocky : Themed
    {
        private static readonly Color EyeWhite = new Color(0.97f, 0.97f, 1f);
        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.14f);

        private float _w;
        private RectTransform _body;
        private RectTransform _hat;
        private RoundedRect _top, _front, _hatTop, _hatFront, _mouth;
        private readonly RoundedRect[] _eyes = new RoundedRect[2];
        private readonly RoundedRect[] _pupils = new RoundedRect[2];
        private MascotMood _mood;
        private float _since;
        private bool _still;

        /// <summary>
        /// A Blocky <paramref name="width"/> wide, laid out like any child of <paramref name="parent"/>.
        /// A <paramref name="still"/> one neither pops in nor moves.
        /// </summary>
        public static Blocky Create(Transform parent, float width, bool still = false)
        {
            RectTransform root = UiKit.Rect("Blocky", parent);
            float height = width * 1.45f; // body plus the hat
            UiKit.Size(root, width, height);
            root.sizeDelta = new Vector2(width, height);
            Blocky blocky = root.gameObject.AddComponent<Blocky>();
            blocky._still = still;
            blocky.Build(width);
            blocky.Refresh();
            return blocky;
        }

        /// <summary>Stands on the top edge of a sheet, right of centre, looking at the score.</summary>
        public static Blocky Perch(RectTransform sheet)
        {
            Blocky blocky = Create(sheet, 64f);
            var root = (RectTransform)blocky.transform;
            UiKit.IgnoreLayout(root);
            root.anchorMin = root.anchorMax = new Vector2(0.78f, 1f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, -6f); // feet just over the edge
            return blocky;
        }

        private void Build(float w)
        {
            _w = w;
            _body = UiKit.Rect("Body", transform);
            _body.anchorMin = _body.anchorMax = _body.pivot = new Vector2(0.5f, 0f); // squashes from the feet
            _body.sizeDelta = new Vector2(w, w * 1.12f);
            _top = Part(_body, "Top", 0f, w * 0.82f, w, w * 0.3f, w * 0.08f);
            _front = Part(_body, "Front", 0f, 0f, w, w * 0.9f, w * 0.08f);
            for (int i = 0; i < 2; i++)
            {
                _eyes[i] = Part(_body, "Eye", (i == 0 ? -0.18f : 0.18f) * w, w * 0.33f, w * 0.24f, w * 0.3f, w * 0.12f);
                _pupils[i] = Part(_eyes[i].rectTransform, "Pupil", 0f, 0f, w * 0.13f, w * 0.13f, w * 0.065f);
                _pupils[i].rectTransform.anchorMin = _pupils[i].rectTransform.anchorMax = _pupils[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            _mouth = Part(_body, "Mouth", 0f, w * 0.15f, w * 0.08f, w * 0.1f, w * 0.04f);
            // The gold slab lands a hair right of centre, covering the head's top face.
            _hat = UiKit.Rect("Hat", _body);
            _hat.anchorMin = _hat.anchorMax = _hat.pivot = new Vector2(0.5f, 0f);
            _hat.sizeDelta = new Vector2(w, w * 0.5f);
            _hat.anchoredPosition = new Vector2(w * 0.09f, w * 0.9f);
            _hatTop = Part(_hat, "HatTop", 0f, w * 0.17f, w, w * 0.3f, w * 0.05f);
            _hatFront = Part(_hat, "HatFront", 0f, 0f, w, w * 0.2f, w * 0.05f);
            SetMood(MascotMood.Happy);
        }

        private RoundedRect Part(RectTransform parent, string name, float x, float y, float width, float height, float radius)
        {
            RoundedRect shape = UiKit.Shape(parent, name, radius);
            RectTransform rt = shape.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
            ThemedPart.Mark(shape, this);
            return shape;
        }

        public void SetMood(MascotMood mood)
        {
            _mood = mood;
            _since = Time.unscaledTime;
            _hat.gameObject.SetActive(mood == MascotMood.Cheer);
            float eyeHeight = mood == MascotMood.Rally ? 0.18f : 0.3f; // a determined squint
            foreach (RoundedRect eye in _eyes) eye.rectTransform.sizeDelta = new Vector2(_w * 0.24f, _w * eyeHeight);
            _mouth.rectTransform.sizeDelta = mood == MascotMood.Cheer ? new Vector2(_w * 0.16f, _w * 0.13f)
                : mood == MascotMood.Rally ? new Vector2(_w * 0.16f, _w * 0.04f) : new Vector2(_w * 0.08f, _w * 0.1f);
            // Cheer looks up at the hat; the others look left and down, at the score.
            Vector2 look = mood == MascotMood.Cheer ? new Vector2(0f, 0.3f) : mood == MascotMood.Rally ? new Vector2(-0.35f, 0f) : new Vector2(-0.3f, -0.3f);
            foreach (RoundedRect pupil in _pupils) pupil.rectTransform.anchoredPosition = look * (_w * 0.06f);
            if (!_still && !Look.ReduceMotion && isActiveAndEnabled) Tween.PopIn(transform); // the root: Update poses the body
        }

        public override void Apply(ThemePalette p)
        {
            if (_body == null) return; // AddComponent enables it before Build
            Color body = Get(p, T.Accent2);
            _front.Fill = body;
            _top.Fill = Color.Lerp(body, Color.white, 0.35f);
            Glow glow = p.Glow(T.Accent2Glow);
            if (glow.IsNone) _front.SetShadow(Color.clear, 0f, 0f);
            else _front.SetShadow(Look.ToColor(glow.Color), glow.Blur, glow.Y);
            _hatFront.Fill = Get(p, T.MedalGold);
            _hatTop.Fill = Get(p, T.ArcadeGoldTop);
            foreach (RoundedRect eye in _eyes) eye.Fill = EyeWhite;
            foreach (RoundedRect pupil in _pupils) pupil.Fill = Ink;
            _mouth.Fill = Ink;
        }

        private void Update()
        {
            if (_still || Look.ReduceMotion)
            {
                Pose(0f, 0f, 0f);
                return;
            }
            float t = Time.unscaledTime - _since;
            switch (_mood)
            {
                case MascotMood.Cheer:
                    float phase = t % 1.1f / 1.1f; // a hop, then a beat on the ground
                    float hop = phase < 0.45f ? phase / 0.45f : 1f;
                    float land = phase >= 0.45f && phase < 0.6f ? 1f - (phase - 0.45f) / 0.15f : 0f;
                    Pose(_w * 0.28f * 4f * hop * (1f - hop), 0.03f * Mathf.Sin(t * 2.2f) - 0.1f * land, 5f * Mathf.Sin(t * 6f));
                    break;
                case MascotMood.Rally:
                    float bounce = Mathf.Max(0f, 1f - t / 0.8f); // two quick "again!" bounces, then ready
                    Pose(_w * 0.1f * Mathf.Abs(Mathf.Sin(t * 8f)) * bounce, 0.02f * Mathf.Sin(t * 3f), 0f);
                    break;
                default:
                    Pose(0f, 0.03f * Mathf.Sin(t * 2.2f), 0f);
                    break;
            }
        }

        /// <param name="squash">Positive stretches up, negative squashes.</param>
        private void Pose(float y, float squash, float hatTilt)
        {
            _body.anchoredPosition = new Vector2(0f, y);
            _body.localScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f);
            _hat.localRotation = Quaternion.Euler(0f, 0f, hatTilt);
        }
    }
}
