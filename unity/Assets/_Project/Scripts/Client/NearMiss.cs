using System;

namespace StackIt.Client
{
    public enum NearMissKind
    {
        None,
        AlmostPerfect, // a slice just outside the perfect window
        AlmostSaved,   // a miss that only just failed to touch the tower
    }

    /// <summary>A drop that was razor-thin either way, and by how much (shown as millimetres).</summary>
    public readonly struct NearMiss
    {
        public static readonly NearMiss None = new NearMiss(NearMissKind.None, 0f);

        /// <summary>A block (30,000 sim units) reads as 10 cm, so 300 units are a millimetre.</summary>
        public const int UnitsPerMillimetre = 300;

        /// <summary>A slice counts as "almost perfect" within 1.5 mm of the perfect window.</summary>
        public const int PerfectMargin = 450;

        /// <summary>A miss counts as "almost saved" when the block cleared the tower by under 5 mm.</summary>
        public const int SavedMargin = 1500;

        public readonly NearMissKind Kind;
        public readonly float Millimetres;

        public NearMiss(NearMissKind kind, float millimetres)
        {
            Kind = kind;
            Millimetres = millimetres;
        }

        /// <param name="missed">The block missed the tower entirely.</param>
        /// <param name="perfect">The drop was perfect.</param>
        /// <param name="offset">How far the block was from the tower's top, in sim units.</param>
        /// <param name="span">The top block's width along the axis the block moved on.</param>
        /// <param name="tolerance">The perfect window at the block's speed.</param>
        public static NearMiss Classify(bool missed, bool perfect, int offset, int span, int tolerance)
        {
            int distance = Math.Abs(offset);
            if (missed)
            {
                int past = distance - span;
                return past <= SavedMargin ? new NearMiss(NearMissKind.AlmostSaved, ToMillimetres(past)) : None;
            }
            if (perfect) return None;
            int beyond = distance - tolerance;
            return beyond <= PerfectMargin ? new NearMiss(NearMissKind.AlmostPerfect, ToMillimetres(beyond)) : None;
        }

        // Never "0.0 mm": the smallest thing shown is 0.1.
        private static float ToMillimetres(int units) =>
            Math.Max(0.1f, (float)Math.Round(units / (double)UnitsPerMillimetre, 1));
    }
}
