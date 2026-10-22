using System;
using System.Collections.Generic;

namespace StackIt.Core.Simulation
{
    /// <summary>
    /// Deterministic stack simulation, integer math only. Mirrors backend/app/domain/sim/engine.py;
    /// the golden vectors in shared/sim-vectors keep both in lockstep. The game view drives it one
    /// tick at a time and only reads state; it never mutates it directly.
    /// </summary>
    public sealed class StackSim
    {
        private readonly Pcg32 _rng;
        private readonly List<Step> _steps = new List<Step>();

        public uint Seed { get; }
        public bool ContinuesAllowed { get; }
        public int Tick { get; private set; }
        public Phase Phase { get; private set; } = Phase.Playing;
        public Block Top { get; private set; } = new Block(
            -SimConstants.HalfBlock, SimConstants.HalfBlock, -SimConstants.HalfBlock, SimConstants.HalfBlock);
        public int Placed { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Perfects { get; private set; }
        public int ContinuesUsed { get; private set; }
        public MovingBlock Moving { get; private set; }
        public IReadOnlyList<Step> Steps => _steps;

        public bool Ended => Phase == Phase.AwaitingContinue;
        public int MaxContinues => ContinuesAllowed ? SimConstants.MaxContinues : 0;
        public bool CanContinue => Ended && ContinuesUsed < MaxContinues;

        /// <summary>Earliest tick at which a tap on the current block is legal.</summary>
        public int EarliestTapTick => Moving.SpawnTick + SimConstants.MinTapGapTicks;

        public StackSim(uint seed, bool continuesAllowed = true)
        {
            Seed = seed;
            ContinuesAllowed = continuesAllowed;
            _rng = new Pcg32(seed);
            Spawn(0);
        }

        private void Spawn(int t)
        {
            uint r = _rng.NextUInt();
            int ramp = Math.Min(SimConstants.SpeedBase + SimConstants.SpeedRamp * Placed, SimConstants.SpeedMax);
            int jitter = (int)((r >> 1) % (uint)(SimConstants.SpeedJitter + 1));
            bool startLow = (r & 1u) == 0u;
            Moving = new MovingBlock
            {
                Axis = Placed % 2 == 0 ? Axis.X : Axis.Z,
                Offset = startLow ? -SimConstants.Travel : SimConstants.Travel,
                Dir = startLow ? 1 : -1,
                Speed = ramp + jitter,
                SpawnTick = t,
            };
            Tick = t;
        }

        public void AdvanceTo(int t)
        {
            if (t < Tick) throw new InvalidRunException("events_out_of_order");
            if (Phase != Phase.Playing)
            {
                Tick = t;
                return;
            }

            MovingBlock m = Moving;
            for (int i = Tick; i < t; i++)
            {
                m.Offset += m.Dir * m.Speed;
                if (m.Offset > SimConstants.Travel)
                {
                    m.Offset = 2 * SimConstants.Travel - m.Offset;
                    m.Dir = -1;
                }
                else if (m.Offset < -SimConstants.Travel)
                {
                    m.Offset = -2 * SimConstants.Travel - m.Offset;
                    m.Dir = 1;
                }
            }
            Tick = t;
        }

        public Step Apply(InputEvent e)
        {
            if (e.Tick > SimConstants.MaxRunTicks) throw new InvalidRunException("run_too_long");
            if (e.Tick < Tick) throw new InvalidRunException("events_out_of_order");
            Step step = e.Type == InputType.Tap ? DoTap(e.Tick) : DoContinue(e.Tick);
            _steps.Add(step);
            return step;
        }

        private Step DoTap(int t)
        {
            if (Phase != Phase.Playing) throw new InvalidRunException("event_after_game_over");
            if (t < Moving.SpawnTick + SimConstants.MinTapGapTicks) throw new InvalidRunException("tap_too_early");
            AdvanceTo(t);

            MovingBlock m = Moving;
            int lo = Top.Lo(m.Axis);
            int hi = Top.Hi(m.Axis);
            int n = Placed + 1;
            int offset = m.Offset;
            bool perfect;

            if (Math.Abs(offset) <= SimConstants.PerfectToleranceFor(m.Speed))
            {
                perfect = true;
                Combo += 1;
                Perfects += 1;
                if (Combo % SimConstants.RegrowEvery == 0)
                {
                    lo -= SimConstants.RegrowHalf;
                    hi += SimConstants.RegrowHalf;
                    if (hi - lo > SimConstants.BlockSize) hi = lo + SimConstants.BlockSize;
                }
            }
            else if (Math.Abs(offset) >= hi - lo)
            {
                Combo = 0;
                Phase = Phase.AwaitingContinue;
                return new Step { Kind = StepKind.Miss, Tick = t, N = n, Axis = m.Axis, Offset = offset, Score = Score };
            }
            else
            {
                perfect = false;
                Combo = 0;
                int newLo = Math.Max(lo + offset, lo);
                int newHi = Math.Min(hi + offset, hi);
                lo = newLo;
                hi = newHi;
            }

            int gained = 1 + (perfect ? Math.Min(Combo, SimConstants.ComboBonusCap) : 0);
            Placed = n;
            Score += gained;
            MaxCombo = Math.Max(MaxCombo, Combo);
            Top = Top.WithSpan(m.Axis, lo, hi);
            var step = new Step
            {
                Kind = StepKind.Place,
                Tick = t,
                N = n,
                Axis = m.Axis,
                Offset = offset,
                Block = Top,
                Perfect = perfect,
                Combo = Combo,
                Gained = gained,
                Score = Score,
            };
            Spawn(t);
            return step;
        }

        private Step DoContinue(int t)
        {
            if (Phase != Phase.AwaitingContinue) throw new InvalidRunException("continue_without_miss");
            if (ContinuesUsed >= MaxContinues) throw new InvalidRunException("continue_not_allowed");
            ContinuesUsed += 1;
            Phase = Phase.Playing;
            Spawn(t);
            return new Step { Kind = StepKind.Continue, Tick = t, N = Placed + 1, Score = Score };
        }

        /// <summary>Re-simulates a whole run, as the server does.</summary>
        public static StackSim Replay(uint seed, IEnumerable<InputEvent> events, bool continuesAllowed)
        {
            var sim = new StackSim(seed, continuesAllowed);
            foreach (InputEvent e in events) sim.Apply(e);
            return sim;
        }
    }
}
