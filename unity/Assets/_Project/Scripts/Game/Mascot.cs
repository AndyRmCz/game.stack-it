using StackIt.Client;
using StackIt.UI;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// "Blocky": a little bevelled block with a face who stands at the foot of the tower, ultraviolet
    /// in every look. A block built from code, with the blocks' own shader, wearing the 2D Blocky's
    /// glass and face on a small world canvas on its front. It breathes, hops
    /// on perfects (higher on streak fanfares, beaming), shivers while the block is thin, looks up
    /// as the tower grows and takes the run's end to heart. Reduce Motion keeps it still. Purely
    /// visual: the game never depends on it.
    /// </summary>
    public sealed class Mascot : MonoBehaviour
    {
        private const float Half = 0.45f;             // body half-size
        private const float Ground = -2.25f;          // the base block's foot (StackView: base 4 blocks tall)
        private static readonly Vector3 Spot = new Vector3(1.35f, 0f, -2.05f); // screen-right, in front of the base
        private const float FaceUnits = 100f;         // the world canvas's width in its own units
        private const float FaceLift = 0.004f;        // off the front, so the block never hides it
        private const float JoySeconds = 1.4f, ReactionSeconds = 2.8f;
        private static readonly Color GlassTop = new Color32(0x6E, 0x4C, 0xF0, 255);
        private static readonly Color GlassSide = new Color32(0x2E, 0x17, 0x9C, 255);

        private Transform _root;
        private Transform _body;
        private BlockyFace _face;
        private FaceAnimator _expressions;
        private MascotMood _rest;    // what it goes back to
        private float _moodUntil;    // a passing mood ends here; 0: none
        private MaterialPropertyBlock _props;
        private float _hop;          // 0..1 progress of the current hop (1 = landed)
        private float _hopHeight;
        private float _nervous;      // 0..1
        private float _sad;          // 0..1, eased
        private bool _sadTarget;
        private float _lookUp;       // 0..1

        public bool ReduceMotion { get; set; }

        /// <summary>Films the world: lets the UI find Blocky on screen.</summary>
        public Camera ViewCamera { get; set; }

        /// <summary>The middle of the body, in world space.</summary>
        public Vector3 Centre => _body.position;

        private void Awake()
        {
            _props = new MaterialPropertyBlock();
            _root = new GameObject("Mascot").transform;
            _root.SetParent(transform, false);
            _root.localPosition = Spot + Vector3.up * Ground;
            _root.localRotation = Quaternion.Euler(0f, 45f, 0f); // its front (-Z) faces the camera

            _body = Part("Body", _root, new Vector3(Half, Half, Half), new Vector3(0f, Half, 0f), 0.08f);
            Paint(_body, GlassTop, GlassSide, GlassSide);
            _face = Face(_body);
            _expressions = new FaceAnimator(Random.Range(int.MinValue, int.MaxValue));
            _hop = 1f;
        }

        /// <summary>A world canvas lying on the body's front, which faces the camera.</summary>
        private static BlockyFace Face(Transform body)
        {
            var go = new GameObject("Face", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(body, false);
            rt.sizeDelta = new Vector2(FaceUnits, FaceUnits * FaceDrawing.Height);
            rt.localPosition = new Vector3(0f, 0f, -Half - FaceLift);
            rt.localScale = Vector3.one * (Half * 2f / FaceUnits);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.vertexColorAlwaysGammaSpace = true;

            RectTransform glass = BlockyFace.AddGlass(rt).rectTransform;
            glass.sizeDelta = new Vector2(FaceUnits, FaceUnits);
            glass.GetComponent<UnityEngine.UI.RawImage>().uvRect =
                new Rect(BlockyGlass.FrontU, BlockyGlass.FrontV, BlockyGlass.FrontWidth, BlockyGlass.FrontHeight);

            var graphic = new GameObject("Drawing", typeof(RectTransform));
            var drawing = (RectTransform)graphic.transform;
            drawing.SetParent(rt, false);
            drawing.anchorMin = Vector2.zero;
            drawing.anchorMax = Vector2.one;
            drawing.offsetMin = drawing.offsetMax = Vector2.zero;
            var face = graphic.AddComponent<BlockyFace>();
            face.raycastTarget = false;
            return face;
        }

        /// <summary>Home or a new run: happy and at rest.</summary>
        public void ResetMood()
        {
            _sadTarget = false;
            _nervous = 0f;
            _lookUp = 0f;
            _moodUntil = 0f;
            Rest(MascotMood.Happy);
        }

        /// <summary>After each drop: hop on a perfect (a big one on a streak fanfare); nervous when the block is thin.</summary>
        public void Placed(bool perfect, int combo, int height, float widthFraction)
        {
            _lookUp = Mathf.Clamp01(height / 25f);
            bool thin = widthFraction < MusicPlan.ThinBlock;
            _nervous = thin ? 1f : 0f;
            Rest(thin ? MascotMood.Nervous : MascotMood.Happy);
            if (!perfect) return;
            int fanfare = Feel.StreakFanfare(combo);
            Hop(fanfare > 0 ? 0.35f + 0.1f * fanfare : 0.15f);
            if (fanfare > 0) Show(MascotMood.Joy, JoySeconds);
        }

        /// <summary>The run really ended: its face says how it went; a bad end droops and looks down.</summary>
        public void GameOver(MascotMood mood)
        {
            bool glad = mood == MascotMood.Cheer || mood == MascotMood.Joy;
            _sadTarget = !glad && mood != MascotMood.Happy;
            _nervous = 0f;
            _moodUntil = 0f;
            Rest(mood);
            if (glad) Hop(0.35f);
        }

        /// <summary>A passing reaction (a poke, a greeting); joy comes with a hop.</summary>
        public void React(MascotMood mood)
        {
            Show(mood, ReactionSeconds);
            if (mood == MascotMood.Joy) Hop(0.25f);
        }

        private void Hop(float height)
        {
            _hopHeight = height;
            _hop = 0f;
        }

        private void Rest(MascotMood mood)
        {
            _rest = mood;
            if (_moodUntil <= 0f) _expressions.Set(mood, snap: ReduceMotion);
        }

        private void Show(MascotMood mood, float seconds)
        {
            _moodUntil = Time.unscaledTime + seconds;
            _expressions.Set(mood, snap: ReduceMotion);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            _sad = Mathf.MoveTowards(_sad, _sadTarget ? 1f : 0f, dt * 2.5f);
            if (_moodUntil > 0f && t >= _moodUntil)
            {
                _moodUntil = 0f;
                _expressions.Set(_rest, snap: ReduceMotion);
            }
            FacePose face = ReduceMotion ? _expressions.Target : _expressions.Tick(dt);

            float y = 0f, squash = 0f, shiver = 0f;
            if (!ReduceMotion)
            {
                squash = 0.03f * Mathf.Sin(t * 2.2f); // breathing
                if (_hop < 1f)
                {
                    _hop = Mathf.Min(1f, _hop + dt / 0.42f);
                    y = _hopHeight * 4f * _hop * (1f - _hop);           // a parabola
                    if (_hop > 0.85f) squash -= 0.12f * (1f - _hop) / 0.15f; // lands with a squash
                }
                if (_nervous > 0f) shiver = 0.012f * Mathf.Sin(t * 70f) * _nervous;
            }
            squash += face.Squash - 0.15f * _sad; // the mood's posture, and a droop
            _body.localPosition = new Vector3(shiver, Half * (1f + squash) + y, 0f);
            _body.localScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f);

            // Eyes: up as the tower grows, down when sad.
            face.LookY = Mathf.Clamp(face.LookY + 0.8f * _lookUp - 0.5f * _sad, -1f, 1f);
            _face.SetPose(face);
        }

        private static Transform Part(string name, Transform parent, Vector3 half, Vector3 position, float bevel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = StackView.BuildMesh(half.x, half.y, half.z, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Block;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void Paint(Transform part, Color top, Color left, Color right)
        {
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(_props);
            _props.SetColor(Materials.Top, top);
            _props.SetColor(Materials.Left, left);
            _props.SetColor(Materials.Right, right);
            _props.SetFloat(Materials.Glow, 0f);
            renderer.SetPropertyBlock(_props);
        }
    }
}
