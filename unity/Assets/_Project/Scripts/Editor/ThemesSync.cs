using System.IO;
using UnityEditor;
using UnityEngine;

namespace StackIt.EditorTools
{
    /// <summary>
    /// shared/theme/themes.json is the single source of the look tokens (the web pages read it
    /// too); Unity can only load files under Assets, so this keeps Resources/StackIt/themes.json a
    /// byte-for-byte copy. Runs when the editor loads, from Setup Project and before builds.
    /// </summary>
    public static class ThemesSync
    {
        private const string Source = "../shared/theme/themes.json";
        internal const string Copy = "Assets/_Project/Resources/StackIt/themes.json";

        /// <summary>Copies the shared file if the Unity copy differs. Returns true when it copied.</summary>
        public static bool Run()
        {
            if (!File.Exists(Source))
            {
                Debug.LogWarning($"{Source} not found; keeping {Copy} as is.");
                return false;
            }
            string text = File.ReadAllText(Source);
            if (File.Exists(Copy) && File.ReadAllText(Copy) == text) return false;
            File.WriteAllText(Copy, text);
            AssetDatabase.ImportAsset(Copy);
            Debug.Log($"Updated {Copy} from shared/theme/themes.json.");
            return true;
        }
    }
}
