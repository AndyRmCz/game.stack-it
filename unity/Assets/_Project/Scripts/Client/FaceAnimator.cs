using System;

namespace StackIt.Client
{
    /// <summary>
    /// Keeps a face alive: eases from one mood to the next, blinks and glances around. Shared by
    /// the 2D and the 3D Blocky; each frame's pose goes to <see cref="FaceDrawing"/>.
    /// </summary>
    public sealed class FaceAnimator
    {
        private const float EasePerSecond = 14f;
        private const float BlinkSeconds = 0.14f, MinBlinkGap = 2f, MaxBlinkGap = 5f;
        private const float GlanceSeconds = 0.8f, MinGlanceGap = 3f, MaxGlanceGap = 6.5f, GlanceReach = 0.55f;

        private readonly Random _random;
        private FacePose _target;
        private float _toBlink, _blink;
        private float _toGlance, _glance, _glanceX, _glanceY;

        public FacePose Current { get; private set; }

        /// <summary>The mood's own pose: what a still Blocky shows.</summary>
        public FacePose Target => _target;

        public FaceAnimator(int seed)
        {
            _random = new Random(seed);
            _toBlink = Between(MinBlinkGap, MaxBlinkGap);
            _toGlance = Between(MinGlanceGap, MaxGlanceGap);
            Set(MascotMood.Happy, snap: true);
        }

        public void Set(MascotMood mood, bool snap = false)
        {
            _target = FacePoses.For(mood);
            if (snap) Current = _target;
        }

        /// <returns>The pose to draw: the mood's, with this moment's blink and glance on top.</returns>
        public FacePose Tick(float dt)
        {
            Current = FacePose.Lerp(Current, _target, 1f - (float)Math.Exp(-EasePerSecond * dt));
            FacePose shown = Current;
            Blink(dt, ref shown);
            Glance(dt, ref shown);
            return shown;
        }

        private void Blink(float dt, ref FacePose shown)
        {
            _toBlink -= dt;
            if (_toBlink <= 0f)
            {
                _blink = BlinkSeconds;
                _toBlink = Between(MinBlinkGap, MaxBlinkGap);
            }
            if (_blink <= 0f) return;
            shown.EyeOpen *= 1f - (float)Math.Sin(Math.PI * (1f - _blink / BlinkSeconds));
            _blink -= dt;
        }

        private void Glance(float dt, ref FacePose shown)
        {
            _toGlance -= dt;
            if (_toGlance <= 0f)
            {
                _glance = GlanceSeconds;
                _glanceX = Between(-GlanceReach, GlanceReach);
                _glanceY = Between(-GlanceReach, GlanceReach) * 0.5f;
                _toGlance = Between(MinGlanceGap, MaxGlanceGap);
            }
            if (_glance <= 0f) return;
            float k = (float)Math.Sin(Math.PI * (1f - _glance / GlanceSeconds)); // out and back
            shown.LookX = Clamp(shown.LookX + _glanceX * k);
            shown.LookY = Clamp(shown.LookY + _glanceY * k);
            _glance -= dt;
        }

        private float Between(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private static float Clamp(float v) => Math.Max(-1f, Math.Min(1f, v));
    }
}
