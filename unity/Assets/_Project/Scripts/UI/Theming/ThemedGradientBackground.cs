using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// The screen background: bg.gradient through its three stops, plus bg.centerGlow (Neon) as a
    /// radial glow behind the hero, sized as a fraction of the screen width.
    /// </summary>
    public sealed class ThemedGradientBackground : Themed
    {
        public GradientBackground Target;
        public RoundedRect CenterGlow;

        public override void Apply(ThemePalette p)
        {
            if (Target == null) return; // AddComponent runs OnEnable before the builder sets Target
            Color[] stops = Look.Colors(T.BgGradient);
            Target.SetStops(stops[0], stops[1], stops[2], p.Number(T.BgGradientMid));
            if (CenterGlow == null) return;
            Glow glow = p.Glow(T.BgCenterGlow);
            CenterGlow.gameObject.SetActive(!glow.IsNone);
            float width = ((RectTransform)Target.transform).rect.width;
            if (width <= 0f) width = 390f;
            CenterGlow.Fill = Color.clear;
            CenterGlow.SetShadow(Look.ToColor(glow.Color), glow.Radius * width, 0f);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && Target != null) Refresh();
        }
    }
}
