using System;

namespace StackIt.Core.Simulation
{
    /// <summary>Rules from shared/SIMULATION.md. Must match backend/app/domain/sim/engine.py.</summary>
    public static class SimConstants
    {
        public const int SimVersion = 1;
        public const int TickRate = 60;
        public const int UnitsPerWorldUnit = 10_000;

        public const int BlockSize = 30_000;
        public const int HalfBlock = 15_000;
        public const int Travel = 45_000;
        public const int PerfectTolerance = 450;
        public const int SpeedBase = 900;
        public const int SpeedRamp = 12;
        public const int SpeedMax = 2_400;
        public const int SpeedJitter = 150;
        public const int RegrowEvery = 4;
        public const int RegrowHalf = 750;
        public const int ComboBonusCap = 5;
        public const int MinTapGapTicks = 6;
        public const int MaxRunTicks = 216_000;
        public const int MaxContinues = 1;

        /// <summary>The perfect window grows with speed so every pass has a tick inside it.</summary>
        public static int PerfectToleranceFor(int speed) => PerfectTolerance + speed / 2;
    }

    public enum Axis
    {
        X,
        Z,
    }

    public enum Phase
    {
        Playing,
        AwaitingContinue,
    }

    public enum InputType
    {
        Tap,
        Continue,
    }

    public enum StepKind
    {
        Place,
        Miss,
        Continue,
    }

    public readonly struct InputEvent
    {
        public readonly int Tick;
        public readonly InputType Type;

        public InputEvent(int tick, InputType type)
        {
            Tick = tick;
            Type = type;
        }

        public string TypeName => Type == InputType.Tap ? "tap" : "continue";

        /// <summary>Parses the wire name; unknown names throw the same error code as the server.</summary>
        public static InputEvent Parse(int tick, string type)
        {
            switch (type)
            {
                case "tap": return new InputEvent(tick, InputType.Tap);
                case "continue": return new InputEvent(tick, InputType.Continue);
                default: throw new InvalidRunException("unknown_event");
            }
        }
    }

    /// <summary>Axis-aligned footprint as half-open spans, in units.</summary>
    public readonly struct Block : IEquatable<Block>
    {
        public readonly int X0;
        public readonly int X1;
        public readonly int Z0;
        public readonly int Z1;

        public Block(int x0, int x1, int z0, int z1)
        {
            X0 = x0;
            X1 = x1;
            Z0 = z0;
            Z1 = z1;
        }

        public int Lo(Axis axis) => axis == Axis.X ? X0 : Z0;
        public int Hi(Axis axis) => axis == Axis.X ? X1 : Z1;
        public int Width => X1 - X0;
        public int Depth => Z1 - Z0;

        public Block WithSpan(Axis axis, int lo, int hi) =>
            axis == Axis.X ? new Block(lo, hi, Z0, Z1) : new Block(X0, X1, lo, hi);

        public bool Equals(Block other) =>
            X0 == other.X0 && X1 == other.X1 && Z0 == other.Z0 && Z1 == other.Z1;

        public override bool Equals(object obj) => obj is Block other && Equals(other);

        public override int GetHashCode() => (((X0 * 397) ^ X1) * 397 ^ Z0) * 397 ^ Z1;
    }

    public sealed class MovingBlock
    {
        public Axis Axis;
        public int Offset;
        public int Dir;
        public int Speed;
        public int SpawnTick;
    }

    public sealed class Step
    {
        public StepKind Kind;
        public int Tick;
        public int N;
        public Axis Axis;
        public int Offset;
        public Block Block;
        public bool Perfect;
        public int Combo;
        public int Gained;
        public int Score;
    }

    public sealed class InvalidRunException : Exception
    {
        public readonly string Code;

        public InvalidRunException(string code) : base(code)
        {
            Code = code;
        }
    }
}
