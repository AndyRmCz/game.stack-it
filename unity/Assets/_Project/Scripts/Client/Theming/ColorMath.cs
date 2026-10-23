using System;
using System.Globalization;

namespace StackIt.Client.Theming
{
    /// <summary>An sRGB colour with alpha, 0..1 per channel (engine-free; UI code converts it).</summary>
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);

        public bool IsNone => A <= 0f;

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object obj) => obj is Rgba o && Equals(o);
        public override int GetHashCode() => (R, G, B, A).GetHashCode();
        public override string ToString() => ColorMath.ToHex(this);
    }

    /// <summary>Hex parsing, shading, mixing and WCAG contrast.</summary>
    public static class ColorMath
    {
        /// <summary>Parses #RRGGBB or #RRGGBBAA.</summary>
        public static Rgba Hex(string hex)
        {
            if (hex == null || hex.Length != 7 && hex.Length != 9 || hex[0] != '#')
                throw new FormatException($"Expected #RRGGBB or #RRGGBBAA, got '{hex}'.");
            float Channel(int at) => int.Parse(hex.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
            return new Rgba(Channel(1), Channel(3), Channel(5), hex.Length == 9 ? Channel(7) : 1f);
        }

        public static string ToHex(Rgba c)
        {
            int Byte(float v) => (int)Math.Round(Clamp01(v) * 255f);
            string rgb = $"#{Byte(c.R):X2}{Byte(c.G):X2}{Byte(c.B):X2}";
            return c.A >= 1f ? rgb : rgb + Byte(c.A).ToString("X2");
        }

        /// <summary>Multiplies the colour channels (a darker or lighter shade; alpha kept).</summary>
        public static Rgba Shade(Rgba c, float factor) =>
            new Rgba(Clamp01(c.R * factor), Clamp01(c.G * factor), Clamp01(c.B * factor), c.A);

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        /// <summary>Alpha-composites <paramref name="top"/> over an opaque <paramref name="under"/>.</summary>
        public static Rgba Over(Rgba top, Rgba under) =>
            new Rgba(top.R * top.A + under.R * (1 - top.A), top.G * top.A + under.G * (1 - top.A), top.B * top.A + under.B * (1 - top.A));

        /// <summary>WCAG 2.x relative luminance of the (opaque) colour.</summary>
        public static double Luminance(Rgba c)
        {
            static double Linear(float v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        /// <summary>WCAG contrast ratio, 1..21.</summary>
        public static double Contrast(Rgba a, Rgba b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
