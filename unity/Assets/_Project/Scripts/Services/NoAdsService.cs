using System;
using System.Threading.Tasks;
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

        public Task<bool> ShowRewardedAsync() => Task.FromResult(Application.isEditor);
        public Task ShowInterstitialAsync() => Task.CompletedTask;
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
