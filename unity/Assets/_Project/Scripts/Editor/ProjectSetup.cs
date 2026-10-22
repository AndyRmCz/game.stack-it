using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using StackIt.Bootstrap;
using StackIt.Game;
using StackIt.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

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
            CreateFonts();
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

            // Alpha-blended unlit, for perfect rings and dust: the transparent variant must exist in
            // an asset to be included in player builds.
            Material fx = EnsureMaterial("ParticleMaterial", "Universal Render Pipeline/Particles/Unlit");
            Materials.MakeTransparent(fx);
            EditorUtility.SetDirty(fx);
        }

        // --- fonts ---------------------------------------------------------------------------------

        private const string FontSourceDir = "Assets/_Project/Fonts";
        private const string FontAssetDir = ResourcesDir + "/Fonts";

        /// <summary>
        /// Bakes static TMP font assets from the subset TTFs in Assets/_Project/Fonts (only the atlases
        /// ship, not the TTFs). Text fonts fall back to the icon font, then to TMP's default font for
        /// characters outside the subset (e.g. in player names). Delete an asset to rebuild it.
        /// </summary>
        private static void CreateFonts()
        {
            Directory.CreateDirectory(FontAssetDir);
            TMP_FontAsset icons = CreateFont("MaterialSymbolsRounded.ttf", Fonts.IconsName, Icons.All, 96, null);
            var fallbacks = new List<TMP_FontAsset> { icons };
            if (TMP_Settings.defaultFontAsset != null) fallbacks.Add(TMP_Settings.defaultFontAsset);
            string text = TextCharacters();
            CreateFont("Fredoka-SemiBold.ttf", Fonts.DisplayName, text, 64, fallbacks);
            CreateFont("NunitoSans-SemiBold.ttf", Fonts.BodyName, text, 64, fallbacks);
            CreateFont("NunitoSans-ExtraBold.ttf", Fonts.BodyBoldName, text, 64, fallbacks);
        }

        /// <summary>Printable ASCII, Latin-1 (Spanish and most Western European text) and typographic punctuation.</summary>
        private static string TextCharacters()
        {
            var chars = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) chars.Append(c);
            for (char c = ' '; c <= 'ÿ'; c++) chars.Append(c);
            chars.Append("  –—‘’“”•…‹›€");
            return chars.ToString();
        }

        private static TMP_FontAsset CreateFont(string file, string name, string characters, int samplingSize, List<TMP_FontAsset> fallbacks)
        {
            string path = $"{FontAssetDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                if (fallbacks != null) existing.fallbackFontAssetTable = fallbacks;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontSourceDir}/{file}");
            if (font == null) throw new InvalidOperationException($"Font {FontSourceDir}/{file} not found.");
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, samplingSize / 8, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            asset.name = name;
            if (!asset.TryAddCharacters(characters, out string missing) && !string.IsNullOrEmpty(missing))
                Debug.LogWarning($"{name}: {missing.Length} characters not in {file} (they use the fallback fonts).");
            asset.atlasPopulationMode = AtlasPopulationMode.Static; // fixed atlas; the TTF is not needed at runtime
            if (fallbacks != null) asset.fallbackFontAssetTable = fallbacks;

            AssetDatabase.CreateAsset(asset, path);
            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            return asset;
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
