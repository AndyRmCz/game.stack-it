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
        if (ids == null) return; // a test build keeps Google's test IDs

        var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(GoogleAdsSettings));
        settings.FindProperty("adMobIOSAppId").stringValue = ids.AppId;
        settings.ApplyModifiedPropertiesWithoutUndo();

        AdsConfig ads = AdsConfig.Load();
        ads.iosRewarded = ids.Rewarded;
        ads.iosInterstitial = ids.Interstitial;
        ads.iosBanner = ids.Banner;
        ads.debugForceEeaConsent = false;
        EditorUtility.SetDirty(ads);
        AssetDatabase.SaveAssets();
    }
}
#endif
