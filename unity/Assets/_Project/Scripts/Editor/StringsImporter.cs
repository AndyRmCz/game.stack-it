using System.IO;
using StackIt.Client;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace StackIt.EditorTools
{
    /// <summary>
    /// Imports Resources/StackIt/strings.csv into the Localization package: creates the settings
    /// asset, the English and Spanish locales and the "UI" string table collection if missing,
    /// then writes every key. The CSV stays the single file translators edit.
    /// </summary>
    public static class StringsImporter
    {
        private const string CsvPath = "Assets/_Project/Resources/StackIt/strings.csv";
        private const string LocalizationDir = "Assets/_Project/Localization";
        private const string TableName = "UI";

        [MenuItem("Stack IT/Import Strings", priority = 10)]
        public static void Import()
        {
            StringsCsv csv = StringsCsv.Parse(File.ReadAllText(CsvPath));
            Directory.CreateDirectory(LocalizationDir);
            EnsureSettings();

            foreach (string code in csv.Languages) EnsureLocale(code);

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(TableName, LocalizationDir);

            foreach (string code in csv.Languages)
            {
                var table = collection.GetTable(new LocaleIdentifier(code)) as StringTable;
                if (table == null) continue;
                foreach (string key in csv.Keys)
                {
                    string value = csv.Get(code, key);
                    if (value != null) table.AddEntry(key, value);
                }
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log($"Imported {CsvPath} into the '{TableName}' string table.");
        }

        private static void EnsureSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, LocalizationDir + "/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        private static void EnsureLocale(string code)
        {
            foreach (Locale existing in LocalizationEditorSettings.GetLocales())
                if (existing.Identifier.Code == code) return;
            Locale locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, $"{LocalizationDir}/{code}.asset");
            LocalizationEditorSettings.AddLocale(locale);
        }
    }
}
