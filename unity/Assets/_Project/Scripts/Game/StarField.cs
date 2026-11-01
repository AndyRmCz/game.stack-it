using StackIt.Client;
using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Stars falling behind the tower, like the store art's sky. <see cref="StarDrift"/> moves them;
    /// this draws them as particles just in front of the <see cref="SkyBackdrop"/> in the look's
    /// sky.stars colours. Looks with sky.starTwinkle twinkle them and draw the brightest few as
    /// four-point sparkles (two crossed streaks). Sizes follow the screen, so the game-over zoom
    /// doesn't shrink them. Reduce motion holds them still. Purely visual.
    /// </summary>
    public sealed class StarField : MonoBehaviour
    {
        private const float MinSize = 0.006f; // fractions of the screen height
        private const float MaxSize = 0.016f;
        private const int Sparkles = 3;
        private const float Depth = 0.85f;    // of the far plane: in front of the sky (0.9), behind the tower

        private Camera _camera;
        private ParticleSystem _system;
        private ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[0];
        private StarDrift _drift = new StarDrift(0, 0);
        private Color[] _tones = { Color.white };
        private float _twinkle = -1f;
        private int _height;

        public bool ReduceMotion { get; set; }

        public static StarField Create(Camera camera)
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(camera.transform, false);
            var field = go.AddComponent<StarField>();
            field._camera = camera;
            field._system = go.AddComponent<ParticleSystem>();
            field._system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = field._system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // camera space: the stars stay on screen
            main.startSize3D = true;
            main.gravityModifier = 0f;
            main.useUnscaledTime = true;
            ParticleSystem.EmissionModule emission = field._system.emission;
            emission.enabled = false; // placed by hand every frame
            ParticleSystem.ShapeModule shape = field._system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Materials.Dust;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            field._system.Play();
            return field;
        }

        public void SetLook(ThemePalette look)
        {
            Rgba[] tones = look.Colors(T.SkyStars);
            _tones = new Color[Mathf.Max(1, tones.Length)];
            for (int i = 0; i < _tones.Length; i++) _tones[i] = i < tones.Length ? Palette.ToColor(tones[i]) : Color.clear;
            float twinkle = Mathf.Clamp01(look.Number(T.SkyStarTwinkle));
            int count = Mathf.Max(0, Mathf.RoundToInt(look.Number(T.SkyStarCount)));
            // Same sky (count, sparkles) as before: keep the stars where they are, only recolour.
            if (count == _drift.Stars.Count && Mathf.Approximately(twinkle, _twinkle)) return;
            _twinkle = twinkle;
            int sparkles = twinkle > 0f ? Mathf.Min(Sparkles, count) : 0;
            _drift = new StarDrift(count, seed: 7, tones: _tones.Length, sparkles: sparkles);
            _particles = new ParticleSystem.Particle[count + sparkles];
        }

        /// <summary>The tower's height: each new block slides the sky down. A new tower restarts the count.</summary>
        public void SetHeight(int blocks)
        {
            if (blocks > _height) _drift.Climb(blocks - _height);
            _height = blocks;
        }

        /// <summary>A perfect-streak fanfare (1..3): the stars hurry for a moment.</summary>
        public void Streak(int fanfare)
        {
            if (fanfare > 0) _drift.Boost(0.4f + 0.2f * fanfare);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            _drift.Step(Time.unscaledDeltaTime, ReduceMotion);
            float h = _camera.orthographicSize * 2f;
            float w = h * _camera.aspect;
            float z = _camera.farClipPlane * Depth;
            float t = Time.unscaledTime;
            int n = 0;
            for (int i = 0; i < _drift.Stars.Count; i++)
            {
                StarDrift.Star s = _drift.Stars[i];
                Color c = _tones[s.Tone % _tones.Length];
                if (_twinkle > 0f && !ReduceMotion) c.a *= 1f - _twinkle * 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f + s.Phase));
                var position = new Vector3((s.X - 0.5f) * w, (s.Y - 0.5f) * h, z);
                float size = Mathf.Lerp(MinSize, MaxSize, s.Size) * h;
                if (s.Sparkle)
                {
                    // The art's twinkles: a long thin streak each way, a touch bigger than the rest.
                    float longSide = MaxSize * 2.6f * h, thin = MaxSize * 0.45f * h;
                    Set(n++, position, new Vector3(longSide, thin, 1f), c);
                    Set(n++, position, new Vector3(thin, longSide, 1f), c);
                }
                else Set(n++, position, new Vector3(size, size, 1f), c);
            }
            _system.SetParticles(_particles, n);
        }

        private void Set(int index, Vector3 position, Vector3 size, Color color)
        {
            ref ParticleSystem.Particle p = ref _particles[index];
            p.position = position;
            p.startSize3D = size;
            p.startColor = color;
            p.startLifetime = 1000f;
            p.remainingLifetime = 1000f;
        }
    }
}
