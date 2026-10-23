using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>The kinds of surface; each maps to a set of tokens.</summary>
    public enum SurfaceKind
    {
        Card,     // surface, its border and shadow (cards, icon buttons, coin pill)
        Raised,   // modal cards and bottom sheets over the game: opaque even where surface is glassy
        Nav,      // the floating bottom navigation
        Sunken,   // tracks of toggles and segmented controls, inputs
        Toast,    // toasts
        Highlight, // the player's own row: accent tint (Pop) or accent border (Neon)
        Solid,    // one fill token, no border or shadow (chips, dots, badges)
    }

    /// <summary>Fills, borders and shadows a <see cref="RoundedRect"/> from tokens.</summary>
    public sealed class ThemedSurface : Themed
    {
        public RoundedRect Target;
        public SurfaceKind Kind;
        public string FillToken;   // Solid only
        public float FillAlpha = 1f;
        public string GlowToken;   // optional glow/shadow token (e.g. accent2.glow on a dot)
        public string RingToken;   // optional ring colour token (Solid)
        public float RingWidth;

        public static ThemedSurface Bind(RoundedRect target, SurfaceKind kind, string fill = null, string glow = null)
        {
            var themed = target.GetComponent<ThemedSurface>();
            if (themed == null) themed = target.gameObject.AddComponent<ThemedSurface>();
            themed.Target = target;
            themed.Kind = kind;
            themed.FillToken = fill;
            themed.GlowToken = glow;
            themed.Refresh();
            return themed;
        }

        public override void Apply(ThemePalette p)
        {
            if (Target == null) return;
            switch (Kind)
            {
                case SurfaceKind.Card:
                    Target.Fill = Get(p, T.Surface);
                    Target.Border = Get(p, T.SurfaceBorder);
                    Target.BorderWidth = p.Number(T.SurfaceBorderWidth);
                    Shadow(p.Glow(T.SurfaceShadow));
                    break;
                case SurfaceKind.Raised:
                    Target.Fill = Get(p, T.SurfaceRaised);
                    Target.Border = Get(p, T.SurfaceBorder);
                    Target.BorderWidth = p.Number(T.SurfaceBorderWidth);
                    Shadow(p.Glow(T.SurfaceShadow));
                    break;
                case SurfaceKind.Nav:
                    Target.Fill = Get(p, T.SurfaceNav);
                    Target.Border = Get(p, T.SurfaceNavBorder);
                    Target.BorderWidth = 1f;
                    Shadow(p.Glow(T.SurfaceShadow));
                    break;
                case SurfaceKind.Sunken:
                    Target.Fill = Get(p, T.SurfaceSunken);
                    Target.BorderWidth = 0f;
                    Shadow(default);
                    break;
                case SurfaceKind.Highlight:
                    Target.Fill = Get(p, T.SurfaceHighlight);
                    Target.Border = Get(p, T.SurfaceHighlightBorder);
                    Target.BorderWidth = 1.5f;
                    Shadow(default);
                    break;
                case SurfaceKind.Toast:
                    Target.Fill = Get(p, T.ToastFill);
                    Target.BorderWidth = 0f;
                    Shadow(p.Glow(T.SurfaceShadow));
                    break;
                default:
                    if (!Has(FillToken)) return;
                    Color fill = Get(p, FillToken);
                    fill.a *= FillAlpha;
                    Target.Fill = fill;
                    Target.BorderWidth = Has(RingToken) ? RingWidth : 0f;
                    if (Has(RingToken)) Target.Border = Get(p, RingToken);
                    Shadow(Has(GlowToken) ? p.Glow(GlowToken) : default);
                    break;
            }
            if (Kind != SurfaceKind.Solid && Has(GlowToken)) Shadow(p.Glow(GlowToken));
        }

        /// <summary>Set means non-empty: Unity serializes unset (null) public strings as "".</summary>
        private static bool Has(string token) => !string.IsNullOrEmpty(token);

        private void Shadow(Glow glow)
        {
            if (glow.IsNone) Target.SetShadow(Color.clear, 0f, 0f);
            else Target.SetShadow(Look.ToColor(glow.Color), glow.Blur, glow.Y);
        }
    }
}
