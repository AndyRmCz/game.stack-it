using System;
using System.Collections.Generic;

namespace StackIt.Client.Music
{
    /// <summary>One synthesized sound: rendered once (off the main thread), then repitched per note.</summary>
    public sealed class Instrument
    {
        public readonly string Name;

        /// <summary>False for drums and textures: their notes are always played at semitone 0.</summary>
        public readonly bool Pitched;

        /// <summary>The notes sounding in the clip, in semitones above its G (a power chord is 0, 7, 12).</summary>
        public readonly int[] Intervals;

        private readonly Func<float[]> _render;

        public Instrument(string name, bool pitched, Func<float[]> render, int[] intervals = null)
        {
            Name = name;
            Pitched = pitched;
            _render = render;
            Intervals = intervals ?? new[] { 0 };
        }

        public float[] Render() => _render();
    }

    /// <summary>One note to play at a step.</summary>
    public readonly struct Note
    {
        public readonly int Instrument;

        /// <summary>Semitones above the instrument's G (−12..12).</summary>
        public readonly int Semitones;

        public readonly float Volume;

        /// <summary>Late start, as a fraction of a 16th-note step (swing, strums, flams).</summary>
        public readonly float Delay;

        /// <summary>−1 left .. 1 right.</summary>
        public readonly float Pan;

        public Note(int instrument, int semitones, float volume, float delay = 0f, float pan = 0f)
        {
            Instrument = instrument;
            Semitones = semitones;
            Volume = volume;
            Delay = delay;
            Pan = pan;
        }
    }

    /// <summary>
    /// A soundtrack: its instruments and its score, played one 16th-note step at a time. Every
    /// style follows the tower like the original music: the tempo rises as the tower grows and the
    /// block narrows, and layers join (the <see cref="MusicLayers"/> fields name the original
    /// instruments; a style reads them as base, melody, groove, bass, drive and tension). Pitched
    /// notes stay in G major / E minor, so the perfect chime (G pentatonic) is always in key.
    /// </summary>
    public abstract class MusicStyle
    {
        public const int StepsPerBar = 16;

        /// <summary>Pitch classes of G major (= E natural minor), in semitones above G.</summary>
        public static readonly int[] GMajor = { 0, 2, 4, 5, 7, 9, 11 };

        public abstract string Id { get; }

        /// <summary>Bars before the score repeats.</summary>
        public abstract int Bars { get; }

        public abstract Instrument[] Instruments { get; }

        /// <summary>Tempo at game over, where the wind-down ends.</summary>
        public virtual float EndBpm => Tempo(MusicMood.Run, 0, 1f) * 2f / 3f;

        /// <summary>
        /// Overall volume, so every soundtrack sounds about as loud as Classic on a phone (Home and
        /// mid-run are matched separately; the music tests check it).
        /// </summary>
        public virtual float Level(MusicMood mood) => 1f;

        public abstract float Tempo(MusicMood mood, int blocks, float widthFraction);

        public abstract MusicLayers Layers(MusicMood mood, int blocks, float widthFraction);

        /// <summary>Adds the notes of one step to <paramref name="into"/>.</summary>
        public abstract void Notes(MusicMood mood, MusicLayers layers, int bar, int step, List<Note> into);

        /// <summary>The usual tempo curve: up with height, up as the block narrows, capped.</summary>
        protected static float Curve(MusicMood mood, int blocks, float widthFraction, float home, float start, float tall, float thin, float max)
        {
            if (mood == MusicMood.Home) return home;
            float height = Dsp.Clamp01(blocks / (float)MusicPlan.FullHeight);
            float narrow = 1f - Dsp.Clamp01(widthFraction);
            return Math.Min(max, start + tall * height + thin * narrow);
        }

        /// <summary>
        /// Layers for the genre styles: a run starts with its beat (the genre should be heard at
        /// once, and many runs end before 25 blocks), the bass joins at 10 blocks, the full drive
        /// at 25, and the tension layer plays while the block is thin. Home is chords and melody.
        /// </summary>
        protected static MusicLayers GenreLayers(MusicMood mood, int blocks, float widthFraction)
        {
            if (mood == MusicMood.Home) return new MusicLayers(true, true, false, false, false, false);
            return new MusicLayers(
                pad: true,
                arp: true,
                shaker: true,
                bass: blocks >= 10,
                kick: blocks >= 25,
                tick: widthFraction < MusicPlan.ThinBlock);
        }

        /// <summary>All the tones of a chord, each <paramref name="strum"/> of a step after the last.</summary>
        protected static void Chord(List<Note> into, int instrument, int[] tones, float volume, float strum = 0f)
        {
            for (int i = 0; i < tones.Length; i++) into.Add(new Note(instrument, tones[i], volume, strum * i, (i - (tones.Length - 1) / 2f) * 0.25f));
        }

        /// <summary>Plays the notes of a melody bar, written as (step, semitones) pairs, that fall on this step.</summary>
        protected static void Line(List<Note> into, int instrument, (int Step, int Note)[] bar, int step, float volume, float pan = 0f)
        {
            foreach ((int s, int note) in bar)
                if (s == step) into.Add(new Note(instrument, note, volume, 0f, pan));
        }

        protected static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
