using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>
    /// K-pop style instrumental in G major: punchy dance drums with claps, syncopated supersaw
    /// chord stabs, an octave-jumping synth bass, bell arpeggios and a bright saw hook, with a
    /// riser and a snare roll for tension. Eight bars: C D Em G, then Em C G D.
    /// </summary>
    public sealed class KpopStyle : MusicStyle
    {
        public const string StyleId = "music_kpop";

        private const int Stab = 0, Pad = 1, Lead = 2, Bass = 3, Bell = 4, Kick = 5, Clap = 6, Hat = 7, Riser = 8, Snare = 9;

        private static readonly int[][] Chords =
        {
            new[] { -3, 0, 5 }, new[] { -5, -1, 2 }, new[] { -3, 0, 4 }, new[] { -5, 0, 4 }, // C D Em G
            new[] { -3, 0, 4 }, new[] { -3, 0, 5 }, new[] { -5, 0, 4 }, new[] { -5, -1, 2 }, // Em C G D
        };
        private static readonly int[] Roots = { -7, -5, -3, 0, -3, -7, 0, -5 };

        private static readonly int[] StabSteps = { 0, 3, 6, 10 };
        private static readonly int[] BassSteps = { 0, 3, 6, 8, 11, 14 };

        private static readonly (int, int)[][] Hook =
        {
            new[] { (0, 7), (2, 9), (4, 7), (6, 4), (10, 4), (12, 2) },
            new[] { (0, 2), (2, 4), (4, 2), (6, -1), (10, 2) },
            new[] { (0, 4), (2, 7), (4, 9), (6, 7), (10, 4), (12, 2) },
            new[] { (0, 4), (6, 2), (8, 0) },
            new[] { (0, 9), (2, 9), (4, 12), (6, 9), (10, 7) },
            new[] { (0, 7), (2, 9), (4, 7), (6, 4), (12, 2) },
            new[] { (0, 4), (2, 7), (4, 9), (8, 7), (12, 4) },
            new[] { (0, 2), (4, 4), (8, 7), (12, 9) },
        };

        public override string Id => StyleId;
        public override int Bars => 8;

        public override Instrument[] Instruments { get; } =
        {
            new Instrument("kp_stab", true, () => Kit.SuperSaw(Dsp.G3, 5, 18f, 0.45f, 3200f, 0.004f, 0.18f, 0.35f, 0.1f, 41)),
            new Instrument("kp_pad", true, () => Kit.SuperSaw(Dsp.G3, 3, 10f, 2.4f, 1000f, 0.35f, 1f, 0.8f, 0.6f, 42)),
            new Instrument("kp_lead", true, () => Kit.SuperSaw(Dsp.G4, 7, 20f, 0.4f, 6000f, 0.005f, 0.2f, 0.6f, 0.08f, 43, 5.5f, 8f)),
            new Instrument("kp_bass", true, () => Kit.SynthBass(Dsp.G2, 0.26f, 1600f, 240f, 20f, 0.3f, 0.6f, 2f)),
            new Instrument("kp_bell", true, () => Kit.Bell(Dsp.G4, 0.5f)),
            new Instrument("kp_kick", false, () => Kit.Kick(170f, 50f, 35f, 11f, 0.3f, 3f, 0.35f, 44)),
            new Instrument("kp_clap", false, () => Kit.Clap(0.3f, 45)),
            new Instrument("kp_hat", false, () => Kit.Hat(0.04f, 90f, 8500f, 46)),
            new Instrument("kp_riser", false, () => Kit.Riser(2f, 47)),
            new Instrument("kp_snare", false, () => Kit.Snare(220f, 380f, 40f, 30f, 3000f, 0.15f, 48)),
        };

        public override float Level(MusicMood mood) => mood == MusicMood.Home ? 1f : 0.91f; // matched to Classic on a phone

        public override float Tempo(MusicMood mood, int blocks, float widthFraction) =>
            Curve(mood, blocks, widthFraction, home: 96f, start: 116f, tall: 8f, thin: 4f, max: 126f);

        public override MusicLayers Layers(MusicMood mood, int blocks, float widthFraction) => GenreLayers(mood, blocks, widthFraction);

        public override void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into)
        {
            int b = Mod(bar, Bars);
            int[] chord = Chords[b];
            bool home = mood == MusicMood.Home;

            if (layers.Pad)
            {
                if (home && step == 0) Chord(into, Pad, chord, 0.12f);
                if (!home && System.Array.IndexOf(StabSteps, step) >= 0) Chord(into, Stab, chord, step == 0 ? 0.09f : 0.07f);
            }

            if (layers.Arp && (home ? step % 4 == 0 : !layers.Kick && step % 2 == 0))
            {
                int note = chord[(step / 2) % chord.Length] + 12;
                into.Add(new Note(Bell, note > 12 ? note - 12 : note, home ? 0.12f : 0.1f, 0f, step % 4 == 0 ? -0.3f : 0.3f));
            }
            if (layers.Kick) Line(into, Lead, Hook[b], step, 0.13f);

            if (layers.Shaker)
            {
                if (step % 4 == 0) into.Add(new Note(Kick, 0, 0.5f));
                if (step == 4 || step == 12) into.Add(new Note(Clap, 0, 0.24f));
                if (step % 4 == 2 || (layers.Kick && step % 2 == 1)) into.Add(new Note(Hat, 0, step % 4 == 2 ? 0.11f : 0.05f, 0f, 0.3f));
            }

            if (layers.Bass && System.Array.IndexOf(BassSteps, step) >= 0)
            {
                int root = Roots[b];
                bool up = step == 6 || step == 14;
                into.Add(new Note(Bass, up ? root + 12 : root, up ? 0.18f : 0.24f));
            }

            if (layers.Tick)
            {
                if (step == 0 && b % 2 == 0) into.Add(new Note(Riser, 0, 0.1f));
                if (b % 2 == 1 && step >= 8) into.Add(new Note(Snare, 0, 0.06f + 0.015f * (step - 8))); // the roll into the next bar
            }
        }
    }
}
