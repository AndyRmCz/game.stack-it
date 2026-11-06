using System;
using System.Collections.Generic;
using System.Linq;

namespace StackIt.Client
{
    /// <summary>What just happened, as Blocky sees it: each moment is a mood and a few lines to say.</summary>
    public enum BlockyMoment
    {
        Ordinary,
        GoodRun,
        NewBest,
        NearBest,
        EarlyOut,
        DuelWon,
        DuelLost,
        ContinueOffer,
        Greeting,
        Returned,
        Melted,
        Reward,
        Tickled,
        Noticed,
        Warned,
        Offended,
    }

    /// <summary>
    /// Blocky's personality: a tiny, proud, dramatic foreman who takes the tower personally. Joy
    /// is kept for moments that earn it (a best, a great run, the day's first visit, coins, the
    /// melt after a sulk), so it still means something when it shows up.
    /// </summary>
    public static class BlockyMoments
    {
        public const int EarlyOutScore = 3;
        public const int SulkAfterDays = 3;
        public const int LongestLine = 44; // the bubble's two lines
        private const int GoodRunFloor = 10; // below this a best is too small to be near
        private const int OrdinarySpeaksEvery = 3; // a comment on every run would wear thin

        private static readonly Dictionary<BlockyMoment, (MascotMood Mood, string Key, int Lines)> Table =
            new Dictionary<BlockyMoment, (MascotMood, string, int)>
            {
                [BlockyMoment.Ordinary] = (MascotMood.Happy, "ordinary", 3),
                [BlockyMoment.GoodRun] = (MascotMood.Joy, "good_run", 3),
                [BlockyMoment.NewBest] = (MascotMood.Cheer, "new_best", 3),
                [BlockyMoment.NearBest] = (MascotMood.Agony, "near_best", 2),
                [BlockyMoment.EarlyOut] = (MascotMood.Shocked, "early_out", 2),
                [BlockyMoment.DuelWon] = (MascotMood.Smug, "duel_won", 2),
                [BlockyMoment.DuelLost] = (MascotMood.Rally, "duel_lost", 2),
                [BlockyMoment.ContinueOffer] = (MascotMood.Rally, "continue", 2),
                [BlockyMoment.Greeting] = (MascotMood.Joy, "greeting", 3),
                [BlockyMoment.Returned] = (MascotMood.Sulking, "returned", 2),
                [BlockyMoment.Melted] = (MascotMood.Joy, "melted", 2),
                [BlockyMoment.Reward] = (MascotMood.Joy, "reward", 2),
                [BlockyMoment.Tickled] = (MascotMood.Joy, "tickled", 2),
                [BlockyMoment.Noticed] = (MascotMood.Smug, "noticed", 1),
                [BlockyMoment.Warned] = (MascotMood.Rally, "warned", 1),
                [BlockyMoment.Offended] = (MascotMood.Sulking, "offended", 2),
            };

        public static IEnumerable<BlockyMoment> All => Enum.GetValues(typeof(BlockyMoment)).Cast<BlockyMoment>();

        public static MascotMood Mood(BlockyMoment moment) => Table[moment].Mood;

        public static int LineCount(BlockyMoment moment) => Table[moment].Lines;

        /// <param name="turn">Any counter: lines take turns as it grows.</param>
        public static string LineKey(BlockyMoment moment, int turn)
        {
            (MascotMood _, string key, int lines) = Table[moment];
            return "blocky." + key + "." + Math.Abs(turn % lines);
        }

        public static bool Speaks(BlockyMoment moment, int roll) =>
            moment != BlockyMoment.Ordinary || roll % OrdinarySpeaksEvery == 0;

        /// <param name="previousBest">The best before this run.</param>
        /// <param name="duelWon">Null outside a challenge run.</param>
        public static BlockyMoment ForResult(int score, int previousBest, bool newRecord, bool? duelWon)
        {
            if (duelWon == false) return BlockyMoment.DuelLost;
            if (newRecord) return BlockyMoment.NewBest;
            if (duelWon == true) return BlockyMoment.DuelWon;
            if (previousBest - score == 1) return BlockyMoment.NearBest;
            if (score <= EarlyOutScore) return BlockyMoment.EarlyOut;
            if (previousBest >= GoodRunFloor && score * 4 >= previousBest * 3) return BlockyMoment.GoodRun;
            return BlockyMoment.Ordinary;
        }

        /// <param name="daysAway">Since the last visit; null on the very first one. Null result: nothing to say.</param>
        public static BlockyMoment? ForHome(int? daysAway)
        {
            if (daysAway == 0) return null;
            return daysAway >= SulkAfterDays ? BlockyMoment.Returned : BlockyMoment.Greeting;
        }

        /// <summary>What comes right after: a sulk never lasts, Blocky is too glad you're back.</summary>
        public static BlockyMoment? Followup(BlockyMoment moment) =>
            moment == BlockyMoment.Returned ? BlockyMoment.Melted : (BlockyMoment?)null;
    }

    /// <summary>Tapping Blocky: a giggle first, then it wears thin. A pause forgives everything.</summary>
    public sealed class BlockyPokes
    {
        public const float ForgetSeconds = 6f;

        private static readonly BlockyMoment[] Steps =
        {
            BlockyMoment.Tickled, BlockyMoment.Noticed, BlockyMoment.Warned, BlockyMoment.Offended,
        };

        private int _taps;
        private float _last = float.NegativeInfinity;

        /// <param name="now">Seconds, on any clock.</param>
        public BlockyMoment Poke(float now)
        {
            if (now - _last > ForgetSeconds) _taps = 0;
            _last = now;
            return Steps[Math.Min(_taps++, Steps.Length - 1)];
        }
    }
}
