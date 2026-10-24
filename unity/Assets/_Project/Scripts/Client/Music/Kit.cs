using System;

namespace StackIt.Client.Music
{
    /// <summary>
    /// The synthesized instruments the genre styles share. Each returns one mono note (pitched ones
    /// at the G they're asked for) or one drum hit, normalized to a peak of 1 so a style's note
    /// volumes are its mix. Every random part has a fixed seed: the same sound on every phone.
    /// </summary>
    public static class Kit
    {
        private const float Tau = 2f * (float)Math.PI;

        // --- drums: built the way analog drum machines (the TR-808 among them) make them -------

        /// <summary>A sine whose pitch drops fast (the "boom"), with a click of noise on the attack.</summary>
        public static float[] Kick(float startHz, float endHz, float sweep, float decay, float click, float drive, float seconds, int seed)
        {
            int count = Dsp.Samples(seconds);
            var data = new float[count];
            float[] noise = Dsp.Noise(count, seed);
            Dsp.Highpass(noise, 2000f);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                phase += (endHz + (startHz - endHz) * Dsp.Exp(-t * sweep)) / Dsp.SampleRate;
                data[i] = (float)Math.Sin(Tau * phase) * Dsp.Hit(t, 0.001f, decay) + click * noise[i] * Dsp.Exp(-t * 300f);
            }
            if (drive > 1f) Dsp.Drive(data, drive);
            return Dsp.Declick(Dsp.Normalize(data), 0f);
        }

