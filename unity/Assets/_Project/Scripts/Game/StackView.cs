using System.Collections.Generic;
using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Core.Simulation;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Renders the simulation: placed blocks, the moving block (interpolated between ticks), and
    /// sliced-off overhangs that fall with physics. Blocks are bevelled boxes with exact face
    /// colours from the look: the base in block.base, the tower climbing block.ramp (or a bought
    /// skin), the moving block always in block.moving so it reads at a glance. They squash a little
    /// when they land, flash on a perfect, and fade towards the horizon far below the top. On Home
    /// the moving block slides above the base (attract mode). Purely visual; it never feeds back
    /// into the sim.
    /// </summary>
    public sealed class StackView : MonoBehaviour
    {
        public const float UnitsToWorld = 1f / SimConstants.UnitsPerWorldUnit;
        public const float BlockHeight = 0.5f;
        private const float BaseDepth = 4f * BlockHeight; // the hero: four base blocks tall
        private const float Bevel = 0.05f;
        private const int VisibleBlocks = 70;
        private const float DebrisLifetime = 2.5f;
        private const float FadeFrom = 8f;   // blocks below the top
        private const float FadeTo = 36f;
        private const float FadeMax = 0.55f;
        private const float FlashGlow = 0.6f;

        private struct Faces
        {
            public Color Top, Left, Right;
        }

        private readonly List<Transform> _placed = new List<Transform>();
        private readonly List<Faces> _faces = new List<Faces>();
        private readonly List<GameObject> _debris = new List<GameObject>();
        private readonly Dictionary<(int, int, int), Mesh> _meshes = new Dictionary<(int, int, int), Mesh>();
        private MaterialPropertyBlock _props; // Unity forbids creating it in a field initializer
        private Transform _base;
        private Transform _moving;
        private Transform _shadow;
        private Block _baseBlock;
        private string _skin = Palette.DefaultBlockSkin;
        private ThemePalette _look;
        private Color _horizon = Color.white;
        private float _emission;
        private bool _attract;

        public Vector3 TopCenter { get; private set; }

        private void Awake() => _props = new MaterialPropertyBlock();

        /// <summary>The look and the equipped skin: recolours every block now.</summary>
        public void SetLook(ThemePalette look, string skin, Color horizon)
        {
            _look = look;
            _skin = string.IsNullOrEmpty(skin) ? Palette.DefaultBlockSkin : skin;
            _horizon = horizon;
            _emission = look.Number(T.BlockEmission);
            for (int i = 0; i < _placed.Count; i++) _faces[i] = FacesFor(i + 1);
            if (_shadow != null) PaintShadow();
            RefreshAtmosphere();
        }

        /// <summary>A drifting (bought) sky moved on: the far blocks fade towards its new horizon.</summary>
        public void SetHorizon(Color horizon)
        {
            _horizon = horizon;
            RefreshAtmosphere();
        }

        /// <summary>Home: the moving block slides back and forth above the base.</summary>
        public void SetAttract(bool on)
        {
            _attract = on;
            if (!on && _moving != null) _moving.gameObject.SetActive(false);
        }

        public void ResetStack(Block baseBlock)
        {
            foreach (Transform t in _placed) Destroy(t.gameObject);
            _placed.Clear();
            _faces.Clear();
            foreach (GameObject d in _debris) if (d != null) Destroy(d);
            _debris.Clear();
            if (_moving != null) Destroy(_moving.gameObject);
            foreach (Mesh mesh in _meshes.Values) Destroy(mesh);
            _meshes.Clear();

            _baseBlock = baseBlock;
            if (_base == null) _base = NewBlock("Base", withCollider: true).transform;
            Shape(_base, baseBlock.Width * UnitsToWorld, BaseDepth, baseBlock.Depth * UnitsToWorld);
            Position(_base, baseBlock, -BaseDepth / 2f - BlockHeight / 2f);
            if (_shadow == null) _shadow = GroundShadow();
            float footprint = Mathf.Max(baseBlock.Width, baseBlock.Depth) * UnitsToWorld * 2.1f;
            _shadow.localPosition = new Vector3(_base.localPosition.x, -BaseDepth - BlockHeight / 2f + 0.01f, _base.localPosition.z);
            _shadow.localScale = new Vector3(footprint, footprint, 1f);
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
            Paint(_moving.GetComponent<MeshRenderer>(), MovingFaces(), _emission);
            _moving.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_attract || _moving == null || _look == null) return;
            // Home: a calm back-and-forth above the base, like the first block of a run.
            float offset = Mathf.Sin(Time.unscaledTime * 1.3f) * SimConstants.Travel * 0.55f;
            Block b = _baseBlock;
            var shifted = new Block(b.X0 + (int)offset, b.X1 + (int)offset, b.Z0, b.Z1);
            Shape(_moving, shifted.Width * UnitsToWorld, BlockHeight, shifted.Depth * UnitsToWorld);
            Position(_moving, shifted, Y(1));
            Paint(_moving.GetComponent<MeshRenderer>(), MovingFaces(), _emission);
            _moving.gameObject.SetActive(true);
        }

        /// <summary>Adds the placed block and drops the sliced overhang. Returns the new block's center.</summary>
        public Vector3 Place(Step step, Block previousTop)
        {
            GameObject block = NewBlock("Block " + step.N, withCollider: true);
            Shape(block.transform, step.Block.Width * UnitsToWorld, BlockHeight, step.Block.Depth * UnitsToWorld);
            Position(block.transform, step.Block, Y(step.N));
            Faces faces = FacesFor(step.N);
            var renderer = block.GetComponent<MeshRenderer>();
            Paint(renderer, faces, _emission);
            _placed.Add(block.transform);
            _faces.Add(faces);

            Squash(block.transform, step.Perfect ? 0.14f : 0.09f);
            if (step.Perfect) Flash(renderer, faces);
            if (!step.Perfect) DropOverhang(step, previousTop, faces);
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
            SpawnDebris(shifted, Y(step.N), MovingFaces(), step.Axis, step.Offset);
            _moving.gameObject.SetActive(false);
        }

        public static float Y(int n) => (n - 1) * BlockHeight;

        /// <summary>The top-face colour of block <paramref name="n"/> (effects tint with it).</summary>
        public Color ColorFor(int n) => FacesFor(n).Top;

        private Faces FacesFor(int n)
        {
            if (_look == null) return new Faces { Top = Color.white, Left = Color.gray, Right = Color.gray };
            if (n <= 0 && Palette.FollowsLook(_skin)) return FromTokens(T.BlockBase);
            Color top = Palette.BlockTop(_skin, n, _look);
            return new Faces
            {
                Top = top,
                Left = top * HeightRamp.LeftShade,
                Right = top * HeightRamp.RightShade,
            };
        }

        private Faces MovingFaces() => _look == null ? FacesFor(1) : FromTokens(T.BlockMoving);

        private Faces FromTokens(string token)
        {
            Rgba[] c = _look.Colors(token);
            return new Faces { Top = Palette.ToColor(c[0]), Left = Palette.ToColor(c[1]), Right = Palette.ToColor(c[2]) };
        }

        private void DropOverhang(Step step, Block previousTop, Faces faces)
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
            SpawnDebris(piece, Y(step.N), faces, step.Axis, step.Offset);
        }

        private void SpawnDebris(Block block, float y, Faces faces, Axis axis, int offset)
        {
            GameObject piece = NewBlock("Debris", withCollider: true);
            Shape(piece.transform, block.Width * UnitsToWorld, BlockHeight, block.Depth * UnitsToWorld);
            Position(piece.transform, block, y);
            Paint(piece.GetComponent<MeshRenderer>(), faces, _emission);
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

        /// <summary>The perfect block glows and cools down.</summary>
        private void Flash(MeshRenderer renderer, Faces faces)
        {
            float rest = _emission;
            Tween.Run(renderer, 0.45f, k =>
            {
                if (renderer != null) Paint(renderer, faces, rest + FlashGlow * (1f - Tween.EaseOutCubic(k)));
            });
        }

        /// <summary>Blocks sinking far below the top fade towards the horizon (the background's bottom).</summary>
        private void RefreshAtmosphere()
        {
            if (_base == null || _look == null) return;
            int count = _placed.Count;
            for (int i = Mathf.Max(0, count - VisibleBlocks); i < count; i++)
            {
                float fade = FadeMax * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeFrom, FadeTo, count - 1 - i));
                Paint(_placed[i].GetComponent<MeshRenderer>(), Toward(_faces[i], fade), _emission * (1f - fade));
            }
            float baseFade = FadeMax * Mathf.InverseLerp(FadeFrom, FadeTo, count);
            Paint(_base.GetComponent<MeshRenderer>(), Toward(FacesFor(0), baseFade), _emission * (1f - baseFade));
        }

        private Faces Toward(Faces f, float fade) => fade <= 0f ? f : new Faces
        {
            Top = Color.Lerp(f.Top, _horizon, fade),
            Left = Color.Lerp(f.Left, _horizon, fade),
            Right = Color.Lerp(f.Right, _horizon, fade),
        };

        private void HideFarBelow()
        {
            int hideUntil = _placed.Count - VisibleBlocks;
            for (int i = 0; i < hideUntil; i++)
                if (_placed[i].gameObject.activeSelf) _placed[i].gameObject.SetActive(false);
        }

        /// <summary>A soft ellipse on the ground under the base (block.groundShadow).</summary>
        private Transform GroundShadow()
        {
            var go = new GameObject("GroundShadow");
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Dust;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shadow = go.transform;
            if (_look != null) PaintShadow();
            return go.transform;
        }

        private void PaintShadow()
        {
            var renderer = _shadow.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(_props);
            _props.SetColor(Materials.BaseColor, Palette.ToColor(_look.Color(T.BlockGroundShadow)));
            renderer.SetPropertyBlock(_props);
        }

        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "Shadow" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
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

        private void Paint(MeshRenderer renderer, Faces faces, float glow)
        {
            renderer.GetPropertyBlock(_props);
            _props.SetColor(Materials.Top, faces.Top);
            _props.SetColor(Materials.Left, faces.Left);
            _props.SetColor(Materials.Right, faces.Right);
            _props.SetFloat(Materials.Glow, glow);
            renderer.SetPropertyBlock(_props);
        }
    }
}
