using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StackIt.Client;
using StackIt.Client.Music;

namespace StackIt.Tests
{
    public class MusicStyleTests
    {
        private static IEnumerable<string> Ids() => MusicStyles.Ids;

        // Where a run can be: Home, the start, the bass, the full drive, and a thin block up high.
        private static readonly MusicState[] States =
        {
            new MusicState(MusicMood.Home, 0, 1f),
            new MusicState(MusicMood.Run, 0, 1f),
            new MusicState(MusicMood.Run, 15, 0.8f),
            new MusicState(MusicMood.Run, 30, 0.6f),
            new MusicState(MusicMood.Run, 45, 0.2f),
        };

        private static readonly MusicState Busiest = new MusicState(MusicMood.Run, 45, 0.2f);

        private static readonly Dictionary<string, MusicRenderer> Renderers = new Dictionary<string, MusicRenderer>();

        private static MusicRenderer Renderer(string id)
        {
            if (!Renderers.TryGetValue(id, out MusicRenderer r)) Renderers[id] = r = new MusicRenderer(MusicStyles.Create(id));
            return r;
        }

        private static List<Note> Bar(MusicStyle style, MusicState s, int bar)
        {
            var notes = new List<Note>();
            MusicLayers layers = style.Layers(s.Mood, s.Blocks, s.Width);
            for (int step = 0; step < MusicStyle.StepsPerBar; step++) style.Notes(s.Mood, layers, bar, step, notes);
            return notes;
        }

        [Test]
        public void IdsAreUniqueAndEachCreatesItsStyle()
        {
            Assert.AreEqual(MusicStyles.Ids.Length, MusicStyles.Ids.Distinct().Count());
            foreach (string id in MusicStyles.Ids)
            {
                Assert.AreEqual(id, MusicStyles.Create(id).Id);
                Assert.LessOrEqual(id.Length, 32, "fits the backend's skin id column");
            }
        }

        [Test]
        public void UnknownOrMissingIdsPlayClassic()
        {
            Assert.AreEqual(ClassicStyle.StyleId, MusicStyles.Default);
            Assert.AreEqual(ClassicStyle.StyleId, MusicStyles.Create("music_polka").Id);
            Assert.AreEqual(ClassicStyle.StyleId, MusicStyles.Create(null).Id);
        }

        [TestCaseSource(nameof(Ids))]
        public void EveryNoteIsValidAndInTheChimesKey(string id)
        {
            // The perfect chime climbs G pentatonic; G major (= E minor) never clashes with it.
            MusicStyle style = MusicStyles.Create(id);
            foreach (MusicState s in States)
            for (int bar = 0; bar < style.Bars * 2; bar++)
            foreach (Note n in Bar(style, s, bar))
            {
                string where = $"{id} {s.Mood} {s.Blocks} bar {bar}";
                Assert.That(n.Instrument, Is.InRange(0, style.Instruments.Length - 1), where);
                Assert.That(n.Volume, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), where);
                Assert.That(n.Delay, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), where);
                Assert.That(n.Pan, Is.InRange(-1f, 1f), where);
                Instrument instrument = style.Instruments[n.Instrument];
                if (!instrument.Pitched)
                {
                    Assert.AreEqual(0, n.Semitones, $"{where}: {instrument.Name} is a drum");
                    continue;
                }
                // Repitching further than an octave changes a sound's character and length too much.
                Assert.That(n.Semitones, Is.InRange(-12, 12), $"{where}: {instrument.Name}");
                foreach (int interval in instrument.Intervals)
                    Assert.Contains(((n.Semitones + interval) % 12 + 12) % 12, MusicStyle.GMajor, $"{where}: {instrument.Name} {n.Semitones}");
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void TempoIsCalmAtHomeAndRisesWithTheTowerUpToItsCap(string id)
        {
            MusicStyle style = MusicStyles.Create(id);
            float start = style.Tempo(MusicMood.Run, 0, 1f);
            Assert.Less(style.Tempo(MusicMood.Home, 0, 1f), start);
            Assert.Greater(style.Tempo(MusicMood.Run, 30, 1f), start, "taller");
            Assert.Greater(style.Tempo(MusicMood.Run, 0, 0.3f), start, "narrower");
            float max = style.Tempo(MusicMood.Run, 1000, 0f);
            Assert.AreEqual(max, style.Tempo(MusicMood.Run, 2000, 0f), "capped");
            Assert.Less(style.EndBpm, start, "game over winds down");
        }

        [TestCaseSource(nameof(Ids))]
        public void TheMusicBuildsAsTheRunGoesOn(string id)
        {
            MusicStyle style = MusicStyles.Create(id);
            int Count(MusicState s) => Enumerable.Range(0, style.Bars).Sum(bar => Bar(style, s, bar).Count);
            int home = Count(States[0]);
            int start = Count(States[1]);
            int high = Count(States[3]);
            int thin = Count(States[4]);
            Assert.Less(home, high, "Home is calmer than a tall run");
            Assert.Less(start, high, "layers join as the tower grows");
            Assert.Greater(thin, high, "a thin block adds the tension layer");
        }

