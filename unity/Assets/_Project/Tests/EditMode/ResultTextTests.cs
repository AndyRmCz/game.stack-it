using System;
using System.Linq;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class ResultTextTests
    {
        private const BlockOutcome P = BlockOutcome.Perfect, C = BlockOutcome.Clean, S = BlockOutcome.Sliced;

        [Test]
        public void TheDailyNumberCountsUtcDaysFromTheBackendsEpoch()
        {
            Assert.AreEqual(1, DailyTower.Number(new DateTime(2026, 10, 24, 23, 59, 0, DateTimeKind.Utc)));
            Assert.AreEqual(142, DailyTower.Number(new DateTime(2027, 3, 14, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void ABlockIsCleanWhenUnderATenthIsSlicedOff()
        {
            Assert.AreEqual(P, ResultText.Classify(true, 30000, 30000));
            Assert.AreEqual(C, ResultText.Classify(false, 30000, 27001));
            Assert.AreEqual(S, ResultText.Classify(false, 30000, 27000));
        }

        [Test]
        public void ShowsTheDailyNumberScoreAndTenBlocksPerRow()
        {
            var blocks = Enumerable.Repeat(P, 10).Concat(new[] { C, S }).ToList();
            string text = ResultText.Build(142, 87, blocks);
            string[] lines = text.Split('\n');
            Assert.AreEqual("Stack IT · Daily #142 · 87", lines[0]);
            Assert.AreEqual(string.Concat(Enumerable.Repeat(ResultText.Perfect, 10)), lines[1]);
            Assert.AreEqual(ResultText.Clean + ResultText.Sliced, lines[2]);
        }

        [Test]
        public void ANormalRunHasNoDailyNumberAndLongTowersAreCut()
        {
            string text = ResultText.Build(0, 300, Enumerable.Repeat(S, 75).ToList());
            string[] lines = text.Split('\n');
            Assert.AreEqual("Stack IT · 300", lines[0]);
            Assert.AreEqual(1 + ResultText.MaxRows + 1, lines.Length);
            Assert.AreEqual("+15", lines.Last());
        }
    }
}
