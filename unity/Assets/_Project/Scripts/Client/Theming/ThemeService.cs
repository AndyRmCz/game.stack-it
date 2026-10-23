using System;

namespace StackIt.Client.Theming
{
    public interface IThemeService
    {
        ThemeId Current { get; }
        ThemePalette Palette { get; }

        /// <summary>Raised once per actual change of look (not when the same look is set again).</summary>
        event Action<ThemePalette> Changed;

        void Set(ThemeId id);
    }

    /// <summary>
    /// The chosen look, saved locally (a device preference, not an account setting). First launch
    /// uses the catalog's default (Neon).
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        public const string StoreKey = "stackit.look";

        private readonly ThemeCatalog _catalog;
        private readonly IKeyValueStore _store;

        public ThemeService(ThemeCatalog catalog, IKeyValueStore store)
        {
            _catalog = catalog;
            _store = store;
            Current = ThemeCatalog.TryIdOf(store.GetString(StoreKey), out ThemeId saved) ? saved : catalog.Default;
        }

        public ThemeId Current { get; private set; }

        public ThemePalette Palette => _catalog.Get(Current);

        public event Action<ThemePalette> Changed;

        public void Set(ThemeId id)
        {
            if (id == Current) return;
            Current = id;
            _store.SetString(StoreKey, ThemeCatalog.Key(id));
            Changed?.Invoke(Palette);
        }
    }
}
