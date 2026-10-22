using StackIt.Services;

namespace StackIt.UI
{
    /// <summary>
    /// Shorthand for localized strings. Keys live in Assets/_Project/Localization/strings.csv and
    /// are imported into the "UI" string table of the Unity Localization package.
    /// </summary>
    public static class L
    {
        public static ILocalizer Localizer;

        public static string T(string key) => Localizer?.Get(key) ?? key;

        public static string T(string key, params object[] args) => UiKit.Format(T(key), args);
    }
}
