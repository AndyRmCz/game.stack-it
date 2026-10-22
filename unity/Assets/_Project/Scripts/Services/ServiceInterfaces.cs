using System;
using System.Threading.Tasks;

namespace StackIt.Services
{
    /// <summary>Rewarded / interstitial / banner ads. NoAdsService in the editor, AdMob on device.</summary>
    public interface IAdsService
    {
        bool IsInitialized { get; }
        bool RewardedReady { get; }

        /// <summary>Consent (UMP), tracking permission (ATT) and SDK init, in that order.</summary>
        Task InitializeAsync();

        /// <summary>Shows a rewarded ad; true only if the user earned the reward.</summary>
        Task<bool> ShowRewardedAsync();

        Task ShowInterstitialAsync();
        void ShowBanner();
        void HideBanner();

        /// <summary>Re-opens the consent form (Settings ▸ Privacy choices), when required in the region.</summary>
        void ShowPrivacyOptions();
        bool PrivacyOptionsRequired { get; }
    }

    public interface IShareService
    {
        /// <summary>Opens the native share sheet with text + link and, if given, an MP4 file.</summary>
        void Share(string text, string url, string videoPath);
    }

    public interface IRecorderService
    {
        bool IsSupported { get; }
        void BeginRun();
        void EndRun();

        /// <summary>Encodes the last ~15 s plus an end card. Returns the MP4 path, or null.</summary>
        Task<string> ExportClipAsync(EndCard card);
    }

    public readonly struct EndCard
    {
        public readonly int Score;
        public readonly string Headline;
        public readonly string Code;

        public EndCard(int score, string headline, string code)
        {
            Score = score;
            Headline = headline;
            Code = code;
        }
    }

    public enum HapticKind
    {
        Light,
        Medium,
        Heavy,
        Success,
    }

    public interface IHaptics
    {
        bool Enabled { get; set; }
        void Play(HapticKind kind);
    }

    public enum SignInProvider
    {
        Google,
        Apple,
    }

    public readonly struct ProviderToken
    {
        public readonly string IdToken;
        public readonly string Nonce;

        public ProviderToken(string idToken, string nonce)
        {
            IdToken = idToken;
            Nonce = nonce;
        }
    }

    /// <summary>Native Google / Sign in with Apple. Returns an OIDC ID token for the backend.</summary>
    public interface ISignInService
    {
        bool IsAvailable(SignInProvider provider);
        Task<ProviderToken> SignInAsync(SignInProvider provider);
    }

    /// <summary>Post-processing hooks used by the hype layer (URP volume on device).</summary>
    public interface IPostFx
    {
        void ChromaticPulse(float strength);
        void SetBloom(float intensity);
    }

    public interface ILocalizer
    {
        string Language { get; }
        event Action Changed;
        string Get(string key);
        void SetLanguage(string code);
    }

    /// <summary>Thrown for API answers the UI should explain (error code from the server).</summary>
    public sealed class ApiException : Exception
    {
        public readonly int Status;
        public readonly string Code;

        public ApiException(int status, string code, string message) : base(message ?? code)
        {
            Status = status;
            Code = code;
        }

        public bool IsOffline => Status == 0;
    }
}
