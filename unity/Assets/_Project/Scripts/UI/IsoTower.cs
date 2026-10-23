using System.Collections.Generic;
using StackIt.Client.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// A small isometric tower drawn in 2D (top, left and right faces per block): the splash
    /// animation and the shop's live previews. Blocks stack bottom-up; the top one can slide back
    /// and forth like the moving block. Colours come from a block skin (skin previews) or from the
    /// look (<see cref="ThemedIsoTower"/>).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IsoTower : MaskableGraphic
    {
        private static readonly float Cos30 = Mathf.Cos(30f * Mathf.Deg2Rad);

        public struct BlockFaces
        {
            public Color Top, Left, Right;
        }

        private readonly List<BlockFaces> _blocks = new List<BlockFaces>();
        private readonly List<float> _drop = new List<float>(); // per-block vertical offset (animation), in block heights
        private BlockFaces _moving;
        private bool _hasMoving;
        private float _slide; // -1..1

        /// <summary>Block footprint relative to the rect width (1 = the block's diamond spans the rect).</summary>
        public float Footprint = 0.62f;

        /// <summary>Block height relative to the block's side.</summary>
        public float Thickness = 0.42f;

        public int Count => _blocks.Count;

        public void SetBlocks(IList<BlockFaces> blocks)
        {
            _blocks.Clear();
            _blocks.AddRange(blocks);
            while (_drop.Count < _blocks.Count) _drop.Add(0f);
            SetVerticesDirty();
        }

        public void SetMoving(BlockFaces moving, bool visible)
        {
            _moving = moving;
            _hasMoving = visible;
            SetVerticesDirty();
        }

        public void SetSlide(float slide)
        {
            _slide = Mathf.Clamp(slide, -1f, 1f);
            SetVerticesDirty();
        }

        public void SetDrop(int index, float blocksAbove)
        {
            if (index < 0 || index >= _drop.Count) return;
            _drop[index] = blocksAbove;
            SetVerticesDirty();
        }

        /// <summary>Faces from a top colour, shaded like the 3D blocks (left 0.85, right 0.70).</summary>
        public static BlockFaces FromTop(Color top)
        {
            return new BlockFaces
            {
                Top = top,
                Left = new Color(top.r * HeightRamp.LeftShade, top.g * HeightRamp.LeftShade, top.b * HeightRamp.LeftShade, top.a),
                Right = new Color(top.r * HeightRamp.RightShade, top.g * HeightRamp.RightShade, top.b * HeightRamp.RightShade, top.a),
            };
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            int total = _blocks.Count + (_hasMoving ? 1 : 0);
            if (total == 0) return;
            float side = r.width * Footprint / (2f * Cos30);       // block side length in UI units
            float height = side * Thickness;
            float towerHeight = total * height + side;             // tower plus the top diamond
            float scale = Mathf.Min(1f, r.height / towerHeight);
            side *= scale;
            height *= scale;
            var baseCentre = new Vector2(r.center.x, r.yMin + side * 0.5f * scale);
            for (int i = 0; i < _blocks.Count; i++)
                Block(vh, baseCentre + new Vector2(0f, (i + _drop[i]) * height), side, height, _blocks[i]);
            if (_hasMoving)
            {
                Vector2 slide = new Vector2(Cos30, 0.5f) * (_slide * side * 0.45f);
                Block(vh, baseCentre + new Vector2(0f, _blocks.Count * height) + slide, side, height, _moving);
            }
        }

        /// <summary>One cube-ish block whose bottom face is centred at <paramref name="c"/>.</summary>
        private void Block(VertexHelper vh, Vector2 c, float side, float height, BlockFaces faces)
        {
            // Isometric unit vectors: +x goes right and up, +z goes left and up, +y straight up.
            Vector2 ex = new Vector2(Cos30, 0.5f) * (side * 0.5f);
            Vector2 ez = new Vector2(-Cos30, 0.5f) * (side * 0.5f);
            Vector2 up = new Vector2(0f, height);
            Vector2 front = c - ex - ez;  // nearest bottom corner
            Vector2 right = c + ex - ez;
            Vector2 left = c - ex + ez;
            Vector2 back = c + ex + ez;
            Quad(vh, left, front, front + up, left + up, faces.Left);
            Quad(vh, front, right, right + up, front + up, faces.Right);
            Quad(vh, front + up, right + up, back + up, left + up, faces.Top);
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color fill)
        {
            int i = vh.currentVertCount;
            Color32 tint = fill * color;
            vh.AddVert(a, tint, Vector2.zero);
            vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero);
            vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
