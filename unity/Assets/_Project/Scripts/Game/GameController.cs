using System;
using System.Collections.Generic;
using StackIt.Client;
using StackIt.Core.Simulation;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>Everything the game needs to start a run.</summary>
    public sealed class RunSetup
    {
        public uint Seed;
        public string RunId; // null = offline, unranked run
        public bool ContinuesAllowed = true;
        public string ChallengeCode;
        public string RivalName;
        public int RivalHeight;
        public int RivalScore;
        public int PersonalBest;
        public int DailyNumber; // 0 = not a Daily Tower run
    }

    public enum RunPhase
    {
        Idle,
        Playing,
        AwaitingContinue,
        Finished,
    }

    /// <summary>
    /// Game loop: advances the deterministic simulation at a fixed 60 Hz, turns taps into input
    /// events on the current tick, records them for the server replay, and raises events for the
    /// view, hype layer and HUD. The tick math lives in StackSim; this class only drives it.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float TickSeconds = 1f / SimConstants.TickRate;
        private const int MaxCatchUpTicks = 8;

        private readonly List<InputEvent> _events = new List<InputEvent>();
        private readonly List<BlockOutcome> _outcomes = new List<BlockOutcome>();
        private StackView _view;
        private HypeFx _fx;
        private CameraRig _rig;
        private SkyBackdrop _sky;
        private RivalGhostView _ghost;
        private RivalTracker _rival;
        private StackSim _sim;
        private float _accumulator;

        public RunSetup Setup { get; private set; }
        public RunPhase Phase { get; private set; }
        public int Score => _sim?.Score ?? 0;
        public int Height => _sim?.Placed ?? 0;
        public int ContinuesUsed => _sim?.ContinuesUsed ?? 0;
        public int Combo => _sim?.Combo ?? 0;
        public bool IsNewRecord => Score > (Setup?.PersonalBest ?? 0) && Score > 0;
        public IReadOnlyList<InputEvent> Events => _events;

        /// <summary>How each block of this run landed, for the shareable result.</summary>
        public IReadOnlyList<BlockOutcome> Outcomes => _outcomes;
        public bool CanContinue => Phase == RunPhase.AwaitingContinue && _sim.CanContinue;

        /// <summary>Paused runs freeze the simulation (no ticks pass, so the replay is unaffected) and ignore taps.</summary>
        public bool Paused { get; private set; }
        public RivalGhostView Ghost => _ghost;

        public event Action<Step> Placed;
        public event Action<RivalEvent> RivalProgress;
        public event Action Missed;

        /// <summary>A razor-thin drop: just outside perfect, or a miss that only just cleared the tower.</summary>
        public event Action<NearMiss> NearMissed;

        public void Init(StackView view, HypeFx fx, CameraRig rig, SkyBackdrop sky, RivalGhostView ghost)
        {
            _view = view;
            _fx = fx;
            _rig = rig;
            _sky = sky;
            _ghost = ghost;
        }

        public void Begin(RunSetup setup)
        {
            Paused = false;
            Setup = setup;
            _sim = new StackSim(setup.Seed, setup.ContinuesAllowed);
            _events.Clear();
            _outcomes.Clear();
            _accumulator = 0f;
            _rival = new RivalTracker(setup.RivalHeight);
            _view.ResetStack(_sim.Top);
            _rig.SetZoomOut(0f);
            _rig.Follow(0f, snap: true);
            _sky.SetHeight(0);
            if (setup.RivalHeight > 0) _ghost.Show(setup.RivalName, setup.RivalHeight);
            else _ghost.Hide();
            Phase = RunPhase.Playing;
        }

        /// <summary>Called by the full-screen tap catcher in the HUD.</summary>
        public void Tap()
        {
            if (Phase != RunPhase.Playing || Paused) return;
            // Taps in the first few ticks after a spawn are ignored, like the server requires.
            if (_sim.Tick < _sim.EarliestTapTick) return;

            Block previousTop = _sim.Top;
            int span = previousTop.Hi(_sim.Moving.Axis) - previousTop.Lo(_sim.Moving.Axis);
            int tolerance = SimConstants.PerfectToleranceFor(_sim.Moving.Speed);
            var input = new InputEvent(_sim.Tick, InputType.Tap);
            Step step = _sim.Apply(input);
            _events.Add(input);
            NearMiss near = NearMiss.Classify(step.Kind == StepKind.Miss, step.Perfect, step.Offset, span, tolerance);
            if (near.Kind != NearMissKind.None)
            {
                _fx.NearMiss(near);
                NearMissed?.Invoke(near);
            }

            if (step.Kind == StepKind.Miss)
            {
                _view.Miss(step, previousTop);
                _fx.Missed();
                Phase = RunPhase.AwaitingContinue;
                _rig.SetZoomOut(1f);
                if (!CanContinue && IsNewRecord) _fx.NewRecord();
                Missed?.Invoke();
                return;
            }

            _outcomes.Add(ResultText.Classify(step.Perfect, span, step.Block.Hi(step.Axis) - step.Block.Lo(step.Axis)));
            Vector3 top = _view.Place(step, previousTop);
            var size = new Vector3(step.Block.Width * StackView.UnitsToWorld, StackView.BlockHeight, step.Block.Depth * StackView.UnitsToWorld);
            Color color = _view.ColorFor(step.N);
            if (step.Perfect) _fx.Perfect(top, size, color, step.Combo);
            else _fx.Placed(top, size, color);
            _rig.Follow(top.y);
            _sky.SetHeight(step.N);
            _view.SetHorizon(_sky.Horizon);

            RivalEvent rival = _rival.Update(step.N);
            if (rival == RivalEvent.Passed)
            {
                _fx.RivalPassed(top, color);
                _ghost.Hide();
            }
            if (rival != RivalEvent.None) RivalProgress?.Invoke(rival);
            Placed?.Invoke(step);
        }

        /// <summary>Applies a rewarded continue (recorded as an input event and replayed by the server).</summary>
        public bool Continue()
        {
            if (!CanContinue) return false;
            var input = new InputEvent(_sim.Tick, InputType.Continue);
            _sim.Apply(input);
            _events.Add(input);
            _accumulator = 0f;
            _rig.SetZoomOut(0f);
            Phase = RunPhase.Playing;
            return true;
        }

        public void Pause()
        {
            if (Phase == RunPhase.Playing) Paused = true;
        }

        public void Resume() => Paused = false;

        /// <summary>Back to Home: no run, the view shows the attract loop.</summary>
        public void Idle()
        {
            Phase = RunPhase.Idle;
            Paused = false;
        }

        /// <summary>Ends the run for good (no continue). Returns false if it was already finished.</summary>
        public bool Finish()
        {
            if (Phase != RunPhase.AwaitingContinue) return false;
            Phase = RunPhase.Finished;
            if (IsNewRecord && Setup.ContinuesAllowed && _sim.ContinuesUsed == 0) _fx.NewRecord();
            if (IsNewRecord) _fx.Confetti(_view.TopCenter);
            if (!_fx.ReduceMotion) _view.Collapse(); // the run is really over: the top of the tower topples
            return true;
        }

        private void Update()
        {
            if (Phase == RunPhase.Idle) return; // Home's attract loop owns the moving block
            if (Phase != RunPhase.Playing || Paused)
            {
                if (_sim != null && !Paused) _view.ShowMoving(_sim, 0f);
                return;
            }
            // Real time, not Time.deltaTime: slow-motion effects must never change the simulation.
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, TickSeconds * MaxCatchUpTicks);
            while (_accumulator >= TickSeconds)
            {
                _accumulator -= TickSeconds;
                if (_sim.Tick >= SimConstants.MaxRunTicks) break;
                _sim.AdvanceTo(_sim.Tick + 1);
            }
            _view.ShowMoving(_sim, _accumulator / TickSeconds);
        }
    }
}
