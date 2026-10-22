using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// Outline glow for a TMP label, applied in Start. Screens are built under an inactive root, so
    /// TMP has not run Awake (which loads the font material) when UiKit creates the label; setting
    /// outline properties then throws. Start runs once the screen is first shown, after Awake.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TextGlow : MonoBehaviour
    {
        public float Width;
        public Color Color;

        private void Start()
        {
            var label = GetComponent<TextMeshProUGUI>();
            label.outlineWidth = Width;
            label.outlineColor = Color;
        }
    }
}
