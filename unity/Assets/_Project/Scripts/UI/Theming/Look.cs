using System;
using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// The UI's view of the current look: themed components read colours here (by token name) and
    /// re-apply on <see cref="Changed"/>. The App hands it the <see cref="IThemeService"/>; until then
    /// (or in edit-mode tools) it shows the catalog's default look.
    /// </summary>
    public static class Look
    {
        private static IThemeService _service;
        private static ThemeCatalog _catalog;

        public static event Action<ThemePalette> Changed;

        /// <summary>Reduce motion: no breathing, shake, glow pulses or slow motion (simple fades stay).</summary>
        public static bool ReduceMotion { get; set; }

        public static ThemeCatalog Catalog
        {
            get
            {
                if (_catalog != null) return _catalog;
                var json = Resources.Load<TextAsset>("StackIt/themes");
                if (json == null) throw new InvalidOperationException("Missing Resources/StackIt/themes.json (copied from shared/theme by Setup Project).");
                return _catalog = ThemeCatalog.Parse(json.text);
            }
        }

        public static ThemePalette P => _service != null ? _service.Palette : Catalog.Get(Catalog.Default);

        public static IThemeService Service => _service;

        public static void Init(IThemeService service)
        {
            if (_service != null) _service.Changed -= Raise;
            _service = service;
            _service.Changed += Raise;
        }

        private static void Raise(ThemePalette palette) => Changed?.Invoke(palette);

        public static Color C(string token) => ToColor(P.Color(token));

        public static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        public static Color[] Colors(string token)
        {
            Rgba[] source = P.Colors(token);
            var colors = new Color[source.Length];
            for (int i = 0; i < source.Length; i++) colors[i] = ToColor(source[i]);
            return colors;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
