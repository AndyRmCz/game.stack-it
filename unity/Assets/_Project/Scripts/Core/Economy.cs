using System;

namespace StackIt.Core
{
    /// <summary>Mirrors backend/app/domain/economy.py so the game-over screen can show coins instantly.</summary>
    public static class Economy
    {
        public const int CoinsPerScoreDivisor = 5;

        public static int CoinsForRun(int score) => Math.Max(score, 0) / CoinsPerScoreDivisor;
    }
}
