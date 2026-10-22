using System.Collections;
using System.Collections.Generic;
using StackIt.Services;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// The feel layer, calm by design: a soft puff of dust on every landing, an outline ring that
    /// breathes out from a perfect drop with a climbing chime, a gentle brightness swell on combos,
    /// haptics, and a slow-motion beat on a new record. Gameplay never depends on any of this.
    /// </summary>
    public sealed class HypeFx : MonoBehaviour
    {
        private const int RingPool = 6;
        private const float RingThickness = 0.07f;

        private readonly List<MeshRenderer> _rings = new List<MeshRenderer>();
        private ParticleSystem _dust;
        private MaterialPropertyBlock _props; // Unity forbids creating it in a field initializer
        private CameraRig _rig;
        private IPostFx _post;
        private IHaptics _haptics;
        private AudioService _audio;
        private Coroutine _slowMo;
        private int _nextRing;

        private void Awake() => _props = new MaterialPropertyBlock();

        public void Init(CameraRig rig, IPostFx post, IHaptics haptics, AudioService audio)
        {
            _rig = rig;
            _post = post;
            _haptics = haptics;
            _audio = audio;
            _dust = CreateDust();
            for (int i = 0; i < RingPool; i++) _rings.Add(CreateRing());
        }

        public void Placed(Vector3 top, Vector3 size, Color color)
        {
            _audio.Place();
            _haptics.Play(HapticKind.Light);
            Dust(top, size, color, 8, 0.6f);
        }

        public void Perfect(Vector3 top, Vector3 size, Color color, int combo)
        {
            _audio.Place();
            _audio.Perfect(combo);
            _haptics.Play(combo >= 5 ? HapticKind.Medium : HapticKind.Light);
            Ring(top, size, Color.white, 0.9f, 1.28f + Mathf.Min(combo, 8) * 0.03f);
            if (combo >= 3) Ring(top, size, Color.white, 0.5f, 1.6f, 0.12f);
            _post.Pulse(Mathf.Min(0.25f + combo * 0.08f, 1f));
            Dust(top, size, Color.Lerp(color, Color.white, 0.6f), 12 + Mathf.Min(combo, 8) * 2, 0.9f);
        }

        public void Missed()
        {
            _audio.Miss();
            _haptics.Play(HapticKind.Heavy);
            _rig.Shake(0.12f);
        }

        public void RivalPassed(Vector3 top, Color color)
        {
            _audio.Record();
            _haptics.Play(HapticKind.Success);
            _post.Pulse(1f);
            var size = new Vector3(3f, StackView.BlockHeight, 3f);
            Ring(top, size, new Color(0.18f, 0.5f, 0.98f), 1f, 1.9f);
            Dust(top, size, Color.white, 40, 1.4f);
        }

        /// <summary>Brief slow-motion as the last block falls on a new record.</summary>
        public void NewRecord()
        {
            _audio.Record();
            _haptics.Play(HapticKind.Success);
            if (_slowMo != null) StopCoroutine(_slowMo);
            _slowMo = StartCoroutine(SlowMotion(0.35f, 0.9f));
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

        /// <summary>An outline the size of the block's top face that grows and fades.</summary>
        private void Ring(Vector3 top, Vector3 size, Color color, float alpha, float growTo, float delay = 0f)
        {
            MeshRenderer ring = _rings[_nextRing];
            _nextRing = (_nextRing + 1) % _rings.Count;
            ring.GetComponent<MeshFilter>().sharedMesh = FrameMesh(ring.GetComponent<MeshFilter>().sharedMesh, size.x / 2f + 0.06f, size.z / 2f + 0.06f);
            Transform t = ring.transform;
            t.localPosition = top + Vector3.up * (size.y / 2f + 0.01f);
            t.localScale = Vector3.one;
            SetAlpha(ring, color, 0f);
            ring.gameObject.SetActive(true);
            float duration = 0.6f + delay;
            Tween.Run(ring, duration, k =>
            {
                if (ring == null) return;
                float local = Mathf.Clamp01((k * duration - delay) / (duration - delay));
                float e = Tween.EaseOutCubic(local);
                float s = Mathf.Lerp(1f, growTo, e);
                t.localScale = new Vector3(s, 1f, s);
                SetAlpha(ring, color, local <= 0f ? 0f : alpha * (1f - e));
            }, () => { if (ring != null) ring.gameObject.SetActive(false); });
        }

        private void SetAlpha(MeshRenderer renderer, Color color, float alpha)
        {
            renderer.GetPropertyBlock(_props);
            _props.SetColor(Materials.BaseColor, new Color(color.r, color.g, color.b, alpha));
            renderer.SetPropertyBlock(_props);
        }

        private void Dust(Vector3 center, Vector3 size, Color color, int count, float speed)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                // Puffs out from the rim of the block's bottom edge, where it landed.
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rim = new Vector3(Mathf.Clamp(dir.x * size.x, -size.x, size.x) * 0.5f, -size.y * 0.4f, Mathf.Clamp(dir.z * size.z, -size.z, size.z) * 0.5f);
                p.position = center + rim;
                p.velocity = (dir + Vector3.up * Random.Range(0.1f, 0.5f)) * speed * Random.Range(0.5f, 1f);
                p.startColor = new Color(color.r, color.g, color.b, Random.Range(0.5f, 0.85f));
                p.startSize = Random.Range(0.1f, 0.24f);
                p.startLifetime = Random.Range(0.5f, 0.9f);
                _dust.Emit(p, 1);
            }
        }

        private ParticleSystem CreateDust()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.05f;
            main.useUnscaledTime = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Materials.Dust;
            ps.Play();
            return ps;
        }

        private MeshRenderer CreateRing()
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Fx;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return renderer;
        }

        /// <summary>A flat square outline facing up, rebuilt in place for each ring.</summary>
        private static Mesh FrameMesh(Mesh mesh, float hx, float hz)
        {
            if (mesh == null) mesh = new Mesh { name = "Ring" };
            float ix = hx - RingThickness, iz = hz - RingThickness;
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz),
                new Vector3(-ix, 0, -iz), new Vector3(ix, 0, -iz), new Vector3(ix, 0, iz), new Vector3(-ix, 0, iz),
            };
            // Each side is a quad between the outer and inner squares, wound to face +Y.
            mesh.triangles = new[]
            {
                0, 4, 1, 1, 4, 5,
                1, 5, 2, 2, 5, 6,
                2, 6, 3, 3, 6, 7,
                3, 7, 0, 0, 7, 4,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
