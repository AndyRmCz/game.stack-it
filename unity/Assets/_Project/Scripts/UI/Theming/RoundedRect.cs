using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Every UI surface: a rounded rectangle (a circle when the radius is half its size) with an
    /// optional inner border and an outer shadow or glow, drawn by the StackIt/UI/RoundedRect SDF
    /// shader. All parameters go in vertex data, so surfaces share one material and batch.
    /// Colours are sRGB like everywhere else in the UI; <see cref="Graphic.color"/> tints the whole.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedRect : MaskableGraphic
    {
        private const AdditionalCanvasShaderChannels Channels = AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3
            | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

        private static Material _shared;

        private Color _fill = Color.white;
        private float _radius;
        private float _borderWidth;
        private Color _border = Color.clear;
        private Color _shadow = Color.clear;
        private float _shadowBlur;
        private float _shadowY;
        private float _spread;

        /// <summary>The one material every RoundedRect uses (the shader lives in Resources so builds include it).</summary>
        public static Material SharedMaterial
        {
            get
            {
                if (_shared != null) return _shared;
                Shader shader = Resources.Load<Shader>("StackIt/Shaders/UIRoundedRect");
                if (shader == null)
                {
                    Debug.LogWarning("Missing Resources/StackIt/Shaders/UIRoundedRect.shader; surfaces fall back to plain rectangles.");
                    shader = Shader.Find("UI/Default");
                }
                _shared = new Material(shader) { name = "UI RoundedRect" };
                return _shared;
            }
        }

        public override Material defaultMaterial => SharedMaterial;

        public Color Fill { get => _fill; set => Set(ref _fill, value); }
        public float Radius { get => _radius; set => Set(ref _radius, value); }
        public float BorderWidth { get => _borderWidth; set => Set(ref _borderWidth, value); }
        public Color Border { get => _border; set => Set(ref _border, value); }
        public Color Shadow { get => _shadow; set => Set(ref _shadow, value); }
        public float ShadowBlur { get => _shadowBlur; set => Set(ref _shadowBlur, value); }

        /// <summary>How far the shadow sits below the shape (0 for a glow).</summary>
        public float ShadowY { get => _shadowY; set => Set(ref _shadowY, value); }

        /// <summary>Grows the shadow beyond the shape (a radial glow from a zero-size shape uses blur only).</summary>
        public float Spread { get => _spread; set => Set(ref _spread, value); }

        /// <summary>Sets the shadow (or glow) in one go.</summary>
        public void SetShadow(Color color, float blur, float y)
        {
            _shadow = color;
            _shadowBlur = blur;
            _shadowY = y;
            SetVerticesDirty();
        }

        private void Set<TValue>(ref TValue field, TValue value)
        {
            if (Equals(field, value)) return;
            field = value;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas c = canvas;
            if (c != null) c.additionalShaderChannels |= Channels;
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            Canvas c = canvas;
            if (c != null) c.additionalShaderChannels |= Channels;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            bool shadow = _shadow.a > 0f;
            // Room around the shape for its shadow or glow, plus a little for anti-aliasing.
            float margin = 1f + (shadow ? _shadowBlur + _spread : 0f);
            float below = shadow ? Mathf.Max(0f, _shadowY) : 0f;
            float above = shadow ? Mathf.Max(0f, -_shadowY) : 0f;
            Vector2 centre = r.center;
            var half = new Vector4(0, 0, r.width * 0.5f, r.height * 0.5f);
            var parameters = new Vector4(_radius, _borderWidth, _shadowBlur, _shadowY);
            var spread = new Vector3(_spread, 0, 0);
            Color32 tint = color;

            float x0 = r.xMin - margin, x1 = r.xMax + margin;
            float y0 = r.yMin - margin - below, y1 = r.yMax + margin + above;
            Add(vh, x0, y0, centre, half, tint, parameters, spread);
            Add(vh, x0, y1, centre, half, tint, parameters, spread);
            Add(vh, x1, y1, centre, half, tint, parameters, spread);
            Add(vh, x1, y0, centre, half, tint, parameters, spread);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void Add(VertexHelper vh, float x, float y, Vector2 centre, Vector4 half, Color32 tint, Vector4 parameters, Vector3 spread)
        {
            var v = UIVertex.simpleVert;
            v.position = new Vector3(x, y);
            v.color = tint;
            v.uv0 = new Vector4(x - centre.x, y - centre.y, half.z, half.w);
            v.uv1 = _fill;
            v.uv2 = _border;
            v.uv3 = _shadow;
            v.tangent = parameters;
            v.normal = spread;
            vh.AddVert(v);
        }
    }
}
