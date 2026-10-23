using System;
using UnityEditor;
using UnityEngine;

namespace StackIt.EditorTools
{
    /// <summary>
    /// Keeps the generated assets in step with their sources, so a pull doesn't need a trip to
    /// Stack IT ▸ Setup Project: when the editor loads it syncs the look tokens from
    /// shared/theme/themes.json and bakes the TMP font assets if they are missing or stale, and it
    /// re-imports strings.csv into the string tables whenever the CSV changes.
    /// </summary>
    [InitializeOnLoad]
    public class AutoSetup : AssetPostprocessor
    {
        static AutoSetup()
        {
            EditorApplication.delayCall += OnEditorLoaded;
        }

        private static void OnEditorLoaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Run(() => ThemesSync.Run());
            if (!ProjectSetup.FontsStale()) return;
            Debug.Log("Baking the Stack IT font assets (missing or out of date).");
            Run(ProjectSetup.CreateFonts);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Array.IndexOf(imported, StringsImporter.CsvPath) < 0) return;
            // Not inside the import callback: the Localization assets it writes would re-enter it.
            EditorApplication.delayCall += () => Run(StringsImporter.Import);
        }

        private static void Run(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogWarning("Automatic setup failed; run Stack IT ▸ Setup Project.");
            }
        }
    }
}
