using System;
using System.Collections.Generic;
using System.Text;

namespace StackIt.Client
{
    /// <summary>How one block landed, for the shareable result grid.</summary>
    public enum BlockOutcome
    {
        Perfect,
        Clean,  // a sliver sliced off (under 10 % of the width)
        Sliced,
    }

    /// <summary>
    /// The Daily Tower (H1): its number, from the same epoch as the backend's
    /// app/domain/daily.py (2026-10-24 is Daily #1, days counted in UTC).
    /// </summary>
    public static class DailyTower
    {
        public static readonly DateTime Epoch = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

        public static int Number(DateTime utcNow) => (int)(utcNow.Date - Epoch.Date).TotalDays + 1;
    }

    /// <summary>
    /// The spoiler-free result players paste into chats (H2), Wordle-style: a title line, then one
    /// emoji per block, ten to a row: purple perfect, blue clean, orange sliced.
    /// </summary>
    public static class ResultText
    {
        public const string Perfect = "\U0001F7EA";
        public const string Clean = "\U0001F7E6";
        public const string Sliced = "\U0001F7E7";
        public const int PerRow = 10;
        public const int MaxRows = 6;

        public static BlockOutcome Classify(bool perfect, int widthBefore, int widthAfter)
        {
            if (perfect) return BlockOutcome.Perfect;
            return widthBefore - widthAfter < widthBefore / 10 ? BlockOutcome.Clean : BlockOutcome.Sliced;
        }

        /// <param name="dailyNumber">The Daily Tower's number, or 0 for a normal run.</param>
        public static string Build(int dailyNumber, int score, IReadOnlyList<BlockOutcome> blocks)
        {
            var text = new StringBuilder("Milli");
            if (dailyNumber > 0) text.Append(" · Daily #").Append(dailyNumber);
            text.Append(" · ").Append(score);
            int shown = Math.Min(blocks.Count, PerRow * MaxRows);
            for (int i = 0; i < shown; i++)
            {
                if (i % PerRow == 0) text.Append('\n');
                text.Append(blocks[i] == BlockOutcome.Perfect ? Perfect : blocks[i] == BlockOutcome.Clean ? Clean : Sliced);
            }
            if (blocks.Count > shown) text.Append("\n+").Append(blocks.Count - shown);
            return text.ToString();
        }
    }
}
