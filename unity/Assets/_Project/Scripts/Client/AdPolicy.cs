using StackIt.Client.Api;

namespace StackIt.Client
{
    /// <summary>
    /// When an interstitial may interrupt the player: never in the first session, never right
    /// after a new record, and only every few runs / minutes. Rewarded ads count as an ad break.
    /// </summary>
    public sealed class AdPolicy
    {
        private readonly int _minRunsBetween;
        private readonly double _minSecondsBetween;
        private int _runsSinceLastAd;
        private double _lastAdAt = double.NegativeInfinity;

        public AdPolicy(int minRunsBetween = 3, double minSecondsBetween = 90)
        {
            _minRunsBetween = minRunsBetween;
            _minSecondsBetween = minSecondsBetween;
        }

        public void RecordRunEnded() => _runsSinceLastAd++;

        public void RecordInterstitialShown(double nowSeconds) => ResetAt(nowSeconds);

        public void RecordRewardedShown(double nowSeconds) => ResetAt(nowSeconds);

        public bool ShouldShowInterstitial(bool isFirstSession, bool newRecord, double nowSeconds)
        {
            if (isFirstSession || newRecord) return false;
            if (_runsSinceLastAd < _minRunsBetween) return false;
            return nowSeconds - _lastAdAt >= _minSecondsBetween;
        }

        /// <summary>
        /// The results screen's "double coins" offer: a verified run that earned coins, not yet
        /// doubled, with a rewarded ad loaded. No result yet (queued offline) means no offer.
        /// </summary>
        public static bool OffersDoubleCoins(SubmitRunResponse result, bool rewardedReady) =>
            result != null && result.Status == "verified" && rewardedReady && result.CoinsEarned > 0 && !result.CoinsDoubled;

        private void ResetAt(double nowSeconds)
        {
            _runsSinceLastAd = 0;
            _lastAdAt = nowSeconds;
        }
    }
}
