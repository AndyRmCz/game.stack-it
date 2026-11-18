using StackIt.Client;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Blocky's face: one graphic that draws a <see cref="FacePose"/>. It floats in the glass body,
    /// on the 2D Blocky's canvas or on a small world canvas on the 3D one. Blocky is ultraviolet in
    /// every look, so these colours belong to it and not to the theme.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlockyFace : MaskableGraphic, IFaceCanvas
    {
        private static readonly Color32[] Fills = BuildFills();

        private FacePose _pose;
        private VertexHelper _vh;
        private Vector2 _foot;
        private float _scale;

        public FacePose Pose => _pose;

        private static Color32[] BuildFills()
        {
            var fills = new Color32[System.Enum.GetValues(typeof(FacePart)).Length];
            fills[(int)FacePart.Shadow] = new Color32(0x08, 0x02, 0x24, 88);
            fills[(int)FacePart.Halo] = new Color32(0xB7, 0x9C, 0xFF, 90);
            fills[(int)FacePart.Blush] = new Color32(0xF0, 0x7A, 0xD8, 87);
            fills[(int)FacePart.Eye] = new Color32(0xF6, 0xF2, 0xFF, 255);
            fills[(int)FacePart.EyeShade] = new Color32(0x17, 0x09, 0x3F, 51);
            fills[(int)FacePart.Pupil] = new Color32(0x17, 0x09, 0x3F, 255);
            fills[(int)FacePart.Glint] = new Color32(0xFF, 0xFF, 0xFF, 255);
            fills[(int)FacePart.Brow] = new Color32(0xDD, 0xD3, 0xFF, 255);
            fills[(int)FacePart.Mouth] = new Color32(0xE6, 0xDE, 0xFF, 255);
            fills[(int)FacePart.Inside] = new Color32(0x15, 0x08, 0x40, 255);
            fills[(int)FacePart.Tongue] = new Color32(0xE8, 0x6F, 0xA8, 255);
            fills[(int)FacePart.Teeth] = new Color32(0xF6, 0xF2, 0xFF, 255);
            return fills;
        }

        /// <summary>The glass art, for a body to lay under its face.</summary>
        public static RawImage AddGlass(RectTransform parent)
        {
            RectTransform rt = UiKit.Rect("Glass", parent);
            var glass = rt.gameObject.AddComponent<RawImage>();
            glass.texture = Resources.Load<Texture2D>(BlockyGlass.Resource);
            // Without it the RawImage draws a white square: say why instead of shipping one.
            if (glass.texture == null) Debug.LogError($"Blocky's glass ({BlockyGlass.Resource}) didn't load as a 2D texture: check its import settings.");
            glass.raycastTarget = false;
            IgnoreTheming.Mark(glass, "Blocky's glass, the same in every look");
            return glass;
        }

        public void SetPose(in FacePose pose)
        {
            if (pose.Equals(_pose)) return;
            _pose = pose;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            _vh = vh;
            _scale = r.width;
            _foot = new Vector2(r.center.x, r.yMin);
            FaceDrawing.Draw(_pose, this);
            _vh = null;
        }

        void IFaceCanvas.Quad(FacePart part, in FaceVertex a, in FaceVertex b, in FaceVertex c, in FaceVertex d)
        {
            int i = _vh.currentVertCount;
            Color32 fill = Fills[(int)part];
            Vertex(fill, a);
            Vertex(fill, b);
            Vertex(fill, c);
            Vertex(fill, d);
            _vh.AddTriangle(i, i + 1, i + 2);
            _vh.AddTriangle(i, i + 2, i + 3);
        }

        private void Vertex(Color32 fill, in FaceVertex v)
        {
            fill.a = (byte)(fill.a * v.Alpha);
            _vh.AddVert(_foot + new Vector2(v.X, v.Y) * _scale, fill, Vector2.zero);
        }
    }
}
