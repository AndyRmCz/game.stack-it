using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>
    /// Synthwave in E minor: detuned saw pads, a pulsing filtered bass, a square-wave arpeggio, a
    /// singing saw lead, a big gated snare. Eight bars: Em C G D, then C D Em Em.
    /// </summary>
    public sealed class SynthwaveStyle : MusicStyle
    {
        public const string StyleId = "music_synthwave";

        private const int Pad = 0, Bass = 1, Arp = 2, Lead = 3, Kick = 4, Snare = 5, Hat = 6, Riser = 7;

        // Chords around G3 and their bass roots (semitones above G2).
        private static readonly int[][] Chords =
        {
            new[] { -3, 0, 4 }, new[] { -3, 0, 5 }, new[] { -5, 0, 4 }, new[] { -5, -1, 2 }, // Em C G D
            new[] { -3, 0, 5 }, new[] { -5, -1, 2 }, new[] { -3, 0, 4 }, new[] { -3, 0, 4 }, // C D Em Em
        };
        private static readonly int[] Roots = { -3, -7, 0, -5, -7, -5, -3, -3 };

        // The lead's melody (E minor pentatonic around G4), two phrases of four bars.
        private static readonly (int, int)[][] Melody =
        {
            new[] { (0, 4), (6, 7), (8, 9) },
            new[] { (0, 7), (6, 4), (10, 2) },
            new[] { (0, 4), (4, 2), (8, 0), (12, 2) },
            new[] { (0, 2), (8, 4), (12, -3) },
            new[] { (0, 9), (6, 7), (8, 4) },
            new[] { (0, 7), (6, 9), (8, 12) },
            new[] { (0, 9), (12, 7) },
            new[] { (0, 4), (8, -3) },
        };

        private static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 2, 3 };

        public override string Id => StyleId;
        public override int Bars => 8;

        public override Instrument[] Instruments { get; } =
        {
            new Instrument("sw_pad", true, () => Room(Kit.SuperSaw(Dsp.G3, 3, 12f, 2.6f, 1600f, 0.3f, 1f, 0.8f, 0.6f, 11))),
            new Instrument("sw_bass", true, () => Kit.SynthBass(Dsp.G2, 0.24f, 2400f, 280f, 18f, 0.4f, 0f, 1.5f)),
            new Instrument("sw_arp", true, () => Kit.SynthBass(Dsp.G4, 0.2f, 5000f, 900f, 25f, 0.2f, 1f, 1f)),
            new Instrument("sw_lead", true, () => Room(Kit.SuperSaw(Dsp.G4, 2, 7f, 0.9f, 3000f, 0.02f, 0.4f, 0.7f, 0.2f, 12, 5.5f, 12f))),
            new Instrument("sw_kick", false, () => Kit.Kick(150f, 48f, 30f, 9f, 0.2f, 1.5f, 0.4f, 13)),
            new Instrument("sw_snare", false, GatedSnare),
            new Instrument("sw_hat", false, () => Kit.Hat(0.06f, 60f, 7000f, 14)),
            new Instrument("sw_riser", false, () => Kit.Riser(2f, 15)),
        };

        public override float Level(MusicMood mood) => mood == MusicMood.Home ? 1.22f : 0.8f; // matched to Classic on a phone

        public override float Tempo(MusicMood mood, int blocks, float widthFraction) =>
            Curve(mood, blocks, widthFraction, home: 92f, start: 104f, tall: 10f, thin: 6f, max: 118f);

        public override MusicLayers Layers(MusicMood mood, int blocks, float widthFraction) => GenreLayers(mood, blocks, widthFraction);

        public override void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into)
        {
            int b = Mod(bar, Bars);
            int[] chord = Chords[b];
            bool home = mood == MusicMood.Home;
            if (layers.Pad && step == 0) Chord(into, Pad, chord, home ? 0.12f : 0.11f);

            if (layers.Arp && (home ? step % 4 == 0 : layers.Kick || step % 2 == 0))
            {
                int index = ArpOrder[(step / (layers.Kick && !home ? 1 : 2)) % ArpOrder.Length];
                int note = index == 3 ? chord[0] + 12 : chord[index] + 12;
                if (note > 12) note -= 12;
                into.Add(new Note(Arp, note, home ? 0.09f : 0.08f, 0f, step % 4 == 2 ? 0.3f : -0.3f));
            }

            if (layers.Shaker) // the beat: kick and snare from the first block, hats on the off-beats
            {
                if (step == 0 || step == 8 || (layers.Kick && step % 4 == 0)) into.Add(new Note(Kick, 0, 0.5f));
                if (step == 4 || step == 12) into.Add(new Note(Snare, 0, 0.32f));
                if (step % 4 == 2 || (layers.Kick && step % 2 == 0)) into.Add(new Note(Hat, 0, step % 4 == 2 ? 0.12f : 0.07f));
                if (layers.Kick && b % 4 == 3 && step >= 12) into.Add(new Note(Snare, 0, 0.12f + 0.04f * (step - 12))); // fill
            }

            if (layers.Bass && step % 2 == 0)
            {
                int root = Roots[b];
                into.Add(new Note(Bass, layers.Kick && step % 4 == 2 && root + 12 <= 12 ? root + 12 : root, 0.26f));
            }

            if (layers.Kick) Line(into, Lead, Melody[b], step, 0.15f, 0.1f);

            if (layers.Tick)
            {
                if (step == 0 && b % 2 == 0) into.Add(new Note(Riser, 0, 0.1f));
                if (step % 2 == 1) into.Add(new Note(Hat, 0, 0.05f));
            }
        }

        private static float[] Room(float[] x)
        {
            Dsp.Room(x, 0.35f);
            return Dsp.Normalize(x);
        }

        /// <summary>The '80s snare: a snare in a big room whose tail is cut off sharply (the "gate").</summary>
        private static float[] GatedSnare()
        {
            float[] snare = Kit.Snare(190f, 330f, 20f, 14f, 1800f, 0.42f, 16);
            Dsp.Room(snare, 0.9f, 0.6f);
            Dsp.Shape(snare, t => t < 0.26f ? 1f : Dsp.Clamp01(1f - (t - 0.26f) / 0.03f));
            return Dsp.Declick(Dsp.Normalize(snare));
        }
    }
}
