using System.IO;
using StackIt.Bootstrap;
using StackIt.Client;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace StackIt.EditorTools
{
    /// <summary>
    /// Platform wiring derived from AppConfig at build time:
    /// Android: deep-link intent filters + FileProvider (StackItManifest.androidlib).
    /// iOS: Associated Domains, Sign in with Apple, ATT text, Google Sign-In URL scheme, frameworks.
    /// </summary>
    public sealed class BuildHooks : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string AndroidManifestPath = "Assets/Plugins/Android/StackItManifest.androidlib/AndroidManifest.xml";
        private const string TrackingUsage =
            "Your data is used to show you ads that are more relevant to you. Ads keep Milli free.";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ThemesSync.Run();
            if (report.summary.platform == BuildTarget.Android) WriteAndroidManifest();
        }

        [MenuItem("Stack IT/Regenerate Android Manifest", priority = 20)]
        public static void WriteAndroidManifest()
        {
            AppConfig config = AppConfig.Load();
            // Unity 6 defaults to GameActivity; the intent filters must target whichever entry is used.
            bool gameActivity = (PlayerSettings.Android.applicationEntry & AndroidApplicationEntry.GameActivity) != 0;
            string activity = gameActivity ? "com.unity3d.player.UnityPlayerGameActivity" : "com.unity3d.player.UnityPlayerActivity";
            string xml = DeepLinkManifest.AndroidLibraryManifest(config.apiBaseUrl, config.urlScheme, activity);
            if (File.Exists(AndroidManifestPath) && File.ReadAllText(AndroidManifestPath) == xml) return;
            File.WriteAllText(AndroidManifestPath, xml);
            AssetDatabase.ImportAsset(AndroidManifestPath);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform == BuildTarget.iOS) ConfigureXcode(report.summary.outputPath);
#endif
        }

#if UNITY_IOS
        private static void ConfigureXcode(string buildPath)
        {
            AppConfig config = AppConfig.Load();
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(frameworkTarget, "AuthenticationServices.framework", false);
            project.WriteToFile(projectPath);

            // Universal Links + Sign in with Apple.
            var capabilities = new ProjectCapabilityManager(projectPath, "Unity-iPhone/StackIt.entitlements", null, project.GetUnityMainTargetGuid());
            capabilities.AddAssociatedDomains(new[] { "applinks:" + DeepLinkManifest.Host(config.apiBaseUrl) });
            capabilities.AddSignInWithApple();
            capabilities.WriteToFile();

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;
            root.SetString("NSUserTrackingUsageDescription", TrackingUsage);
            if (!string.IsNullOrEmpty(config.googleIosClientId))
            {
                root.SetString("GIDClientID", config.googleIosClientId);
                // Google Sign-In redirects back through the reversed client ID scheme.
                string reversed = string.Join(".", System.Linq.Enumerable.Reverse(config.googleIosClientId.Split('.')));
                PlistElementArray urlTypes = root["CFBundleURLTypes"] as PlistElementArray ?? root.CreateArray("CFBundleURLTypes");
                urlTypes.AddDict().CreateArray("CFBundleURLSchemes").AddString(reversed);
            }
            plist.WriteToFile(plistPath);
        }
#endif
    }
}
