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
    }
}
