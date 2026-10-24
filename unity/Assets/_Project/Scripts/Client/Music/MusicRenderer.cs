using System;
using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>Where the game is while a bar plays.</summary>
    public readonly struct MusicState
    {
        public readonly MusicMood Mood;
        public readonly int Blocks;
        public readonly float Width;

        public MusicState(MusicMood mood, int blocks, float width)
        {
            Mood = mood;
            Blocks = blocks;
            Width = width;
        }
    }

    public struct RenderStats
    {
        public float Peak;
        public float Rms;

        /// <summary>RMS without the bass below ~120 Hz, which phone speakers can't play: roughly how loud it sounds on a phone.</summary>
        public float PhoneRms;
        public int Notes;

        /// <summary>Notes cut off while still clearly audible because the voice pool ran out.</summary>
        public int LoudSteals;
    }

    /// <summary>
    /// Plays a style offline into a stereo buffer the way MusicService plays it in the game: the
    /// same steps, tempo, volumes and 24-voice <see cref="VoicePool"/> (a reused voice stops when
    /// its next note is scheduled, up to the lookahead before that note starts). Used by the tests (loudness, voice stealing) and by the preview
    /// tool that writes WAV files to listen to.
    /// </summary>
    public sealed class MusicRenderer
    {
        public const int Voices = 24;
        public const float Master = 0.55f;
        public const double Lookahead = 0.12;
        public const float HomeVolume = 0.6f;
        private const float LoudCut = 0.02f;
        private const float TailSeconds = 4f;

        private readonly MusicStyle _style;
        private readonly float[][] _clips;

        public MusicRenderer(MusicStyle style)
        {
            _style = style;
            _clips = new float[style.Instruments.Length][];
            for (int i = 0; i < _clips.Length; i++) _clips[i] = style.Instruments[i].Render();
        }

        public static float MoodVolume(MusicMood mood) => mood == MusicMood.Home ? HomeVolume : 1f;

        private sealed class Playback
        {
            public int Clip;
            public long Start;
            public long End = long.MaxValue;
            public float Pitch;
            public float Left;
            public float Right;
        }

        /// <summary>Renders <paramref name="bars"/> bars; <paramref name="stateAtBar"/> says where the game is at each bar.</summary>
        public float[] Render(int bars, Func<int, MusicState> stateAtBar, out RenderStats stats)
        {
            stats = default;
            var plays = new List<Playback>();
            var slots = new Playback[Voices];
            var pool = new VoicePool(Voices);
            var notes = new List<Note>();
            double t = 0;
            for (int bar = 0; bar < bars; bar++)
            {
                MusicState s = stateAtBar(bar);
                MusicLayers layers = _style.Layers(s.Mood, s.Blocks, s.Width);
                double stepSeconds = 60.0 / _style.Tempo(s.Mood, s.Blocks, s.Width) / 4.0;
                float gain = Master * MoodVolume(s.Mood) * _style.Level(s.Mood);
                for (int step = 0; step < MusicStyle.StepsPerBar; step++)
                {
                    notes.Clear();
                    _style.Notes(s.Mood, layers, bar, step, notes);
                    foreach (Note note in notes)
                    {
                        if (note.Volume <= 0.0001f) continue;
                        double start = t + note.Delay * stepSeconds;
                        float pitch = Dsp.Ratio(note.Semitones);
                        var play = new Playback
                        {
                            Clip = note.Instrument,
                            Start = (long)(start * Dsp.SampleRate),
                            Pitch = pitch,
                            Left = note.Volume * gain * Math.Min(1f, 1f - note.Pan),
                            Right = note.Volume * gain * Math.Min(1f, 1f + note.Pan),
                        };
                        double from = start - Lookahead; // the game schedules up to this much ahead
                        int voice = pool.Take(from, start + _clips[note.Instrument].Length / (double)pitch / Dsp.SampleRate);
                        Playback previous = slots[voice];
                        if (previous != null)
                        {
                            long cut = Math.Max(previous.Start, (long)(from * Dsp.SampleRate));
                            if (cut < previous.End)
                            {
                                previous.End = cut;
                                if (LevelAt(previous, cut) > LoudCut) stats.LoudSteals++;
                            }
                        }
                        slots[voice] = play;
                        plays.Add(play);
                        stats.Notes++;
                    }
                    t += stepSeconds;
                }
            }

            long length = (long)((t + TailSeconds) * Dsp.SampleRate);
            var mix = new float[length * 2];
            foreach (Playback p in plays) Add(mix, p, length);

            double sum = 0;
            foreach (float v in mix)
            {
                stats.Peak = Math.Max(stats.Peak, Math.Abs(v));
                sum += v * v;
            }
            stats.Rms = (float)Math.Sqrt(sum / mix.Length);

            var mono = new float[length];
            for (long n = 0; n < length; n++) mono[n] = 0.5f * (mix[n * 2] + mix[n * 2 + 1]);
            Dsp.Highpass(mono, 120f);
            Dsp.Highpass(mono, 120f);
            double phone = 0;
            foreach (float v in mono) phone += v * v;
            stats.PhoneRms = (float)Math.Sqrt(phone / mono.Length);
            return mix;
        }

        /// <summary>Loudest sample (with its volume) in the 10 ms after <paramref name="at"/>, or 0 if the note has ended.</summary>
        private float LevelAt(Playback p, long at)
        {
            float[] clip = _clips[p.Clip];
            float max = 0f;
            for (int i = 0; i < Dsp.SampleRate / 100; i++)
            {
                double pos = (at - p.Start + i) * (double)p.Pitch;
                if (pos >= clip.Length) break;
                max = Math.Max(max, Math.Abs(clip[(int)pos]));
            }
            return max * Math.Max(p.Left, p.Right);
        }

        private void Add(float[] mix, Playback p, long length)
        {
            float[] clip = _clips[p.Clip];
            long end = Math.Min(Math.Min(p.End, length), p.Start + (long)Math.Ceiling(clip.Length / p.Pitch));
            for (long n = p.Start; n < end; n++)
            {
                double pos = (n - p.Start) * (double)p.Pitch;
                int a = (int)pos;
                if (a >= clip.Length) break;
                float frac = (float)(pos - a);
                float v = clip[a] + ((a + 1 < clip.Length ? clip[a + 1] : 0f) - clip[a]) * frac;
                mix[n * 2] += v * p.Left;
                mix[n * 2 + 1] += v * p.Right;
            }
        }
    }
}
