using System;
using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>
    /// The original, free soundtrack: <see cref="MusicPlan"/> played on a marimba, a warm pad, a
    /// plucked bass, a shaker, a soft kick and a tension tick. Moved here unchanged from
    /// MusicService (same formulas, same note order, same volumes).
    /// </summary>
    public sealed class ClassicStyle : MusicStyle
    {
        public const string StyleId = "music_classic";

        private const int Marimba = 0, Pad = 1, Bass = 2, Shaker = 3, Kick = 4, Tick = 5;
        private const float BaseNote = Dsp.G4; // same key as the perfect chime

        public override string Id => StyleId;
        public override int Bars => MusicPlan.Bars;
        public override float EndBpm => 56f;

        public override Instrument[] Instruments { get; } =
        {
            new Instrument("m_marimba", true, () => Dsp.Render(1.0f, MarimbaWave)),
            new Instrument("m_pad", true, () => Dsp.Render(3.8f, PadWave)),
            new Instrument("m_bass", true, () => Dsp.Render(0.7f, BassWave)),
            new Instrument("m_shaker", false, ShakerClip),
            new Instrument("m_kick", false, () => Dsp.Render(0.32f, KickWave)),
            new Instrument("m_tick", false, () => Dsp.Render(0.04f, TickWave)),
        };

        public override float Tempo(MusicMood mood, int blocks, float widthFraction) => MusicPlan.Tempo(mood, blocks, widthFraction);

        public override MusicLayers Layers(MusicMood mood, int blocks, float widthFraction) => MusicPlan.Layers(mood, blocks, widthFraction);

        public override void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into)
        {
            if (step == 0 && layers.Pad)
                foreach (int note in MusicPlan.Pad(bar)) into.Add(new Note(Pad, note, 0.16f));
            int? arp = layers.Arp ? MusicPlan.Arp(mood, bar, step) : null;
            if (arp != null) into.Add(new Note(Marimba, arp.Value, step % 4 == 0 ? 0.26f : 0.18f));
            int? bass = layers.Bass ? MusicPlan.Bass(bar, step) : null;
            if (bass != null) into.Add(new Note(Bass, bass.Value + 24, 0.32f)); // the clip is G2
            if (layers.Kick && MusicPlan.Kick(step)) into.Add(new Note(Kick, 0, 0.42f));
            if (layers.Shaker && MusicPlan.Shaker(step)) into.Add(new Note(Shaker, 0, step % 4 == 3 ? 0.1f : 0.06f));
            if (layers.Tick) into.Add(new Note(Tick, 0, step % 4 == 0 ? 0.07f : 0.04f));
        }

        // --- instruments (one note each at G4, or G2 for the bass) -------------------------------

        /// <summary>Soft mallet: fundamental plus a bright fourth partial that dies fast.</summary>
        private static float MarimbaWave(float t)
        {
            float attack = Dsp.Clamp01(t / 0.004f);
            return attack * (Dsp.Sin(BaseNote, t) * Dsp.Exp(-t * 5f) + 0.25f * Dsp.Sin(BaseNote * 4f, t) * Dsp.Exp(-t * 22f)) * 0.8f;
        }

        /// <summary>Warm pad: slow swell, a gentle chorus of three detuned sines and an octave.</summary>
        private static float PadWave(float t)
        {
            float env = Dsp.Clamp01(t / 0.6f) * Dsp.Clamp01((3.8f - t) / 1.4f);
            float tone = Dsp.Sin(BaseNote * 0.5f, t) + 0.6f * Dsp.Sin(BaseNote * 0.5f * 1.004f, t) + 0.6f * Dsp.Sin(BaseNote * 0.5f * 0.996f, t)
                + 0.25f * Dsp.Sin(BaseNote, t);
            return env * tone * 0.3f;
        }

        /// <summary>Round plucked bass (G2).</summary>
        private static float BassWave(float t)
        {
            float hz = BaseNote / 4f;
            float env = Dsp.Clamp01(t / 0.006f) * Dsp.Exp(-t * 4f);
            return env * (Dsp.Sin(hz, t) + 0.15f * Dsp.Sin(hz * 3f, t)) * 0.8f;
        }

        /// <summary>Short airy noise burst.</summary>
        private static float[] ShakerClip()
        {
            var noise = new Random(7);
            return Dsp.Render(0.09f, t =>
            {
                float env = Dsp.Clamp01(t / 0.01f) * Dsp.Exp(-t * 45f);
                return env * ((float)noise.NextDouble() * 2f - 1f) * 0.5f;
            });
        }

        /// <summary>Soft thump: a sine sweeping down.</summary>
        private static float KickWave(float t)
        {
            float hz = Dsp.Lerp(130f, 45f, Dsp.Clamp01(t / 0.12f));
            return (float)Math.Sin(2f * (float)Math.PI * hz * t) * Dsp.Exp(-t * 14f);
        }

        /// <summary>Tiny high click for tension.</summary>
        private static float TickWave(float t) => Dsp.Sin(2400f, t) * Dsp.Exp(-t * 160f) * 0.6f;
    }
}
