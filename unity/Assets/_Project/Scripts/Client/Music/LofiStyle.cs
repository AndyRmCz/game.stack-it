using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>
    /// Lo-fi in G major: a wobbly electric piano on seventh chords, a round bass, soft swung drums,
    /// a vibraphone melody and vinyl crackle under everything. Eight bars: Cmaj7 Bm7 Em7 Am7, then
    /// Cmaj7 D7 Gmaj7 Em7.
    /// </summary>
    public sealed class LofiStyle : MusicStyle
    {
        public const string StyleId = "music_lofi";

        private const int Keys = 0, Bass = 1, Kick = 2, Snare = 3, Hat = 4, Crackle = 5, Vibes = 6, Rim = 7;
        private const float Swing = 0.3f; // off-beat 16ths land a third of a step late

        private static readonly int[][] Chords =
        {
            new[] { -7, -3, 0, 4 }, new[] { -1, 2, 4, 7 }, new[] { -3, 0, 4, 7 }, new[] { -3, 0, 2, 5 }, // Cmaj7 Bm7 Em7 Am7
            new[] { -7, -3, 0, 4 }, new[] { -5, -1, 2, 5 }, new[] { -1, 0, 4, 7 }, new[] { -3, 0, 4, 7 }, // Cmaj7 D7 Gmaj7 Em7
        };
        private static readonly int[] Roots = { -7, -8, -3, 2, -7, -5, 0, -3 };

        private static readonly (int, int)[][] Melody =
        {
            new[] { (0, 4), (3, 2), (6, 0), (10, -3) },
            new[] { (2, 2), (6, 4), (10, 7) },
            new[] { (0, 9), (6, 7), (10, 4) },
            new[] { (4, 2), (7, 0), (12, 2) },
            new[] { (0, 4), (3, 7), (6, 9), (10, 7) },
            new[] { (2, 9), (6, 12), (12, 9) },
            new[] { (0, 7), (6, 4), (10, 2) },
            new[] { (4, 0), (12, -3) },
        };

        public override string Id => StyleId;
        public override int Bars => 8;

        public override Instrument[] Instruments { get; } =
        {
            new Instrument("lf_keys", true, () => Room(Kit.Keys(Dsp.G3, 2.8f, 7f))),
            new Instrument("lf_bass", true, RoundBass),
            new Instrument("lf_kick", false, () => Dull(Kit.Kick(110f, 45f, 25f, 10f, 0.1f, 1f, 0.35f, 21), 2500f)),
            new Instrument("lf_snare", false, () => Dull(Kit.Snare(200f, 310f, 30f, 18f, 2200f, 0.25f, 22), 6000f)),
            new Instrument("lf_hat", false, () => Dull(Kit.Hat(0.05f, 70f, 6000f, 23), 7000f)),
            new Instrument("lf_crackle", false, () => Kit.Crackle(4f, 24)),
            new Instrument("lf_vibes", true, () => Room(Kit.Vibes(Dsp.G4, 2f))),
            new Instrument("lf_rim", false, () => Dull(Kit.Rim(25), 6000f)),
        };

        public override float Level(MusicMood mood) => mood == MusicMood.Home ? 1.19f : 1.23f; // matched to Classic on a phone

        public override float Tempo(MusicMood mood, int blocks, float widthFraction) =>
            Curve(mood, blocks, widthFraction, home: 70f, start: 78f, tall: 6f, thin: 4f, max: 88f);

        public override MusicLayers Layers(MusicMood mood, int blocks, float widthFraction) => GenreLayers(mood, blocks, widthFraction);

        public override void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into)
        {
            int b = Mod(bar, Bars);
            bool home = mood == MusicMood.Home;
            float swing = step % 2 == 1 ? Swing : 0f;

            if (step == 0) into.Add(new Note(Crackle, 0, 0.1f));
            if (layers.Pad && step == 0) Chord(into, Keys, Chords[b], 0.13f, 0.18f);
            if (layers.Pad && !home && step == 10) Chord(into, Keys, Chords[b], 0.07f, 0.1f);

            if (layers.Arp && (home || layers.Kick)) Line(into, Vibes, Melody[b], step, home ? 0.12f : 0.1f, 0.2f);

            if (layers.Shaker) // a soft boom-bap from the first block
            {
                if (step == 0 || step == 10 || (layers.Kick && step == 7)) into.Add(new Note(Kick, 0, 0.45f, swing));
                if (step == 4 || step == 12) into.Add(new Note(Snare, 0, 0.3f));
                if (step % 2 == 0 || layers.Kick) into.Add(new Note(Hat, 0, step % 4 == 2 ? 0.1f : 0.06f, swing, 0.2f));
            }

            if (layers.Bass)
            {
                int root = Roots[b];
                if (step == 0) into.Add(new Note(Bass, root, 0.3f));
                if (step == 7) into.Add(new Note(Bass, root, 0.18f, swing));
                if (step == 10) into.Add(new Note(Bass, root + 7 <= 12 ? root + 7 : root - 5, 0.22f));
            }

            if (layers.Tick && step % 2 == 1) into.Add(new Note(Rim, 0, 0.08f, swing, -0.3f));
        }

        private static float[] RoundBass()
        {
            float[] data = Dsp.Render(1.1f, t => (Dsp.Sin(Dsp.G2, t) + 0.3f * Dsp.Sin(Dsp.G2 * 2f, t)) * Dsp.Hit(t, 0.008f, 3f));
            Dsp.Drive(data, 1.8f);
            return Dsp.Declick(Dsp.Normalize(data));
        }

        private static float[] Room(float[] x)
        {
            Dsp.Room(x, 0.3f);
            return Dsp.Normalize(x);
        }

        /// <summary>Dusty: everything a little darker, as if sampled from an old record.</summary>
        private static float[] Dull(float[] x, float hz)
        {
            Dsp.Lowpass(x, hz);
            return Dsp.Normalize(x);
        }
    }
}
