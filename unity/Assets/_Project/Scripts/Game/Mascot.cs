using StackIt.Client;
using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// "Blocky": a little bevelled block with eyes who stands at the foot of the tower, in the
    /// look's second accent. Built from code (no art files), with the blocks' own shader, so it
    /// matches every look. It breathes, hops on perfects (higher on streak fanfares), shivers
    /// while the block is thin, looks up as the tower grows and droops at game over. Reduce
    /// Motion keeps it still. Purely visual: the game never depends on it.
    /// </summary>
    public sealed class Mascot : MonoBehaviour
    {
        private const float Half = 0.3f;              // body half-size
        private const float Ground = -2.25f;          // the base block's foot (StackView: base 4 blocks tall)
        private static readonly Vector3 Spot = new Vector3(1.35f, 0f, -2.05f); // screen-right, in front of the base
        private static readonly Color EyeWhite = new Color(0.97f, 0.97f, 1f);
        private static readonly Color Pupil = new Color(0.1f, 0.1f, 0.14f);

        private Transform _root;
        private Transform _body;
        private Transform[] _pupils;
        private MaterialPropertyBlock _props;
        private float _hop;          // 0..1 progress of the current hop (1 = landed)
        private float _hopHeight;
        private float _nervous;      // 0..1
        private float _sad;          // 0..1, eased
        private bool _sadTarget;
        private float _lookUp;       // 0..1

        public bool ReduceMotion { get; set; }

        private void Awake()
        {
            _props = new MaterialPropertyBlock();
            _root = new GameObject("Mascot").transform;
            _root.SetParent(transform, false);
            _root.localPosition = Spot + Vector3.up * Ground;
            _root.localRotation = Quaternion.Euler(0f, 45f, 0f); // its front (-Z) faces the camera

            _body = Part("Body", _root, new Vector3(Half, Half, Half), new Vector3(0f, Half, 0f), 0.08f);
            _pupils = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.11f : 0.11f;
                Transform eye = Part("Eye", _body, new Vector3(0.065f, 0.08f, 0.02f), new Vector3(x, 0.06f, -Half - 0.005f), 0.02f);
                Paint(eye, EyeWhite, EyeWhite, EyeWhite);
                _pupils[i] = Part("Pupil", eye, new Vector3(0.035f, 0.045f, 0.012f), new Vector3(0f, -0.01f, -0.02f), 0.01f);
                Paint(_pupils[i], Pupil, Pupil, Pupil);
            }
            _hop = 1f;
        }

        /// <summary>The look's second accent, shaded like a block.</summary>
        public void SetLook(ThemePalette look)
        {
            Color top = Palette.ToColor(look.Color(T.Accent2));
            Paint(_body, top, top * HeightRamp.LeftShade, top * HeightRamp.RightShade);
        }

        /// <summary>Home or a new run: happy and at rest.</summary>
        public void ResetMood()
        {
            _sadTarget = false;
            _nervous = 0f;
            _lookUp = 0f;
        }

        /// <summary>After each drop: hop on a perfect (a big one on a streak fanfare); nervous when the block is thin.</summary>
        public void Placed(bool perfect, int combo, int height, float widthFraction)
        {
            _lookUp = Mathf.Clamp01(height / 25f);
            _nervous = widthFraction < MusicPlan.ThinBlock ? 1f : 0f;
            if (!perfect) return;
            int fanfare = Feel.StreakFanfare(combo);
            _hopHeight = fanfare > 0 ? 0.35f + 0.1f * fanfare : 0.15f;
            _hop = 0f;
        }

        /// <summary>The run really ended: it droops and looks down.</summary>
        public void GameOver()
        {
            _sadTarget = true;
            _nervous = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            _sad = Mathf.MoveTowards(_sad, _sadTarget ? 1f : 0f, dt * 2.5f);

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
            squash -= 0.15f * _sad; // droop
            _body.localPosition = new Vector3(shiver, Half * (1f + squash) + y, 0f);
            _body.localScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f);

            // Pupils: up as the tower grows, down when sad.
            float look = Mathf.Lerp(-0.01f, 0.02f, _lookUp) - 0.03f * _sad;
            foreach (Transform p in _pupils) p.localPosition = new Vector3(0f, look, -0.02f);
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
