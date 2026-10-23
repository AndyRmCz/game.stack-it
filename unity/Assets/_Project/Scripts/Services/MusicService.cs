using StackIt.Client;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Generated music (no audio files): plays <see cref="MusicPlan"/> with instruments synthesized
    /// at start-up (marimba, pad, bass, shaker, kick, tick). Notes are scheduled a little ahead on the
    /// audio clock, one 16th-note step at a time, so the tempo can change smoothly without changing
    /// pitch: calm on Home, building as the tower grows and the block narrows, slowing and fading
    /// out at game over.
    /// </summary>
    public sealed class MusicService : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float BaseNote = 392f; // G4: same key as the perfect chime
        private const double Lookahead = 0.12;
        private const int Voices = 24;
        private const float Master = 0.55f;

        private AudioSource[] _voices;
        private int _next;
        private AudioClip _marimba, _pad, _bass, _shaker, _kick, _tick;

        private MusicMood _mood = MusicMood.Home;
        private int _blocks;
        private float _width = 1f;
        private float _bpm = MusicPlan.HomeBpm;
        private bool _playing;
        private bool _enabled = true;
        private float _volume;        // current master fade 0..1
        private float _targetVolume;  // where the fade is heading
        private float _fadeSpeed = 1.5f;
        private bool _winding;        // game over: slowing down and fading out
        private double _nextStep;
        private int _step;
        private int _bar;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value) Stop();
            }
        }

        private void Awake()
        {
            _voices = new AudioSource[Voices];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _marimba = Synth("m_marimba", 1.0f, Marimba);
            _pad = Synth("m_pad", 3.8f, Pad);
            _bass = Synth("m_bass", 0.7f, Bass);
            _shaker = Synth("m_shaker", 0.09f, Shaker);
            _kick = Synth("m_kick", 0.32f, Kick);
            _tick = Synth("m_tick", 0.04f, Tick);
        }

        /// <summary>Home and the menus: the calm loop.</summary>
        public void Home() => Begin(MusicMood.Home, 0.6f);

        /// <summary>A run starts: the full loop at its starting tempo.</summary>
        public void StartRun()
        {
            _blocks = 0;
            _width = 1f;
            Begin(MusicMood.Run, 1f);
        }

        /// <summary>After each placement: height and the block's width (fraction of the starting width).</summary>
        public void Progress(int blocks, float widthFraction)
        {
            _blocks = blocks;
            _width = widthFraction;
        }

        /// <summary>Game over: the music slows down and fades out.</summary>
        public void GameOver()
        {
            if (!_playing) return;
            _winding = true;
            _targetVolume = 0f;
            _fadeSpeed = 0.7f;
        }

        public void Pause(bool paused)
        {
            if (!_playing) return;
            _targetVolume = paused ? 0.25f : 1f;
            _fadeSpeed = 3f;
        }

        public void Stop()
        {
            _playing = false;
            foreach (AudioSource v in _voices) v.Stop();
        }

        private void Begin(MusicMood mood, float volume)
        {
            if (!_enabled) return;
            _mood = mood;
            _winding = false;
            _targetVolume = volume;
            _fadeSpeed = 1.5f;
            if (_playing) return; // keep the beat running; tempo and layers follow on the next step
            _playing = true;
            _volume = 0f;
            _bpm = MusicPlan.Tempo(mood, _blocks, _width);
            _step = 0;
            _bar = 0;
            _nextStep = AudioSettings.dspTime + 0.1;
        }

        private void Update()
        {
            if (!_playing) return;
            float dt = Time.unscaledDeltaTime;
            _volume = Mathf.MoveTowards(_volume, _targetVolume, dt * _fadeSpeed);
            float target = _winding ? 56f : MusicPlan.Tempo(_mood, _blocks, _width);
            _bpm = Mathf.MoveTowards(_bpm, target, dt * (_winding ? 22f : 6f)); // ease, never jump
            if (_winding && _volume <= 0.001f)
            {
                Stop();
                return;
            }
            while (_nextStep < AudioSettings.dspTime + Lookahead)
            {
                PlayStep(_nextStep);
                _nextStep += 60.0 / _bpm / 4.0;
                if (++_step == MusicPlan.StepsPerBar)
                {
                    _step = 0;
                    _bar++;
                }
            }
        }

        private void PlayStep(double at)
        {
            MusicLayers layers = MusicPlan.Layers(_mood, _blocks, _width);
            float v = _volume * Master;
            if (_step == 0 && layers.Pad)
                foreach (int note in MusicPlan.Pad(_bar)) Schedule(_pad, note, 0.16f * v, at);
            int? arp = layers.Arp ? MusicPlan.Arp(_mood, _bar, _step) : null;
            if (arp != null) Schedule(_marimba, arp.Value, (_step % 4 == 0 ? 0.26f : 0.18f) * v, at);
            int? bass = layers.Bass ? MusicPlan.Bass(_bar, _step) : null;
            if (bass != null) Schedule(_bass, bass.Value + 24, 0.32f * v, at); // the clip is G2
            if (layers.Kick && MusicPlan.Kick(_step)) Schedule(_kick, 0, 0.42f * v, at);
            if (layers.Shaker && MusicPlan.Shaker(_step)) Schedule(_shaker, 0, (_step % 4 == 3 ? 0.1f : 0.06f) * v, at);
            if (layers.Tick) Schedule(_tick, 0, (_step % 4 == 0 ? 0.07f : 0.04f) * v, at);
        }

        private void Schedule(AudioClip clip, int semitones, float volume, double at)
        {
            if (volume <= 0.0001f) return;
            AudioSource voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            voice.clip = clip;
            voice.pitch = Mathf.Pow(2f, semitones / 12f);
            voice.volume = volume;
            voice.PlayScheduled(at);
        }

        // --- instruments (one note each at G4, or G2 for the bass; pitched by the voice) ---------

        private delegate float Wave(float t);

        private static AudioClip Synth(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        /// <summary>Soft mallet: fundamental plus a bright fourth partial that dies fast.</summary>
        private static float Marimba(float t)
        {
            float attack = Mathf.Clamp01(t / 0.004f);
            return attack * (Sin(BaseNote, t) * Mathf.Exp(-t * 5f) + 0.25f * Sin(BaseNote * 4f, t) * Mathf.Exp(-t * 22f)) * 0.8f;
        }

        /// <summary>Warm pad: slow swell, a gentle chorus of three detuned sines and an octave.</summary>
        private static float Pad(float t)
        {
            float env = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((3.8f - t) / 1.4f);
            float tone = Sin(BaseNote * 0.5f, t) + 0.6f * Sin(BaseNote * 0.5f * 1.004f, t) + 0.6f * Sin(BaseNote * 0.5f * 0.996f, t)
                + 0.25f * Sin(BaseNote, t);
            return env * tone * 0.3f;
        }

        /// <summary>Round plucked bass (G2).</summary>
        private static float Bass(float t)
        {
            float hz = BaseNote / 4f;
            float env = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t * 4f);
            return env * (Sin(hz, t) + 0.15f * Sin(hz * 3f, t)) * 0.8f;
        }

        private static readonly System.Random Noise = new System.Random(7);

        /// <summary>Short airy noise burst.</summary>
        private static float Shaker(float t)
        {
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 45f);
            return env * ((float)Noise.NextDouble() * 2f - 1f) * 0.5f;
        }

        /// <summary>Soft thump: a sine sweeping down.</summary>
        private static float Kick(float t)
        {
            float hz = Mathf.Lerp(130f, 45f, Mathf.Clamp01(t / 0.12f));
            return Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 14f);
        }

        /// <summary>Tiny high click for tension.</summary>
        private static float Tick(float t) => Sin(2400f, t) * Mathf.Exp(-t * 160f) * 0.6f;
    }
}
