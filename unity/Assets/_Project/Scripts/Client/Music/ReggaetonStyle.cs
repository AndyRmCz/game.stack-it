using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>
    /// Reggaeton in E minor: a four-on-the-floor 808 kick under an off-beat snare pattern (our own
    /// variation of the genre's dembow feel, no samples), a saturated 808 bass, a soft pad and a
    /// plucked hook. Eight bars: Em C G D, then Am C Em D.
    /// </summary>
    public sealed class ReggaetonStyle : MusicStyle
    {
        public const string StyleId = "music_reggaeton";

        private const int Pad = 0, Pluck = 1, Sub = 2, Kick = 3, Snare = 4, Hat = 5, OpenHat = 6, Riser = 7, Clap = 8;

        private static readonly int[][] Chords =
        {
            new[] { -3, 0, 4 }, new[] { -3, 0, 5 }, new[] { -5, 0, 4 }, new[] { -5, -1, 2 }, // Em C G D
            new[] { -3, 2, 5 }, new[] { -3, 0, 5 }, new[] { -3, 0, 4 }, new[] { -5, -1, 2 }, // Am C Em D
        };
        private static readonly int[] Roots = { -3, -7, 0, -5, 2, -7, -3, -5 };

        // Snare steps: the A section, and the B section's answer with a pickup into the next bar.
        private static readonly int[] SnareA = { 3, 6, 11, 14 };
        private static readonly int[] SnareB = { 3, 7, 11, 14, 15 };

        private static readonly (int, int)[][] Hook =
        {
            new[] { (0, 4), (3, 4), (6, 2), (8, 0), (11, 2) },
            new[] { (0, 0), (3, 0), (6, -3), (10, 0) },
            new[] { (0, 4), (3, 7), (6, 4), (8, 2), (11, 0) },
            new[] { (0, 2), (6, -1), (10, 2) },
            new[] { (0, 9), (3, 7), (6, 9), (10, 7) },
            new[] { (0, 4), (3, 7), (6, 4), (11, 2) },
            new[] { (0, 4), (3, 2), (6, 0), (8, -3) },
            new[] { (0, 2), (4, -1), (8, 2), (12, 4) },
        };

        public override string Id => StyleId;
        public override int Bars => 8;

        public override Instrument[] Instruments { get; } =
        {
            new Instrument("rg_pad", true, () => Kit.SuperSaw(Dsp.G3, 2, 9f, 2.2f, 1300f, 0.08f, 0.8f, 0.7f, 0.5f, 31)),
            new Instrument("rg_pluck", true, () => Kit.PluckedString(Dsp.G4, 0.8f, 0.85f, 0.995f, 0.3f, 32)),
            new Instrument("rg_sub", true, () => Kit.Sub(Dsp.G2, 0.7f, 3f)),
            new Instrument("rg_kick", false, () => Kit.Kick(120f, 50f, 22f, 7f, 0.25f, 2f, 0.5f, 33)),
            new Instrument("rg_snare", false, () => Kit.Snare(210f, 350f, 35f, 25f, 2600f, 0.18f, 34)),
            new Instrument("rg_hat", false, () => Kit.Hat(0.04f, 90f, 8000f, 35)),
            new Instrument("rg_openhat", false, () => Kit.Hat(0.25f, 12f, 7500f, 36)),
            new Instrument("rg_riser", false, () => Kit.Riser(2f, 37)),
            new Instrument("rg_clap", false, () => Kit.Clap(0.25f, 38)),
        };

        public override float Level(MusicMood mood) => mood == MusicMood.Home ? 1.24f : 0.89f; // matched to Classic on a phone

        public override float Tempo(MusicMood mood, int blocks, float widthFraction) =>
            Curve(mood, blocks, widthFraction, home: 84f, start: 92f, tall: 6f, thin: 3f, max: 100f);

        public override MusicLayers Layers(MusicMood mood, int blocks, float widthFraction) => GenreLayers(mood, blocks, widthFraction);

        public override void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into)
        {
            int b = Mod(bar, Bars);
            bool home = mood == MusicMood.Home;
            if (layers.Pad && step == 0) Chord(into, Pad, Chords[b], home ? 0.12f : 0.09f);
            if (layers.Arp && (!home || step % 4 == 0)) Line(into, Pluck, Hook[b], step, home ? 0.14f : 0.15f, -0.15f);

            if (layers.Shaker)
            {
                if (step % 4 == 0) into.Add(new Note(Kick, 0, 0.5f));
                int[] snares = b < 4 ? SnareA : SnareB;
                if (System.Array.IndexOf(snares, step) >= 0 && (step != 15 || b % 4 == 3)) into.Add(new Note(Snare, 0, step == 15 ? 0.14f : 0.26f));
                if (step % 2 == 0) into.Add(new Note(Hat, 0, step % 4 == 2 ? 0.1f : 0.06f, 0f, 0.25f));
            }

            if (layers.Bass && (step == 0 || step == 3 || step == 8 || step == 11)) into.Add(new Note(Sub, Roots[b], step % 8 == 0 ? 0.32f : 0.24f));

            if (layers.Kick)
            {
                if (step == 2 || step == 10) into.Add(new Note(OpenHat, 0, 0.08f, 0f, -0.25f));
                if (step == 6 || step == 14) into.Add(new Note(Clap, 0, 0.14f, 0.05f));
            }

            if (layers.Tick)
            {
                if (step == 0 && b % 2 == 0) into.Add(new Note(Riser, 0, 0.1f));
                if (step % 2 == 1) into.Add(new Note(Hat, 0, 0.05f, 0f, -0.25f));
            }
        }
    }
}
