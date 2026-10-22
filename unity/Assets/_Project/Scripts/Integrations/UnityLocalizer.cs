using System;
using System.Threading.Tasks;
using StackIt.Client;
using StackIt.Services;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace StackIt.Integrations
{
    /// <summary>
    /// UI strings through the Unity Localization package ("UI" string table, English + Spanish).
    /// The table is generated from Resources/StackIt/strings.csv by Stack IT ▸ Import Strings; the
    /// same CSV is the fallback if the package is not set up yet.
    /// </summary>
    public sealed class UnityLocalizer : ILocalizer
    {
        private const string TableName = "UI";
        private StringTable _table;
        private StringsCsv _fallback;

        public string Language { get; private set; } = "en";
        public event Action Changed;

        public async Task InitializeAsync(string preferred)
        {
            var csv = Resources.Load<TextAsset>("StackIt/strings");
            if (csv != null) _fallback = StringsCsv.Parse(csv.text);
            Language = preferred ?? DeviceLanguage();
            try
            {
                await LocalizationSettings.InitializationOperation.Task;
                SelectLocale(Language);
                await LoadTable();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Localization package unavailable, using strings.csv: " + e.Message);
            }
        }

        public string Get(string key)
        {
            string value = _table?.GetEntry(key)?.GetLocalizedString();
            if (string.IsNullOrEmpty(value)) value = _fallback?.Get(Language, key);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        public async void SetLanguage(string code)
        {
            Language = code;
            try
            {
                SelectLocale(code);
                await LoadTable();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Locale switch failed, using strings.csv: " + e.Message);
                _table = null;
            }
            Changed?.Invoke();
        }

        private static void SelectLocale(string code)
        {
            foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
            {
                if (locale.Identifier.Code == code)
                {
                    LocalizationSettings.SelectedLocale = locale;
                    return;
                }
            }
        }

        private async Task LoadTable()
        {
            _table = await LocalizationSettings.StringDatabase.GetTableAsync(TableName).Task;
        }

        private static string DeviceLanguage() =>
            Application.systemLanguage == SystemLanguage.Spanish ? "es" : "en";
    }
}
