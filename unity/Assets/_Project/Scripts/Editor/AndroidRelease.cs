using System;
using StackIt.Bootstrap;
using StackIt.Client;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StackIt.EditorTools
{
    /// <summary>
    /// Stack IT ▸ Build Android Release: the Play bundle with the real AdMob IDs, read from the
    /// ADMOB_ANDROID_* environment variables (SETUP.md 8.7). They go into the ad settings for this
    /// build only and what was there comes back after it, so the repo, and every other build, keeps
    /// Google's test IDs.
    /// </summary>
    public static class AndroidRelease
    {
        private const string GoogleAdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        private const string Title = "Android release";

        [MenuItem("Stack IT/Build Android Release", priority = 30)]
        public static void Build()
        {
            ReleaseAdIds ids;
            try
            {
                ids = ReleaseAdIds.Read("ANDROID", Environment.GetEnvironmentVariable);
            }
            catch (InvalidOperationException e)
            {
                Fail(e.Message);
                return;
            }
            if (ids == null)
            {
                Fail("Set ADMOB_ANDROID_APP_ID, ADMOB_ANDROID_REWARDED, ADMOB_ANDROID_INTERSTITIAL and " +
                     "ADMOB_ANDROID_BANNER, then reopen Unity Hub and Unity (SETUP.md 8.7).");
                return;
            }
            AdsConfig ads = AdsConfig.Load();
            if (!ads.adsEnabled)
            {
                Fail("AdsConfig has ads turned off: a release without ads earns nothing.");
                return;
            }
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) ||
                string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                Fail("Enter the upload keystore and both passwords in Player ▸ Android ▸ Publishing Settings " +
                     "first. Unity forgets the passwords when it closes.");
                return;
            }

            string name = $"Milli-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}";
            string path = EditorUtility.SaveFilePanel("Save the Play bundle", "Builds", name, "aab");
            if (string.IsNullOrEmpty(path)) return;

            string testAppId = AndroidAppId();
            string testAds = EditorJsonUtility.ToJson(ads);
            bool appBundle = EditorUserBuildSettings.buildAppBundle;
            bool development = EditorUserBuildSettings.development;
            BuildReport report;
            try
            {
                SetAndroidAppId(ids.AppId);
                ads.androidRewarded = ids.Rewarded;
                ads.androidInterstitial = ids.Interstitial;
                ads.androidBanner = ids.Banner;
                ads.debugForceEeaConsent = false;
                Save(ads);
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = path,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                SetAndroidAppId(testAppId);
                EditorJsonUtility.FromJsonOverwrite(testAds, ads);
                Save(ads);
                EditorUserBuildSettings.buildAppBundle = appBundle;
                EditorUserBuildSettings.development = development;
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail("The build failed (" + report.summary.result + "): see the Console.");
                return;
            }
            Debug.Log("Android release with real ads: " + path);
            EditorUtility.RevealInFinder(path);
        }

        private static string AndroidAppId() =>
            new SerializedObject(AssetDatabase.LoadMainAssetAtPath(GoogleAdsSettings)).FindProperty("adMobAndroidAppId").stringValue;

        private static void SetAndroidAppId(string appId)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(GoogleAdsSettings);
            var settings = new SerializedObject(asset);
            settings.FindProperty("adMobAndroidAppId").stringValue = appId;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Save(asset);
        }

        private static void Save(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static void Fail(string message)
        {
            Debug.LogError(Title + ": " + message);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
