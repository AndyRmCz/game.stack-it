using System;
using System.Collections.Generic;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Minimal tween runner (no DOTween dependency): enough for punch scales, fades and slides.
    /// Runs on unscaled time so UI motion keeps going during slow-motion.
    /// </summary>
    public sealed class Tween : MonoBehaviour
    {
        private sealed class Job
        {
            public object Owner;
            public float Duration;
            public float Elapsed;
            public Action<float> Step;
            public Action Done;
        }

        private static Tween _runner;
        private readonly List<Job> _jobs = new List<Job>();

        private static Tween Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("Tweens");
                    DontDestroyOnLoad(go);
                    _runner = go.AddComponent<Tween>();
                }
                return _runner;
            }
        }

        /// <summary>Runs <paramref name="step"/>(t) for t in [0,1]; a new tween on the same owner replaces the old one.</summary>
        public static void Run(object owner, float duration, Action<float> step, Action done = null)
        {
            Runner._jobs.RemoveAll(j => owner != null && ReferenceEquals(j.Owner, owner));
            Runner._jobs.Add(new Job { Owner = owner, Duration = Mathf.Max(0.0001f, duration), Step = step, Done = done });
        }

        public static void Kill(object owner) => Runner._jobs.RemoveAll(j => ReferenceEquals(j.Owner, owner));

        public static void Punch(Transform target, float amount = 0.35f, float duration = 0.35f)
        {
            Run(target, duration, t =>
            {
                if (target == null) return;
                float s = 1f + amount * Mathf.Sin(t * Mathf.PI) * (1f - t);
                target.localScale = new Vector3(s, s, 1f);
            }, () => { if (target != null) target.localScale = Vector3.one; });
        }

        public static void Fade(CanvasGroup group, float to, float duration, Action done = null)
        {
            float from = group.alpha;
            Run(group, duration, t => { if (group != null) group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t)); }, done);
        }

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                if (i >= _jobs.Count) continue;
                Job job = _jobs[i];
                job.Elapsed += dt;
                float t = Mathf.Clamp01(job.Elapsed / job.Duration);
                job.Step(t);
                if (t >= 1f)
                {
                    _jobs.Remove(job);
                    job.Done?.Invoke();
                }
            }
        }
    }
}
