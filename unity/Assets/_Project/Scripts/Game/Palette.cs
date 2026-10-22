using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Block skins and sky themes (ids match the backend's skins table). Colors shift with height so
    /// a tall tower reads as a climb through the synthwave sky.
    /// </summary>
    public static class Palette
    {
        public const string DefaultBlockSkin = "classic";
        public const string DefaultTheme = "synthwave";

        /// <summary>HDR emission multiplier: values above 1 bloom.</summary>
        public const float Emission = 1.6f;

        public static Color Block(string skin, int index)
        {
            switch (skin)
            {
                case "glass": return Color.HSVToRGB(Wrap(0.55f + 0.01f * Mathf.Sin(index * 0.4f)), 0.22f, 1f);
                case "magma": return Color.HSVToRGB(Wrap(0.02f + 0.05f * Mathf.Sin(index * 0.35f)), 0.92f, 1f);
                case "aurora": return Color.HSVToRGB(Wrap(0.42f + 0.12f * Mathf.Sin(index * 0.18f)), 0.75f, 1f);
                case "circuit": return index % 2 == 0 ? new Color(0.1f, 1f, 0.55f) : new Color(0.18f, 0.88f, 0.9f);
                case "prism": return Color.HSVToRGB(Wrap(index * 0.083f), 0.8f, 1f);
                case "gold": return Color.HSVToRGB(0.12f, 0.62f, 0.75f + 0.25f * Mathf.PingPong(index * 0.1f, 1f));
                default: return Color.HSVToRGB(Wrap(0.5f + index * 0.022f), 0.78f, 1f); // classic: cyan → magenta → amber
            }
        }

        /// <summary>Sky gradient (top, bottom) for a theme; <paramref name="height01"/> drifts the hues.</summary>
        public static void Sky(string theme, float height01, out Color top, out Color bottom)
        {
            Color t0, b0, t1, b1;
            switch (theme)
            {
                case "midnight":
                    t0 = Hex(0x05070f); b0 = Hex(0x1a2350); t1 = Hex(0x000000); b1 = Hex(0x2c1a5c);
                    break;
                case "sunset":
                    t0 = Hex(0x2b0f3a); b0 = Hex(0xff7a59); t1 = Hex(0x120626); b1 = Hex(0xb03a7a);
                    break;
                case "vapor":
                    t0 = Hex(0x1b1035); b0 = Hex(0x2de2e6); t1 = Hex(0x2a0a4a); b1 = Hex(0xff71ce);
                    break;
                case "matrix":
                    t0 = Hex(0x000500); b0 = Hex(0x003b1a); t1 = Hex(0x000000); b1 = Hex(0x0b5c2a);
                    break;
                default: // synthwave
                    t0 = Hex(0x140a2e); b0 = Hex(0x6a1b6e); t1 = Hex(0x07031a); b1 = Hex(0x1d4e89);
                    break;
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(height01));
            top = Color.Lerp(t0, t1, k);
            bottom = Color.Lerp(b0, b1, k);
        }

        public static readonly Color Ghost = new Color(0.18f, 0.89f, 0.9f);

        private static float Wrap(float h) => h - Mathf.Floor(h);

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
