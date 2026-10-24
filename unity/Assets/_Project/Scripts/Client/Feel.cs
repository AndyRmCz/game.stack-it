using System;
using System.Collections.Generic;

namespace StackIt.Client
{
    /// <summary>Engine-free math behind the game's look and sound, kept here so it is unit tested.</summary>
    public static class Feel
    {
        private static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };
        private const int MaxSemitones = 24;

        /// <summary>
        /// Pitch of the perfect chime, in semitones above the base note: consecutive perfects climb
        /// the major pentatonic scale (no note ever clashes), up to two octaves.
        /// </summary>
        public static int PerfectSemitones(int combo)
        {
            if (combo <= 1) return 0;
            int degree = combo - 1;
            int semitones = 12 * (degree / Pentatonic.Length) + Pentatonic[degree % Pentatonic.Length];
            return Math.Min(semitones, MaxSemitones);
        }

        /// <summary>
        /// The fanfare a perfect streak earns: tier 1 at 5 in a row, 2 at 10, 3 at 20 and every 10
        /// after that; 0 for any other count.
        /// </summary>
        public static int StreakFanfare(int combo) =>
            combo == 5 ? 1 : combo == 10 ? 2 : combo >= 20 && combo % 10 == 0 ? 3 : 0;

        /// <summary>Which pair of <paramref name="stops"/> brackets <paramref name="x"/>, and how far along (0..1).</summary>
        public static void Segment(IReadOnlyList<float> stops, float x, out int index, out float t)
        {
            if (stops.Count < 2) throw new ArgumentException("need at least two stops", nameof(stops));
            if (x <= stops[0])
            {
                index = 0;
                t = 0f;
                return;
            }
            for (int i = 0; i < stops.Count - 1; i++)
            {
                if (x < stops[i + 1])
                {
                    index = i;
                    t = (x - stops[i]) / (stops[i + 1] - stops[i]);
                    return;
                }
            }
            index = stops.Count - 2;
            t = 1f;
        }
    }

    /// <summary>Flat arrays ready for UnityEngine.Mesh (xyz triplets, one normal per vertex).</summary>
    public sealed class MeshData
    {
        public float[] Positions;
        public float[] Normals;
        public int[] Triangles;

        public int VertexCount => Positions.Length / 3;
    }

    /// <summary>
    /// A box with its edges cut at 45° (flat-shaded): 6 faces, 12 edge strips, 8 corner triangles.
    /// The strips catch the light, which is what makes a block read as a solid object instead of a
    /// flat-colored cube. Built at the block's real size so bevels never stretch.
    /// </summary>
    public static class ChamferBox
    {
        public static MeshData Build(float hx, float hy, float hz, float bevel)
        {
            float[] h = { hx, hy, hz };
            float b = Math.Max(0.0005f, Math.Min(bevel, 0.45f * Math.Min(hx, Math.Min(hy, hz))));
            var builder = new Builder();

            // Faces: the four points of each corner that lie on that face.
            for (int axis = 0; axis < 3; axis++)
            for (int s = -1; s <= 1; s += 2)
            {
                var points = new List<float[]>();
                foreach (int[] corner in Corners)
                    if (corner[axis] == s) points.Add(Point(h, b, axis, corner));
                builder.Polygon(points, Unit(axis, s));
            }

            // Edge strips between two faces, running along the third axis.
            for (int a = 0; a < 3; a++)
            for (int c = a + 1; c < 3; c++)
            for (int sa = -1; sa <= 1; sa += 2)
            for (int sc = -1; sc <= 1; sc += 2)
            {
                var points = new List<float[]>();
                foreach (int[] corner in Corners)
                {
                    if (corner[a] != sa || corner[c] != sc) continue;
                    points.Add(Point(h, b, a, corner));
                    points.Add(Point(h, b, c, corner));
                }
                float[] n = Add(Unit(a, sa), Unit(c, sc));
                builder.Polygon(points, Normalize(n));
            }

            // Corner triangles.
            foreach (int[] corner in Corners)
            {
                var points = new List<float[]> { Point(h, b, 0, corner), Point(h, b, 1, corner), Point(h, b, 2, corner) };
                builder.Polygon(points, Normalize(new float[] { corner[0], corner[1], corner[2] }));
            }
            return builder.ToMeshData();
        }

        private static readonly int[][] Corners = BuildCorners();

        private static int[][] BuildCorners()
        {
            var corners = new List<int[]>();
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                corners.Add(new[] { x, y, z });
            return corners.ToArray();
        }

        /// <summary>The corner's point on the face perpendicular to <paramref name="axis"/>.</summary>
        private static float[] Point(float[] h, float b, int axis, int[] corner)
        {
            var p = new float[3];
            for (int i = 0; i < 3; i++) p[i] = corner[i] * (i == axis ? h[i] : h[i] - b);
            return p;
        }

        private static float[] Unit(int axis, int sign)
        {
            var v = new float[3];
            v[axis] = sign;
            return v;
        }

        private static float[] Add(float[] u, float[] v) => new[] { u[0] + v[0], u[1] + v[1], u[2] + v[2] };
        private static float[] Sub(float[] u, float[] v) => new[] { u[0] - v[0], u[1] - v[1], u[2] - v[2] };
        private static float Dot(float[] u, float[] v) => u[0] * v[0] + u[1] * v[1] + u[2] * v[2];

        private static float[] Cross(float[] u, float[] v) =>
            new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };

        private static float[] Normalize(float[] v)
        {
            float length = (float)Math.Sqrt(Dot(v, v));
            return new[] { v[0] / length, v[1] / length, v[2] / length };
        }

        private sealed class Builder
        {
            private readonly List<float> _positions = new List<float>();
            private readonly List<float> _normals = new List<float>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>Adds a convex polygon: sorts its points around the center, fans it, faces it along <paramref name="normal"/>.</summary>
            public void Polygon(List<float[]> points, float[] normal)
            {
                float[] center = new float[3];
                foreach (float[] p in points)
                    for (int i = 0; i < 3; i++) center[i] += p[i] / points.Count;
                float[] u = Normalize(Sub(points[0], center));
                float[] v = Cross(normal, u);
                points.Sort((p, q) =>
                {
                    float[] dp = Sub(p, center), dq = Sub(q, center);
                    return Math.Atan2(Dot(dp, v), Dot(dp, u)).CompareTo(Math.Atan2(Dot(dq, v), Dot(dq, u)));
                });
                // Unity treats a triangle as front-facing when (b - a) x (c - a) points along its normal.
                bool flip = Dot(Cross(Sub(points[1], points[0]), Sub(points[2], points[0])), normal) < 0f;

                int first = _positions.Count / 3;
                foreach (float[] p in points)
                {
                    _positions.AddRange(p);
                    _normals.AddRange(normal);
                }
                for (int i = 1; i < points.Count - 1; i++)
                {
                    _triangles.Add(first);
                    _triangles.Add(first + (flip ? i + 1 : i));
                    _triangles.Add(first + (flip ? i : i + 1));
                }
            }

            public MeshData ToMeshData() => new MeshData
            {
                Positions = _positions.ToArray(),
                Normals = _normals.ToArray(),
                Triangles = _triangles.ToArray(),
            };
        }
    }
}
