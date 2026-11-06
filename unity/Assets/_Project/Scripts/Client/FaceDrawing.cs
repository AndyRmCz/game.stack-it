using System;

namespace StackIt.Client
{
    public enum FacePart
    {
        Shadow, // the face's shadow on the block's back wall
        Halo,   // light the etched lines catch around them
        Blush,
        Eye,
        EyeShade,
        Pupil,  // ink: pupils and upper lids
        Glint,
        Brow,   // etched: brows, and eyes shut in a smile
        Mouth,  // etched: a closed mouth
        Inside, // of an open mouth
        Tongue,
        Teeth,
    }

    public readonly struct FaceVertex
    {
        public readonly float X, Y, Alpha;

        public FaceVertex(float x, float y, float alpha)
        {
            X = x;
            Y = y;
            Alpha = alpha;
        }
    }

    /// <summary>Takes the face's quads (corners in order) in face units: see <see cref="FaceDrawing"/>.</summary>
    public interface IFaceCanvas
    {
        void Quad(FacePart part, in FaceVertex a, in FaceVertex b, in FaceVertex c, in FaceVertex d);
    }

    /// <summary>
    /// Where the glass art (Resources/StackIt/BlockyGlass.png, made by Tools/blocky_glass.py) sits,
    /// in the units of <see cref="FaceDrawing"/>. The block's front fills the middle of it.
    /// </summary>
    public static class BlockyGlass
    {
        public const string Resource = "StackIt/BlockyGlass";
        public const float Width = 520f / 480f, Height = 568f / 480f, Foot = -0.04f;

        public const float FrontU = (Width - 1f) / 2f / Width, FrontWidth = 1f / Width;
        public const float FrontV = -Foot / Height, FrontHeight = FaceDrawing.Height / Height;
    }

    /// <summary>
    /// Turns a <see cref="FacePose"/> into quads. Units are block widths: x from -0.5 to 0.5, y from
    /// the block's foot up to <see cref="Height"/>. The face floats inside the glass: everything
    /// has a soft edge, the lines swell and taper like a hand's, and the whole face throws a
    /// shadow on the back wall. Eyes are shapes, not lids painted over a circle, so the block's
    /// colour shows through wherever they close.
    /// </summary>
    public static class FaceDrawing
    {
        public const float Height = 0.9f;

        private const float EyeX = 0.155f, EyeY = 0.49f, EyeRx = 0.118f, EyeRy = 0.136f;
        private const float PupilRadius = 0.08f, GlintRadius = 0.027f, SecondGlintRadius = 0.012f;
        private const float LidArch = 0.3f, ShutBelow = 0.12f; // thinner than this, an eye is just its lid
        private const float BrowGap = 0.062f;
        private const float MouthY = 0.25f, MouthLift = 0.45f, GapeRoundness = 0.62f, LineFadesBy = 0.03f;
        private const float CheekX = 0.31f, CheekY = 0.285f, CheekRx = 0.06f, CheekRy = 0.04f, CheekSoft = 0.04f;
        private const float EdgeSoft = 0.006f, NormalReach = 0.002f;
        private const float ShadowSoft = 0.03f, ShadowDrop = 0.03f, HaloSoft = 0.022f;

        private interface IRegion
        {
            float Top(float x);
            float Bottom(float x);
        }

        private interface IPath
        {
            void At(float t, out float x, out float y);
        }

