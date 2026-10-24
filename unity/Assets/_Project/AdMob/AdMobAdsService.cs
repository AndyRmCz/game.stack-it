// AdMob adapter. Lives outside every .asmdef (compiled into Assembly-CSharp) so it can see the
// Google Mobile Ads plugin however that plugin is packaged, and so the rest of the project builds
// without it. Stack IT ▸ Setup Project adds the STACKIT_ADMOB define once the plugin is installed.
#if STACKIT_ADMOB
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using StackIt.Bootstrap;
using StackIt.Client;
using StackIt.Services;
using Debug = UnityEngine.Debug;
using UnityEngine;
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace StackIt.AdMob
{
    public sealed class AdMobAdsService : IAdsService
    {
        private readonly AdsConfig _config;
        private RewardedAd _rewarded;
        private InterstitialAd _interstitial;
        private BannerView _banner;
        private bool _bannerWanted;

        public bool IsInitialized { get; private set; }
        public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();

        public event Action<AdEvent> AdEvent;

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public AdMobAdsService(AdsConfig config)
        {
            _config = config;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            AdsServiceRegistry.Factory = () => new AdMobAdsService(AdsConfig.Load());
        }

        /// <summary>Order required by Google and Apple: UMP consent → ATT prompt → SDK init.</summary>
        public async Task InitializeAsync()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            await GatherConsentAsync();
            await RequestTrackingAuthorizationAsync();
            if (!ConsentInformation.CanRequestAds()) return;

            var done = new TaskCompletionSource<bool>();
            MobileAds.Initialize(_ => done.TrySetResult(true));
            await done.Task;
            IsInitialized = true;
            LoadRewarded();
            LoadInterstitial();
            if (_bannerWanted) ShowBanner();
        }

        private Task GatherConsentAsync()
        {
            var done = new TaskCompletionSource<bool>();
            var request = new ConsentRequestParameters();
            if (_config.debugForceEeaConsent)
            {
                request.ConsentDebugSettings = new ConsentDebugSettings { DebugGeography = DebugGeography.EEA };
            }
            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning("UMP update failed: " + updateError.Message);
                    done.TrySetResult(false);
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("UMP form failed: " + formError.Message);
                    done.TrySetResult(true);
                });
            });
            return done.Task;
        }

        private static async Task RequestTrackingAuthorizationAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() !=
                ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED) return;
            ATTrackingStatusBinding.RequestAuthorizationTracking();
            // The binding has no completion callback: wait until the user answers (max ~60 s).
            for (int i = 0; i < 600; i++)
            {
                await Task.Delay(100);
                if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() !=
                    ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED) return;
            }
#else
            await Task.CompletedTask;
