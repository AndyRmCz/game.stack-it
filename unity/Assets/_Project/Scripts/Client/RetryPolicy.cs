using System;

namespace StackIt.Client
{
    /// <summary>Exponential backoff with "equal jitter" for transient API failures.</summary>
    public sealed class RetryPolicy
    {
        private readonly double _baseDelay;
        private readonly double _maxDelay;
        private readonly Func<double> _random;

        public int MaxAttempts { get; }

        public RetryPolicy(int maxAttempts = 4, double baseDelaySeconds = 0.5, double maxDelaySeconds = 8,
            Func<double> random = null)
        {
            MaxAttempts = maxAttempts;
            _baseDelay = baseDelaySeconds;
            _maxDelay = maxDelaySeconds;
            var rng = new Random();
            _random = random ?? rng.NextDouble;
        }

        public bool CanRetry(int attemptsMade) => attemptsMade < MaxAttempts;

        /// <summary>Delay before retry number <paramref name="attempt"/> (1-based): half fixed, half random.</summary>
        public double DelaySeconds(int attempt)
        {
            double exp = Math.Min(_maxDelay, _baseDelay * Math.Pow(2, Math.Max(0, attempt - 1)));
            return exp * (0.5 + 0.5 * _random());
        }

        /// <summary>Server answers worth retrying. Client errors (4xx except 408/429) are final.</summary>
        public static bool IsTransientStatus(int status) =>
            status == 408 || status == 429 || (status >= 500 && status <= 599);
    }
}
