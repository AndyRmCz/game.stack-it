using System;
using System.IO;
using System.Linq;
using StackIt.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StackIt.EditorTools
{
    /// <summary>
    /// One-click project setup (Stack IT ▸ Setup Project). Idempotent: run it after cloning, after
    /// changing AppConfig, or after installing the AdMob plugin.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ResourcesDir = "Assets/_Project/Resources/StackIt";
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string AdMobDefine = "STACKIT_ADMOB";

        [MenuItem("Stack IT/Setup Project", priority = 0)]
        public static void Run()
        {
            Directory.CreateDirectory(ResourcesDir);
            AppConfig config = EnsureAsset<AppConfig>(ResourcesDir + "/AppConfig.asset");
            EnsureAsset<AdsConfig>(ResourcesDir + "/AdsConfig.asset");
            CreateMaterials();
            CreateScene();
            ApplyPlayerSettings(config);
            UpdateAdMobDefine();
            ImportTmpEssentials();
            StringsImporter.Import();
            AssetDatabase.SaveAssets();
            Debug.Log("Stack IT setup complete. Press Play in Assets/_Project/Scenes/Main.unity.");
        }

        /// <summary>Entry point for CI / batch mode: Unity -batchmode -executeMethod StackIt.EditorTools.ProjectSetup.RunBatch -quit</summary>
        public static void RunBatch() => Run();

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateMaterials()
        {
            // Materials live in Resources so their URP shaders are included in player builds.
            Material block = EnsureMaterial("BlockMaterial", "Universal Render Pipeline/Lit");
            block.EnableKeyword("_EMISSION");
            block.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            block.SetColor("_EmissionColor", Color.white);
            block.SetFloat("_Smoothness", 0.65f);
            block.enableInstancing = true;

            EnsureMaterial("SkyMaterial", "Universal Render Pipeline/Unlit");
            EnsureMaterial("ParticleMaterial", "Universal Render Pipeline/Particles/Unlit");
        }

        private static Material EnsureMaterial(string name, string shaderName)
        {
            string path = $"{ResourcesDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{shaderName}' not found: is URP installed?");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // Empty on purpose: App boots itself and builds the world and UI in code.
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ApplyPlayerSettings(AppConfig config)
        {
            PlayerSettings.productName = "Stack IT";
            PlayerSettings.companyName = "Stack IT";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, config.bundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, config.bundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.iOSUrlSchemes = new[] { config.urlScheme };
        }

        /// <summary>Compiles the AdMob adapter only when the Google Mobile Ads plugin is present.</summary>
        private static void UpdateAdMobDefine()
        {
            bool present = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetType("GoogleMobileAds.Api.MobileAds", false) != null);
            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';')
                    .Where(d => d.Length > 0 && d != AdMobDefine).ToList();
                if (present) defines.Add(AdMobDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
            if (!present) Debug.Log("Google Mobile Ads plugin not found: ads are disabled (see SETUP.md, section 5).");
        }

        private static void ImportTmpEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
        }
    }
}
