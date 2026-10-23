using System;
using System.Collections.Generic;

namespace StackIt.Client.Theming
{
    public enum ThemeId
    {
        Pop,
        Neon,
    }

    /// <summary>A shadow or glow: colour, vertical offset and blur (or, for radial glows, a radius).</summary>
    public readonly struct Glow
    {
        public readonly Rgba Color;
        public readonly float Y;
        public readonly float Blur;
        public readonly float Radius;

        public Glow(Rgba color, float y, float blur, float radius)
        {
            Color = color;
            Y = y;
            Blur = blur;
            Radius = radius;
        }

        public bool IsNone => Color.IsNone;
    }

    /// <summary>One look's tokens, read from themes.json. Unknown tokens throw, naming the token.</summary>
    public sealed class ThemePalette
    {
        private readonly Dictionary<string, object> _tokens;

        public ThemePalette(ThemeId id, bool dark, Dictionary<string, object> tokens)
        {
            Id = id;
            Dark = dark;
            _tokens = tokens;
        }

        public ThemeId Id { get; }

        /// <summary>Light text on a dark background (status bar, web color-scheme).</summary>
        public bool Dark { get; }

        public IEnumerable<string> Tokens => _tokens.Keys;

        public bool Has(string token) => _tokens.ContainsKey(token);

        public Rgba Color(string token) => ColorMath.Hex(Get<string>(token));

        public Rgba[] Colors(string token)
        {
            List<object> list = Get<List<object>>(token);
            var colors = new Rgba[list.Count];
            for (int i = 0; i < list.Count; i++) colors[i] = ColorMath.Hex((string)list[i]);
            return colors;
        }

        public float Number(string token) => (float)Get<double>(token);

        public Glow Glow(string token)
        {
            Dictionary<string, object> o = Get<Dictionary<string, object>>(token);
            float Field(string key) => o.TryGetValue(key, out object v) ? (float)(double)v : 0f;
            return new Glow(ColorMath.Hex((string)o["color"]), Field("y"), Field("blur"), Field("radius"));
        }

        /// <summary>The raw JSON value (string, double, list or object), for exporting the tokens as CSS.</summary>
        public object Raw(string token) => Get<object>(token);

        private T Get<T>(string token)
        {
            if (!_tokens.TryGetValue(token, out object value))
                throw new KeyNotFoundException($"Theme '{Id}' has no token '{token}'.");
            if (value is T typed) return typed;
            throw new FormatException($"Theme '{Id}' token '{token}' is not a {typeof(T).Name}.");
        }
    }

    /// <summary>Every look in themes.json.</summary>
    public sealed class ThemeCatalog
    {
        private readonly Dictionary<ThemeId, ThemePalette> _themes;

        private ThemeCatalog(ThemeId defaultId, Dictionary<ThemeId, ThemePalette> themes)
        {
            Default = defaultId;
            _themes = themes;
        }

        public ThemeId Default { get; }

        public IEnumerable<ThemePalette> All => _themes.Values;

        public ThemePalette Get(ThemeId id) => _themes[id];

        public static ThemeCatalog Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var themes = new Dictionary<ThemeId, ThemePalette>();
            foreach (KeyValuePair<string, object> entry in (Dictionary<string, object>)root["themes"])
            {
                var theme = (Dictionary<string, object>)entry.Value;
                ThemeId id = IdOf(entry.Key);
                themes[id] = new ThemePalette(id, (bool)theme["dark"], (Dictionary<string, object>)theme["tokens"]);
            }
            foreach (ThemeId id in Enum.GetValues(typeof(ThemeId)))
                if (!themes.ContainsKey(id)) throw new FormatException($"themes.json has no '{Key(id)}' theme.");
            return new ThemeCatalog(IdOf((string)root["default"]), themes);
        }

        /// <summary>The id as written in themes.json and saved settings ("pop", "neon").</summary>
        public static string Key(ThemeId id) => id.ToString().ToLowerInvariant();

        public static ThemeId IdOf(string key)
        {
            foreach (ThemeId id in Enum.GetValues(typeof(ThemeId)))
                if (string.Equals(Key(id), key, StringComparison.OrdinalIgnoreCase)) return id;
            throw new FormatException($"Unknown theme '{key}'.");
        }

        public static bool TryIdOf(string key, out ThemeId id)
        {
            foreach (ThemeId candidate in Enum.GetValues(typeof(ThemeId)))
            {
                if (!string.Equals(Key(candidate), key, StringComparison.OrdinalIgnoreCase)) continue;
                id = candidate;
                return true;
            }
            id = default;
            return false;
        }
    }
}
