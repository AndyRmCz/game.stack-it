using StackIt.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StackIt.Integrations
{
    /// <summary>
    /// Global URP volume: bloom for the emissive blocks, a chromatic-aberration pulse on perfects,
    /// a violet vignette and neutral tonemapping. Built in code so there is no profile asset to lose.
    /// </summary>
    public sealed class UrpPostFx : MonoBehaviour, IPostFx
    {
        private Bloom _bloom;
        private ChromaticAberration _chromatic;
        private float _pulse;

        public void Configure(Camera camera)
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>(true);
            _bloom.intensity.Override(1.15f);
            _bloom.threshold.Override(0.85f);
            _bloom.scatter.Override(0.72f);

            _chromatic = profile.Add<ChromaticAberration>(true);
            _chromatic.intensity.Override(0f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.color.Override(new Color(0.12f, 0f, 0.22f));

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        public void ChromaticPulse(float strength) => _pulse = Mathf.Max(_pulse, Mathf.Clamp01(strength));

        public void SetBloom(float intensity)
        {
            if (_bloom != null) _bloom.intensity.value = intensity;
        }

        private void Update()
        {
            if (_chromatic == null) return;
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.unscaledDeltaTime * 2.2f);
            _chromatic.intensity.value = _pulse;
        }
    }
}
