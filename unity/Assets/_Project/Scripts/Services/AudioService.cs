using StackIt.Client;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Sound effects. Recorded sounds (Kenney, CC0) live in Resources/StackIt/Audio: a soft wooden
    /// knock for landings, a tap for buttons, a chime for rewards. The perfect chime is a synthesized
    /// marimba whose pitch climbs the pentatonic scale with each consecutive perfect (Feel), so a
    /// streak plays a little melody instead of an alarm.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float BaseNote = 392f; // G4: the chime's first note

        private AudioSource[] _voices;
        private int _next;
        private AudioClip[] _lands;
        private AudioClip _tap;
        private AudioClip _miss;
        private AudioClip _reward;
        private AudioClip _perfect;
        private AudioClip _record;
        private AudioClip[] _fanfares;

        public bool Enabled { get; set; } = true;

        private void Awake()
        {
            _voices = new AudioSource[8];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _lands = new[] { Load("land_1"), Load("land_2"), Load("land_3") };
            _tap = Load("ui_tap");
            _miss = Load("miss");
            _reward = Load("reward");
            _perfect = Synth("perfect", 1.1f, t => Marimba(t, BaseNote) * 0.5f);
            _record = Synth("record", 1.4f, t => Arpeggio(t) * 0.45f);
            _fanfares = new[]
            {
                Synth("fanfare_1", 1.6f, t => Fanfare(t, 1) * 0.4f),
                Synth("fanfare_2", 1.8f, t => Fanfare(t, 2) * 0.36f),
                Synth("fanfare_3", 2.0f, t => Fanfare(t, 3) * 0.32f),
            };
        }

        public void Place() => Play(_lands[Random.Range(0, _lands.Length)], Random.Range(0.94f, 1.06f), 0.6f);

        public void Perfect(int combo) => Play(_perfect, Mathf.Pow(2f, Feel.PerfectSemitones(combo) / 12f), 0.7f);

        public void Miss() => Play(_miss, 0.9f, 0.8f);
        public void Click() => Play(_tap, Random.Range(0.97f, 1.03f), 0.45f);
        public void Reward() => Play(_reward, 1f, 0.7f);
        public void Record() => Play(_record, 1f, 0.7f);

        /// <summary>A perfect-streak milestone: tier 1 (5 in a row), 2 (10) or 3 (20 and every 10 after).</summary>
        public void Fanfare(int tier) => Play(_fanfares[Mathf.Clamp(tier, 1, 3) - 1], 1f, 0.75f);

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (!Enabled || clip == null) return;
            AudioSource voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }

        private static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>("StackIt/Audio/" + name);
            if (clip == null) Debug.LogWarning($"Missing sound Resources/StackIt/Audio/{name}.");
            return clip;
        }

        private delegate float Wave(float time);

        private static AudioClip Synth(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Wooden bar: a warm fundamental, a bright 4th partial that dies fast, a soft attack.</summary>
        private static float Marimba(float t, float freq)
        {
            float attack = Mathf.Clamp01(t / 0.004f);
            float body = Mathf.Sin(2 * Mathf.PI * freq * t) * Mathf.Exp(-t * 5.5f);
            float bright = 0.35f * Mathf.Sin(2 * Mathf.PI * freq * 3.98f * t) * Mathf.Exp(-t * 28f);
            float air = 0.12f * Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * Mathf.Exp(-t * 9f);
            return (body + bright + air) * attack;
        }

        /// <summary>
        /// A quick rising run up G pentatonic that lands on a held chord: longer and higher with
        /// each tier, so a 20-streak sounds bigger than a 5. Always in the chime's key.
        /// </summary>
        private static float Fanfare(float t, int tier)
        {
            int[] run = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };
            int notes = 3 + 2 * tier;
            float sum = 0f;
            for (int i = 0; i < notes; i++)
            {
                float start = i * 0.055f;
                if (t >= start) sum += Marimba(t - start, BaseNote * Mathf.Pow(2f, run[i] / 12f)) * 0.5f;
            }
            float chordAt = notes * 0.055f;
            if (t >= chordAt)
                foreach (int note in new[] { 0, 4, 7, 12 }) // G major, held
                    sum += Marimba(t - chordAt, BaseNote * Mathf.Pow(2f, (note + (tier == 3 ? 12 : 0)) / 12f)) * 0.35f;
            return sum;
        }

        /// <summary>Four rising pentatonic notes, let ring.</summary>
        private static float Arpeggio(float t)
        {
            int[] steps = { 0, 4, 7, 12 };
            float sum = 0f;
            for (int i = 0; i < steps.Length; i++)
            {
                float start = i * 0.11f;
                if (t >= start) sum += Marimba(t - start, BaseNote * Mathf.Pow(2f, (steps[i] + 7) / 12f)) * 0.6f;
            }
            return sum;
        }
    }
}
