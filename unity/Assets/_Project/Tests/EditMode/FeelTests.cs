using System;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class ChamferBoxTests
    {
        private static float[] Sub(float[] p, int a, int b) =>
            new[] { p[a * 3] - p[b * 3], p[a * 3 + 1] - p[b * 3 + 1], p[a * 3 + 2] - p[b * 3 + 2] };

        private static float[] Cross(float[] u, float[] v) =>
            new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };

        private static float Dot(float[] u, float[] n, int i) => u[0] * n[i * 3] + u[1] * n[i * 3 + 1] + u[2] * n[i * 3 + 2];

        [Test]
        public void HasFlatFacesEdgesAndCorners()
        {
            MeshData mesh = ChamferBox.Build(1.5f, 0.25f, 1f, 0.05f);
            Assert.AreEqual(6 * 4 + 12 * 4 + 8 * 3, mesh.VertexCount);
            Assert.AreEqual((6 * 2 + 12 * 2 + 8) * 3, mesh.Triangles.Length);
        }

        [TestCase(1.5f, 0.25f, 1f, 0.05f)]
        [TestCase(0.02f, 0.25f, 3f, 0.05f)] // a sliver left after a bad drop
        [TestCase(1f, 1f, 1f, 5f)]          // bevel far too large: clamped
        public void EveryTriangleFacesOutwards(float hx, float hy, float hz, float bevel)
        {
            // Unity draws a triangle when (b - a) x (c - a) points along the face normal.
            MeshData mesh = ChamferBox.Build(hx, hy, hz, bevel);
            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t], b = mesh.Triangles[t + 1], c = mesh.Triangles[t + 2];
                float[] n = Cross(Sub(mesh.Positions, b, a), Sub(mesh.Positions, c, a));
                Assert.Greater(Dot(n, mesh.Normals, a), 0f, $"triangle {t / 3} is back-facing");
                float[] center = { (mesh.Positions[a * 3] + mesh.Positions[b * 3] + mesh.Positions[c * 3]) / 3f,
                    (mesh.Positions[a * 3 + 1] + mesh.Positions[b * 3 + 1] + mesh.Positions[c * 3 + 1]) / 3f,
                    (mesh.Positions[a * 3 + 2] + mesh.Positions[b * 3 + 2] + mesh.Positions[c * 3 + 2]) / 3f };
                Assert.Greater(Dot(center, mesh.Normals, a), 0f, $"triangle {t / 3} normal points inwards");
            }
        }

        [Test]
        public void EnclosesSlightlyLessThanTheBox()
        {
            // Signed tetrahedron volumes: a closed, outward-wound surface gives its positive volume.
            const float hx = 1.5f, hy = 0.25f, hz = 1f;
            MeshData mesh = ChamferBox.Build(hx, hy, hz, 0.05f);
            double volume = 0;
            float[] p = mesh.Positions;
            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t] * 3, b = mesh.Triangles[t + 1] * 3, c = mesh.Triangles[t + 2] * 3;
                volume += (p[a] * (p[b + 1] * p[c + 2] - p[b + 2] * p[c + 1])
                         - p[a + 1] * (p[b] * p[c + 2] - p[b + 2] * p[c])
                         + p[a + 2] * (p[b] * p[c + 1] - p[b + 1] * p[c])) / 6.0;
            }
            double box = 8.0 * hx * hy * hz;
            Assert.Less(volume, box);
            Assert.Greater(volume, box * 0.95);
        }

        [Test]
        public void StaysInsideTheBoxAndReachesEveryFace()
        {
            MeshData mesh = ChamferBox.Build(1.5f, 0.25f, 1f, 0.05f);
            float[] h = { 1.5f, 0.25f, 1f };
            for (int axis = 0; axis < 3; axis++)
            {
                float min = float.MaxValue, max = float.MinValue;
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    min = Math.Min(min, mesh.Positions[v * 3 + axis]);
                    max = Math.Max(max, mesh.Positions[v * 3 + axis]);
                }
                Assert.AreEqual(-h[axis], min, 1e-5);
                Assert.AreEqual(h[axis], max, 1e-5);
            }
        }

        [Test]
        public void NormalsAreUnitLength()
        {
            MeshData mesh = ChamferBox.Build(2f, 0.25f, 0.4f, 0.05f);
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                float x = mesh.Normals[v * 3], y = mesh.Normals[v * 3 + 1], z = mesh.Normals[v * 3 + 2];
                Assert.AreEqual(1f, Math.Sqrt(x * x + y * y + z * z), 1e-5);
            }
        }
    }

    public class FeelTests
    {
        [Test]
        public void PerfectChimeClimbsTheMajorPentatonicScale()
        {
            int[] expected = { 0, 2, 4, 7, 9, 12, 14, 16 };
            for (int combo = 1; combo <= expected.Length; combo++)
                Assert.AreEqual(expected[combo - 1], Feel.PerfectSemitones(combo), "combo " + combo);
        }

        [Test]
        public void PerfectChimeStopsClimbingAfterTwoOctaves()
        {
            int previous = -1;
            for (int combo = 0; combo < 60; combo++)
            {
                int s = Feel.PerfectSemitones(combo);
                Assert.That(new[] { 0, 2, 4, 7, 9 }, Does.Contain(s % 12));
                Assert.GreaterOrEqual(s, previous);
                Assert.LessOrEqual(s, 24);
                previous = s;
            }
        }

        [TestCase(0f, 0, 0f)]
        [TestCase(10f, 0, 0.5f)]
        [TestCase(20f, 1, 0f)]
        [TestCase(35f, 1, 0.5f)]
        [TestCase(500f, 2, 1f)]
        [TestCase(-3f, 0, 0f)]
        public void FindsTheSegmentBetweenStops(float x, int segment, float t)
        {
            float[] stops = { 0f, 20f, 50f, 80f };
            Feel.Segment(stops, x, out int i, out float local);
            Assert.AreEqual(segment, i);
            Assert.AreEqual(t, local, 1e-5);
        }
    }
}
