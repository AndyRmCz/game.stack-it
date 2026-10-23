using System;
using System.Collections.Generic;

namespace StackIt.Client.Theming
{
    /// <summary>
    /// The tower's colour as it grows: the ramp's stops are <see cref="BlocksPerStop"/> blocks apart,
    /// interpolated in between, and the last stop blends back into the first, so the ramp loops.
    /// Side faces are the top colour shaded (left 0.85, right 0.70).
    /// </summary>
    public static class HeightRamp
    {
        public const int BlocksPerStop = 10;
        public const float LeftShade = 0.85f;
        public const float RightShade = 0.70f;

        public static Rgba At(IReadOnlyList<Rgba> stops, float block)
        {
            if (stops == null || stops.Count == 0) throw new ArgumentException("The ramp needs at least one stop.");
            float position = Math.Max(0f, block) / BlocksPerStop;
            int whole = (int)Math.Floor(position);
            int i = whole % stops.Count;
            return ColorMath.Lerp(stops[i], stops[(i + 1) % stops.Count], position - whole);
        }

        public static void Faces(Rgba top, out Rgba left, out Rgba right)
        {
            left = ColorMath.Shade(top, LeftShade);
            right = ColorMath.Shade(top, RightShade);
        }
    }
}
