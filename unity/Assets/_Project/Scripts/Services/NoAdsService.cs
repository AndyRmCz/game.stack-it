using System;
using System.Threading.Tasks;
using StackIt.Client;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Used in the editor and whenever the AdMob plugin is not installed. Rewarded ads "succeed"
    /// instantly in the editor so the continue / double-coins flows can be tested.
    /// </summary>
    public sealed class NoAdsService : IAdsService
    {
        public bool IsInitialized { get; private set; }
        public bool RewardedReady => Application.isEditor;
        public bool PrivacyOptionsRequired => false;

        public Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public event Action<AdEvent> AdEvent;

        public Task<bool> ShowRewardedAsync(string placement)
        {
            if (!Application.isEditor) return Task.FromResult(false);
            // The editor's instant "ad" goes through telemetry like a real one, so the pipeline can be tested.
            foreach (string name in new[] { TelemetryEvents.AdShown, TelemetryEvents.AdRewarded, TelemetryEvents.AdClosed })
                AdEvent?.Invoke(new AdEvent(name, AdFormats.Rewarded, placement));
            return Task.FromResult(true);
        }

        public Task ShowInterstitialAsync(string placement) => Task.CompletedTask;
        public void ShowBanner() { }
        public void HideBanner() { }
        public void ShowPrivacyOptions() { }
    }

    /// <summary>
    /// Hook for the AdMob adapter, which lives outside this assembly (Assets/_Project/AdMob) so the
    /// project compiles with or without the Google Mobile Ads plugin installed.
    /// </summary>
    public static class AdsServiceRegistry
    {
        public static Func<IAdsService> Factory;

        public static IAdsService Create() => Factory != null ? Factory() : new NoAdsService();
    }
}