        [TestCaseSource(nameof(Ids))]
        public void InstrumentsRenderCleanFiniteSound(string id)
        {
            foreach (Instrument instrument in MusicStyles.Create(id).Instruments)
            {
                float[] clip = instrument.Render();
                Assert.Greater(clip.Length, 0, instrument.Name);
                Assert.IsTrue(clip.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), instrument.Name);
                float peak = clip.Max(Math.Abs);
                Assert.That(peak, Is.GreaterThan(0.05f).And.LessThanOrEqualTo(1.0001f), instrument.Name);
                // Classic's instruments stay exactly as they were (its bass stops at 5 % level).
                if (id != ClassicStyle.StyleId) Assert.Less(Math.Abs(clip[clip.Length - 1]), 0.01f, $"{instrument.Name} ends without a click");
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void PitchedInstrumentsAreTunedToG(string id)
        {
            // Every pitched clip is a G (some octave); within 15 cents it sounds in tune with the chime.
            foreach (Instrument instrument in MusicStyles.Create(id).Instruments.Where(i => i.Pitched))
            {
                float[] clip = instrument.Render();
                double cents = TuningCents(clip);
                Assert.That(cents, Is.InRange(-15.0, 15.0), $"{instrument.Name} is {cents:0} cents off G");
            }
        }

        /// <summary>
        /// How far the clip's period is from the nearest G's, in cents: autocorrelation over a slice
        /// after the attack, refined to a fraction of a sample around the best octave of G.
        /// </summary>
        private static double TuningCents(float[] clip)
        {
            int from = Math.Min(clip.Length / 4, Dsp.Samples(0.03f));
            int length = Math.Min(clip.Length - from - Dsp.SampleRate / 40, Dsp.Samples(0.2f));
            double Correlation(int lag)
            {
                double sum = 0, a = 0, b = 0;
                for (int i = from; i < from + length; i++)
                {
                    sum += clip[i] * clip[i + lag];
                    a += clip[i] * clip[i];
                    b += clip[i + lag] * clip[i + lag];
                }
                return sum / Math.Sqrt(a * b + 1e-12);
            }
            double bestPeriod = 0, bestScore = double.MinValue;
            foreach (float g in new[] { Dsp.G1 * 2f, Dsp.G2 * 2f, Dsp.G3 * 2f, Dsp.G4 * 2f, Dsp.G1, Dsp.G2, Dsp.G3, Dsp.G4 })
            {
                double expected = Dsp.SampleRate / (double)g;
                if (expected * 1.1 + 2 >= Dsp.SampleRate / 40) continue;
                int lo = (int)(expected * 0.95), hi = (int)Math.Ceiling(expected * 1.05);
                int best = lo;
                for (int lag = lo; lag <= hi; lag++) if (Correlation(lag) > Correlation(best)) best = lag;
                double score = Correlation(best);
                if (score <= bestScore + 0.02) continue;
                // A parabola through the peak and its neighbours finds the period between samples.
                double y0 = Correlation(best - 1), y1 = score, y2 = Correlation(best + 1);
                double shift = 0.5 * (y0 - y2) / (y0 - 2 * y1 + y2);
                bestScore = score;
                bestPeriod = best + (double.IsNaN(shift) ? 0 : shift);
            }
            double hz = Dsp.SampleRate / bestPeriod;
            double cents = 1200 * Math.Log(hz / Dsp.G4, 2);
            return cents - 1200 * Math.Round(cents / 1200);
        }

        [TestCaseSource(nameof(Ids))]
        public void TheBusiestMixLeavesHeadroomAndNeverCutsNotesAudibly(string id)
        {
            MusicRenderer renderer = Renderer(id);
            int bars = MusicStyles.Create(id).Bars;
            renderer.Render(bars, _ => Busiest, out RenderStats stats);
            // The sound effects play on top of the music, so it must stay well below full scale.
            Assert.LessOrEqual(stats.Peak, 0.95f, id);
            Assert.AreEqual(0, stats.LoudSteals, $"{id}: the 24-voice pool cut notes while still loud");
        }

        [Test]
        public void EveryGenreSoundsAboutAsLoudAsClassicOnAPhone()
        {
            // Switching soundtracks must not jump in volume: within 3 dB of the original, at Home
            // and mid-run, measured without the deep bass phone speakers can't play.
            foreach (MusicState s in new[] { States[0], States[3] })
            {
                float Loudness(string id)
                {
                    Renderer(id).Render(8, _ => s, out RenderStats stats);
                    return stats.PhoneRms;
                }
                float classic = Loudness(ClassicStyle.StyleId);
                foreach (string id in MusicStyles.Ids.Skip(1))
                {
                    double db = 20 * Math.Log10(Loudness(id) / classic);
                    Assert.That(db, Is.InRange(-3.0, 3.0), $"{id} ({s.Mood}) is {db:0.0} dB from Classic");
                }
            }
        }

        [Test]
        public void ClassicStillPlaysTheOriginalScore()
        {
            var style = new ClassicStyle();
            for (int bar = 0; bar < MusicPlan.Bars; bar++)
            for (int step = 0; step < MusicStyle.StepsPerBar; step++)
            {
                var notes = new List<Note>();
                MusicLayers all = MusicPlan.Layers(MusicMood.Run, 45, 0.2f);
                style.Notes(MusicMood.Run, all, bar, step, notes);
                var expected = new List<int>();
                if (step == 0) expected.AddRange(MusicPlan.Pad(bar));
                if (MusicPlan.Arp(MusicMood.Run, bar, step) is int arp) expected.Add(arp);
                if (MusicPlan.Bass(bar, step) is int bass) expected.Add(bass + 24);
                if (MusicPlan.Kick(step)) expected.Add(0);
                if (MusicPlan.Shaker(step)) expected.Add(0);
                expected.Add(0); // the tension tick, every step
                CollectionAssert.AreEqual(expected, notes.Select(n => n.Semitones), $"bar {bar} step {step}");
            }
            Assert.AreEqual(MusicPlan.StartBpm, style.Tempo(MusicMood.Run, 0, 1f));
            Assert.AreEqual(56f, style.EndBpm);
        }
    }
}
