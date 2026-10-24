using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackIt.Client;
using StackIt.Client.Music;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Generated music (no audio files): plays a <see cref="MusicStyle"/> (Classic or a soundtrack
    /// from the shop) whose instruments are synthesized in plain C#. A new style's instruments are
    /// rendered off the main thread and take over at the next bar. Notes are scheduled a little
    /// ahead on the audio clock, one 16th-note step at a time, so the tempo can change smoothly
    /// without changing pitch: calm on Home, building as the tower grows and the block narrows,
    /// slowing and fading out at game over. The shop can audition a style for a few seconds.
    /// </summary>
    public sealed class MusicService : MonoBehaviour
    {
        private const float Master = MusicRenderer.Master;
        private const double Lookahead = MusicRenderer.Lookahead;
        private const int AuditionBlocks = 30; // a shop preview plays the style mid-run, with its beat and bass

        private AudioSource[] _voices;
        private VoicePool _pool;
        private readonly List<Note> _notes = new List<Note>();

        private MusicStyle _style;
        private AudioClip[] _clips;
        private MusicStyle _loading;              // being synthesized
        private Task<float[][]> _render;
        private MusicStyle _ready;                // synthesized, waiting for the next bar
        private AudioClip[] _readyClips;
        private bool _swapNow;

        private string _equipped = MusicStyles.Default;
        private string _audition;                 // the style being previewed, or null
        private float _auditionSeconds;
        private float _auditionEnds = float.MaxValue;

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
                if (!value && _audition == null) Stop();
            }
        }

        /// <summary>The style the player has on (a preview or a one-run trial may be playing instead).</summary>
        public string Equipped => _equipped;

        /// <summary>The style playing now, or about to (once its instruments are synthesized).</summary>
        public string StyleId => (_loading ?? _ready ?? _style).Id;

        private void Awake()
        {
            _voices = new AudioSource[MusicRenderer.Voices];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _pool = new VoicePool(_voices.Length);
            _style = new ClassicStyle(); // small: synthesized right away, so music can start at once
            _clips = Clips(_style, Render(_style));
        }

        private void OnDestroy() => Release(_clips);

        /// <summary>The player's soundtrack. Takes over at the next bar once its instruments are ready.</summary>
        public void Equip(string id)
        {
            _equipped = MusicStyles.Create(id).Id;
            if (_audition == null) Load(_equipped);
        }

        /// <summary>Plays another style instead of the equipped one (a one-run trial); null goes back.</summary>
        public void Override(string id)
        {
            if (_audition == null) Load(id ?? _equipped);
        }

        /// <summary>A shop preview: the style mid-run for <paramref name="seconds"/>, then back to what was playing.</summary>
        public void Audition(string id, float seconds)
        {
            _audition = MusicStyles.Create(id).Id;
            _auditionSeconds = seconds;
            _auditionEnds = float.MaxValue; // the clock starts when the style actually plays
            _swapNow = true;
            Load(_audition);
            if (!_playing) Begin(MusicMood.Home, 0.6f, force: true);
            _targetVolume = 1f;
        }

        /// <summary>Ends a preview early (leaving the shop).</summary>
        public void EndAudition()
        {
            if (_audition == null) return;
            _audition = null;
            _auditionEnds = float.MaxValue;
            _swapNow = true;
            Load(_equipped);
            if (!_enabled) Stop();
            else _targetVolume = _mood == MusicMood.Home ? 0.6f : 1f;
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
            _pool.Clear();
        }

        private void Begin(MusicMood mood, float volume, bool force = false)
        {
            if (!_enabled && !force) return;
            _mood = mood;
            _winding = false;
            _targetVolume = volume;
            _fadeSpeed = 1.5f;
            if (_playing) return; // keep the beat running; tempo and layers follow on the next step
            _playing = true;
            _volume = 0f;
            _bpm = _style.Tempo(mood, _blocks, _width);
            _step = 0;
            _bar = 0;
            _nextStep = AudioSettings.dspTime + 0.1;
        }

        // --- styles ------------------------------------------------------------------------------

        private void Load(string id)
        {
            MusicStyle target = _loading ?? _ready ?? _style;
            if (target.Id == id) return;
            if (_ready != null)
            {
                Release(_readyClips);
                _ready = null;
                _readyClips = null;
            }
            if (_style.Id == id)
            {
                _loading = null;
                _render = null;
                return;
            }
            MusicStyle style = MusicStyles.Create(id);
            _loading = style;
            _render = Task.Run(() => Render(style));
        }

        private static float[][] Render(MusicStyle style) => style.Instruments.Select(i => i.Render()).ToArray();

        private static AudioClip[] Clips(MusicStyle style, float[][] data)
        {
            var clips = new AudioClip[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                clips[i] = AudioClip.Create(style.Instruments[i].Name, data[i].Length, 1, Dsp.SampleRate, false);
                clips[i].SetData(data[i], 0);
            }
            return clips;
        }

        private static void Release(AudioClip[] clips)
        {
            if (clips == null) return;
            foreach (AudioClip clip in clips) Destroy(clip, 6f); // after any note still ringing on it
        }

        /// <summary>Collects a finished render; the new style starts at the next bar (or now, for a preview).</summary>
        private void PollRender()
        {
            if (_render == null || !_render.IsCompleted) return;
            Task<float[][]> render = _render;
            MusicStyle style = _loading;
            _render = null;
            _loading = null;
            if (render.IsFaulted)
            {
                Debug.LogException(render.Exception);
                return;
            }
            _ready = style;
            _readyClips = Clips(style, render.Result);
        }

        private void Swap()
        {
            Release(_clips);
            _style = _ready;
            _clips = _readyClips;
            _ready = null;
            _readyClips = null;
            _swapNow = false;
            _step = 0;
            _bar = 0;
            if (_audition != null && _style.Id == _audition) _auditionEnds = Time.unscaledTime + _auditionSeconds;
        }

        // --- playback ----------------------------------------------------------------------------

        private void Update()
        {
            PollRender();
            if (_ready != null && (!_playing || _swapNow)) Swap();
            if (_audition != null && Time.unscaledTime >= _auditionEnds) EndAudition();
            if (!_playing) return;

            MusicState s = Current();
            float dt = Time.unscaledDeltaTime;
            _volume = Mathf.MoveTowards(_volume, _targetVolume, dt * _fadeSpeed);
            float target = _winding ? _style.EndBpm : _style.Tempo(s.Mood, s.Blocks, s.Width);
            _bpm = Mathf.MoveTowards(_bpm, target, dt * (_winding ? 22f : 6f)); // ease, never jump
            if (_winding && _volume <= 0.001f)
            {
                Stop();
                return;
            }
            while (_nextStep < AudioSettings.dspTime + Lookahead)
            {
                if (_step == 0 && _ready != null) Swap(); // a new soundtrack starts on a bar line
                PlayStep(_nextStep);
                _nextStep += 60.0 / _bpm / 4.0;
                if (++_step == MusicStyle.StepsPerBar)
                {
                    _step = 0;
                    _bar++;
                }
            }
        }

        private MusicState Current() =>
            _audition != null && _style.Id == _audition && !_winding
                ? new MusicState(MusicMood.Run, AuditionBlocks, 1f)
                : new MusicState(_mood, _blocks, _width);

        private void PlayStep(double at)
        {
            MusicState s = Current();
            MusicLayers layers = _style.Layers(s.Mood, s.Blocks, s.Width);
            float v = _volume * Master * _style.Level(s.Mood);
            double stepSeconds = 60.0 / _bpm / 4.0;
            _notes.Clear();
            _style.Notes(s.Mood, layers, _bar, _step, _notes);
            foreach (Note note in _notes) Schedule(note, note.Volume * v, at + note.Delay * stepSeconds);
        }

        private void Schedule(Note note, float volume, double at)
        {
            if (volume <= 0.0001f) return;
            AudioClip clip = _clips[note.Instrument];
            float pitch = Dsp.Ratio(note.Semitones);
            int index = _pool.Take(AudioSettings.dspTime, at + clip.length / pitch);
            AudioSource voice = _voices[index];
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.panStereo = note.Pan;
            voice.PlayScheduled(at);
        }
    }
}
