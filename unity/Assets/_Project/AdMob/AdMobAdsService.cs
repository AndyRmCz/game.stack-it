// AdMob adapter. Lives outside every .asmdef (compiled into Assembly-CSharp) so it can see the
// Google Mobile Ads plugin however that plugin is packaged, and so the rest of the project builds
// without it. Stack IT ▸ Setup Project adds the STACKIT_ADMOB define once the plugin is installed.
#if STACKIT_ADMOB
using System;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using StackIt.Bootstrap;
using StackIt.Services;
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

        public Task<bool> ShowRewardedAsync()
        {
            var result = new TaskCompletionSource<bool>();
            if (!RewardedReady)
            {
                LoadRewarded();
                result.SetResult(false);
                return result.Task;
            }
            bool earned = false;
            RewardedAd ad = _rewarded;
            _rewarded = null;
            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                LoadRewarded();
                result.TrySetResult(earned);
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                ad.Destroy();
                LoadRewarded();
                result.TrySetResult(false);
            };
            ad.Show(_ => earned = true);
            return result.Task;
        }

        public Task ShowInterstitialAsync()
        {
            var result = new TaskCompletionSource<bool>();
            if (_interstitial == null || !_interstitial.CanShowAd())
            {
                LoadInterstitial();
                result.SetResult(false);
                return result.Task;
            }
            InterstitialAd ad = _interstitial;
            _interstitial = null;
            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                LoadInterstitial();
                result.TrySetResult(true);
            };
            ad.OnAdFullScreenContentFailed += _ =>
            {
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
                _banner = new BannerView(_config.Banner, AdSize.Banner, AdPosition.Bottom);
                _banner.LoadAd(new AdRequest());
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
            RewardedAd.Load(_config.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.Log("Rewarded load failed: " + error?.GetMessage());
                    return;
                }
                _rewarded = ad;
            });
        }

        private void LoadInterstitial()
        {
            if (!IsInitialized || _interstitial != null) return;
            InterstitialAd.Load(_config.Interstitial, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.Log("Interstitial load failed: " + error?.GetMessage());
                    return;
                }
                _interstitial = ad;
            });
        }
    }
}
#endif
