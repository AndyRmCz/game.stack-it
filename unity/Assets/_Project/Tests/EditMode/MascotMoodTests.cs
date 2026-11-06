using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class MascotMoodTests
    {
        private static BlockyMoment Result(int score, int best, bool record = false, bool? won = null) =>
            BlockyMoments.ForResult(score, best, record, won);

        private static StringsCsv Table() =>
            StringsCsv.Parse(File.ReadAllText(Path.Combine(TestPaths.ResourcesDir(), "strings.csv")));

        [Test]
        public void AnOrdinaryRunIsJustHappy()
        {
            Assert.AreEqual(BlockyMoment.Ordinary, Result(20, 60));
            Assert.AreEqual(MascotMood.Happy, BlockyMoments.Mood(BlockyMoment.Ordinary));
        }

        [Test]
        public void ANewBestCheers()
        {
            Assert.AreEqual(BlockyMoment.NewBest, Result(61, 60, record: true));
            Assert.AreEqual(MascotMood.Cheer, BlockyMoments.Mood(BlockyMoment.NewBest));
        }

        [Test]
        public void ANewBestOutranksAWonDuel()
        {
            Assert.AreEqual(BlockyMoment.NewBest, Result(61, 60, record: true, won: true));
        }

        [Test]
        public void AWonDuelIsSmugAndALostOneDetermined()
        {
            Assert.AreEqual(BlockyMoment.DuelWon, Result(20, 60, won: true));
            Assert.AreEqual(MascotMood.Smug, BlockyMoments.Mood(BlockyMoment.DuelWon));
            Assert.AreEqual(BlockyMoment.DuelLost, Result(20, 60, won: false));
            Assert.AreEqual(MascotMood.Rally, BlockyMoments.Mood(BlockyMoment.DuelLost));
        }

        [Test]
        public void ALostDuelOutranksANewBest()
        {
            // The rally is the call to action: the rematch.
            Assert.AreEqual(BlockyMoment.DuelLost, Result(61, 60, record: true, won: false));
        }

        [Test]
        public void OnePointShortOfYourBestIsAgony()
        {
            Assert.AreEqual(BlockyMoment.NearBest, Result(59, 60));
            Assert.AreEqual(MascotMood.Agony, BlockyMoments.Mood(BlockyMoment.NearBest));
        }

        [Test]
        public void OutOnTheFirstBlocksIsAShock()
        {
            Assert.AreEqual(BlockyMoment.EarlyOut, Result(BlockyMoments.EarlyOutScore, 60));
            Assert.AreEqual(BlockyMoment.Ordinary, Result(BlockyMoments.EarlyOutScore + 1, 60));
            Assert.AreEqual(MascotMood.Shocked, BlockyMoments.Mood(BlockyMoment.EarlyOut));
        }

        [Test]
        public void ARunCloseToYourBestIsAJoy()
        {
            Assert.AreEqual(BlockyMoment.GoodRun, Result(45, 60));
            Assert.AreEqual(BlockyMoment.Ordinary, Result(44, 60));
            Assert.AreEqual(MascotMood.Joy, BlockyMoments.Mood(BlockyMoment.GoodRun));
        }

        [Test]
        public void ABeginnersSmallBestIsNoGoodRunYet()
        {
            Assert.AreEqual(BlockyMoment.Ordinary, Result(5, 8));
        }

        [Test]
        public void TheFirstVisitOfADayIsAJoy()
        {
            Assert.AreEqual(BlockyMoment.Greeting, BlockyMoments.ForHome(1));
            Assert.AreEqual(BlockyMoment.Greeting, BlockyMoments.ForHome(null), "the very first launch");
            Assert.AreEqual(MascotMood.Joy, BlockyMoments.Mood(BlockyMoment.Greeting));
        }

        [Test]
        public void ComingBackLaterTheSameDaySaysNothing()
        {
            Assert.IsNull(BlockyMoments.ForHome(0));
        }

        [Test]
        public void DaysAwaySulksThenMeltsIntoJoy()
        {
            Assert.AreEqual(BlockyMoment.Returned, BlockyMoments.ForHome(BlockyMoments.SulkAfterDays));
            Assert.AreEqual(MascotMood.Sulking, BlockyMoments.Mood(BlockyMoment.Returned));
            Assert.AreEqual(BlockyMoment.Melted, BlockyMoments.Followup(BlockyMoment.Returned));
            Assert.AreEqual(MascotMood.Joy, BlockyMoments.Mood(BlockyMoment.Melted));
            Assert.IsNull(BlockyMoments.Followup(BlockyMoment.Melted));
        }

        [Test]
        public void AnOrdinaryRunOnlySometimesGetsAComment()
        {
            int spoken = Enumerable.Range(0, 30).Count(roll => BlockyMoments.Speaks(BlockyMoment.Ordinary, roll));
            Assert.AreEqual(10, spoken);
        }

        [Test]
        public void EveryOtherMomentAlwaysGetsAComment()
        {
            foreach (BlockyMoment moment in BlockyMoments.All.Where(m => m != BlockyMoment.Ordinary))
                Assert.IsTrue(BlockyMoments.Speaks(moment, 1), moment.ToString());
        }

        [Test]
        public void PokesStartWithAGiggleAndWearThinFromThere()
        {
            var pokes = new BlockyPokes();
            Assert.AreEqual(BlockyMoment.Tickled, pokes.Poke(0f));
            Assert.AreEqual(BlockyMoment.Noticed, pokes.Poke(1f));
            Assert.AreEqual(BlockyMoment.Warned, pokes.Poke(2f));
            Assert.AreEqual(BlockyMoment.Offended, pokes.Poke(3f));
            Assert.AreEqual(BlockyMoment.Offended, pokes.Poke(4f));
            Assert.AreEqual(MascotMood.Joy, BlockyMoments.Mood(BlockyMoment.Tickled));
            Assert.AreEqual(MascotMood.Sulking, BlockyMoments.Mood(BlockyMoment.Offended));
        }

        [Test]
        public void APauseBetweenPokesIsForgiven()
        {
            var pokes = new BlockyPokes();
            pokes.Poke(0f);
            pokes.Poke(1f);
            Assert.AreEqual(BlockyMoment.Tickled, pokes.Poke(1f + BlockyPokes.ForgetSeconds + 0.1f));
        }

        [Test]
        public void LinesTakeTurns()
        {
            int lines = BlockyMoments.LineCount(BlockyMoment.NewBest);
            Assert.Greater(lines, 1);
            Assert.AreNotEqual(BlockyMoments.LineKey(BlockyMoment.NewBest, 0), BlockyMoments.LineKey(BlockyMoment.NewBest, 1));
            Assert.AreEqual(BlockyMoments.LineKey(BlockyMoment.NewBest, 0), BlockyMoments.LineKey(BlockyMoment.NewBest, lines));
        }

        [Test]
        public void EveryLineIsInTheStringTable()
        {
            StringsCsv table = Table();
            var keys = new List<string>();
            foreach (BlockyMoment moment in BlockyMoments.All)
                for (int i = 0; i < BlockyMoments.LineCount(moment); i++) keys.Add(BlockyMoments.LineKey(moment, i));
            Assert.That(keys.Where(k => table.Get("en", k) == null), Is.Empty);
            Assert.That(keys.Where(k => table.Get("es", k) == null), Is.Empty);
        }

        [Test]
        public void EachLineStaysShortEnoughForTheBubble()
        {
            StringsCsv table = Table();
            foreach (string key in table.Keys.Where(k => k.StartsWith("blocky.")))
            {
                Assert.LessOrEqual(table.Get("en", key).Length, BlockyMoments.LongestLine, key);
                Assert.LessOrEqual(table.Get("es", key).Length, BlockyMoments.LongestLine, key);
            }
        }
    }
}
