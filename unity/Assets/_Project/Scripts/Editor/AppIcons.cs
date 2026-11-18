using System;
using StackIt.Client;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace StackIt.EditorTools
{
    /// <summary>
    /// The app icons (backend/scripts/generate_app_icons.py), put in every icon slot of Player
    /// Settings whenever they are imported (as plain 2D pictures: PlainTextures). Android's adaptive
    /// slots get the night and Blocky as separate layers (AppIconLayers); the rest, the whole icon.
    /// </summary>
    public class AppIcons : AssetPostprocessor
    {
        private const string Dir = "Assets/_Project/Art/Icons/";
        private const string Icon = Dir + "AppIcon-Neon.png"; // the store icon
        private const string Background = Dir + "AppIcon-Neon-Background.png";
        private const string Foreground = Dir + "AppIcon-Neon-Foreground.png";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Array.Exists(imported, path => path.StartsWith(Dir, StringComparison.Ordinal)))
                EditorApplication.delayCall += Assign;
        }

        [MenuItem("Stack IT/Reimport App Icons", priority = 25)]
        public static void Reimport()
        {
            // Re-importing them assigns them (OnPostprocessAllAssets).
            AssetDatabase.ImportAsset(Dir.TrimEnd('/'), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        }

        public static void Assign()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Icon);
            if (icon == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var layers = new AppIconLayers<Texture2D>(
                icon,
                AssetDatabase.LoadAssetAtPath<Texture2D>(Background),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Foreground));
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (PlatformIcon slot in slots) slot.SetTextures(layers.For(slot.maxLayerCount));
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("App icons set in Player Settings (Android: adaptive, round and legacy).");
        }
    }
}
