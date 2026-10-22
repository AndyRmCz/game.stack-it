using System.Collections.Generic;
using StackIt.Core.Simulation;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Renders the simulation: placed blocks, the moving block (interpolated between ticks), and
    /// sliced-off overhangs that fall with physics. Purely visual; it never feeds back into the sim.
    /// </summary>
    public sealed class StackView : MonoBehaviour
    {
        public const float UnitsToWorld = 1f / SimConstants.UnitsPerWorldUnit;
        public const float BlockHeight = 0.5f;
        private const float BaseDepth = 6f;
        private const int VisibleBlocks = 70;
        private const float DebrisLifetime = 2.5f;

        private readonly List<Transform> _placed = new List<Transform>();
        private readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        private Transform _base;
        private Transform _moving;
        private string _skin = Palette.DefaultBlockSkin;

        public Vector3 TopCenter { get; private set; }

        public void SetSkin(string skin) => _skin = string.IsNullOrEmpty(skin) ? Palette.DefaultBlockSkin : skin;

        public void ResetStack(Block baseBlock)
        {
            foreach (Transform t in _placed) Destroy(t.gameObject);
            _placed.Clear();
            if (_moving != null) Destroy(_moving.gameObject);
            if (_base == null)
            {
                _base = NewCube("Base", withCollider: true).transform;
            }
            Fit(_base, baseBlock, -BaseDepth / 2f - BlockHeight / 2f, BaseDepth);
            Paint(_base, Palette.Block(_skin, 0) * 0.6f, 0.25f);
            _moving = NewCube("Moving", withCollider: false).transform;
            _moving.gameObject.SetActive(false);
            TopCenter = new Vector3(0f, -BlockHeight / 2f, 0f);
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
            Fit(_moving, shifted, Y(sim.Placed + 1), BlockHeight);
            Paint(_moving, Palette.Block(_skin, sim.Placed + 1), 1f);
            _moving.gameObject.SetActive(true);
        }

        /// <summary>Adds the placed block and drops the sliced overhang. Returns the new block's center.</summary>
        public Vector3 Place(Step step, Block previousTop)
        {
            GameObject cube = NewCube("Block " + step.N, withCollider: true);
            Fit(cube.transform, step.Block, Y(step.N), BlockHeight);
            Paint(cube.transform, Palette.Block(_skin, step.N), step.Perfect ? 1.4f : 1f);
            _placed.Add(cube.transform);

            if (!step.Perfect) DropOverhang(step, previousTop);
            HideFarBelow();
            TopCenter = cube.transform.position;
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
            GameObject piece = NewCube("Debris", withCollider: true);
            Fit(piece.transform, block, y, BlockHeight);
            Paint(piece.transform, color, 0.8f);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            float push = Mathf.Sign(offset) * 1.2f;
            body.linearVelocity = axis == Axis.X ? new Vector3(push, 0f, 0f) : new Vector3(0f, 0f, push);
            body.angularVelocity = Random.insideUnitSphere * 2f;
            Destroy(piece, DebrisLifetime);
        }

        private void HideFarBelow()
        {
            int hideUntil = _placed.Count - VisibleBlocks;
            for (int i = 0; i < hideUntil; i++)
                if (_placed[i].gameObject.activeSelf) _placed[i].gameObject.SetActive(false);
        }

        private GameObject NewCube(string name, bool withCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, false);
            if (!withCollider) Destroy(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Block;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return cube;
        }

        private static void Fit(Transform t, Block b, float centerY, float height)
        {
            t.localPosition = new Vector3((b.X0 + b.X1) * 0.5f * UnitsToWorld, centerY, (b.Z0 + b.Z1) * 0.5f * UnitsToWorld);
            t.localScale = new Vector3(b.Width * UnitsToWorld, height, b.Depth * UnitsToWorld);
        }

        private void Paint(Transform t, Color color, float glow)
        {
            var renderer = t.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(_props);
            _props.SetColor(Materials.BaseColor, color);
            _props.SetColor(Materials.EmissionColor, color * (Palette.Emission * glow * 0.35f));
            renderer.SetPropertyBlock(_props);
        }
    }
}
