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
        Task<bool> ShowRewardedAsync(string placement);

        Task ShowInterstitialAsync(string placement);
        void ShowBanner();
        void HideBanner();

        /// <summary>Re-opens the consent form (Settings ▸ Privacy choices), when required in the region.</summary>
        void ShowPrivacyOptions();
        bool PrivacyOptionsRequired { get; }

        /// <summary>Ad lifecycle for telemetry (requested, loaded, shown, paid…), on the main thread.</summary>
        event Action<AdEvent> AdEvent;
    }

    /// <summary>One step of an ad's life. <see cref="Name"/> is a <c>TelemetryEvents.Ad*</c> name.</summary>
    public readonly struct AdEvent
    {
        public readonly string Name;
        public readonly string Format;
        public readonly string Placement;
        public readonly int? ErrorCode;
        public readonly long? LatencyMs;
        public readonly long? ValueMicros;
        public readonly string Currency;
        public readonly string Precision;
        public readonly string Source;

        public AdEvent(string name, string format, string placement = null, int? errorCode = null, long? latencyMs = null,
            long? valueMicros = null, string currency = null, string precision = null, string source = null)
        {
            Name = name;
            Format = format;
            Placement = placement;
            ErrorCode = errorCode;
            LatencyMs = latencyMs;
            ValueMicros = valueMicros;
            Currency = currency;
            Precision = precision;
            Source = source;
        }
    }

    /// <summary>Product telemetry (docs/PLAN.md, "Measurement findings"). Names: <c>TelemetryEvents</c>.</summary>
    public interface ITelemetry
    {
        void Track(string name, params (string Key, object Value)[] props);
    }

    /// <summary>Drops every event (tests, screens built without the app).</summary>
    public sealed class NullTelemetry : ITelemetry
    {
        public static readonly NullTelemetry Instance = new NullTelemetry();

        public void Track(string name, params (string Key, object Value)[] props) { }
    }

    /// <summary>Where a share goes: the system sheet, or straight into one app (H5).</summary>
    public enum ShareTarget
    {
        System,
        TikTok,
        InstagramStory,
    }

    public interface IShareService
    {
        /// <summary>Opens the native share sheet with text + link and, if given, a file (the challenge card PNG).</summary>
        void Share(string text, string url, string filePath);

        /// <summary>Shares straight into one app; falls back to the sheet when that isn't possible.</summary>
        void ShareTo(ShareTarget target, string text, string url, string filePath);
    }

    /// <summary>What the challenge card shows (drawn by UI's EndCardRenderer).</summary>
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
        /// <summary>A brief, gentle brightening of the whole frame and a chromatic pulse (0..1).</summary>
        void Pulse(float strength);

        /// <summary>The look's post-processing: bloom (intensity 0 = off) and the perfect's chromatic pulse.</summary>
        void SetLook(float bloomIntensity, float bloomThreshold, float bloomScatter, float chromaticPulse);
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