        /// <summary>One way of laying down quads: which part, how soft, how strong, how far off.</summary>
        private readonly struct Pen
        {
            public readonly float Soft, Alpha;
            private readonly IFaceCanvas _canvas;
            private readonly FacePart _part;
            private readonly float _dx, _dy;

            public Pen(IFaceCanvas canvas, FacePart part, float soft = EdgeSoft, float alpha = 1f, float dx = 0f, float dy = 0f)
            {
                _canvas = canvas;
                _part = part;
                Soft = soft;
                Alpha = alpha;
                _dx = dx;
                _dy = dy;
            }

            public Pen Faded(float by) => new Pen(_canvas, _part, Soft, Alpha * by, _dx, _dy);

            public void Quad(float ax, float ay, float aAlpha, float bx, float by, float bAlpha, float cx, float cy, float cAlpha, float dx, float dy, float dAlpha)
            {
                _canvas.Quad(_part,
                    new FaceVertex(ax + _dx, ay + _dy, aAlpha),
                    new FaceVertex(bx + _dx, by + _dy, bAlpha),
                    new FaceVertex(cx + _dx, cy + _dy, cAlpha),
                    new FaceVertex(dx + _dx, dy + _dy, dAlpha));
            }
        }

        /// <summary>A line's half-thickness along its length: from one tip up to a swell, down to the other.</summary>
        private readonly struct Taper
        {
            private readonly float _start, _peak, _end, _peakAt;

            public Taper(float start, float peak, float end, float peakAt = 0.5f)
            {
                _start = start;
                _peak = peak;
                _end = end;
                _peakAt = peakAt;
            }

            public float At(float t)
            {
                bool rising = t < _peakAt;
                float u = rising ? t / _peakAt : (1f - t) / (1f - _peakAt);
                float eased = (float)Math.Sin(u * Math.PI / 2f);
                float tip = rising ? _start : _end;
                return tip + (_peak - tip) * eased;
            }
        }

        private readonly struct Oval : IRegion
        {
            private readonly float _x, _y, _rx, _ry;

            public Oval(float x, float y, float rx, float ry)
            {
                _x = x;
                _y = y;
                _rx = rx;
                _ry = ry;
            }

            private float Half(float x)
            {
                float u = (x - _x) / _rx;
                return _ry * (float)Math.Sqrt(Math.Max(0f, 1f - u * u));
            }

            public float Top(float x) => _y + Half(x);
            public float Bottom(float x) => _y - Half(x);
        }

        /// <summary>An open eye: an oval under the upper lid's line, above the lower lid's.</summary>
        private readonly struct EyeShape : IRegion
        {
            public readonly float X;
            private readonly Oval _oval;
            private readonly float _lid, _tilt, _lower;
            private readonly int _side;

            public EyeShape(int side, in FacePose pose)
            {
                _side = side;
                X = side * EyeX + pose.FaceShift;
                _oval = new Oval(X, EyeY, EyeRx, EyeRy);
                _lid = EyeY + EyeRy * (2f * (pose.EyeOpen + side * pose.EyeSkew) - 1f);
                _tilt = pose.LidTilt + side * pose.LidSkew;
                _lower = Math.Min(1f, pose.LowerLid + pose.Beam * 1.2f); // a smile starts by pushing the cheeks up
            }

            public float Lid(float x)
            {
                float u = (x - X) / EyeRx;
                return _lid + _tilt * _side * (x - X) + LidArch * EyeRy * (1f - u * u);
            }

            public float Crest(float x) => Math.Min(_oval.Top(x), Lid(x));

            public float Top(float x) => Crest(x);

            public float Bottom(float x)
            {
                float u = (x - X) / EyeRx;
                return Math.Max(_oval.Bottom(x), EyeY - 1.02f * EyeRy + 2f * EyeRy * _lower * (1f - 0.45f * u * u));
            }
        }

        private readonly struct Above : IRegion
        {
            private readonly float _y;

            public Above(float y) => _y = y;

            public float Top(float x) => float.MaxValue;
            public float Bottom(float x) => _y;
        }

        private readonly struct Inside<TA, TB> : IRegion where TA : struct, IRegion where TB : struct, IRegion
        {
            private readonly TA _a;
            private readonly TB _b;

            public Inside(in TA a, in TB b)
            {
                _a = a;
                _b = b;
            }

            public float Top(float x) => Math.Min(_a.Top(x), _b.Top(x));
            public float Bottom(float x) => Math.Max(_a.Bottom(x), _b.Bottom(x));
        }

