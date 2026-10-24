using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class ClipMomentsTests
    {
        [Test]
        public void WithoutHighlightsTheClipIsTheWholeWindow()
        {
            Assert.AreEqual(15.0, new ClipMoments().Seconds(100.0, 15.0));
        }

        [Test]
        public void TheClipStartsJustBeforeTheStrongestMoment()
        {
            var moments = new ClipMoments();
            moments.Mark(90.0, ClipMoments.StreakWeight);
            moments.Mark(92.0, ClipMoments.RivalWeight);
            moments.Mark(95.0, ClipMoments.AlmostPerfectWeight);
            Assert.AreEqual(100.0 - 92.0 + ClipMoments.LeadIn, moments.Seconds(100.0, 15.0), 1e-9);
        }

        [Test]
        public void AMomentRightAtTheEndStillKeepsSomeContext()
        {
            var moments = new ClipMoments();
            moments.Mark(99.5, ClipMoments.AlmostSavedWeight);
            Assert.AreEqual(ClipMoments.MinSeconds, moments.Seconds(100.0, 15.0));
        }

        [Test]
        public void MomentsOutsideTheRecordedWindowAreIgnored()
        {
            var moments = new ClipMoments();
            moments.Mark(80.0, ClipMoments.AlmostSavedWeight); // 20 s before the end: no longer buffered
            moments.Mark(93.0, ClipMoments.StreakWeight);
            Assert.AreEqual(100.0 - 93.0 + ClipMoments.LeadIn, moments.Seconds(100.0, 15.0), 1e-9);
        }

        [Test]
        public void ClearingForgetsTheLastRun()
        {
            var moments = new ClipMoments();
            moments.Mark(95.0, ClipMoments.RivalWeight);
            moments.Clear();
            Assert.AreEqual(15.0, moments.Seconds(100.0, 15.0));
        }
    }
}
