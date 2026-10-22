using System.Collections.Generic;
using StackIt.Client;
using StackIt.Core.Simulation;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Renders the simulation: placed blocks, the moving block (interpolated between ticks), and
    /// sliced-off overhangs that fall with physics. Blocks are bevelled boxes built at their real
    /// size; they squash a little when they land, flash softly on a perfect, and fade into the
    /// horizon as they sink below the top. Purely visual; it never feeds back into the sim.
    /// </summary>
    public sealed class StackView : MonoBehaviour
    {
        public const float UnitsToWorld = 1f / SimConstants.UnitsPerWorldUnit;
        public const float BlockHeight = 0.5f;
        private const float BaseDepth = 6f;
        private const float Bevel = 0.05f;
        private const int VisibleBlocks = 70;
        private const float DebrisLifetime = 2.5f;
        private const float FadeFrom = 6f;   // blocks below the top
        private const float FadeTo = 30f;
        private const float FadeMax = 0.8f;

        private readonly List<Transform> _placed = new List<Transform>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<GameObject> _debris = new List<GameObject>();
        private readonly Dictionary<(int, int, int), Mesh> _meshes = new Dictionary<(int, int, int), Mesh>();
        private MaterialPropertyBlock _props; // Unity forbids creating it in a field initializer
        private Transform _base;
        private Transform _moving;
        private string _skin = Palette.DefaultBlockSkin;
        private Color _horizon = new Color(1f, 0.86f, 0.8f);

        public Vector3 TopCenter { get; private set; }

        private void Awake() => _props = new MaterialPropertyBlock();

        public void SetSkin(string skin) => _skin = string.IsNullOrEmpty(skin) ? Palette.DefaultBlockSkin : skin;

        /// <summary>The sky's horizon color: blocks far below the top fade towards it.</summary>
        public void SetHorizon(Color horizon)
        {
            _horizon = horizon;
            RefreshAtmosphere();
        }

        public void ResetStack(Block baseBlock)
        {
            foreach (Transform t in _placed) Destroy(t.gameObject);
            _placed.Clear();
            _colors.Clear();
            foreach (GameObject d in _debris) if (d != null) Destroy(d);
            _debris.Clear();
            if (_moving != null) Destroy(_moving.gameObject);
            foreach (Mesh mesh in _meshes.Values) Destroy(mesh);
            _meshes.Clear();

            if (_base == null) _base = NewBlock("Base", withCollider: true).transform;
            Shape(_base, baseBlock.Width * UnitsToWorld, BaseDepth, baseBlock.Depth * UnitsToWorld);
            Position(_base, baseBlock, -BaseDepth / 2f - BlockHeight / 2f);
            _moving = NewBlock("Moving", withCollider: false).transform;
            _moving.gameObject.SetActive(false);
            TopCenter = new Vector3(0f, -BlockHeight / 2f, 0f);
            RefreshAtmosphere();
        }

        /// <summary>Positions the moving block. <paramref name="alpha"/> interpolates towards the next tick.</summary>
        public void ShowMoving(StackSim sim, float alpha)
        {
            if (sim.Ended)
            {
                _moving.gameObject.SetActive(false);
                return;
            }
            MovingBlock m = sim.Moving;
            float offset = Mathf.Clamp(m.Offset + m.Dir * m.Speed * alpha, -SimConstants.Travel, SimConstants.Travel);
            Block top = sim.Top;
            Block shifted = m.Axis == Axis.X
                ? new Block(top.X0 + (int)offset, top.X1 + (int)offset, top.Z0, top.Z1)
                : new Block(top.X0, top.X1, top.Z0 + (int)offset, top.Z1 + (int)offset);
            Shape(_moving, shifted.Width * UnitsToWorld, BlockHeight, shifted.Depth * UnitsToWorld);
            Position(_moving, shifted, Y(sim.Placed + 1));
            Paint(_moving.GetComponent<MeshRenderer>(), Palette.Block(_skin, sim.Placed + 1), 0f);
            _moving.gameObject.SetActive(true);
        }

        /// <summary>Adds the placed block and drops the sliced overhang. Returns the new block's center.</summary>
        public Vector3 Place(Step step, Block previousTop)
        {
            GameObject block = NewBlock("Block " + step.N, withCollider: true);
            Shape(block.transform, step.Block.Width * UnitsToWorld, BlockHeight, step.Block.Depth * UnitsToWorld);
            Position(block.transform, step.Block, Y(step.N));
            Color color = Palette.Block(_skin, step.N);
            var renderer = block.GetComponent<MeshRenderer>();
            Paint(renderer, color, 0f);
            _placed.Add(block.transform);
            _colors.Add(color);

            Squash(block.transform, step.Perfect ? 0.14f : 0.09f);
            if (step.Perfect) Flash(renderer, color);
            if (!step.Perfect) DropOverhang(step, previousTop);
            HideFarBelow();
            RefreshAtmosphere();
            TopCenter = block.transform.localPosition;
            return TopCenter;
        }

        /// <summary>The whole moving block falls: the run is over (or waiting for a continue).</summary>
        public void Miss(Step step, Block top)
        {
            Block shifted = step.Axis == Axis.X
                ? new Block(top.X0 + step.Offset, top.X1 + step.Offset, top.Z0, top.Z1)
                : new Block(top.X0, top.X1, top.Z0 + step.Offset, top.Z1 + step.Offset);
            SpawnDebris(shifted, Y(step.N), Palette.Block(_skin, step.N), step.Axis, step.Offset);
            _moving.gameObject.SetActive(false);
        }

        public static float Y(int n) => (n - 1) * BlockHeight;

        public Color ColorFor(int n) => Palette.Block(_skin, n);

        private void DropOverhang(Step step, Block previousTop)
        {
            int lo = previousTop.Lo(step.Axis) + step.Offset;
            int hi = previousTop.Hi(step.Axis) + step.Offset;
            int keptLo = step.Block.Lo(step.Axis);
            int keptHi = step.Block.Hi(step.Axis);
            // The overhang is whatever part of the moving block lies outside the kept span.
            int cutLo = step.Offset > 0 ? keptHi : lo;
            int cutHi = step.Offset > 0 ? hi : keptLo;
            if (cutHi <= cutLo) return;
            Block piece = step.Block.WithSpan(step.Axis, cutLo, cutHi);
            SpawnDebris(piece, Y(step.N), Palette.Block(_skin, step.N), step.Axis, step.Offset);
        }

        private void SpawnDebris(Block block, float y, Color color, Axis axis, int offset)
        {
            GameObject piece = NewBlock("Debris", withCollider: true);
            Shape(piece.transform, block.Width * UnitsToWorld, BlockHeight, block.Depth * UnitsToWorld);
            Position(piece.transform, block, y);
            Paint(piece.GetComponent<MeshRenderer>(), color, 0f);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            float push = Mathf.Sign(offset) * 1.2f;
            body.linearVelocity = axis == Axis.X ? new Vector3(push, 0f, 0f) : new Vector3(0f, 0f, push);
            body.angularVelocity = Random.insideUnitSphere * 1.5f;
            _debris.Add(piece);
            Destroy(piece, DebrisLifetime);
        }

        /// <summary>A quick squash-and-settle on landing, keeping the block's bottom in place.</summary>
        private static void Squash(Transform t, float amount)
        {
            float restY = t.localPosition.y;
            Tween.Run(t, 0.42f, k =>
            {
                if (t == null) return;
                // Squashed at k = 0, a small stretch, then settles: damped cosine.
                float sy = 1f - amount * (1f - k) * (1f - k) * Mathf.Cos(3f * Mathf.PI * k);
                float sxz = 1f + (1f - sy) * 0.5f;
                t.localScale = new Vector3(sxz, sy, sxz);
                Vector3 p = t.localPosition;
                t.localPosition = new Vector3(p.x, restY - (1f - sy) * BlockHeight * 0.5f, p.z);
            }, () =>
            {
                if (t == null) return;
                t.localScale = Vector3.one;
                Vector3 p = t.localPosition;
                t.localPosition = new Vector3(p.x, restY, p.z);
            });
        }

        /// <summary>The perfect block glows softly and cools down.</summary>
        private void Flash(MeshRenderer renderer, Color color)
        {
            Tween.Run(renderer, 0.45f, k =>
            {
                if (renderer != null) Paint(renderer, color, 0.55f * (1f - Tween.EaseOutCubic(k)));
            });
        }

        /// <summary>Recolors the blocks sinking below the top towards the horizon.</summary>
        private void RefreshAtmosphere()
        {
            if (_base == null) return;
            int count = _placed.Count;
            for (int i = Mathf.Max(0, count - VisibleBlocks); i < count - FadeFrom; i++)
            {
                float fade = FadeMax * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeFrom, FadeTo, count - 1 - i));
                Paint(_placed[i].GetComponent<MeshRenderer>(), Color.Lerp(_colors[i], _horizon, fade), 0f);
            }
            float baseFade = Mathf.Lerp(0.35f, FadeMax, Mathf.InverseLerp(0f, FadeTo, count));
            Paint(_base.GetComponent<MeshRenderer>(), Color.Lerp(Palette.Block(_skin, 0), _horizon, baseFade), 0f);
        }

        private void HideFarBelow()
        {
            int hideUntil = _placed.Count - VisibleBlocks;
            for (int i = 0; i < hideUntil; i++)
                if (_placed[i].gameObject.activeSelf) _placed[i].gameObject.SetActive(false);
        }

        private GameObject NewBlock(string name, bool withCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Block;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (withCollider) go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>Gives a block its bevelled mesh (shared between blocks of the same size) and collider.</summary>
        private void Shape(Transform t, float width, float height, float depth)
        {
            var key = (Mathf.RoundToInt(width * 1000f), Mathf.RoundToInt(height * 1000f), Mathf.RoundToInt(depth * 1000f));
            if (!_meshes.TryGetValue(key, out Mesh mesh))
            {
                mesh = BuildMesh(width / 2f, height / 2f, depth / 2f);
                _meshes[key] = mesh;
            }
            var filter = t.GetComponent<MeshFilter>();
            if (filter.sharedMesh != mesh) filter.sharedMesh = mesh;
            var box = t.GetComponent<BoxCollider>();
            if (box != null) box.size = new Vector3(width, height, depth);
        }

        private static Mesh BuildMesh(float hx, float hy, float hz)
        {
            MeshData data = ChamferBox.Build(hx, hy, hz, Bevel);
            var vertices = new Vector3[data.VertexCount];
            var normals = new Vector3[data.VertexCount];
            for (int i = 0; i < data.VertexCount; i++)
            {
                vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                normals[i] = new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
            }
            var mesh = new Mesh { name = "Block" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Position(Transform t, Block b, float centerY)
        {
            t.localPosition = new Vector3((b.X0 + b.X1) * 0.5f * UnitsToWorld, centerY, (b.Z0 + b.Z1) * 0.5f * UnitsToWorld);
            t.localScale = Vector3.one;
        }

        private void Paint(MeshRenderer renderer, Color color, float glow)
        {
            renderer.GetPropertyBlock(_props);
            _props.SetColor(Materials.BaseColor, color);
            _props.SetColor(Materials.EmissionColor, Color.Lerp(Color.black, color + Color.white * 0.4f, glow));
            renderer.SetPropertyBlock(_props);
        }
    }
}
