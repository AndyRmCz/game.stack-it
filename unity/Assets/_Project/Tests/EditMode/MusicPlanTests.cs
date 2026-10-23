using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class MusicPlanTests
    {
        [Test]
        public void SpeedsUpAsTheTowerGrowsAndTheBlockShrinks()
        {
            float start = MusicPlan.Tempo(MusicMood.Run, 0, 1f);
            Assert.AreEqual(MusicPlan.StartBpm, start);
            Assert.Greater(MusicPlan.Tempo(MusicMood.Run, 30, 1f), start, "taller");
            Assert.Greater(MusicPlan.Tempo(MusicMood.Run, 0, 0.4f), start, "narrower");
            Assert.Greater(MusicPlan.Tempo(MusicMood.Run, 30, 0.4f), MusicPlan.Tempo(MusicMood.Run, 30, 1f));
        }

        [Test]
        public void NeverExceedsTheMaximumTempo()
        {
            Assert.AreEqual(MusicPlan.MaxBpm, MusicPlan.Tempo(MusicMood.Run, 500, 0f));
        }

        [Test]
        public void HomeIsCalmAndSparse()
        {
            Assert.Less(MusicPlan.Tempo(MusicMood.Home, 0, 1f), MusicPlan.StartBpm);
            MusicLayers home = MusicPlan.Layers(MusicMood.Home, 0, 1f);
            Assert.IsTrue(home.Pad);
            Assert.IsFalse(home.Kick || home.Bass || home.Shaker || home.Tick);
            int notes = 0;
            for (int step = 0; step < MusicPlan.StepsPerBar; step++)
                if (MusicPlan.Arp(MusicMood.Home, 0, step) != null) notes++;
            Assert.AreEqual(2, notes);
        }

        [Test]
        public void LayersJoinAsTheTowerGrowsAndTheTickMarksAThinBlock()
        {
            MusicLayers start = MusicPlan.Layers(MusicMood.Run, 0, 1f);
            Assert.IsTrue(start.Pad && start.Arp);
            Assert.IsFalse(start.Shaker || start.Bass || start.Kick || start.Tick);
            MusicLayers tall = MusicPlan.Layers(MusicMood.Run, 45, 1f);
            Assert.IsTrue(tall.Shaker && tall.Bass && tall.Kick);
            Assert.IsTrue(MusicPlan.Layers(MusicMood.Run, 5, MusicPlan.ThinBlock - 0.01f).Tick);
        }

        [Test]
        public void TheMelodyStaysInTheChimesKey()
        {
            // The perfect chime climbs G pentatonic; the arpeggio must never clash with it.
            for (int bar = 0; bar < MusicPlan.Bars * 2; bar++)
            for (int step = 0; step < MusicPlan.StepsPerBar; step++)
            {
                int? note = MusicPlan.Arp(MusicMood.Run, bar, step);
                if (note != null) Assert.IsTrue(MusicPlan.InScale(note.Value), $"bar {bar} step {step}: {note}");
            }
        }
    }
}
