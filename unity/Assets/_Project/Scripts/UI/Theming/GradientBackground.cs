using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Full-bleed vertical gradient through three stops (top, middle, bottom), the middle at
    /// <see cref="Middle"/> from the top. Drawn by the RoundedRect shader with the colours in the
    /// fill channel, so it is interpolated per pixel like a CSS gradient.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GradientBackground : MaskableGraphic
    {
        private Color _top = Color.white, _middle = Color.white, _bottom = Color.white;
        private float _mid = 0.5f;

        public override Material defaultMaterial => RoundedRect.SharedMaterial;

        public float Middle => _mid;

        public void SetStops(Color top, Color middle, Color bottom, float middleFromTop)
        {
            _top = top;
            _middle = middle;
            _bottom = bottom;
            _mid = Mathf.Clamp01(middleFromTop);
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas c = canvas;
            if (c != null) c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3
                | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float yMid = Mathf.Lerp(r.yMax, r.yMin, _mid);
            Row(vh, r, r.yMax, _top);
            Row(vh, r, yMid, _middle);
            Row(vh, r, r.yMin, _bottom);
            // Rows of two vertices: top 0-1, middle 2-3, bottom 4-5.
            vh.AddTriangle(0, 1, 3);
            vh.AddTriangle(0, 3, 2);
            vh.AddTriangle(2, 3, 5);
            vh.AddTriangle(2, 5, 4);
        }

        private void Row(VertexHelper vh, Rect r, float y, Color fill)
        {
            foreach (float x in new[] { r.xMin, r.xMax })
            {
                var v = UIVertex.simpleVert;
                v.position = new Vector3(x, y);
                v.color = color;
                v.uv0 = new Vector4(0, 0, 1e5f, 1e5f); // deep inside a huge box: always filled
                v.uv1 = fill;
                v.uv2 = Color.clear;
                v.uv3 = Color.clear;
                v.tangent = Vector4.zero;
                vh.AddVert(v);
            }
        }
    }
}
