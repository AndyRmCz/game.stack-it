using NUnit.Framework;
using StackIt.Client;
using StackIt.Core.Simulation;

namespace StackIt.Tests
{
    public class NearMissTests
    {
        private const int Span = SimConstants.BlockSize;
        private static readonly int Tolerance = SimConstants.PerfectToleranceFor(SimConstants.SpeedBase);

        [Test]
        public void AMissThatBarelyClearedTheTowerIsAlmostSaved()
        {
            NearMiss near = NearMiss.Classify(missed: true, perfect: false, offset: -(Span + 120), span: Span, tolerance: Tolerance);
            Assert.AreEqual(NearMissKind.AlmostSaved, near.Kind);
            Assert.AreEqual(0.4f, near.Millimetres, 1e-4f);
        }

        [Test]
        public void ATouchingMissShowsAtLeastATenthOfAMillimetre()
        {
            NearMiss near = NearMiss.Classify(true, false, Span, Span, Tolerance);
            Assert.AreEqual(NearMissKind.AlmostSaved, near.Kind);
            Assert.AreEqual(0.1f, near.Millimetres, 1e-4f);
        }

        [Test]
        public void AFarMissIsNothingSpecial()
        {
            Assert.AreEqual(NearMissKind.None, NearMiss.Classify(true, false, Span + NearMiss.SavedMargin + 1, Span, Tolerance).Kind);
        }

        [Test]
        public void ASliceJustOutsideThePerfectWindowIsAlmostPerfect()
        {
            NearMiss near = NearMiss.Classify(false, false, Tolerance + 300, Span, Tolerance);
            Assert.AreEqual(NearMissKind.AlmostPerfect, near.Kind);
            Assert.AreEqual(1f, near.Millimetres, 1e-4f);
            Assert.AreEqual(NearMissKind.None, NearMiss.Classify(false, false, Tolerance + NearMiss.PerfectMargin + 1, Span, Tolerance).Kind);
        }

        [Test]
        public void PerfectDropsAreNeverNearMisses()
        {
            Assert.AreEqual(NearMissKind.None, NearMiss.Classify(false, true, Tolerance, Span, Tolerance).Kind);
        }

        [TestCase(4, 0)]
        [TestCase(5, 1)]
        [TestCase(6, 0)]
        [TestCase(10, 2)]
        [TestCase(15, 0)]
        [TestCase(20, 3)]
        [TestCase(30, 3)]
        [TestCase(35, 0)]
        public void StreakFanfaresAtFiveTenTwentyThenEveryTen(int combo, int tier)
        {
            Assert.AreEqual(tier, Feel.StreakFanfare(combo));
        }
    }
}
