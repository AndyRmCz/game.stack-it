using System;
using System.Collections.Generic;

namespace StackIt.Client
{
    /// <summary>
    /// The clip's best moment (H3). Highlights are marked during a run with a weight; the shared
    /// clip then starts just before the strongest one in the recorder's window, instead of always
    /// 15 s before the end, so viewers see the moment within the first seconds. The HUD's own
    /// banners ("Perfect ×10", "You passed @Ana!") are in the captured frames: they're the caption.
    /// </summary>
    public sealed class ClipMoments
    {
        public const float StreakWeight = 1f;    // per 5 perfects in a row
        public const float RivalWeight = 3f;     // passed a friend
        public const float AlmostPerfectWeight = 1f;
        public const float AlmostSavedWeight = 4f; // the miss by a hair that ended the run
        public const double LeadIn = 1.0;        // seconds shown before the moment
        public const double MinSeconds = 6.0;    // enough context to understand the tower

        private readonly List<(double Time, float Weight)> _moments = new List<(double, float)>();

        public void Clear() => _moments.Clear();

        public void Mark(double time, float weight)
        {
            if (weight > 0f) _moments.Add((time, weight));
        }

        /// <summary>
        /// How many seconds before <paramref name="end"/> the clip should start: from just before
        /// the best moment in the last <paramref name="maxSeconds"/> (later wins a tie), never
        /// shorter than <see cref="MinSeconds"/>; the whole window when nothing stood out.
        /// </summary>
        public double Seconds(double end, double maxSeconds)
        {
            double best = double.NaN;
            float bestWeight = 0f;
            foreach ((double time, float weight) in _moments)
            {
                if (time > end || end - time > maxSeconds - LeadIn) continue;
                if (weight >= bestWeight)
                {
                    best = time;
                    bestWeight = weight;
                }
            }
            if (double.IsNaN(best)) return maxSeconds;
            return Math.Min(maxSeconds, Math.Max(MinSeconds, end - best + LeadIn));
        }
    }
}
