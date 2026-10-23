using StackIt.Client;
using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Block skins and sky themes (ids match the backend's skins table; the names are historical).
    /// The default skin ("classic") and sky ("synthwave") follow the look (Pop/Neon): the tower takes
    /// the look's height ramp and the sky its background gradient. The other, purchasable ones keep
    /// their own colours in every look: each sky moves through four moments as the tower grows.
    /// </summary>
    public static class Palette
    {
        public const string DefaultBlockSkin = "classic";
        public const string DefaultTheme = "synthwave";

        /// <summary>Tower heights (in blocks) where each sky moment is reached.</summary>
        private static readonly float[] SkyStops = { 0f, 25f, 55f, 90f };

        public static bool FollowsLook(string skinOrTheme) =>
            string.IsNullOrEmpty(skinOrTheme) || skinOrTheme == DefaultBlockSkin || skinOrTheme == DefaultTheme;

        /// <summary>Top-face colour of block <paramref name="index"/> (0 = the base) for a skin in a look.</summary>
        public static Color BlockTop(string skin, int index, ThemePalette look)
        {
            if (!FollowsLook(skin)) return Block(skin, index);
            if (index <= 0) return ToColor(look.Colors(T.BlockBase)[0]);
            return ToColor(HeightRamp.At(look.Colors(T.BlockRamp), index));
        }

        public static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        public static Color Block(string skin, int index)
        {
            switch (skin)
            {
                case "glass": // frosted porcelain
                    return Color.HSVToRGB(0.57f, 0.08f + 0.05f * Mathf.Sin(index * 0.45f), 0.99f);
                case "magma": // terracotta clay
                    return Color.HSVToRGB(Wrap(0.035f + 0.025f * Mathf.Sin(index * 0.3f)), 0.52f, 0.9f);
                case "aurora": // seafoam to lavender
                    return Color.HSVToRGB(Wrap(0.45f + 0.3f * Mathf.PingPong(index * 0.025f, 1f)), 0.36f, 0.95f);
                case "circuit": // mint and sage
                    return index % 2 == 0 ? Hex(0x9be3c3) : Hex(0xbcd9a6);
                case "prism": // the whole pastel wheel
                    return Color.HSVToRGB(Wrap(index * 0.065f), 0.38f, 0.97f);
                case "gold": // champagne
                    return Color.HSVToRGB(0.115f, 0.3f + 0.12f * Mathf.PingPong(index * 0.08f, 1f), 0.97f - 0.08f * Mathf.PingPong(index * 0.05f, 1f));
                default: // classic: rose → peach → lilac → sky → mint, slowly
                    return Color.HSVToRGB(Wrap(0.96f + index * 0.011f), 0.42f + 0.05f * Mathf.Sin(index * 0.5f), 0.96f);
            }
        }

        /// <summary>Sky gradient (top, bottom) for a theme at a tower height in blocks.</summary>
        public static void Sky(string theme, float blocks, out Color top, out Color bottom)
        {
            int[] keys = SkyKeys(theme); // pairs of (top, bottom) per moment
            Feel.Segment(SkyStops, blocks, out int i, out float t);
            float k = Mathf.SmoothStep(0f, 1f, t);
            top = Color.Lerp(Hex(keys[i * 2]), Hex(keys[i * 2 + 2]), k);
            bottom = Color.Lerp(Hex(keys[i * 2 + 1]), Hex(keys[i * 2 + 3]), k);
        }

        private static int[] SkyKeys(string theme)
        {
            switch (theme)
            {
                case "midnight": // moonlit from the start
                    return new[] { 0x2a3566, 0x6f7fb8, 0x1d2550, 0x5a6aa8, 0x141a3d, 0x463f86, 0x0c1026, 0x2c2a5e };
                case "sunset": // peach → rose → ember → plum
                    return new[] { 0xffc6a8, 0xffeedd, 0xe892a8, 0xffcaa6, 0x7a4e8c, 0xf59c7a, 0x2a1f45, 0x7a3e62 };
                case "vapor": // mint and lilac pastels
                    return new[] { 0xb8eee4, 0xf8dcf5, 0xa9d8ff, 0xffe3f3, 0xc5b4ff, 0xffd3e8, 0x5a4a9e, 0x9f7fd1 };
                case "matrix": // matcha forest
                    return new[] { 0xd2e9cc, 0xf3f7e6, 0xa7d3b0, 0xe6f2d5, 0x5e8f75, 0xc8dfb2, 0x1c2e2a, 0x3f5e4c };
                default: // synthwave id, "daybreak" look: dawn → day → dusk → night
                    return new[] { 0xc9c3f0, 0xffdccb, 0x9fd3f2, 0xd8f3e6, 0x8e6fb5, 0xffb59a, 0x1e2346, 0x4a3f7a };
            }
        }

        private static float Wrap(float h) => h - Mathf.Floor(h);

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
