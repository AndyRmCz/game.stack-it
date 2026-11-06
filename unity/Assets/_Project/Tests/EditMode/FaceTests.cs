using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class FaceTests
    {
        private sealed class Counter : IFaceCanvas
        {
            public readonly Dictionary<FacePart, int> Quads = new Dictionary<FacePart, int>();
            public readonly List<(FacePart Part, FaceVertex Vertex)> Vertices = new List<(FacePart, FaceVertex)>();
            public float MinX = float.MaxValue, MaxX = float.MinValue, MinY = float.MaxValue, MaxY = float.MinValue;

            public void Quad(FacePart part, in FaceVertex a, in FaceVertex b, in FaceVertex c, in FaceVertex d)
            {
                Quads[part] = Count(part) + 1;
                foreach (FaceVertex v in new[] { a, b, c, d })
                {
                    Assert.IsFalse(float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Alpha), part.ToString());
                    Assert.That(v.Alpha, Is.InRange(0f, 1f), part.ToString());
                    MinX = Math.Min(MinX, v.X);
                    MaxX = Math.Max(MaxX, v.X);
                    MinY = Math.Min(MinY, v.Y);
                    MaxY = Math.Max(MaxY, v.Y);
                    Vertices.Add((part, v));
                }
            }

            public int Count(FacePart part) => Quads.TryGetValue(part, out int n) ? n : 0;

            public IEnumerable<FaceVertex> Of(FacePart part) => Vertices.Where(v => v.Part == part).Select(v => v.Vertex);

            /// <summary>The highest solid point of a part, between two x values.</summary>
            public float TopOf(FacePart part, float x0, float x1) =>
                Of(part).Where(v => v.Alpha > 0f && v.X >= x0 && v.X <= x1).Max(v => v.Y);
        }

        private static IEnumerable<MascotMood> Moods() => Enum.GetValues(typeof(MascotMood)).Cast<MascotMood>();

        private static Counter Draw(FacePose pose)
        {
            var counter = new Counter();
            FaceDrawing.Draw(pose, counter);
            return counter;
        }

        [Test]
        public void EveryMoodHasItsOwnFace()
        {
            List<FacePose> poses = Moods().Select(FacePoses.For).ToList();
            Assert.AreEqual(poses.Count, poses.Distinct().Count());
        }

        [Test]
        public void EveryFaceStaysOnTheBlocksFront()
        {
            foreach (MascotMood mood in Moods())
            {
                Counter face = Draw(FacePoses.For(mood));
                Assert.Greater(face.MinX, -0.5f, mood.ToString());
                Assert.Less(face.MaxX, 0.5f, mood.ToString());
                Assert.Greater(face.MinY, 0f, mood.ToString());
                Assert.Less(face.MaxY, FaceDrawing.Height, mood.ToString());
            }
        }

        [Test]
        public void BlendingLandsExactlyOnBothEnds()
        {
            FacePose a = FacePoses.For(MascotMood.Happy), b = FacePoses.For(MascotMood.Agony);
            Assert.AreEqual(a, FacePose.Lerp(a, b, 0f));
            Assert.AreEqual(b, FacePose.Lerp(a, b, 1f));
        }

        [Test]
        public void BlendingPassesThroughTheMiddle()
        {
            FacePose a = FacePoses.For(MascotMood.Happy), b = FacePoses.For(MascotMood.Agony);
            Assert.AreEqual((a.MouthCurve + b.MouthCurve) / 2f, FacePose.Lerp(a, b, 0.5f).MouthCurve, 1e-5f);
        }

        [Test]
        public void AnOpenEyeHasWhitesAndAPupil()
        {
            Counter face = Draw(FacePoses.For(MascotMood.Happy));
            Assert.Greater(face.Count(FacePart.Eye), 0);
            Assert.Greater(face.Count(FacePart.Pupil), 0);
        }

        [Test]
        public void AShutEyeIsAStrokeWithNoWhites()
        {
            FacePose pose = FacePoses.For(MascotMood.Happy);
            pose.EyeOpen = 0f;
            Counter face = Draw(pose);
            Assert.AreEqual(0, face.Count(FacePart.Eye));
            Assert.Greater(face.Count(FacePart.Pupil), 0);
        }

        [Test]
        public void JoyShutsTheEyesIntoSmiles()
        {
            Assert.AreEqual(0, Draw(FacePoses.For(MascotMood.Joy)).Count(FacePart.Eye));
        }

        [Test]
        public void AHeavyLidLowersTheEye()
        {
            FacePose open = FacePoses.For(MascotMood.Happy);
            FacePose heavy = open;
            heavy.EyeOpen = 0.5f;
            Assert.Less(Draw(heavy).TopOf(FacePart.Eye, 0f, 0.4f), Draw(open).TopOf(FacePart.Eye, 0f, 0.4f));
        }

        [Test]
        public void ACrossLidSlopesDownToTheNose()
        {
            FacePose pose = FacePoses.For(MascotMood.Happy);
            pose.LidTilt = 0.6f;
            pose.EyeOpen = 0.6f;
            Counter face = Draw(pose);
            Assert.Less(face.TopOf(FacePart.Eye, 0.06f, 0.11f), face.TopOf(FacePart.Eye, 0.2f, 0.25f));
        }

        [Test]
        public void EdgesFadeOutSoTheyNeverLookCutOut()
        {
            foreach (FacePart part in new[] { FacePart.Eye, FacePart.Brow, FacePart.Mouth, FacePart.Shadow })
            {
                List<float> alphas = Draw(FacePoses.For(MascotMood.Happy)).Of(part).Select(v => v.Alpha).ToList();
                Assert.Contains(0f, alphas, part.ToString());
                Assert.Greater(alphas.Max(), 0.9f, part.ToString());
            }
        }

        [Test]
        public void RoundShapesFadeSidewaysToo()
        {
            // A fade that only runs up and down leaves a hard wall at each end: square cheeks.
            FacePose pose = FacePoses.For(MascotMood.Joy);
            pose.FaceShift = 0f;
            List<FaceVertex> blush = Draw(pose).Of(FacePart.Blush).Where(v => v.X > 0f).ToList();
            float solidEnd = blush.Where(v => v.Alpha > 0.99f).Max(v => v.X);
            float fadedEnd = blush.Where(v => v.Alpha == 0f).Max(v => v.X);
            Assert.Greater(fadedEnd, solidEnd + 0.02f);
        }

        [Test]
        public void TheFacesShadowFallsAwayFromTheLight()
        {
            Counter face = Draw(FacePoses.For(MascotMood.Happy));
            Assert.Greater(face.Of(FacePart.Shadow).Average(v => v.X), face.Of(FacePart.Halo).Average(v => v.X));
            Assert.Less(face.Of(FacePart.Shadow).Average(v => v.Y), face.Of(FacePart.Halo).Average(v => v.Y));
        }

        [Test]
        public void ASmugFaceIsLopsided()
        {
            FacePose pose = FacePoses.For(MascotMood.Smug);
            Counter face = Draw(pose);
            float middle = pose.FaceShift + pose.MouthShift;
            Assert.Greater(face.TopOf(FacePart.Brow, 0f, 0.5f), face.TopOf(FacePart.Brow, -0.5f, 0f) + 0.03f, "one brow goes up");
            Assert.Greater(face.TopOf(FacePart.Eye, 0f, 0.5f), face.TopOf(FacePart.Eye, -0.5f, 0f), "one eye opens more");
            Assert.Greater(face.TopOf(FacePart.Mouth, middle, 0.5f), face.TopOf(FacePart.Mouth, -0.5f, middle), "one corner rises");
        }

        [Test]
        public void NoMoodIsAMirrorImage()
        {
            foreach (MascotMood mood in Moods())
            {
                FacePose pose = FacePoses.For(mood);
                Assert.IsTrue(pose.BrowSkew != 0f || pose.MouthSkew != 0f, mood.ToString());
            }
        }

        [Test]
        public void JoyShowsTeethAndAClosedMouthDoesNot()
        {
            Assert.Greater(Draw(FacePoses.For(MascotMood.Joy)).Count(FacePart.Teeth), 0);
            Assert.AreEqual(0, Draw(FacePoses.For(MascotMood.Happy)).Count(FacePart.Teeth));
        }

        [Test]
        public void ALopsidedSmileDigsADimple()
        {
            FacePose lopsided = FacePoses.For(MascotMood.Happy);
            FacePose even = lopsided;
            even.MouthSkew = 0f;
            Assert.Greater(Draw(lopsided).Count(FacePart.Mouth), Draw(even).Count(FacePart.Mouth));
        }

        [Test]
        public void AnOpeningMouthGrowsOutOfItsLine()
        {
            FacePose pose = FacePoses.For(MascotMood.Happy);
            pose.MouthOpen = 0.015f;
            Counter opening = Draw(pose);
            Assert.Greater(opening.Count(FacePart.Inside), 0);
            Assert.Greater(opening.Count(FacePart.Mouth), 0, "the line fades out, it never pops");
            pose.MouthOpen = 0.1f;
            Assert.AreEqual(0, Draw(pose).Count(FacePart.Mouth));
        }

        [Test]
        public void EveryBlendBetweenMoodsCanBeDrawn()
        {
            foreach (MascotMood from in Moods())
            {
                foreach (MascotMood to in Moods())
                {
                    for (float t = 0.1f; t < 1f; t += 0.2f) Draw(FacePose.Lerp(FacePoses.For(from), FacePoses.For(to), t));
                }
            }
        }

        [Test]
        public void AFaceStaysCheapToRedraw()
        {
            foreach (MascotMood mood in Moods())
            {
                Assert.Less(Draw(FacePoses.For(mood)).Quads.Values.Sum(), 1600, mood.ToString());
            }
        }

        [Test]
        public void TheGlassArtsFrontLinesUpWithTheFace()
        {
            Assert.AreEqual(-0.5f, BlockyGlass.FrontU * BlockyGlass.Width - BlockyGlass.Width / 2f, 1e-5f);
            Assert.AreEqual(0.5f, (BlockyGlass.FrontU + BlockyGlass.FrontWidth) * BlockyGlass.Width - BlockyGlass.Width / 2f, 1e-5f);
            Assert.AreEqual(0f, BlockyGlass.FrontV * BlockyGlass.Height + BlockyGlass.Foot, 1e-5f);
            Assert.AreEqual(FaceDrawing.Height, (BlockyGlass.FrontV + BlockyGlass.FrontHeight) * BlockyGlass.Height + BlockyGlass.Foot, 1e-5f);
        }

        [Test]
        public void AClosedMouthHasNoTongue()
        {
            Assert.AreEqual(0, Draw(FacePoses.For(MascotMood.Happy)).Count(FacePart.Tongue));
            Assert.Greater(Draw(FacePoses.For(MascotMood.Cheer)).Count(FacePart.Tongue), 0);
        }

        [Test]
        public void OnlyTheWarmFacesBlush()
        {
            Assert.Greater(Draw(FacePoses.For(MascotMood.Joy)).Count(FacePart.Blush), 0);
            Assert.AreEqual(0, Draw(FacePoses.For(MascotMood.Rally)).Count(FacePart.Blush));
        }

        [Test]
        public void TheAnimatorEasesTowardTheMoodAndArrives()
        {
            var face = new FaceAnimator(1);
            face.Set(MascotMood.Happy, snap: true);
            face.Set(MascotMood.Agony);
            float start = face.Current.MouthCurve;
            face.Tick(0.016f);
            Assert.Less(face.Current.MouthCurve, start);
            Assert.Greater(face.Current.MouthCurve, FacePoses.For(MascotMood.Agony).MouthCurve);
            for (int i = 0; i < 120; i++) face.Tick(0.016f);
            Assert.AreEqual(FacePoses.For(MascotMood.Agony).MouthCurve, face.Current.MouthCurve, 1e-3f);
        }

        [Test]
        public void SnappingSkipsTheBlend()
        {
            var face = new FaceAnimator(1);
            face.Set(MascotMood.Agony, snap: true);
            Assert.AreEqual(FacePoses.For(MascotMood.Agony), face.Current);
        }

        [Test]
        public void IdleBlocksBlinkEveryFewSeconds()
        {
            var face = new FaceAnimator(7);
            face.Set(MascotMood.Happy, snap: true);
            int blinks = 0;
            bool shut = false;
            for (int i = 0; i < 60 * 30; i++)
            {
                bool now = face.Tick(1f / 60f).EyeOpen < 0.3f;
                if (now && !shut) blinks++;
                shut = now;
            }
            Assert.That(blinks, Is.InRange(5, 14), "30 seconds of idling");
        }

        [Test]
        public void IdleBlocksGlanceAround()
        {
            var face = new FaceAnimator(7);
            face.Set(MascotMood.Happy, snap: true);
            float rest = face.Current.LookX, furthest = 0f;
            for (int i = 0; i < 60 * 15; i++) furthest = Math.Max(furthest, Math.Abs(face.Tick(1f / 60f).LookX - rest));
            Assert.Greater(furthest, 0.15f);
        }
    }
}
