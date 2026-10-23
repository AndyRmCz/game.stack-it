using StackIt.Client;
using StackIt.Client.Theming;
using UnityEngine;
using UnityEngine.Rendering;

namespace StackIt.Game
{
    /// <summary>
    /// Full-screen gradient quad behind the world. With the default sky it is the look's background
    /// (bg.gradient through three stops, and Neon's centre glow behind the hero), so Home's 3D
    /// tower sits on exactly the background the menus use. Bought sky themes keep their own
    /// gradients, which drift through four moments as the tower grows. <see cref="Horizon"/> is
    /// what the lower stack fades into.
    /// </summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        private const int Width = 32;
        private const int Height = 128;
        private const float DriftPerSecond = 6f; // blocks of sky per second when catching up
        private const float GlowCentreY = 0.55f;

        private Texture2D _gradient;
        private Material _material;
        private Camera _camera;
        private ThemePalette _look;
        private string _theme = Palette.DefaultTheme;
        private float _shown = -1f;
        private float _target;
        private float _aspect;

        public static SkyBackdrop Create(Camera camera)
        {
            var quad = new GameObject("Sky");
            quad.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            quad.transform.SetParent(camera.transform, false);
            var sky = quad.AddComponent<SkyBackdrop>();
            sky._camera = camera;
            sky._gradient = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            sky._material = new Material(Materials.Sky);
            sky._material.SetTexture(Materials.BaseMap, sky._gradient);
            sky._material.SetColor(Materials.BaseColor, Color.white);
            var renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sky._material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            RenderSettings.fog = false; // the stack fades into the horizon in StackView instead
            return sky;
        }

        /// <summary>The colour the lower stack fades towards.</summary>
        public Color Horizon { get; private set; } = Color.white;

        public void SetLook(ThemePalette look, string theme)
        {
            _look = look;
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

            bool drifts = !Palette.FollowsLook(_theme);
            float next = _shown < 0 || !drifts ? _target : Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime * DriftPerSecond);
            bool aspectChanged = Mathf.Abs(_camera.aspect - _aspect) > 0.001f;
            if ((drifts && Mathf.Abs(next - _shown) > 0.01f) || aspectChanged) Redraw(next);
        }

        private void Redraw(float blocks)
        {
            if (_look == null) return;
            _shown = blocks;
            _aspect = _camera.aspect;
            Color top, middle, bottom;
            float mid;
            Color glow = Color.clear;
            float radius = 0f;
            if (Palette.FollowsLook(_theme))
            {
                Rgba[] stops = _look.Colors(T.BgGradient);
                top = Palette.ToColor(stops[0]);
                middle = Palette.ToColor(stops[1]);
                bottom = Palette.ToColor(stops[2]);
                mid = _look.Number(T.BgGradientMid);
                Glow centre = _look.Glow(T.BgCenterGlow);
                glow = Palette.ToColor(centre.Color);
                radius = centre.Radius;
            }
            else
            {
                Palette.Sky(_theme, blocks, out top, out bottom);
                middle = Color.Lerp(top, bottom, 0.5f);
                mid = 0.5f;
            }

            // The quad is 1.1 screen heights tall and at least as wide (see LateUpdate); the glow's
            // radius is a fraction of the screen width, so convert it to quad heights.
            float aspect = Mathf.Max(1f, _camera.aspect);
            float glowRadius = Mathf.Max(0.01f, radius * _camera.aspect / 1.1f);
            for (int y = 0; y < Height; y++)
            {
                float v = y / (Height - 1f); // 0 = bottom
                float fromTop = 1f - v;
                Color baseColor = fromTop < mid
                    ? Color.Lerp(top, middle, fromTop / mid)
                    : Color.Lerp(middle, bottom, (fromTop - mid) / (1f - mid));
                for (int x = 0; x < Width; x++)
                {
                    Color c = baseColor;
                    if (glow.a > 0f)
                    {
                        float dx = (x / (Width - 1f) - 0.5f) * aspect;
                        float dy = v - GlowCentreY;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / glowRadius;
                        float k = glow.a * Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                        c = Color.Lerp(c, new Color(glow.r, glow.g, glow.b, 1f), k);
                    }
                    _gradient.SetPixel(x, y, c);
                }
            }
            _gradient.Apply(false);
            _camera.backgroundColor = top;
            Horizon = bottom;
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
