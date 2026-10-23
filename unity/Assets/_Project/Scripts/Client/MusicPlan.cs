using System;

namespace StackIt.Client
{
    public enum MusicMood
    {
        Home, // calm: the pad and a sparse marimba, slow
        Run,  // builds with the tower and tightens as the block narrows
    }

    /// <summary>Which instruments play at the current intensity.</summary>
    public readonly struct MusicLayers
    {
        public readonly bool Pad, Arp, Shaker, Bass, Kick, Tick;

        public MusicLayers(bool pad, bool arp, bool shaker, bool bass, bool kick, bool tick)
        {
            Pad = pad;
            Arp = arp;
            Shaker = shaker;
            Bass = bass;
            Kick = kick;
            Tick = tick;
        }
    }

    /// <summary>
    /// The score of the generated music, in G major pentatonic like the perfect chime, so chimes
    /// land as part of the tune. A four-bar loop (G, Em, C, Dsus4) in 16th-note steps. Notes are
    /// semitones from G4. Tempo rises with the tower and as the block narrows; layers join as it
    /// grows; a ticking hat adds tension once the block is thin.
    /// </summary>
    public static class MusicPlan
    {
        public const float HomeBpm = 70f;
        public const float StartBpm = 84f;
        public const float MaxBpm = 120f;
        public const int StepsPerBar = 16;
        public const int Bars = 4;

        /// <summary>Tower height (blocks) at which the height part of the speed-up is complete.</summary>
        public const int FullHeight = 60;

        /// <summary>Block width (fraction of the starting width) below which the tension tick plays.</summary>
        public const float ThinBlock = 0.35f;

        /// <summary>The scale: semitone classes the melody may use (G A B D E).</summary>
        public static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };

        // Pad chords (three voices each) and the arpeggio's tones per bar (pentatonic only).
        private static readonly int[][] PadChords = { new[] { 0, 4, 7 }, new[] { -3, 0, 4 }, new[] { -7, -3, 0 }, new[] { -5, 0, 2 } };
        private static readonly int[][] ArpTones = { new[] { 0, 4, 7, 12 }, new[] { -3, 0, 4, 9 }, new[] { -3, 0, 2, 9 }, new[] { -5, 0, 2, 7 } };
        private static readonly int[] BassRoots = { -24, -27, -19, -17 };
        private static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 2, 1 };

        public static float Tempo(MusicMood mood, int blocks, float widthFraction)
        {
            if (mood == MusicMood.Home) return HomeBpm;
            float height = Clamp01(blocks / (float)FullHeight);
            float narrow = 1f - Clamp01(widthFraction);
            return Math.Min(MaxBpm, StartBpm + 24f * height + 14f * narrow);
        }

        public static MusicLayers Layers(MusicMood mood, int blocks, float widthFraction)
        {
            if (mood == MusicMood.Home) return new MusicLayers(true, true, false, false, false, false);
            return new MusicLayers(
                pad: true,
                arp: true,
                shaker: blocks >= 10,
                bass: blocks >= 25,
                kick: blocks >= 40,
                tick: widthFraction < ThinBlock);
        }

        public static int[] Pad(int bar) => PadChords[Mod(bar, Bars)];

        /// <summary>The marimba note at a step (eighth notes), or null. Home plays only every other beat.</summary>
        public static int? Arp(MusicMood mood, int bar, int step)
        {
            if (step % 2 != 0) return null;
            if (mood == MusicMood.Home && step % 8 != 0) return null;
            int[] tones = ArpTones[Mod(bar, Bars)];
            int index = ArpOrder[step / 2];
            if (Mod(bar, 2) == 1) index = tones.Length - 1 - index; // every other bar runs downwards
            return tones[index];
        }

        public static int? Bass(int bar, int step) => step == 0 || step == 6 || step == 8 ? BassRoots[Mod(bar, Bars)] : (int?)null;

        public static bool Kick(int step) => step % 4 == 0;

        public static bool Shaker(int step) => step % 2 == 1;

        public static bool InScale(int semitone) => Array.IndexOf(Pentatonic, Mod(semitone, 12)) >= 0;

        private static int Mod(int a, int n) => ((a % n) + n) % n;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
