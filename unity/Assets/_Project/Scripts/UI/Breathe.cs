using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// The idle call to action "breathes": 1.00 → 1.03 → 1.00 over 2.4 s, ease in-out. In looks
    /// whose accent.glowPulse is on (Neon) the glow swells with it. Off with Reduce motion.
    /// </summary>
    public sealed class Breathe : MonoBehaviour
    {
        private const float Period = 2.4f;
        private const float Amount = 0.03f;

        public ThemedButton Button;

        private float _start;
        private bool _resting = true;

        private void OnEnable() => _start = Time.unscaledTime;

        private void OnDisable() => Rest();

        private void Update()
        {
            if (Look.ReduceMotion)
            {
                Rest();
                return;
            }
            _resting = false;
            // 0 → 1 → 0, smooth at both ends (half a cosine per breath).
            float k = 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime - _start) / Period * Mathf.PI * 2f);
            float s = 1f + Amount * k;
            transform.localScale = new Vector3(s, s, 1f);
            if (Button != null && Look.P.Number(T.AccentGlowPulse) > 0f)
            {
                Button.GlowLevel = 0.6f + 0.4f * k;
                Button.ApplyGlow();
            }
        }

        private void Rest()
        {
            if (_resting) return;
            _resting = true;
            transform.localScale = Vector3.one;
            if (Button == null) return;
            Button.GlowLevel = 1f;
            Button.ApplyGlow();
        }
    }
}
