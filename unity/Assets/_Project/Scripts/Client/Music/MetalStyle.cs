using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>
    /// Metal in E minor: distorted power chords and palm-muted chugs in a galloping riff (plucked
    /// strings through a waveshaper and a speaker-cabinet filter), a growling bass guitar, a lead
    /// line, tight drums with a crash on each phrase and double kicks for tension. Home is the
    /// same chords on a clean guitar. Eight bars: E5 E5 C5 D5, then G5 D5 E5 B5.
    /// </summary>
    public sealed class MetalStyle : MusicStyle
    {
        public const string StyleId = "music_metal";

        private const int Power = 0, Chug = 1, Bass = 2, Lead = 3, Clean = 4, Kick = 5, Snare = 6, Crash = 7, Ride = 8;

        private static readonly int[] PowerChord = { 0, 7, 12 };
        private static readonly int[] MutedChord = { 0, 7 };

        // Roots above the guitars' G2 (E5 is the low E string); the bass plays them an octave lower.
        private static readonly int[] Roots = { -3, -3, -7, -5, 0, -5, -3, -8 };

        // Clean home chords around G3: Em Em C D, G D Em B(m).
        private static readonly int[][] CleanChords =
        {
            new[] { -3, 0, 4, 9 }, new[] { -3, 0, 4, 9 }, new[] { -7, -3, 0, 5 }, new[] { -5, -1, 2, 7 },
            new[] { -5, 0, 4, 7 }, new[] { -5, -1, 2, 7 }, new[] { -3, 0, 4, 9 }, new[] { -8, -1, 2, 4 },
        };

        // The gallop (one 8th, two 16ths): power chords on the beats 1 and 3, chugs in between.
        private static readonly int[] Gallop = { 0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15 };

        private static readonly (int, int)[][] Solo =
        {
            new[] { (0, 4), (8, 7) },
            new[] { (0, 9), (6, 7), (8, 4), (12, 2) },
            new[] { (0, 0), (8, 4) },
            new[] { (0, 2), (8, -1), (12, 2) },
            new[] { (0, 7), (8, 12) },
            new[] { (0, 9), (8, 7), (12, 2) },
            new[] { (0, 4), (4, 2), (8, 0), (12, -3) },
            new[] { (0, -1), (8, 4) },
        };

        public override string Id => StyleId;
        public override int Bars => 8;

        public override Instrument[] Instruments { get; } =
        {
            new Instrument("mt_power", true, () => Kit.Guitar(Dsp.G2, PowerChord, 1.1f, false, 9f, 51), PowerChord),
            new Instrument("mt_chug", true, () => Kit.Guitar(Dsp.G2, MutedChord, 0.22f, true, 9f, 52), MutedChord),
            new Instrument("mt_bass", true, () => Kit.Guitar(Dsp.G1, new[] { 0 }, 0.5f, true, 2.5f, 53)),
            new Instrument("mt_lead", true, () => Kit.Guitar(Dsp.G3, new[] { 0 }, 1.2f, false, 7f, 54)),
            new Instrument("mt_clean", true, () => Kit.PluckedString(Dsp.G3, 1.6f, 0.55f, 0.996f, 0.35f, 55)),
            new Instrument("mt_kick", false, () => Kit.Kick(180f, 55f, 45f, 18f, 0.6f, 2f, 0.2f, 56)),
            new Instrument("mt_snare", false, BigSnare),
            new Instrument("mt_crash", false, () => Kit.Crash(1.6f, 57)),
            new Instrument("mt_ride", false, () => Kit.Hat(0.18f, 18f, 5000f, 58)),
        };

        public override float Level(MusicMood mood) => mood == MusicMood.Home ? 2.34f : 0.63f; // matched to Classic on a phone

        public override float Tempo(MusicMood mood, int blocks, float widthFraction) =>
            Curve(mood, blocks, widthFraction, home: 80f, start: 124f, tall: 16f, thin: 10f, max: 150f);

        public override float EndBpm => 80f;

        public override MusicLayers Layers(MusicMood mood, int blocks, float widthFraction) => GenreLayers(mood, blocks, widthFraction);

        public override void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into)
        {
            int b = Mod(bar, Bars);
            int root = Roots[b];

            if (mood == MusicMood.Home)
            {
                int[] chord = CleanChords[b];
                if (step % 2 == 0) into.Add(new Note(Clean, chord[(step / 2) % chord.Length], step == 0 ? 0.2f : 0.15f, 0f, step % 4 == 0 ? -0.3f : 0.3f));
                return;
            }

            bool gallop = System.Array.IndexOf(Gallop, step) >= 0;
            if (layers.Pad && (step == 0 || step == 8)) into.Add(new Note(Power, root, 0.2f, 0f, -0.2f));
            else if (layers.Pad && gallop) into.Add(new Note(Chug, root, 0.16f, 0f, 0.2f));

            if (layers.Shaker)
            {
                if (step == 0 || step == 8 || (layers.Kick && gallop)) into.Add(new Note(Kick, 0, layers.Kick && step % 8 != 0 ? 0.32f : 0.45f));
                if (step == 4 || step == 12) into.Add(new Note(Snare, 0, 0.32f));
                if (step % 2 == 0) into.Add(new Note(Ride, 0, step % 4 == 0 ? 0.08f : 0.05f, 0f, 0.35f));
                if (step == 0 && b % 4 == 0) into.Add(new Note(Crash, 0, 0.16f, 0f, -0.35f));
            }

            if (layers.Bass && gallop) into.Add(new Note(Bass, root, step % 8 == 0 ? 0.24f : 0.18f));

            if (layers.Kick && layers.Arp) Line(into, Lead, Solo[b], step, 0.13f, 0.1f);

            if (layers.Tick)
            {
                if (!gallop) into.Add(new Note(Kick, 0, 0.26f)); // double kick fills every 16th
                if (step == 0) into.Add(new Note(Crash, 0, 0.1f, 0f, 0.35f));
            }
        }

        private static float[] BigSnare()
        {
            float[] snare = Kit.Snare(200f, 260f, 18f, 12f, 2000f, 0.35f, 59);
            Dsp.Room(snare, 0.5f);
            return Dsp.Declick(Dsp.Normalize(snare));
        }
    }
}
