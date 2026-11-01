using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>Text roles from the design language (sizes in points at the 390-wide reference).</summary>
    public enum TextRole
    {
        Logo,          // Sora 700, 58, +8 (airy: the letters breathe)
        BigNumber,     // Sora 700, 48-96 (size given per use)
        ButtonPrimary, // Sora 700, 26, +0.5
        Title,         // Sora 700, 28, +1
        Overline,      // Lexend Deca 600, 13, +3, uppercase
        Body,          // Lexend Deca 500, 16
        Pill,          // Lexend Deca 600, 15
        Caption,       // Lexend Deca 500, 12
        Coin,          // Sora 600, 18
    }

    /// <summary>
    /// Gives a label its role's font, size and tracking. Fonts are the same in every look; the
    /// component records the role so tools and tests can see it.
    /// </summary>
    public sealed class ThemedFont : MonoBehaviour
    {
        public TextRole Role;

        public static void Apply(TMP_Text label, TextRole role, float size = 0f)
        {
            var themed = label.GetComponent<ThemedFont>();
            if (themed == null) themed = label.gameObject.AddComponent<ThemedFont>();
            themed.Role = role;
            Spec(role, out TMP_FontAsset font, out float defaultSize, out float tracking, out bool upper);
            label.font = font;
            label.fontSize = size > 0f ? size : defaultSize;
            // Tracking is in points; TMP spacing is in hundredths of an em.
            label.characterSpacing = tracking / label.fontSize * 100f;
            label.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        }

        public static void Spec(TextRole role, out TMP_FontAsset font, out float size, out float tracking, out bool upper)
        {
            upper = false;
            tracking = 0f;
            switch (role)
            {
                case TextRole.Logo: font = Fonts.Display; size = 58f; tracking = 8f; break;
                case TextRole.BigNumber: font = Fonts.Display; size = 72f; break;
                case TextRole.ButtonPrimary: font = Fonts.Display; size = 26f; tracking = 0.5f; break;
                case TextRole.Title: font = Fonts.Display; size = 28f; tracking = 1f; break;
                case TextRole.Overline: font = Fonts.BodyHeavy; size = 13f; tracking = 3f; upper = true; break;
                case TextRole.Pill: font = Fonts.BodyHeavy; size = 15f; break;
                case TextRole.Caption: font = Fonts.Body; size = 12f; break;
                case TextRole.Coin: font = Fonts.DisplaySemi; size = 18f; break;
                default: font = Fonts.Body; size = 16f; break;
            }
        }
    }
}