        private readonly struct Gape : IRegion
        {
            private readonly float _x, _half, _top, _open, _curve, _skew;

            public Gape(in FacePose pose)
            {
                _x = pose.FaceShift + pose.MouthShift;
                _half = pose.MouthWidth;
                _top = MouthY + pose.MouthOpen * MouthLift;
                _open = pose.MouthOpen;
                _curve = pose.MouthCurve;
                _skew = pose.MouthSkew;
            }

            public float Top(float x)
            {
                float u = (x - _x) / _half;
                return _top + _curve * u * u + _skew * u;
            }

            public float Bottom(float x)
            {
                float u = (x - _x) / _half;
                return Top(x) - _open * (float)Math.Pow(Math.Max(0f, 1f - u * u), GapeRoundness);
            }
        }

        private readonly struct Teeth : IRegion
        {
            private readonly Gape _gape;
            private readonly float _depth;

            public Teeth(in Gape gape, float depth)
            {
                _gape = gape;
                _depth = depth;
            }

            public float Top(float x) => _gape.Top(x);
            public float Bottom(float x) => Math.Max(_gape.Bottom(x), _gape.Top(x) - _depth);
        }

        private readonly struct Curve : IPath
        {
            private readonly float _ax, _ay, _bx, _by, _cx, _cy;

            public Curve(float ax, float ay, float bx, float by, float cx, float cy)
            {
                _ax = ax;
                _ay = ay;
                _bx = bx;
                _by = by;
                _cx = cx;
                _cy = cy;
            }

            public void At(float t, out float x, out float y)
            {
                float s = 1f - t;
                x = s * s * _ax + 2f * s * t * _bx + t * t * _cx;
                y = s * s * _ay + 2f * s * t * _by + t * t * _cy;
            }
        }

        /// <summary>A closed mouth, with the wobble of nerves on top.</summary>
        private readonly struct MouthPath : IPath
        {
            private readonly Curve _curve;
            private readonly float _wave;

            public MouthPath(in FacePose pose)
            {
                float x = pose.FaceShift + pose.MouthShift, half = pose.MouthWidth;
                float bow = pose.MouthCurve, skew = pose.MouthSkew;
                _curve = new Curve(x - half, MouthY + bow - skew, x, MouthY - bow, x + half, MouthY + bow + skew);
                _wave = pose.MouthWave;
            }

            public void At(float t, out float x, out float y)
            {
                _curve.At(t, out x, out y);
                y += _wave * (float)(Math.Sin(t * 15f) * Math.Sqrt(Math.Sin(t * Math.PI)));
            }
        }

        private readonly struct LidPath : IPath
        {
            private readonly EyeShape _eye;

            public LidPath(in EyeShape eye) => _eye = eye;

            public void At(float t, out float x, out float y)
            {
                x = _eye.X + (2f * t - 1f) * EyeRx;
                y = _eye.Crest(x);
            }
        }

        public static void Draw(in FacePose pose, IFaceCanvas canvas)
        {
            Silhouette(pose, new Pen(canvas, FacePart.Shadow, ShadowSoft, 1f, ShadowDrop, -ShadowDrop), canvas);
            Silhouette(pose, new Pen(canvas, FacePart.Halo, HaloSoft), canvas);
            Blush(pose, canvas);
            for (int side = -1; side <= 1; side += 2)
            {
                Eye(pose, side, canvas);
                Brow(pose, side, new Pen(canvas, FacePart.Brow));
            }
            Mouth(pose, canvas);
        }

