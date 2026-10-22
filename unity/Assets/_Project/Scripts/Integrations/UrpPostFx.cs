using StackIt.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StackIt.Integrations
{
    /// <summary>
    /// Global URP volume, deliberately light for a calm look and mid-range phones: neutral
    /// tonemapping, a whisper of ink-tinted vignette, and an exposure swell for perfect combos.
    /// Built in code so there is no profile asset to lose.
    /// </summary>
    public sealed class UrpPostFx : MonoBehaviour, IPostFx
    {
        private const float PulseExposure = 0.18f;

        private ColorAdjustments _color;
        private float _pulse;

        public void Configure(Camera camera)
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.6f);
            vignette.color.Override(new Color(0.086f, 0.129f, 0.243f));

            _color = profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0f);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        public void Pulse(float strength) => _pulse = Mathf.Max(_pulse, Mathf.Clamp01(strength));

        private void Update()
        {
            if (_color == null) return;
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.unscaledDeltaTime * 2.5f);
            _color.postExposure.value = PulseExposure * Mathf.SmoothStep(0f, 1f, _pulse);
        }
    }
}
