namespace StackIt.Client.Music
{
    /// <summary>
    /// Picks which of a fixed number of voices plays the next note: a voice that has finished if
    /// there is one, otherwise the one closest to finishing. Plain round-robin cut long notes
    /// (pads, crashes) while they were still loud. Times are in seconds on the audio clock.
    /// </summary>
    public sealed class VoicePool
    {
        private readonly double[] _ends;

        public VoicePool(int voices) => _ends = new double[voices];

        public int Count => _ends.Length;

        /// <summary>When the voice's current note ends (it may be cut earlier by <see cref="Take"/>).</summary>
        public double EndOf(int voice) => _ends[voice];

        /// <summary>
        /// Reserves a voice for a note that ends at <paramref name="end"/>. <paramref name="from"/>
        /// is when the voice is taken over: scheduling a note on a playing voice stops it at once,
        /// so that's the scheduling time, a little before the note starts.
        /// </summary>
        public int Take(double from, double end)
        {
            int best = 0;
            for (int i = 0; i < _ends.Length; i++)
            {
                if (_ends[i] <= from)
                {
                    best = i;
                    break;
                }
                if (_ends[i] < _ends[best]) best = i;
            }
            _ends[best] = end;
            return best;
        }

        public void Clear()
        {
            for (int i = 0; i < _ends.Length; i++) _ends[i] = 0;
        }
    }
}