        /// <summary>Two drum-head tones plus a band of noise for the snares.</summary>
        public static float[] Snare(float tone1, float tone2, float toneDecay, float noiseDecay, float noiseHz, float seconds, int seed)
        {
            int count = Dsp.Samples(seconds);
            float[] noise = Dsp.Noise(count, seed);
            Dsp.Filter(noise, _ => noiseHz, 0.2f, Dsp.FilterMode.Bandpass);
            Dsp.Highpass(noise, 600f);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                float body = (Dsp.Sin(tone1, t) + 0.6f * Dsp.Sin(tone2, t)) * Dsp.Hit(t, 0.001f, toneDecay);
                data[i] = 0.7f * body + 1.6f * noise[i] * Dsp.Hit(t, 0.001f, noiseDecay);
            }
            return Dsp.Declick(Dsp.Normalize(data), 0f);
        }

        /// <summary>Three quick bursts of filtered noise and a short tail: hands clapping together.</summary>
        public static float[] Clap(float seconds, int seed)
        {
            int count = Dsp.Samples(seconds);
            float[] noise = Dsp.Noise(count, seed);
            Dsp.Filter(noise, _ => 1300f, 0.35f, Dsp.FilterMode.Bandpass);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                float env = 0f;
                for (int b = 0; b < 3; b++)
                {
                    float tb = t - b * 0.011f;
                    if (tb >= 0f) env = Math.Max(env, Dsp.Exp(-tb * 260f));
                }
                if (t > 0.03f) env = Math.Max(env, 0.55f * Dsp.Exp(-(t - 0.03f) * 16f));
                data[i] = noise[i] * env;
            }
            return Dsp.Declick(Dsp.Normalize(data), 0f);
        }

        // The six square-wave frequencies an 808's cymbal circuit mixes into a metallic shimmer.
        private static readonly float[] Metal = { 205.3f, 304.4f, 369.6f, 522.7f, 540f, 800f };

        /// <summary>Hi-hat: six detuned squares through a high-pass. A slow decay makes it open.</summary>
        public static float[] Hat(float seconds, float decay, float brightHz, int seed)
        {
            int count = Dsp.Samples(seconds);
            float[] data = MetalNoise(count, 1f, seed, 0.3f);
            Dsp.Filter(data, _ => brightHz, 0.2f, Dsp.FilterMode.Highpass);
            Dsp.Shape(data, t => Dsp.Hit(t, 0.0008f, decay));
            return Dsp.Declick(Dsp.Normalize(data), 0f);
        }

        /// <summary>Crash cymbal: the hat's metal, lower and with more noise, ringing out.</summary>
        public static float[] Crash(float seconds, int seed)
        {
            int count = Dsp.Samples(seconds);
            float[] data = MetalNoise(count, 1.47f, seed, 0.9f);
            Dsp.Filter(data, _ => 3800f, 0.1f, Dsp.FilterMode.Highpass);
            Dsp.Shape(data, t => Dsp.Hit(t, 0.002f, 2.4f));
            return Dsp.Declick(Dsp.Normalize(data));
        }

        private static float[] MetalNoise(int count, float scale, int seed, float noiseMix)
        {
            var data = new float[count];
            for (int k = 0; k < Metal.Length; k++) Dsp.Mix(data, Dsp.Pulse(Metal[k] * scale, count, 0.5f, k * 0.13f), 1f / Metal.Length);
            Dsp.Mix(data, Dsp.Noise(count, seed), noiseMix);
            return data;
        }

        /// <summary>A rim click: a high knock and a tick of noise.</summary>
        public static float[] Rim(int seed)
        {
            int count = Dsp.Samples(0.05f);
            float[] noise = Dsp.Noise(count, seed);
            Dsp.Filter(noise, _ => 3000f, 0.3f, Dsp.FilterMode.Bandpass);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                data[i] = Dsp.Sin(1700f, t) * Dsp.Exp(-t * 120f) + 0.8f * noise[i] * Dsp.Exp(-t * 200f);
            }
            return Dsp.Declick(Dsp.Normalize(data), 0f);
        }

        // --- effects that are notes of their own -------------------------------------------------

        /// <summary>Noise swept upwards and swelling: builds tension into the next bar.</summary>
        public static float[] Riser(float seconds, int seed)
        {
            int count = Dsp.Samples(seconds);
            float[] data = Dsp.Noise(count, seed);
            Dsp.Filter(data, i => 300f * (float)Math.Pow(25.0, i / (double)count), 0.6f, Dsp.FilterMode.Bandpass);
            Dsp.Shape(data, t => (t / seconds) * (t / seconds));
            return Dsp.Declick(Dsp.Normalize(data), 0.002f, 0.03f);
        }

        /// <summary>Vinyl: a soft hiss with random dust pops.</summary>
        public static float[] Crackle(float seconds, int seed)
        {
            int count = Dsp.Samples(seconds);
            float[] data = Dsp.Noise(count, seed);
            Dsp.Lowpass(data, 3000f);
            for (int i = 0; i < count; i++) data[i] *= 0.08f;
            var random = new Random(seed + 1);
            int pops = (int)(seconds * 7f);
            for (int p = 0; p < pops; p++)
            {
                int at = random.Next(count);
                float size = 0.25f + 0.75f * (float)random.NextDouble();
                int length = Dsp.Samples(0.002f + 0.003f * (float)random.NextDouble());
                for (int i = 0; i < length && at + i < count; i++)
                    data[at + i] += size * (float)(random.NextDouble() * 2.0 - 1.0) * Dsp.Exp(-i / (length * 0.3f));
            }
            Dsp.Lowpass(data, 6000f);
            return Dsp.Declick(Dsp.Normalize(data, 0.8f), 0.05f, 0.05f);
        }

        // --- pitched -----------------------------------------------------------------------------

        /// <summary>Several detuned saws (the "supersaw"), through a low-pass, under an ADSR envelope.</summary>
        public static float[] SuperSaw(float hz, int voices, float spreadCents, float seconds, float cutoffHz,
            float attack, float decay, float sustain, float release, int seed, float vibratoHz = 0f, float vibratoCents = 0f)
        {
            int count = Dsp.Samples(seconds);
            var data = new float[count];
            var random = new Random(seed);
            for (int v = 0; v < voices; v++)
            {
                float cents = voices == 1 ? 0f : spreadCents * (2f * v / (voices - 1) - 1f);
                Dsp.Mix(data, Dsp.Saw(hz * Dsp.Ratio(cents / 100f), count, (float)random.NextDouble(), vibratoHz, vibratoCents));
            }
            Dsp.Filter(data, _ => cutoffHz, 0.1f, Dsp.FilterMode.Lowpass);
            Dsp.Shape(data, t => Dsp.Adsr(t, attack, decay, sustain, release, seconds));
            return Dsp.Declick(Dsp.Normalize(data));
        }

        /// <summary>
        /// Synth bass: a saw (plus some square for <paramref name="pulseMix"/>) through a resonant
        /// low-pass whose cutoff falls fast from <paramref name="fromHz"/> to <paramref name="toHz"/>.
        /// </summary>
        public static float[] SynthBass(float hz, float seconds, float fromHz, float toHz, float sweep, float resonance, float pulseMix, float drive)
        {
            int count = Dsp.Samples(seconds);
            float[] data = Dsp.Saw(hz, count);
            if (pulseMix > 0f) Dsp.Mix(data, Dsp.Pulse(hz, count, 0.5f, 0.25f), pulseMix);
            Dsp.Filter(data, i => toHz + (fromHz - toHz) * Dsp.Exp(-i / (float)Dsp.SampleRate * sweep), resonance, Dsp.FilterMode.Lowpass);
            if (drive > 1f) Dsp.Drive(data, drive);
            Dsp.Shape(data, t => Dsp.Adsr(t, 0.003f, 0.25f, 0.55f, 0.06f, seconds));
            return Dsp.Declick(Dsp.Normalize(data));
        }

        /// <summary>The 808 bass: a sine with a small pitch dip, saturated so phone speakers can hear its harmonics.</summary>
        public static float[] Sub(float hz, float seconds, float drive)
        {
            int count = Dsp.Samples(seconds);
            var data = new float[count];
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                phase += hz * (1f + 0.35f * Dsp.Exp(-t * 60f)) / Dsp.SampleRate;
                data[i] = (float)Math.Sin(Tau * phase);
            }
            Dsp.Drive(data, drive);
            Dsp.Lowpass(data, 2500f);
            Dsp.Shape(data, t => Dsp.Adsr(t, 0.003f, 0.4f, 0.55f, 0.08f, seconds));
            return Dsp.Declick(Dsp.Normalize(data));
        }

        /// <summary>
        /// Electric piano by FM synthesis: a sine modulating a sine at the same pitch, bright on the
        /// attack and mellowing as it rings, a short metallic tine, a gentle tremolo and a tape-like
        /// pitch wobble.
        /// </summary>
        public static float[] Keys(float hz, float seconds, float wobbleCents)
        {
            int count = Dsp.Samples(seconds);
            var data = new float[count];
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                phase += hz * Dsp.Ratio(wobbleCents / 100f * (float)Math.Sin(Tau * 0.55f * t)) / Dsp.SampleRate;
                float index = 0.25f + 1.6f * Dsp.Exp(-t * 5f);
                float tone = (float)Math.Sin(Tau * phase + index * Math.Sin(Tau * phase));
                float tine = 0.12f * (float)Math.Sin(Tau * phase * 7.0) * Dsp.Exp(-t * 35f);
                float tremolo = 1f - 0.12f * (0.5f + 0.5f * (float)Math.Sin(Tau * 4.5f * t));
                data[i] = (tone + tine) * Dsp.Hit(t, 0.002f, 1.5f) * tremolo;
            }
            Dsp.Lowpass(data, 4500f);
            return Dsp.Declick(Dsp.Normalize(data));
        }

        /// <summary>Vibraphone: a mallet tone with a fast tremolo.</summary>
        public static float[] Vibes(float hz, float seconds)
        {
            return Dsp.Declick(Dsp.Normalize(Dsp.Render(seconds, t =>
            {
                float tone = Dsp.Sin(hz, t) + 0.25f * Dsp.Sin(hz * 4f, t) * Dsp.Exp(-t * 12f);
                float tremolo = 1f - 0.25f * (0.5f + 0.5f * Dsp.Sin(5f, t));
                return tone * Dsp.Hit(t, 0.002f, 1.8f) * tremolo * 0.8f;
            })));
        }

        /// <summary>A glassy bell-pluck (FM, modulator an octave up), for pop arpeggios.</summary>
        public static float[] Bell(float hz, float seconds)
        {
            return Dsp.Declick(Dsp.Normalize(Dsp.Render(seconds, t =>
            {
                float index = 2.4f * Dsp.Exp(-t * 14f);
                float tone = (float)Math.Sin(Tau * hz * t + index * Math.Sin(Tau * 2f * hz * t));
                return tone * Dsp.Hit(t, 0.001f, 5f) * 0.8f;
            })));
        }

        /// <summary>A plucked string with a little room around it (nylon-ish, or a clean electric).</summary>
        public static float[] PluckedString(float hz, float seconds, float brightness, float sustain, float room, int seed)
        {
            float[] data = Dsp.Pluck(hz, seconds, brightness, sustain, seed);
            Dsp.Highpass(data, 60f);
            if (room > 0f) Dsp.Room(data, room);
            return Dsp.Declick(Dsp.Normalize(data));
        }

        /// <summary>
        /// Distorted electric guitar: one plucked string per interval (a power chord is 0, 7, 12),
        /// strummed a few milliseconds apart, through heavy soft clipping and a "speaker cabinet"
        /// that cuts the fizz above ~4 kHz and the mud below ~90 Hz. <paramref name="muted"/> damps
        /// the strings with the picking hand (palm mute): the short, dark "chug".
        /// </summary>
        public static float[] Guitar(float hz, int[] intervals, float seconds, bool muted, float drive, int seed)
        {
            int count = Dsp.Samples(seconds);
            var data = new float[count];
            for (int k = 0; k < intervals.Length; k++)
            {
                float[] str = Dsp.Pluck(hz * Dsp.Ratio(intervals[k]), seconds, muted ? 0.3f : 0.85f, muted ? 0.9f : 0.997f, seed + k);
                Dsp.Mix(data, str, 1f, Dsp.Samples(0.004f * k));
            }
            if (muted) Dsp.Shape(data, t => Dsp.Exp(-t * 9f));
            Dsp.Highpass(data, 90f);
            Dsp.Normalize(data);
            Dsp.Drive(data, drive);
            Dsp.Filter(data, _ => 3800f, 0.35f, Dsp.FilterMode.Lowpass);
            Dsp.Lowpass(data, 6000f);
            Dsp.Highpass(data, 100f);
            if (!muted) Dsp.Shape(data, t => Dsp.Clamp01((seconds - t) / 0.08f));
            return Dsp.Declick(Dsp.Normalize(data));
        }
    }
}
