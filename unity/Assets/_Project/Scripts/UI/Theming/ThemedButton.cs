using StackIt.Client.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    public enum ButtonKind
    {
        Primary,   // accent face on a darker edge, with a glow: one per screen
        Danger,    // primary-shaped, for confirming destructive actions
        Secondary, // outlined pill
        Icon,      // square surface holding an icon
    }

    /// <summary>
    /// Colours a button's parts from tokens: <see cref="Edge"/> (the pressable bottom edge and the
    /// glow, primary/danger only), <see cref="Face"/>, and the label and icon (<see cref="Ink"/>).
    /// </summary>
    public sealed class ThemedButton : Themed
    {
        public ButtonKind Kind;
        public RoundedRect Edge;
        public RoundedRect Face;
        public Graphic[] Ink;

        /// <summary>0..1 multiplier on the glow, driven by <see cref="Breathe"/> in looks that pulse.</summary>
        public float GlowLevel = 1f;

        public override void Apply(ThemePalette p)
        {
            // AddComponent runs OnEnable before the builder sets the parts; it applies again after.
            if (Face == null || (Edge == null && (Kind == ButtonKind.Primary || Kind == ButtonKind.Danger))) return;
            Color ink;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    Face.Fill = Get(p, T.Accent);
                    Edge.Fill = Get(p, T.AccentEdge);
                    Glow(p.Glow(T.AccentGlow));
                    ink = Get(p, T.AccentOn);
                    break;
                case ButtonKind.Danger:
                    Color danger = Get(p, T.Danger);
                    Face.Fill = danger;
                    Edge.Fill = new Color(danger.r * 0.68f, danger.g * 0.68f, danger.b * 0.68f, 1f);
                    Edge.SetShadow(Color.clear, 0f, 0f);
                    ink = Get(p, T.DangerOn);
                    break;
                case ButtonKind.Secondary:
                    Face.Fill = Get(p, T.SecondaryFill);
                    Face.Border = Get(p, T.SecondaryBorder);
                    Face.BorderWidth = p.Number(T.SecondaryBorderWidth);
                    ink = Get(p, T.SecondaryText);
                    break;
                default:
                    Face.Fill = Get(p, T.Surface);
                    Face.Border = Get(p, T.SurfaceBorder);
                    Face.BorderWidth = p.Number(T.SurfaceBorderWidth);
                    Glow surface = p.Glow(T.SurfaceShadow);
                    Face.SetShadow(surface.IsNone ? Color.clear : Look.ToColor(surface.Color), surface.Blur, surface.Y);
                    ink = Get(p, T.TextPrimary);
                    break;
            }
            if (Ink == null) return;
            foreach (Graphic g in Ink)
                if (g != null) g.color = ink;
        }

        /// <summary>Re-applies the glow at <see cref="GlowLevel"/> (cheap: just the edge's shadow).</summary>
        public void ApplyGlow()
        {
            if (Kind == ButtonKind.Primary && Edge != null) Glow(Look.P.Glow(T.AccentGlow));
        }

        private void Glow(Glow glow)
        {
            if (glow.IsNone)
            {
                Edge.SetShadow(Color.clear, 0f, 0f);
                return;
            }
            Color c = Look.ToColor(glow.Color);
            c.a *= GlowLevel;
            Edge.SetShadow(c, glow.Blur, glow.Y);
        }
    }
}
