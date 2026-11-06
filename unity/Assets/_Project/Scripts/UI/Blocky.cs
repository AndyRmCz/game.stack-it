using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Game;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Blocky in 2D, front on as in the store art: a block of ultraviolet glass, the same in every
    /// look, with a <see cref="BlockyFace"/> floating inside and the look's glow, if it has one,
    /// around it. It reacts with a <see cref="MascotMood"/>: the face blends to the
    /// mood's, then blinks and glances around, while the body moves in character. Cheer hops under
    /// the gold slab, Joy wiggles, Rally bounces, Sulking turns away. The perched one also talks
    /// and can be poked. Still with Reduce motion, and always on the challenge card (one rendered
    /// frame).
    /// </summary>
    public sealed class Blocky : Themed
    {
        private const float BubbleHeight = 0.42f; // of Blocky's full height: level with its eyes
        private static readonly Color GlassEdge = new Color32(0x1C, 0x0D, 0x63, 255);
        private static readonly Color GlassGlow = new Color32(0x7A, 0x58, 0xFF, 150);
        private static int _turn; // lines take turns across moments

        private float _w;
        private RectTransform _body;
        private RectTransform _hat;
        private RoundedRect _front, _hatTop, _hatFront;
        private BlockyFace _face;
        private FaceAnimator _expressions;
        private SpeechBubble _bubble;
        private readonly BlockyPokes _pokes = new BlockyPokes();
        private MascotMood _mood;
        private float _since;
        private bool _still;

        /// <summary>The current mood's face, before any blink or glance.</summary>
        public FacePose Expression => _expressions.Target;

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

        /// <summary>Stands on the top edge of a sheet, right of centre, looking at the score. This one talks.</summary>
        public static Blocky Perch(RectTransform sheet)
        {
            Blocky blocky = Create(sheet, 80f);
            var root = (RectTransform)blocky.transform;
            UiKit.IgnoreLayout(root);
            root.anchorMin = root.anchorMax = new Vector2(0.78f, 1f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, -6f); // feet just over the edge
            blocky._bubble = new SpeechBubble(root, BubbleHeight);
            blocky.ListenForPokes();
            return blocky;
        }

        private void Build(float w)
        {
            _w = w;
            _expressions = new FaceAnimator(Random.Range(int.MinValue, int.MaxValue));
            _body = UiKit.Rect("Body", transform);
            _body.anchorMin = _body.anchorMax = _body.pivot = new Vector2(0.5f, 0f); // squashes from the feet
            _body.sizeDelta = new Vector2(w, w * 1.12f);
            _front = Part(_body, "Front", 0f, 0f, w, w * FaceDrawing.Height, w * 0.1f); // under the glass: its glow and its tap area

            RawImage glass = BlockyFace.AddGlass(_body);
            RectTransform art = glass.rectTransform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0f);
            art.sizeDelta = new Vector2(w * BlockyGlass.Width, w * BlockyGlass.Height);
            art.anchoredPosition = new Vector2(0f, w * BlockyGlass.Foot);

            RectTransform face = UiKit.Rect("Face", _body);
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0f);
            face.sizeDelta = new Vector2(w, w * FaceDrawing.Height);
            _face = face.gameObject.AddComponent<BlockyFace>();
            _face.raycastTarget = false;
            ThemedPart.Mark(_face, this);

            // The gold slab lands a hair right of centre, covering the head's top face.
            _hat = UiKit.Rect("Hat", _body);
            _hat.anchorMin = _hat.anchorMax = _hat.pivot = new Vector2(0.5f, 0f);
            _hat.sizeDelta = new Vector2(w, w * 0.5f);
            _hat.anchoredPosition = new Vector2(w * 0.09f, w * 0.9f);
            _hatTop = Part(_hat, "HatTop", 0f, w * 0.17f, w, w * 0.3f, w * 0.05f);
            _hatFront = Part(_hat, "HatFront", 0f, 0f, w, w * 0.2f, w * 0.05f);
            SetMood(MascotMood.Happy);
        }

        private void ListenForPokes()
        {
            _front.raycastTarget = true;
            var tap = _front.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Poke);
        }

        private void Poke()
        {
            BlockyMoment moment = _pokes.Poke(Time.unscaledTime);
            SetMood(BlockyMoments.Mood(moment), entrance: false);
            if (!Look.ReduceMotion) Tween.Punch(transform, 0.12f, 0.3f);
            Say(moment);
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

        /// <param name="entrance">Pop in, as Blocky does when a screen brings it up.</param>
        public void SetMood(MascotMood mood, bool entrance = true)
        {
            _mood = mood;
            _since = Time.unscaledTime;
            _hat.gameObject.SetActive(mood == MascotMood.Cheer);
            bool calm = _still || Look.ReduceMotion;
            _expressions.Set(mood, snap: calm);
            if (calm) _face.SetPose(_expressions.Target);
            if (entrance && !calm && isActiveAndEnabled) Tween.PopIn(transform); // the root: Update poses the body
        }

        /// <summary>The gold slab whatever the mood (the challenge card).</summary>
        public void WearSlab() => _hat.gameObject.SetActive(true);

        /// <summary>The moment's mood and, on a Blocky that talks, one of its lines.</summary>
        /// <param name="args">For the line, such as the rival's name.</param>
        public void React(BlockyMoment moment, params object[] args)
        {
            SetMood(BlockyMoments.Mood(moment));
            Say(moment, args);
        }

        private void Say(BlockyMoment moment, params object[] args)
        {
            if (_bubble == null) return;
            if (BlockyMoments.Speaks(moment, Random.Range(0, 300))) _bubble.Say(L.T(BlockyMoments.LineKey(moment, _turn++), args));
            else _bubble.Hide();
        }

        public override void Apply(ThemePalette p)
        {
            if (_body == null) return; // AddComponent enables it before Build
            _front.Fill = GlassEdge;
            Glow glow = p.Glow(T.Accent2Glow); // only its size: the colour is the glass's own
            if (glow.IsNone) _front.SetShadow(Color.clear, 0f, 0f);
            else _front.SetShadow(GlassGlow, glow.Blur, glow.Y);
            _hatFront.Fill = Get(p, T.MedalGold);
            _hatTop.Fill = Get(p, T.ArcadeGoldTop);
        }

        private void Update()
        {
            if (_still || Look.ReduceMotion)
            {
                _face.SetPose(_expressions.Target);
                Move(0f, 0f, _expressions.Target.Squash, 0f, 0f);
                return;
            }
            FacePose face = _expressions.Tick(Time.unscaledDeltaTime);
            _face.SetPose(face);
            float t = Time.unscaledTime - _since;
            switch (_mood)
            {
                case MascotMood.Cheer:
                    float phase = t % 1.1f / 1.1f; // a hop, then a beat on the ground
                    float hop = phase < 0.45f ? phase / 0.45f : 1f;
                    float land = phase >= 0.45f && phase < 0.6f ? 1f - (phase - 0.45f) / 0.15f : 0f;
                    Move(0f, _w * 0.28f * 4f * hop * (1f - hop), face.Squash + 0.03f * Mathf.Sin(t * 2.2f) - 0.1f * land, 5f * Mathf.Sin(t * 6f), 0f);
                    break;
                case MascotMood.Joy:
                    float excitement = Mathf.Max(0.3f, 1f - t / 1.8f); // bursts out, then settles into a wiggle
                    Move(0f, _w * 0.14f * Mathf.Abs(Mathf.Sin(t * 7f)) * excitement, face.Squash + 0.02f * Mathf.Sin(t * 3f), 0f, 5f * Mathf.Sin(t * 7f) * excitement);
                    break;
                case MascotMood.Rally:
                    float bounce = Mathf.Max(0f, 1f - t / 0.8f); // two quick "again!" bounces, then ready
                    Move(0f, _w * 0.1f * Mathf.Abs(Mathf.Sin(t * 8f)) * bounce, face.Squash + 0.02f * Mathf.Sin(t * 3f), 0f, 0f);
                    break;
                case MascotMood.Smug:
                    Move(0f, 0f, face.Squash + 0.02f * Mathf.Sin(t * 2f), 0f, 4f + 1.5f * Mathf.Sin(t * 1.4f)); // leans back, pleased
                    break;
                case MascotMood.Nervous:
                    Move(_w * 0.012f * Mathf.Sin(t * 60f), 0f, face.Squash, 0f, 0f);
                    break;
                case MascotMood.Shocked:
                    Move(0f, t < 0.35f ? _w * 0.22f * Mathf.Sin(t / 0.35f * Mathf.PI) : 0f, face.Squash, 0f, 0f); // jumps once, then stays frozen
                    break;
                case MascotMood.Agony:
                    Move(0f, 0f, face.Squash, 0f, 2.5f * Mathf.Sin(t * 1.1f));
                    break;
                case MascotMood.Sulking:
                    Move(0f, 0f, face.Squash + 0.015f * Mathf.Sin(t * 1.5f), 0f, -5f); // turned away
                    break;
                default:
                    Move(0f, 0f, face.Squash + 0.03f * Mathf.Sin(t * 2.2f), 0f, 0f);
                    break;
            }
        }

        /// <param name="squash">Positive stretches up, negative squashes.</param>
        /// <param name="lean">Degrees, from the feet.</param>
        private void Move(float x, float y, float squash, float hatTilt, float lean)
        {
            _body.anchoredPosition = new Vector2(x, y);
            _body.localScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f);
            _body.localRotation = Quaternion.Euler(0f, 0f, lean);
            _hat.localRotation = Quaternion.Euler(0f, 0f, hatTilt);
        }
    }
}
