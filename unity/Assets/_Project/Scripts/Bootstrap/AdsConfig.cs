using UnityEngine;

namespace StackIt.Bootstrap
{
    /// <summary>
    /// AdMob ad unit IDs. Defaults are Google's public test units: safe to ship to testers, never
    /// earn money. Replace them before release (SETUP.md, section 5).
    /// </summary>
    [CreateAssetMenu(menuName = "Stack IT/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        public const string ResourcePath = "StackIt/AdsConfig";

        public bool adsEnabled = true;

        [Header("Android")]
        public string androidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public string androidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public string androidBanner = "ca-app-pub-3940256099942544/6300978111";

        [Header("iOS")]
        public string iosRewarded = "ca-app-pub-3940256099942544/1712485313";
        public string iosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public string iosBanner = "ca-app-pub-3940256099942544/2934735716";

        [Header("Interstitial frequency cap")]
        public int interstitialMinRuns = 3;
        public float interstitialMinSeconds = 90f;

        [Tooltip("Shows the UMP consent form as if the device were in the EEA (testing only).")]
        public bool debugForceEeaConsent = false;

        public string Rewarded => Application.platform == RuntimePlatform.IPhonePlayer ? iosRewarded : androidRewarded;
        public string Interstitial => Application.platform == RuntimePlatform.IPhonePlayer ? iosInterstitial : androidInterstitial;
        public string Banner => Application.platform == RuntimePlatform.IPhonePlayer ? iosBanner : androidBanner;

        public static AdsConfig Load()
        {
            var config = Resources.Load<AdsConfig>(ResourcePath);
            return config != null ? config : CreateInstance<AdsConfig>();
        }
    }
}
