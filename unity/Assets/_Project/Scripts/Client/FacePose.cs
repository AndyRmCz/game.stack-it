using System;

namespace StackIt.Client
{
    /// <summary>
    /// Blocky's face as numbers, so one mood can blend into the next. Lengths are fractions of the
    /// block's width. Drawn by <see cref="FaceDrawing"/>; the same pose works in 2D and 3D. The
    /// skews part the two sides: the right side gets the amount added, the left takes it away, and
    /// faces come alive in that difference.
    /// </summary>
    public struct FacePose : IEquatable<FacePose>
    {
        public float EyeOpen;     // 1 round, 0 shut
        public float EyeSkew;
        public float LidTilt;     // upper lid's slope: above 0 cross (inner end low), below 0 sad (outer end low)
        public float LidSkew;
        public float LowerLid;    // 0..1, pushed up by the cheeks
        public float Beam;        // 1: eyes shut in a smile
        public float LookX;       // -1..1, right
        public float LookY;       // -1..1, up
        public float PupilShrink; // 0..1
        public float BrowRaise;
        public float BrowSkew;
        public float BrowTilt;    // above 0 worried (inner ends up), below 0 cross
        public float BrowTwist;
        public float FaceShift;   // the whole face, sideways: it turns toward what it's looking at
        public float MouthShift;
        public float MouthWidth;  // half of it
        public float MouthCurve;  // above 0 the corners rise
        public float MouthSkew;   // above 0 the right corner sits higher
        public float MouthOpen;
        public float MouthWave;   // a wobble: nerves
        public float Tongue;      // 0..1
        public float Teeth;       // how deep the top row shows
        public float Blush;       // 0..1
        public float Squash;      // above 0 stretches up, below 0 slumps

        public static FacePose Lerp(in FacePose a, in FacePose b, float t)
        {
            float L(float x, float y) => t >= 1f ? y : x + (y - x) * t;
            return new FacePose
            {
                EyeOpen = L(a.EyeOpen, b.EyeOpen),
                EyeSkew = L(a.EyeSkew, b.EyeSkew),
                LidTilt = L(a.LidTilt, b.LidTilt),
                LidSkew = L(a.LidSkew, b.LidSkew),
                LowerLid = L(a.LowerLid, b.LowerLid),
                Beam = L(a.Beam, b.Beam),
                LookX = L(a.LookX, b.LookX),
                LookY = L(a.LookY, b.LookY),
                PupilShrink = L(a.PupilShrink, b.PupilShrink),
                BrowRaise = L(a.BrowRaise, b.BrowRaise),
                BrowSkew = L(a.BrowSkew, b.BrowSkew),
                BrowTilt = L(a.BrowTilt, b.BrowTilt),
                BrowTwist = L(a.BrowTwist, b.BrowTwist),
                FaceShift = L(a.FaceShift, b.FaceShift),
                MouthShift = L(a.MouthShift, b.MouthShift),
                MouthWidth = L(a.MouthWidth, b.MouthWidth),
                MouthCurve = L(a.MouthCurve, b.MouthCurve),
                MouthSkew = L(a.MouthSkew, b.MouthSkew),
                MouthOpen = L(a.MouthOpen, b.MouthOpen),
                MouthWave = L(a.MouthWave, b.MouthWave),
                Tongue = L(a.Tongue, b.Tongue),
                Teeth = L(a.Teeth, b.Teeth),
                Blush = L(a.Blush, b.Blush),
                Squash = L(a.Squash, b.Squash),
            };
        }

        public bool Equals(FacePose o) =>
            EyeOpen == o.EyeOpen && EyeSkew == o.EyeSkew && LidTilt == o.LidTilt && LidSkew == o.LidSkew
            && LowerLid == o.LowerLid && Beam == o.Beam && LookX == o.LookX && LookY == o.LookY
            && PupilShrink == o.PupilShrink && BrowRaise == o.BrowRaise && BrowSkew == o.BrowSkew
            && BrowTilt == o.BrowTilt && BrowTwist == o.BrowTwist && FaceShift == o.FaceShift
            && MouthShift == o.MouthShift && MouthWidth == o.MouthWidth && MouthCurve == o.MouthCurve
            && MouthSkew == o.MouthSkew && MouthOpen == o.MouthOpen && MouthWave == o.MouthWave
            && Tongue == o.Tongue && Teeth == o.Teeth && Blush == o.Blush && Squash == o.Squash;

