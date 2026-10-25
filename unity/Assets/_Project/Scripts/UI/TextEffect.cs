using StackIt.Client.Theming;
using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    public enum TextEffectKind
    {
        Shadow,   // soft drop shadow (white numbers over the game)
        LogoGlow, // text.logoGlow around the logo and the HUD score (Neon; none in Pop)
        ArcadeGold, // arcade.*: gold gradient letters, dark outline, hard drop shadow (near-miss callout)
    }

    /// <summary>
    /// A TMP underlay used as a soft drop shadow or as the look's glow. Material changes wait for
    /// Start: screens are built under an inactive root, and until TMP's Awake has loaded the font
    /// material, touching material properties throws. Each label using this gets its own material
    /// instance, so it is kept to a few large labels. (The only file that touches TMP materials.)
    /// The shader compiles these keywords only for combinations a built material uses: the
    /// materials in Resources/StackIt/TextVariants keep them in the player build.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TextEffect : Themed
    {
        public TextEffectKind Kind;

        private Color _color;
        private float _softness;
        private float _offset;
        private Color _outline;
        private float _outlineWidth;
        private bool _started;

        public static TextEffect Add(TextMeshProUGUI label, TextEffectKind kind)
        {
            var effect = label.gameObject.AddComponent<TextEffect>();
            effect.Kind = kind;
            effect.Refresh();
            return effect;
        }

        public override void Apply(ThemePalette p)
        {
            _outlineWidth = 0f;
            if (Kind == TextEffectKind.Shadow)
            {
                _color = new Color(0f, 0f, 0f, 0.28f);
                _softness = 0.55f;
                _offset = -0.6f;
            }
            else if (Kind == TextEffectKind.ArcadeGold)
            {
                // Vertex colours: TMP multiplies the gradient by the label's colour, so that stays white.
                var label = GetComponent<TextMeshProUGUI>();
                Color top = Look.ToColor(p.Color(T.ArcadeGoldTop)), bottom = Look.ToColor(p.Color(T.ArcadeGoldBottom));
                label.color = Color.white;
                label.enableVertexGradient = true;
                label.colorGradient = new VertexGradient(top, top, bottom, bottom);
                _outline = Look.ToColor(p.Color(T.ArcadeOutline));
                _outlineWidth = 0.28f;
                _color = Look.ToColor(p.Color(T.ArcadeShadow));
                _softness = 0f; // a hard, printed-looking shadow
                _offset = -0.9f;
            }
            else
            {
                Glow glow = p.Glow(T.TextLogoGlow);
                _color = Look.ToColor(glow.Color);
                _softness = Mathf.Clamp01(glow.Blur / 24f); // 18 px blur ≈ 0.75 of the underlay's softness range
                _offset = 0f;
            }
            if (_started) Push();
        }

        private void Start()
        {
            _started = true;
            Push();
        }

        private void Push()
        {
            var label = GetComponent<TextMeshProUGUI>();
            Material material = label.fontMaterial; // per-label instance
            if (_outlineWidth > 0f)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                material.SetColor(ShaderUtilities.ID_OutlineColor, _outline);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, _outlineWidth);
            }
            if (_color.a <= 0f)
            {
                material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            }
            else
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, _color);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, _offset);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, _softness);
                // Glow and arcade shadow spread past the letters (the arcade one covers the outline).
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, Kind == TextEffectKind.Shadow ? 0f : Kind == TextEffectKind.LogoGlow ? 0.4f : _outlineWidth);
            }
            ShaderUtilities.UpdateShaderRatios(material); // keeps outline + shadow inside the glyphs' padding
            label.UpdateMeshPadding();
        }
    }
}
