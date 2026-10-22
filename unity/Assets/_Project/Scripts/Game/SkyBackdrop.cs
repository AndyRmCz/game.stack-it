using UnityEngine;
using UnityEngine.Rendering;

namespace StackIt.Game
{
    /// <summary>
    /// Full-screen gradient quad behind everything. As the tower grows the sky drifts through the
    /// theme's moments, and the ambient light follows it so blocks pick up the sky's tint;
    /// <see cref="Horizon"/> is what the lower stack melts into.
    /// </summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        private const int Steps = 64;
        private const float DriftPerSecond = 6f; // blocks of sky per second when catching up

        private Texture2D _gradient;
        private Material _material;
        private Camera _camera;
        private string _theme = Palette.DefaultTheme;
        private float _shown = -1f;
        private float _target;

        public static SkyBackdrop Create(Camera camera)
        {
            var quad = new GameObject("Sky");
            quad.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            quad.transform.SetParent(camera.transform, false);
            var sky = quad.AddComponent<SkyBackdrop>();
            sky._camera = camera;
            sky._gradient = new Texture2D(1, Steps, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            sky._material = new Material(Materials.Sky);
            sky._material.SetTexture(Materials.BaseMap, sky._gradient);
            sky._material.SetColor(Materials.BaseColor, Color.white);
            var renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sky._material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            RenderSettings.fog = false; // the stack fades into the horizon in StackView instead (fog would tint this quad too)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            return sky;
        }

        /// <summary>The color the lower stack fades towards.</summary>
        public Color Horizon { get; private set; } = new Color(1f, 0.86f, 0.8f);

        public void SetTheme(string theme)
        {
            _theme = string.IsNullOrEmpty(theme) ? Palette.DefaultTheme : theme;
            Redraw(_target); // now, so Horizon is right for whoever reads it next
        }

        public void SetHeight(int blocks) => _target = blocks;

        private void LateUpdate()
        {
            float distance = _camera.farClipPlane * 0.9f;
            float h = _camera.orthographicSize * 2.2f;
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(h * Mathf.Max(1f, _camera.aspect), h, 1f);

            float next = _shown < 0 ? _target : Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime * DriftPerSecond);
            if (Mathf.Abs(next - _shown) > 0.01f) Redraw(next);
        }

        private void Redraw(float blocks)
        {
            _shown = blocks;
            Palette.Sky(_theme, blocks, out Color top, out Color bottom);
            for (int i = 0; i < Steps; i++)
            {
                float t = i / (Steps - 1f);
                _gradient.SetPixel(0, i, Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t)));
            }
            _gradient.Apply(false);
            _camera.backgroundColor = top;

            // Blocks are lit by the sky: cool from above, warm from the horizon.
            RenderSettings.ambientSkyColor = Color.Lerp(top, Color.white, 0.35f);
            RenderSettings.ambientEquatorColor = Color.Lerp(bottom, Color.white, 0.2f);
            RenderSettings.ambientGroundColor = bottom * 0.7f;
            Horizon = Color.Lerp(bottom, top, 0.25f);
        }

        /// <summary>A unit quad facing the camera (-Z), without a collider.</summary>
        private static Mesh QuadMesh()
        {
            var mesh = new Mesh { name = "SkyQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
