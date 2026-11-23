#if UNITY_CLOUD_BUILD
using System;
using StackIt.Bootstrap;
using StackIt.Client;
using StackIt.EditorTools;
using UnityEditor;

/// <summary>
/// Unity Build Automation's pre-export method (set it to CloudBuildSetup.PreExport, SETUP.md 9.7).
/// A cloud build starts from a clean clone, without the generated files the editor makes when it
/// opens, so it makes them here: the font atlases and the string tables. Each build takes its
/// number from the service, as Apple refuses a build number twice. Real AdMob IDs stay out of the
/// repo: a release build gets them from the build target's variables.
/// </summary>
public static class CloudBuildSetup
{
    private const string GoogleAdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

    public static void PreExport(UnityEngine.CloudBuild.BuildManifestObject manifest)
    {
        ProjectSetup.RunBatch();
        PlayerSettings.iOS.buildNumber = manifest.GetValue<int>("buildNumber").ToString();
        UseRealAds();
    }

    private static void UseRealAds()
    {
        // Throws, failing the build, on a partial set or a test ID: that release would earn nothing.
        ReleaseAdIds ids = ReleaseAdIds.Read("IOS", Environment.GetEnvironmentVariable);
        var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(GoogleAdsSettings));
        SerializedProperty appId = settings.FindProperty("adMobIOSAppId");
        AdsConfig ads = AdsConfig.Load();
        if (ids != null)
        {
            appId.stringValue = ids.AppId;
            settings.ApplyModifiedPropertiesWithoutUndo();
            ads.iosRewarded = ids.Rewarded;
            ads.iosInterstitial = ids.Interstitial;
            ads.iosBanner = ids.Banner;
            ads.debugForceEeaConsent = false;
            EditorUtility.SetDirty(ads);
            AssetDatabase.SaveAssets();
        }
        // Without the variables a test build keeps what the repo has, which must still be one
        // account: real units beside Google's test App ID got no ads at all on TestFlight.
        ReleaseAdIds.CheckSameAccount("IOS", appId.stringValue, ads.iosRewarded, ads.iosInterstitial, ads.iosBanner);
    }
}
#endif
