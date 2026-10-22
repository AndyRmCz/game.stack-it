using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Synthesized sound effects (no audio assets): a pluck for placements, a bell whose pitch
    /// climbs a semitone per consecutive perfect, a low thud for misses and a short arpeggio for
    /// a new record.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int MaxComboSemitones = 14;

        private AudioSource[] _voices;
        private int _next;
        private AudioClip _place;
        private AudioClip _perfect;
        private AudioClip _miss;
        private AudioClip _click;
        private AudioClip _record;

        public bool Enabled { get; set; } = true;

        private void Awake()
        {
            _voices = new AudioSource[6];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _place = Synth("place", 0.18f, (t, f) => Pluck(t, 196f) * 0.55f);
            _perfect = Synth("perfect", 0.6f, (t, f) => Bell(t, 523.25f) * 0.5f);
            _miss = Synth("miss", 0.45f, (t, f) => Thud(t) * 0.8f);
            _click = Synth("click", 0.05f, (t, f) => Mathf.Sin(2 * Mathf.PI * 1200f * t) * (1 - f) * 0.25f);
            _record = Synth("record", 0.9f, (t, f) => Arpeggio(t) * 0.45f);
        }

        public void Place() => Play(_place, Random.Range(0.96f, 1.04f));

        public void Perfect(int combo) =>
            Play(_perfect, Mathf.Pow(2f, Mathf.Min(combo - 1, MaxComboSemitones) / 12f));

        public void Miss() => Play(_miss, 1f);
        public void Click() => Play(_click, 1f);
        public void Record() => Play(_record, 1f);

        private void Play(AudioClip clip, float pitch)
        {
            if (!Enabled || clip == null) return;
            AudioSource voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip);
        }

        private delegate float Wave(float time, float fraction);

        private static AudioClip Synth(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(wave(t, i / (float)count), -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Pluck(float t, float freq)
        {
            float env = Mathf.Exp(-t * 22f);
            return (Mathf.Sin(2 * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(4 * Mathf.PI * freq * t)) * env;
        }

        private static float Bell(float t, float freq)
        {
            float env = Mathf.Exp(-t * 6f);
            return (Mathf.Sin(2 * Mathf.PI * freq * t)
                    + 0.5f * Mathf.Sin(2 * Mathf.PI * freq * 2.01f * t)
                    + 0.25f * Mathf.Sin(2 * Mathf.PI * freq * 3.02f * t)) * env * 0.6f;
        }

        private static float Thud(float t)
        {
            float freq = Mathf.Lerp(110f, 40f, Mathf.Clamp01(t * 3f));
            return Mathf.Sin(2 * Mathf.PI * freq * t) * Mathf.Exp(-t * 7f);
        }

        private static float Arpeggio(float t)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            int i = Mathf.Min((int)(t / 0.12f), notes.Length - 1);
            float local = t - i * 0.12f;
            return Bell(local, notes[i]);
        }
    }
}