#endif
        }

        public Task<bool> ShowRewardedAsync(string placement)
        {
            const string format = AdFormats.Rewarded;
            var result = new TaskCompletionSource<bool>();
            if (!RewardedReady)
            {
                Raise(TelemetryEvents.AdShowFailed, format, placement, NotReady);
                LoadRewarded();
                result.SetResult(false);
                return result.Task;
            }
            bool earned = false;
            RewardedAd ad = _rewarded;
            _rewarded = null;
            ad.OnAdImpressionRecorded += () => Raise(TelemetryEvents.AdShown, format, placement);
            ad.OnAdClicked += () => Raise(TelemetryEvents.AdClicked, format, placement);
            ad.OnAdPaid += value => Paid(format, placement, value, ad.GetResponseInfo());
            ad.OnAdFullScreenContentClosed += () =>
            {
                if (earned) Raise(TelemetryEvents.AdRewarded, format, placement);
                Raise(TelemetryEvents.AdClosed, format, placement);
                ad.Destroy();
                LoadRewarded();
                result.TrySetResult(earned);
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Raise(TelemetryEvents.AdShowFailed, format, placement, error?.GetCode());
                ad.Destroy();
                LoadRewarded();
                result.TrySetResult(false);
            };
            ad.Show(_ => earned = true);
            return result.Task;
        }

        public Task ShowInterstitialAsync(string placement)
        {
            const string format = AdFormats.Interstitial;
            var result = new TaskCompletionSource<bool>();
            if (_interstitial == null || !_interstitial.CanShowAd())
            {
                // A break where an ad was due but none was loaded: lost revenue worth knowing about.
                Raise(TelemetryEvents.AdShowFailed, format, placement, NotReady);
                LoadInterstitial();
                result.SetResult(false);
                return result.Task;
            }
            InterstitialAd ad = _interstitial;
            _interstitial = null;
            ad.OnAdImpressionRecorded += () => Raise(TelemetryEvents.AdShown, format, placement);
            ad.OnAdClicked += () => Raise(TelemetryEvents.AdClicked, format, placement);
            ad.OnAdPaid += value => Paid(format, placement, value, ad.GetResponseInfo());
            ad.OnAdFullScreenContentClosed += () =>
            {
                Raise(TelemetryEvents.AdClosed, format, placement);
                ad.Destroy();
                LoadInterstitial();
                result.TrySetResult(true);
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Raise(TelemetryEvents.AdShowFailed, format, placement, error?.GetCode());
                ad.Destroy();
                LoadInterstitial();
                result.TrySetResult(false);
            };
            ad.Show();
            return result.Task;
        }

        /// <summary>Banners appear on menus only; the router hides them for gameplay screens.</summary>
        public void ShowBanner()
        {
            _bannerWanted = true;
            if (!IsInitialized) return;
            if (_banner == null)
            {
                const string format = AdFormats.Banner;
                BannerView banner = _banner = new BannerView(_config.Banner, AdSize.Banner, AdPosition.Bottom);
                var clock = Stopwatch.StartNew();
                // Banners refresh by themselves: later loads report ad_loaded without a new ad_requested.
                banner.OnBannerAdLoaded += () => Raise(TelemetryEvents.AdLoaded, format, latencyMs: clock.ElapsedMilliseconds);
                banner.OnBannerAdLoadFailed += error => Raise(TelemetryEvents.AdLoadFailed, format, errorCode: error?.GetCode());
                banner.OnAdImpressionRecorded += () => Raise(TelemetryEvents.AdShown, format, AdPlacements.Menu);
                banner.OnAdClicked += () => Raise(TelemetryEvents.AdClicked, format, AdPlacements.Menu);
                banner.OnAdPaid += value => Paid(format, AdPlacements.Menu, value, banner.GetResponseInfo());
                Raise(TelemetryEvents.AdRequested, format);
                banner.LoadAd(new AdRequest());
            }
            _banner.Show();
        }

        public void HideBanner()
        {
            _bannerWanted = false;
            _banner?.Hide();
        }

        public void ShowPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning("Privacy options form failed: " + error.Message);
            });
        }

        private void LoadRewarded()
        {
            if (!IsInitialized || _rewarded != null) return;
            const string format = AdFormats.Rewarded;
            Raise(TelemetryEvents.AdRequested, format);
            var clock = Stopwatch.StartNew();
            RewardedAd.Load(_config.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.Log("Rewarded load failed: " + error?.GetMessage());
                    Raise(TelemetryEvents.AdLoadFailed, format, errorCode: error?.GetCode());
                    return;
                }
                Raise(TelemetryEvents.AdLoaded, format, latencyMs: clock.ElapsedMilliseconds);
                _rewarded = ad;
            });
        }

        private void LoadInterstitial()
        {
            if (!IsInitialized || _interstitial != null) return;
            const string format = AdFormats.Interstitial;
            Raise(TelemetryEvents.AdRequested, format);
            var clock = Stopwatch.StartNew();
            InterstitialAd.Load(_config.Interstitial, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.Log("Interstitial load failed: " + error?.GetMessage());
                    Raise(TelemetryEvents.AdLoadFailed, format, errorCode: error?.GetCode());
                    return;
                }
                Raise(TelemetryEvents.AdLoaded, format, latencyMs: clock.ElapsedMilliseconds);
                _interstitial = ad;
            });
        }

        // --- telemetry ---------------------------------------------------------------------------

        /// <summary>Error code for "asked to show, but nothing was loaded" (not an SDK code).</summary>
        private const int NotReady = -1;

        private void Raise(string name, string format, string placement = null, int? errorCode = null, long? latencyMs = null) =>
            AdEvent?.Invoke(new AdEvent(name, format, placement, errorCode, latencyMs));

        /// <summary>Impression-level revenue (an estimate; AdMob's reports are the source of truth).</summary>
        private void Paid(string format, string placement, AdValue value, ResponseInfo info)
        {
            string source = null;
            try
            {
                source = info?.GetLoadedAdapterResponseInfo()?.AdSourceName;
            }
            catch (Exception)
            {
                // Response info is best effort; the revenue is what matters.
            }
            AdEvent?.Invoke(new AdEvent(TelemetryEvents.AdPaid, format, placement, valueMicros: value.Value,
                currency: value.CurrencyCode, precision: value.Precision.ToString(), source: source));
        }
    }
}
#endif
