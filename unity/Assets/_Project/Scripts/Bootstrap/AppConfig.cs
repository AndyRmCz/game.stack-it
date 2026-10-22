using UnityEngine;

namespace StackIt.Bootstrap
{
    /// <summary>
    /// Every environment-specific value of the client, in one asset
    /// (Assets/_Project/Resources/StackIt/AppConfig.asset, created by Stack IT ▸ Setup Project).
    /// </summary>
    [CreateAssetMenu(menuName = "Stack IT/App Config", fileName = "AppConfig")]
    public sealed class AppConfig : ScriptableObject
    {
        public const string ResourcePath = "StackIt/AppConfig";

        [Tooltip("Same value as the backend's PUBLIC_BASE_URL. API calls and challenge links use it.")]
        public string apiBaseUrl = "http://localhost:8000";

        [Tooltip("Bundle ID / package name. Applied to Player Settings by Stack IT ▸ Setup Project.")]
        public string bundleId = "com.stackit.game";

        [Tooltip("Custom URL scheme used by the landing page's 'Open in app' button.")]
        public string urlScheme = "stackit";

        public string iosAppStoreUrl = "";
        public string androidPlayStoreUrl = "";

        [Tooltip("Google OAuth *web* client ID: Android requests ID tokens for this audience.")]
        public string googleWebClientId = "";

        [Tooltip("Google OAuth iOS client ID (GIDClientID).")]
        public string googleIosClientId = "";

        public static AppConfig Load()
        {
            var config = Resources.Load<AppConfig>(ResourcePath);
            return config != null ? config : CreateInstance<AppConfig>();
        }
    }

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
