using System;

namespace StackIt.Client.Music
{
    /// <summary>
    /// Building blocks for synthesized instruments. Plain C# with no engine types, so an instrument
    /// renders the same in the game, in the tests and in the preview tool. Every pitched instrument
    /// is rendered once at a G and repitched by the player (playback rate), like the original
    /// music.
    /// </summary>
    public static class Dsp
    {
        public const int SampleRate = 44100;
        public const float G4 = 392f;
        public const float G3 = 196f;
        public const float G2 = 98f;
        public const float G1 = 49f;

        private const float Pi = (float)Math.PI;

        public static int Samples(float seconds) => (int)Math.Ceiling(seconds * SampleRate);

        /// <summary>Renders a function of time (seconds), clamped to [-1, 1].</summary>
        public static float[] Render(float seconds, Func<float, float> wave)
        {
            int count = Samples(seconds);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Clamp(wave(i / (float)SampleRate), -1f, 1f);
            return data;
        }

        // --- the same float maths as UnityEngine.Mathf, so the original instruments are unchanged ---

        public static float Sin(float hz, float t) => (float)Math.Sin(2f * Pi * hz * t);
        public static float Exp(float x) => (float)Math.Exp(x);
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Frequency ratio of a pitch shift in semitones.</summary>
        public static float Ratio(float semitones) => (float)Math.Pow(2.0, semitones / 12.0);

        // --- oscillators ------------------------------------------------------------------------

        /// <summary>
        /// Band-limited sawtooth (PolyBLEP), so notes stay clean when repitched up an octave.
        /// <paramref name="vibratoHz"/> and <paramref name="vibratoCents"/> add a slow pitch wobble.
        /// </summary>
        public static float[] Saw(float hz, int count, float phase = 0f, float vibratoHz = 0f, float vibratoCents = 0f)
        {
            var data = new float[count];
            double p = phase;
            for (int i = 0; i < count; i++)
            {
                double f = hz * Wobble(i, vibratoHz, vibratoCents);
                double dt = f / SampleRate;
                data[i] = (float)(2.0 * p - 1.0 - PolyBlep(p, dt));
                p += dt;
                if (p >= 1.0) p -= 1.0;
            }
            return data;
        }

        /// <summary>Band-limited pulse wave (PolyBLEP); <paramref name="width"/> 0.5 is a square.</summary>
        public static float[] Pulse(float hz, int count, float width = 0.5f, float phase = 0f)
        {
            var data = new float[count];
            double p = phase;
            double dt = hz / (double)SampleRate;
            for (int i = 0; i < count; i++)
            {
                double v = p < width ? 1.0 : -1.0;
                v += PolyBlep(p, dt);
                double q = p - width;
                if (q < 0) q += 1.0;
                v -= PolyBlep(q, dt);
                data[i] = (float)v;
                p += dt;
                if (p >= 1.0) p -= 1.0;
            }
            return data;
        }

        private static double PolyBlep(double p, double dt)
        {
            if (p < dt)
            {
                double x = p / dt;
                return x + x - x * x - 1.0;
            }
            if (p > 1.0 - dt)
            {
                double x = (p - 1.0) / dt;
                return x * x + x + x + 1.0;
            }
            return 0.0;
        }

        private static double Wobble(int i, float hz, float cents) =>
            hz <= 0f ? 1.0 : Math.Pow(2.0, cents * Math.Sin(2.0 * Math.PI * hz * i / SampleRate) / 1200.0);

        /// <summary>White noise from a fixed seed, so a render is the same every time.</summary>
        public static float[] Noise(int count, int seed)
        {
            var random = new Random(seed);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return data;
        }

        /// <summary>
        /// Plucked string (Karplus-Strong): a noise burst circulating in a delay line one period
        /// long, with a two-point average that darkens it as it rings. <paramref name="brightness"/>
        /// (0..1) shapes the pluck; <paramref name="sustain"/> is the loss per period (0.99..0.9999).
        /// </summary>
        public static float[] Pluck(float hz, float seconds, float brightness, float sustain, int seed)
        {
            int count = Samples(seconds);
            var y = new float[count];
            float period = SampleRate / hz - 0.5f; // the average adds half a sample of delay
            int burst = (int)Math.Ceiling(period);
            float[] excite = Noise(burst, seed);
            Lowpass(excite, 400f + brightness * 9000f);
            for (int i = 0; i < count; i++)
            {
                float input = i < burst ? excite[i] : 0f;
                float delayed = 0.5f * (Read(y, i - period) + Read(y, i - period - 1f));
                y[i] = input + sustain * delayed;
            }
            return y;
        }

        private static float Read(float[] y, float position)
        {
            if (position < 0f) return 0f;
            int a = (int)position;
            float frac = position - a;
            float next = a + 1 < y.Length ? y[a + 1] : 0f;
            return y[a] + (next - y[a]) * frac;
        }

        // --- filters and shaping (in place) ------------------------------------------------------

