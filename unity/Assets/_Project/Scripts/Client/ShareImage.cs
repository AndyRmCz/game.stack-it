using System;
using System.Linq;

namespace StackIt.Client
{
    /// <summary>
    /// The challenge is shared as a still of the end card (a PNG), not a video: it is ready in a
    /// frame and costs nothing during play. Each share gets a fresh name, so a chat app never
    /// shows a cached card from an earlier run; older ones are deleted by <see cref="Pattern"/>.
    /// </summary>
    public static class ShareImage
    {
        public const string Prefix = "milli-";
        public const string Pattern = Prefix + "*.png";

        public static string FileName(string code, DateTime utc)
        {
            string safe = new string((code ?? "").Where(c => c < 128 && char.IsLetterOrDigit(c)).ToArray());
            return $"{Prefix}{safe}-{utc:yyyyMMdd-HHmmss}.png";
        }

        /// <summary>
        /// The card's little tower: up to <paramref name="count"/> block heights spread evenly from
        /// the base (0) to the score, so each block wears the colour the run had at that height.
        /// </summary>
        public static int[] TowerBlocks(int score, int count)
        {
            score = Math.Max(0, score);
            int n = Math.Max(1, Math.Min(count, score + 1));
            if (n == 1) return new[] { 0 };
            return Enumerable.Range(0, n)
                .Select(i => (int)Math.Round(i * (double)score / (n - 1), MidpointRounding.AwayFromZero))
                .ToArray();
        }
    }
}
