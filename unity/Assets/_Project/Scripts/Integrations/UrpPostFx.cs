using StackIt.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StackIt.Integrations
{
    /// <summary>
    /// Global URP volume, light enough for mid-range phones: neutral tonemapping, a faint vignette,
    /// bloom in looks that glow (Neon; off in Pop), and on perfect drops an exposure swell with a
    /// chromatic-aberration pulse sized by the look. Built in code so there is no profile asset to lose.
    /// </summary>
    public sealed class UrpPostFx : MonoBehaviour, IPostFx
    {
        private const float PulseExposure = 0.18f;

        private ColorAdjustments _color;
        private Bloom _bloom;
        private ChromaticAberration _chromatic;
        private float _pulse;
        private float _chromaticPulse;

        public void Configure(Camera camera)
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.6f);
            vignette.color.Override(Color.black);

            _bloom = profile.Add<Bloom>(true);
            _bloom.intensity.Override(0f);
            _bloom.highQualityFiltering.Override(false); // mobile

            _chromatic = profile.Add<ChromaticAberration>(true);
            _chromatic.intensity.Override(0f);

            _color = profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0f);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        public void SetLook(float bloomIntensity, float bloomThreshold, float bloomScatter, float chromaticPulse)
        {
            if (_bloom == null) return;
            _bloom.active = bloomIntensity > 0f;
            _bloom.intensity.Override(bloomIntensity);
            _bloom.threshold.Override(bloomThreshold);
            _bloom.scatter.Override(bloomScatter);
            _chromaticPulse = chromaticPulse;
        }

        public void Pulse(float strength) => _pulse = Mathf.Max(_pulse, Mathf.Clamp01(strength));

        private void Update()
        {
            if (_color == null) return;
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.unscaledDeltaTime * 2.5f);
            float k = Mathf.SmoothStep(0f, 1f, _pulse);
            _color.postExposure.value = PulseExposure * k;
            _chromatic.intensity.value = _chromaticPulse * k;
        }
    }
}