        /// <summary>One-pole low-pass: a gentle 6 dB/octave darkening.</summary>
        public static void Lowpass(float[] x, float hz)
        {
            float a = OnePole(hz);
            float z = 0f;
            for (int i = 0; i < x.Length; i++) x[i] = z += a * (x[i] - z);
        }

        /// <summary>One-pole high-pass: removes rumble below <paramref name="hz"/>.</summary>
        public static void Highpass(float[] x, float hz)
        {
            float a = OnePole(hz);
            float z = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                z += a * (x[i] - z);
                x[i] -= z;
            }
        }

        private static float OnePole(float hz) => 1f - Exp(-2f * Pi * Math.Min(hz, SampleRate * 0.45f) / SampleRate);

        public enum FilterMode
        {
            Lowpass,
            Bandpass,
            Highpass,
        }

        /// <summary>
        /// Resonant state-variable filter (Zavalishin's TPT form, stable at any cutoff) whose cutoff
        /// can move per sample: the classic synthesizer "wah" of a filter envelope.
        /// </summary>
        public static void Filter(float[] x, Func<int, float> cutoffHz, float resonance, FilterMode mode)
        {
            float k = 2f - 2f * Clamp(resonance, 0f, 0.95f);
            float ic1 = 0f, ic2 = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float g = (float)Math.Tan(Pi * Clamp(cutoffHz(i), 20f, SampleRate * 0.45f) / SampleRate);
                float a1 = 1f / (1f + g * (g + k));
                float v3 = x[i] - ic2;
                float v1 = a1 * ic1 + g * a1 * v3;
                float v2 = ic2 + g * v1;
                ic1 = 2f * v1 - ic1;
                ic2 = 2f * v2 - ic2;
                x[i] = mode == FilterMode.Lowpass ? v2 : mode == FilterMode.Bandpass ? v1 : x[i] - k * v1 - v2;
            }
        }

        /// <summary>Soft clipping distortion; <paramref name="drive"/> 1 is clean, 20 is fuzz.</summary>
        public static void Drive(float[] x, float drive)
        {
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Tanh(drive * x[i]) / norm;
        }

        /// <summary>Multiplies by an envelope given as a function of time (seconds).</summary>
        public static void Shape(float[] x, Func<float, float> envelope)
        {
            for (int i = 0; i < x.Length; i++) x[i] *= envelope(i / (float)SampleRate);
        }

        /// <summary>Attack, decay to a sustain level, then a release that ends at <paramref name="length"/>.</summary>
        public static float Adsr(float t, float attack, float decay, float sustain, float release, float length)
        {
            float level = t < attack ? t / attack : sustain + (1f - sustain) * Exp(-(t - attack) / Math.Max(decay, 1e-4f));
            float tail = Clamp01((length - t) / Math.Max(release, 1e-4f));
            return level * tail;
        }

        /// <summary>A short percussive envelope: a fast attack and an exponential decay.</summary>
        public static float Hit(float t, float attack, float decayRate) => Clamp01(t / attack) * Exp(-t * decayRate);

        /// <summary>A small room: a few spaced, darkened echoes mixed under the dry sound.</summary>
        public static void Room(float[] x, float mix, float decay = 0.45f)
        {
            int[] taps = { 1103, 1447, 1931, 2593, 3407 }; // 25-77 ms, mutually prime so they don't ring
            var wet = new float[x.Length];
            float gain = 1f;
            foreach (int tap in taps)
            {
                gain *= decay + 0.4f;
                for (int i = tap; i < x.Length; i++) wet[i] += x[i - tap] * gain * 0.35f;
            }
            Lowpass(wet, 3000f);
            for (int i = 0; i < x.Length; i++) x[i] += wet[i] * mix;
        }

        // --- mixing helpers ----------------------------------------------------------------------

        /// <summary>Adds <paramref name="src"/> × <paramref name="gain"/> into <paramref name="dst"/>, starting at <paramref name="offset"/>.</summary>
        public static void Mix(float[] dst, float[] src, float gain = 1f, int offset = 0)
        {
            for (int i = 0; i < src.Length && i + offset < dst.Length; i++) dst[i + offset] += src[i] * gain;
        }

        /// <summary>Scales a clip so its loudest sample is <paramref name="peak"/>.</summary>
        public static float[] Normalize(float[] x, float peak = 1f)
        {
            float max = 0f;
            foreach (float v in x) max = Math.Max(max, Math.Abs(v));
            if (max <= 0f) return x;
            float k = peak / max;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
            return x;
        }

        /// <summary>Short fades at both ends, so a clip never starts or stops with a click.</summary>
        public static float[] Declick(float[] x, float fadeInSeconds = 0.002f, float fadeOutSeconds = 0.01f)
        {
            int fadeIn = Math.Min(x.Length, Samples(fadeInSeconds));
            int fadeOut = Math.Min(x.Length, Samples(fadeOutSeconds));
            for (int i = 0; i < fadeIn; i++) x[i] *= i / (float)fadeIn;
            for (int i = 0; i < fadeOut; i++) x[x.Length - 1 - i] *= i / (float)fadeOut;
            return x;
        }
    }
}
