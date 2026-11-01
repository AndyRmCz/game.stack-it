using System;
using System.Collections.Generic;

namespace StackIt.Client
{
    /// <summary>
    /// The stars behind the tower, in screen fractions (0..1, 0 = bottom). They drift down on
    /// their own, slide further down for every block the tower climbs (bigger, nearer stars faster:
    /// parallax sells the climb) and hurry for a moment on a streak. A star that falls off the
    /// bottom comes back at the top somewhere else. Reduce motion holds them still. Engine-free.
    /// </summary>
    public sealed class StarDrift
    {
        public struct Star
        {
            public float X, Y;
            public float Size;  // 0 (far, small) .. 1 (near, big)
            public float Phase; // twinkle offset, radians
            public int Tone;    // index into the look's star colours
            public bool Sparkle; // drawn as a 4-point twinkle, like the store art's brightest few
        }

        public const float IdleSpeed = 0.012f; // screens per second, nearest star
        public const float PerBlock = 0.035f;  // screens one block of climb slides the nearest star
        private const float ClimbEase = 3f;    // a climb plays out over about a third of a second
        private const float BoostDecay = 1.5f; // per second
        private const float BoostSpeed = 4f;   // a full boost runs at 5x idle

        private readonly Star[] _stars;
        private readonly Random _random;
        private float _climb; // screens still to slide
        private float _boost; // 0..1, decays

        public StarDrift(int count, int seed, int tones = 1, int sparkles = 0)
        {
            _random = new Random(seed);
            _stars = new Star[Math.Max(0, count)];
            for (int i = 0; i < _stars.Length; i++)
            {
                float r = Next();
                _stars[i] = new Star
                {
                    X = Next(),
                    Y = Next(),
                    Size = r * r, // mostly small, a few near ones
                    Phase = Next() * 6.2832f,
                    Tone = _random.Next(Math.Max(1, tones)),
                    Sparkle = i < sparkles,
                };
            }
        }

        public IReadOnlyList<Star> Stars => _stars;

        /// <summary>Fall speed factor by size: far stars move at 40%, the nearest at 100%.</summary>
        public static float Speed(float size) => 0.4f + 0.6f * size;

        /// <summary>The tower grew by <paramref name="blocks"/>: the sky slides down to match.</summary>
        public void Climb(int blocks)
        {
            if (blocks > 0) _climb += blocks * PerBlock;
        }

        /// <summary>A streak: the stars hurry, then settle (0..1).</summary>
        public void Boost(float amount) => _boost = Math.Max(_boost, Math.Min(1f, amount));

        public void Step(float dt, bool reduceMotion)
        {
            if (reduceMotion)
            {
                _climb = 0f; // nothing saved up to rush through when motion comes back
                _boost = 0f;
                return;
            }
            if (dt <= 0f) return;
            float slide = _climb * Math.Min(1f, dt * ClimbEase);
            _climb -= slide;
            float idle = IdleSpeed * (1f + BoostSpeed * _boost) * dt;
            _boost = Math.Max(0f, _boost - dt * BoostDecay);
            for (int i = 0; i < _stars.Length; i++)
            {
                ref Star s = ref _stars[i];
                s.Y -= (idle + slide) * Speed(s.Size);
                if (s.Y >= 0f) continue;
                while (s.Y < 0f) s.Y += 1f;
                s.X = Next();
            }
        }

        private float Next() => (float)_random.NextDouble();
    }
}
