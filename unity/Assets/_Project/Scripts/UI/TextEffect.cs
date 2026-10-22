using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// Outline and soft drop shadow (TMP underlay) for a label, applied in Start. Screens are built
    /// under an inactive root, so TMP has not run Awake (which loads the font material) when UiKit
    /// creates the label; touching material properties then throws. Start runs once the screen is
    /// first shown, after Awake. Each label using this gets its own material instance, so it is
    /// kept to a few large labels.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TextEffect : MonoBehaviour
    {
        public float OutlineWidth;
        public Color OutlineColor;
        public bool Shadow;
        public Color ShadowColor = new Color(0.086f, 0.129f, 0.243f, 0.28f);
        public float ShadowOffset = -0.6f;
        public float ShadowSoftness = 0.55f;

        private void Start()
        {
            var label = GetComponent<TextMeshProUGUI>();
            if (OutlineWidth > 0f)
            {
                label.outlineWidth = OutlineWidth;
                label.outlineColor = OutlineColor;
            }
            if (Shadow)
            {
                Material material = label.fontMaterial; // per-label instance
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, ShadowColor);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, ShadowOffset);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, ShadowSoftness);
                label.UpdateMeshPadding();
            }
        }
    }
}
