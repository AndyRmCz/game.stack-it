using System.Linq;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class StarDriftTests
    {
        private static float[] Heights(StarDrift drift) => drift.Stars.Select(s => s.Y).ToArray();

        [Test]
        public void StarsFillTheSkyAndStayOnIt()
        {
            var drift = new StarDrift(40, seed: 3);
            Assert.AreEqual(40, drift.Stars.Count);
            for (int i = 0; i < 600; i++) drift.Step(0.1f, reduceMotion: false); // a minute
            foreach (StarDrift.Star s in drift.Stars)
            {
                Assert.That(s.X, Is.InRange(0f, 1f));
                Assert.That(s.Y, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void StarsFallSlowlyOnTheirOwn()
        {
            var drift = new StarDrift(20, seed: 1);
            float[] before = Heights(drift);
            drift.Step(0.5f, reduceMotion: false);
            float[] after = Heights(drift);
            for (int i = 0; i < before.Length; i++) Assert.Less(after[i], before[i]);
        }

        [Test]
        public void ClimbingSlidesTheSkyDownFasterThanIdling()
        {
            var idle = new StarDrift(20, seed: 5);
            var climbing = new StarDrift(20, seed: 5);
            climbing.Climb(3);
            idle.Step(0.3f, reduceMotion: false);
            climbing.Step(0.3f, reduceMotion: false);
            Assert.Less(climbing.Stars[0].Y, idle.Stars[0].Y);
        }

        [Test]
        public void NearStarsFallFasterThanFarOnes()
        {
            // Bigger stars read as nearer: parallax sells the climb.
            var drift = new StarDrift(60, seed: 9);
            StarDrift.Star far = drift.Stars.OrderBy(s => s.Size).First();
            StarDrift.Star near = drift.Stars.OrderBy(s => s.Size).Last();
            Assert.Greater(StarDrift.Speed(near.Size), StarDrift.Speed(far.Size));
        }

        [Test]
        public void ReduceMotionKeepsTheSkyStill()
        {
            var drift = new StarDrift(20, seed: 2);
            drift.Climb(5);
            drift.Boost(1f);
            float[] before = Heights(drift);
            drift.Step(1f, reduceMotion: true);
            CollectionAssert.AreEqual(before, Heights(drift));
        }

        [Test]
        public void AFallenStarComesBackAtTheTop()
        {
            var drift = new StarDrift(1, seed: 4);
            for (int i = 0; i < 2000 && drift.Stars[0].Y > 0.05f; i++) drift.Step(0.1f, reduceMotion: false);
            drift.Step(1f, reduceMotion: false);
            drift.Climb(10);
            for (int i = 0; i < 20; i++) drift.Step(0.1f, reduceMotion: false);
            Assert.That(drift.Stars[0].Y, Is.InRange(0f, 1f));
        }
    }
}