        /// <summary>The face as one shape: what its shadow and its halo are made of.</summary>
        private static void Silhouette(in FacePose pose, in Pen pen, IFaceCanvas canvas)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new EyeShape(side, pose);
                if (pose.Beam >= 0.5f) Stroke(pen, SmileEye(eye), SmileEyeTaper, 12);
                else if (pose.EyeOpen >= ShutBelow) Fill(pen, eye, eye.X - EyeRx, eye.X + EyeRx, 12);
                Brow(pose, side, pen);
            }
            if (pose.MouthWidth < 0.01f) return;
            if (pose.MouthOpen > 0.005f) Fill(pen, new Gape(pose), Left(pose), Right(pose), 12);
            else Stroke(pen, new MouthPath(pose), MouthTaper, 14);
        }

        private static float Left(in FacePose pose) => pose.FaceShift + pose.MouthShift - pose.MouthWidth;
        private static float Right(in FacePose pose) => pose.FaceShift + pose.MouthShift + pose.MouthWidth;

        private static Taper SmileEyeTaper => new Taper(0.006f, 0.019f, 0.006f);
        private static Taper LidTaper => new Taper(0.003f, 0.016f, 0.003f);
        private static Taper BrowTaper => new Taper(0.012f, 0.024f, 0.006f, 0.4f);
        private static Taper MouthTaper => new Taper(0.004f, 0.017f, 0.004f);
        private static Taper DimpleTaper => new Taper(0.003f, 0.009f, 0.003f);

        private static Curve SmileEye(in EyeShape eye) =>
            new Curve(eye.X - EyeRx, EyeY - 0.03f, eye.X, EyeY + 0.095f, eye.X + EyeRx, EyeY - 0.03f);

        private static void Blush(in FacePose pose, IFaceCanvas canvas)
        {
            if (pose.Blush < 0.02f) return;
            var pen = new Pen(canvas, FacePart.Blush, CheekSoft, pose.Blush);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * CheekX + pose.FaceShift;
                Fill(pen, new Oval(x, CheekY, CheekRx, CheekRy), x - CheekRx, x + CheekRx, 10);
            }
        }

        private static void Eye(in FacePose pose, int side, IFaceCanvas canvas)
        {
            var eye = new EyeShape(side, pose);
            if (pose.Beam >= 0.5f)
            {
                Stroke(new Pen(canvas, FacePart.Brow), SmileEye(eye), SmileEyeTaper, 16);
                return;
            }
            float x0 = eye.X - EyeRx, x1 = eye.X + EyeRx;
            if (pose.EyeOpen >= ShutBelow)
            {
                Fill(new Pen(canvas, FacePart.Eye), eye, x0, x1, 16);
                Fill(new Pen(canvas, FacePart.EyeShade, 0f), new Inside<EyeShape, Above>(eye, new Above(EyeY - 0.03f)), x0, x1, 12, alphaBottom: 0f);

                float radius = PupilRadius * (1f - pose.PupilShrink);
                float px = eye.X + pose.LookX * EyeRx * 0.42f, py = EyeY + pose.LookY * EyeRy * 0.42f;
                Dot(new Pen(canvas, FacePart.Pupil), eye, px, py, radius, 14);
                var glint = new Pen(canvas, FacePart.Glint);
                Dot(glint, eye, px - radius * 0.42f, py + radius * 0.42f, GlintRadius, 8);
                Dot(glint.Faded(0.85f), eye, px + radius * 0.45f, py - radius * 0.42f, SecondGlintRadius, 6);
            }
            Stroke(new Pen(canvas, FacePart.Pupil), new LidPath(eye), LidTaper, 22);
        }

        private static void Dot(in Pen pen, in EyeShape eye, float x, float y, float radius, int columns)
        {
            var round = new Oval(x, y, radius, radius);
            Fill(pen, new Inside<Oval, EyeShape>(round, eye), x - radius, x + radius, columns);
        }

        private static void Brow(in FacePose pose, int side, in Pen pen)
        {
            float x = side * EyeX + pose.FaceShift;
            float y = EyeY + EyeRy + BrowGap + pose.BrowRaise + side * pose.BrowSkew;
            float tilt = pose.BrowTilt + side * pose.BrowTwist;
            float arch = 0.012f * Math.Max(0f, 1f - Math.Abs(tilt) / 0.3f); // a slanted brow goes straight
            var path = new Curve(
                x - side * 0.09f, y + tilt * 0.09f - arch,
                x + side * 0.005f, y + 0.024f + arch,
                x + side * 0.112f, y - tilt * 0.11f - 0.024f);
            Stroke(pen, path, BrowTaper, 12);
        }

        private static void Mouth(in FacePose pose, IFaceCanvas canvas)
        {
            if (pose.MouthWidth < 0.01f) return;
            if (pose.MouthOpen > 0.005f) OpenMouth(pose, canvas);
            float line = 1f - pose.MouthOpen / LineFadesBy;
            if (line <= 0f) return;
            var pen = new Pen(canvas, FacePart.Mouth, EdgeSoft, line);
            Stroke(pen, new MouthPath(pose), MouthTaper, pose.MouthWave > 0.001f ? 28 : 16);
            Dimple(pose, pen);
        }

        private static void OpenMouth(in FacePose pose, IFaceCanvas canvas)
        {
            var gape = new Gape(pose);
            float x0 = Left(pose), x1 = Right(pose), x = (x0 + x1) / 2f, half = pose.MouthWidth;
            Fill(new Pen(canvas, FacePart.Inside), gape, x0, x1, 20);
            if (pose.Tongue > 0.02f)
            {
                float rx = half * 0.62f * pose.Tongue, ry = pose.MouthOpen * 0.62f * pose.Tongue;
                var tongue = new Oval(x, gape.Bottom(x) - 0.02f, rx, ry);
                Fill(new Pen(canvas, FacePart.Tongue), new Inside<Oval, Gape>(tongue, gape), x - rx, x + rx, 12);
            }
            if (pose.Teeth > 0.002f) Fill(new Pen(canvas, FacePart.Teeth), new Teeth(gape, pose.Teeth), x - half * 0.93f, x + half * 0.93f, 14);
        }

        /// <summary>A lopsided smile digs a little tick into its higher cheek.</summary>
        private static void Dimple(in FacePose pose, in Pen pen)
        {
            float strength = Math.Min(1f, Math.Abs(pose.MouthSkew) / 0.01f);
            if (pose.MouthCurve < 0.015f || strength < 0.02f) return;
            int side = pose.MouthSkew > 0f ? 1 : -1;
            float x = pose.FaceShift + pose.MouthShift + side * pose.MouthWidth;
            float y = MouthY + pose.MouthCurve + Math.Abs(pose.MouthSkew);
            var tick = new Curve(x - side * 0.01f, y + 0.03f, x + side * 0.03f, y + 0.002f, x - side * 0.008f, y - 0.03f);
            Stroke(pen.Faded(strength), tick, DimpleTaper, 8);
        }

        /// <summary>Columns from x0 to x1, closer together at the ends, where round shapes turn fastest.</summary>
        private static void Fill<TRegion>(in Pen pen, in TRegion region, float x0, float x1, int columns, float alphaBottom = 1f, float alphaTop = 1f)
            where TRegion : struct, IRegion
        {
            float mid = (x0 + x1) / 2f, half = (x1 - x0) / 2f;
            float low = pen.Alpha * alphaBottom, high = pen.Alpha * alphaTop;
            float left = x0, leftTop = region.Top(left), leftBottom = region.Bottom(left);
            for (int i = 1; i <= columns; i++)
            {
                float right = mid - half * (float)Math.Cos(Math.PI * i / columns);
                float rightTop = region.Top(right), rightBottom = region.Bottom(right);
                if (leftTop > leftBottom || rightTop > rightBottom)
                {
                    float lt = Math.Max(leftTop, leftBottom), rt = Math.Max(rightTop, rightBottom);
                    pen.Quad(left, leftBottom, low, right, rightBottom, low, right, rt, high, left, lt, high);
                    if (pen.Soft > 0f)
                    {
                        float soft = pen.Soft;
                        TopNormal(region, left, x0, x1, out float lnx, out float lny);
                        TopNormal(region, right, x0, x1, out float rnx, out float rny);
                        pen.Quad(left, lt, high, right, rt, high, right + rnx * soft, rt + rny * soft, 0f, left + lnx * soft, lt + lny * soft, 0f);
                        BottomNormal(region, left, x0, x1, out lnx, out lny);
                        BottomNormal(region, right, x0, x1, out rnx, out rny);
                        pen.Quad(left + lnx * soft, leftBottom + lny * soft, 0f, right + rnx * soft, rightBottom + rny * soft, 0f, right, rightBottom, low, left, leftBottom, low);
                    }
                }
                left = right;
                leftTop = rightTop;
                leftBottom = rightBottom;
            }
        }

        // Soft edges fade along these, so round shapes fade sideways at their ends too.
        private static void TopNormal<TRegion>(in TRegion region, float x, float x0, float x1, out float nx, out float ny)
            where TRegion : struct, IRegion
        {
            float a = Math.Max(x - NormalReach, x0), b = Math.Min(x + NormalReach, x1);
            Unit(-(region.Top(b) - region.Top(a)) / (b - a), 1f, out nx, out ny);
        }

        private static void BottomNormal<TRegion>(in TRegion region, float x, float x0, float x1, out float nx, out float ny)
            where TRegion : struct, IRegion
        {
            float a = Math.Max(x - NormalReach, x0), b = Math.Min(x + NormalReach, x1);
            Unit((region.Bottom(b) - region.Bottom(a)) / (b - a), -1f, out nx, out ny);
        }

        /// <summary>A line that swells and tapers along a path, with a soft edge on both sides.</summary>
        private static void Stroke<TPath>(in Pen pen, in TPath path, in Taper taper, int segments) where TPath : struct, IPath
        {
            Sample(path, taper, 0f, out float x, out float y, out float nx, out float ny);
            float soft = pen.Soft;
            for (int i = 1; i <= segments; i++)
            {
                Sample(path, taper, (float)i / segments, out float nextX, out float nextY, out float nextNx, out float nextNy);
                float a = pen.Alpha;
                pen.Quad(x - nx, y - ny, a, nextX - nextNx, nextY - nextNy, a, nextX + nextNx, nextY + nextNy, a, x + nx, y + ny, a);
                if (soft > 0f)
                {
                    Unit(nx, ny, out float ux, out float uy);
                    Unit(nextNx, nextNy, out float nextUx, out float nextUy);
                    float sx = ux * soft, sy = uy * soft, nextSx = nextUx * soft, nextSy = nextUy * soft;
                    pen.Quad(x + nx, y + ny, a, nextX + nextNx, nextY + nextNy, a, nextX + nextNx + nextSx, nextY + nextNy + nextSy, 0f, x + nx + sx, y + ny + sy, 0f);
                    pen.Quad(x - nx - sx, y - ny - sy, 0f, nextX - nextNx - nextSx, nextY - nextNy - nextSy, 0f, nextX - nextNx, nextY - nextNy, a, x - nx, y - ny, a);
                }
                x = nextX;
                y = nextY;
                nx = nextNx;
                ny = nextNy;
            }
        }

        /// <summary>A point on the path, and the normal there as long as the line's half-thickness.</summary>
        private static void Sample<TPath>(in TPath path, in Taper taper, float t, out float x, out float y, out float nx, out float ny)
            where TPath : struct, IPath
        {
            const float Step = 0.01f;
            path.At(t, out x, out y);
            path.At(Math.Max(0f, t - Step), out float beforeX, out float beforeY);
            path.At(Math.Min(1f, t + Step), out float afterX, out float afterY);
            Unit(afterY - beforeY, beforeX - afterX, out nx, out ny);
            float half = taper.At(t);
            nx *= -half;
            ny *= -half;
        }

        private static void Unit(float x, float y, out float ux, out float uy)
        {
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length < 1e-6f)
            {
                ux = 0f;
                uy = 1f;
                return;
            }
            ux = x / length;
            uy = y / length;
        }
    }
}