        public override bool Equals(object obj) => obj is FacePose other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EyeOpen, LidTilt, Beam, LookX, LookY, BrowTilt, MouthCurve, MouthOpen);
    }

    public static class FacePoses
    {
        public static FacePose For(MascotMood mood)
        {
            switch (mood)
            {
                case MascotMood.Cheer:
                    return new FacePose
                    {
                        EyeOpen = 1f, LookX = 0.05f, LookY = 0.55f,
                        BrowRaise = 0.0375f, BrowSkew = 0.0075f, BrowTilt = 0.07f, BrowTwist = 0.01f,
                        MouthWidth = 0.14f, MouthCurve = 0.03f, MouthSkew = -0.006f, MouthOpen = 0.17f, Tongue = 1f,
                        Blush = 1f,
                    };
                case MascotMood.Rally:
                    return new FacePose
                    {
                        EyeOpen = 0.74f, EyeSkew = 0.02f, LidTilt = 0.39f, LidSkew = -0.03f, LowerLid = 0.08f,
                        LookX = -0.45f, LookY = 0.05f, FaceShift = -0.015f,
                        BrowRaise = -0.0225f, BrowSkew = 0.0075f, BrowTilt = -0.39f, BrowTwist = 0.03f,
                        MouthShift = -0.0025f, MouthWidth = 0.1075f, MouthCurve = 0.0205f, MouthSkew = 0.0275f,
                    };
                case MascotMood.Joy:
                    return new FacePose
                    {
                        EyeOpen = 1f, Beam = 1f,
                        BrowRaise = 0.025f, BrowSkew = 0.005f, BrowTilt = 0.1f,
                        MouthWidth = 0.15f, MouthCurve = 0.035f, MouthSkew = 0.008f, MouthOpen = 0.19f, Tongue = 1f, Teeth = 0.03f,
                        Blush = 1f, Squash = 0.03f,
                    };
                case MascotMood.Smug:
                    return new FacePose
                    {
                        EyeOpen = 0.64f, EyeSkew = 0.06f, LidTilt = 0.01f, LidSkew = -0.11f, LowerLid = 0.12f,
                        LookX = -0.6f, FaceShift = 0.02f,
                        BrowRaise = 0.01f, BrowSkew = 0.04f, BrowTilt = -0.02f, BrowTwist = 0.1f,
                        MouthShift = 0.0625f, MouthWidth = 0.1025f, MouthCurve = 0.037f, MouthSkew = 0.0385f,
                        Blush = 0.63f,
                    };
                case MascotMood.Nervous:
                    return new FacePose
                    {
                        EyeOpen = 0.99f, EyeSkew = -0.03f, LookX = 0.55f, LookY = 0.5f, PupilShrink = 0.2f, FaceShift = 0.01f,
                        BrowRaise = 0.04f, BrowSkew = 0.01f, BrowTilt = 0.46f, BrowTwist = 0.04f,
                        MouthWidth = 0.1325f, MouthCurve = -0.003f, MouthSkew = -0.0025f, MouthWave = 0.017f,
                    };
                case MascotMood.Shocked:
                    return new FacePose
                    {
                        EyeOpen = 1.15f, LookX = 0.05f, PupilShrink = 0.38f,
                        BrowRaise = 0.06f, BrowSkew = 0.015f, BrowTilt = 0.13f, BrowTwist = 0.03f,
                        MouthWidth = 0.075f, MouthSkew = -0.005f, MouthOpen = 0.15f,
                        Squash = 0.06f,
                    };
                case MascotMood.Agony:
                    return new FacePose
                    {
                        EyeOpen = 0.68f, EyeSkew = 0.04f, LidTilt = -0.46f, LidSkew = -0.04f, LowerLid = 0.05f, LookY = -0.75f,
                        BrowRaise = 0.04f, BrowSkew = 0.01f, BrowTilt = 0.56f, BrowTwist = 0.06f,
                        MouthWidth = 0.1175f, MouthCurve = -0.061f, MouthSkew = -0.0025f, MouthWave = 0.004f,
                        Squash = -0.1f,
                    };
                case MascotMood.Sulking:
                    return new FacePose
                    {
                        EyeOpen = 0.53f, EyeSkew = 0.03f, LidTilt = 0.12f, LidSkew = 0.02f,
                        LookX = 0.8f, LookY = -0.15f, FaceShift = 0.03f,
                        BrowRaise = -0.0225f, BrowSkew = 0.0075f, BrowTilt = -0.18f, BrowTwist = -0.02f,
                        MouthShift = 0.0375f, MouthWidth = 0.0825f, MouthCurve = -0.025f, MouthSkew = 0.01f,
                        Blush = 0.35f,
                    };
                default:
                    return new FacePose
                    {
                        EyeOpen = 0.85f, EyeSkew = -0.01f, LowerLid = 0.1f, LookX = -0.35f, LookY = -0.1f, FaceShift = -0.012f,
                        BrowRaise = 0.004f, BrowSkew = 0.008f, BrowTilt = 0.04f, BrowTwist = 0.01f,
                        MouthShift = 0.015f, MouthWidth = 0.11f, MouthCurve = 0.056f, MouthSkew = 0.0125f,
                        Blush = 0.77f,
                    };
            }
        }
    }
}
