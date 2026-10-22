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
}
