using System.Collections;
using StackIt.Services;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// The "juice": particle bursts, shake, chromatic pulse, haptics, rising-pitch audio and the
    /// slow-motion beat on a new record. Gameplay never depends on any of this.
    /// </summary>
    public sealed class HypeFx : MonoBehaviour
    {
        private ParticleSystem _burst;
        private CameraRig _rig;
        private IPostFx _post;
        private IHaptics _haptics;
        private AudioService _audio;
        private Coroutine _slowMo;

        public void Init(CameraRig rig, IPostFx post, IHaptics haptics, AudioService audio)
        {
            _rig = rig;
            _post = post;
            _haptics = haptics;
            _audio = audio;
            _burst = CreateBurst();
        }

        public void Placed(Vector3 top, Vector3 size, Color color)
        {
            _audio.Place();
            _haptics.Play(HapticKind.Light);
            _rig.Shake(0.06f);
            Emit(top, size, color, 10, 2.5f);
        }

        public void Perfect(Vector3 top, Vector3 size, Color color, int combo)
        {
            _audio.Perfect(combo);
            _haptics.Play(combo >= 5 ? HapticKind.Heavy : HapticKind.Medium);
            _rig.Shake(Mathf.Min(0.12f + combo * 0.03f, 0.35f));
            _post.ChromaticPulse(Mathf.Min(0.35f + combo * 0.08f, 1f));
            Emit(top, size, color, 24 + Mathf.Min(combo, 10) * 6, 4f + combo * 0.3f);
        }

        public void Missed()
        {
            _audio.Miss();
            _haptics.Play(HapticKind.Heavy);
            _rig.Shake(0.45f);
        }

        public void RivalPassed(Vector3 top, Color color)
        {
            _audio.Record();
            _haptics.Play(HapticKind.Success);
            _post.ChromaticPulse(1f);
            Emit(top, new Vector3(3f, 0.5f, 3f), color, 80, 6f);
        }

        /// <summary>Brief slow-motion as the last block falls on a new record.</summary>
        public void NewRecord()
        {
            _audio.Record();
            _haptics.Play(HapticKind.Success);
            if (_slowMo != null) StopCoroutine(_slowMo);
            _slowMo = StartCoroutine(SlowMotion(0.25f, 0.9f));
        }

        private IEnumerator SlowMotion(float scale, float realSeconds)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            _slowMo = null;
        }

        private void Emit(Vector3 center, Vector3 size, Color color, int count, float speed)
        {
            var p = new ParticleSystem.EmitParams { startColor = color * 1.5f, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                // Spawn along the rim of the top face, flying outwards.
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rim = new Vector3(dir.x * size.x * 0.5f, size.y * 0.5f, dir.z * size.z * 0.5f);
                p.position = center + rim;
                p.velocity = (dir + Vector3.up * Random.Range(0.3f, 1.2f)) * speed * Random.Range(0.5f, 1f);
                p.startSize = Random.Range(0.05f, 0.14f);
                p.startLifetime = Random.Range(0.4f, 0.9f);
                _burst.Emit(p, 1);
            }
        }

        private ParticleSystem CreateBurst()
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.6f;
            main.useUnscaledTime = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Materials.Particle;
            ps.Play();
            return ps;
        }
    }
}
