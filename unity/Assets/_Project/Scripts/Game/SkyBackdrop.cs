using UnityEngine;

namespace StackIt.Game
{
    /// <summary>Full-screen gradient quad behind everything, recolored as the tower grows.</summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        private const int Steps = 64;
        private const float HeightForFullShift = 60f; // blocks

        private Texture2D _gradient;
        private Material _material;
        private Camera _camera;
        private string _theme = Palette.DefaultTheme;
        private float _shown = -1f;
        private float _target;

        public static SkyBackdrop Create(Camera camera)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sky";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(camera.transform, false);
            var sky = quad.AddComponent<SkyBackdrop>();
            sky._camera = camera;
            sky._gradient = new Texture2D(1, Steps, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            sky._material = new Material(Materials.Sky);
            sky._material.SetTexture(Materials.BaseMap, sky._gradient);
            sky._material.SetColor(Materials.BaseColor, Color.white);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = sky._material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return sky;
        }

        public void SetTheme(string theme)
        {
            _theme = string.IsNullOrEmpty(theme) ? Palette.DefaultTheme : theme;
            _shown = -1f;
        }

        public void SetHeight(int blocks) => _target = Mathf.Clamp01(blocks / HeightForFullShift);

        private void LateUpdate()
        {
            float distance = _camera.farClipPlane * 0.9f;
            float h = _camera.orthographicSize * 2.2f;
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(h * Mathf.Max(1f, _camera.aspect), h, 1f);

            float next = Mathf.MoveTowards(_shown < 0 ? _target : _shown, _target, Time.unscaledDeltaTime * 0.2f);
            if (Mathf.Abs(next - _shown) > 0.002f) Redraw(next);
        }

        private void Redraw(float height01)
        {
            _shown = height01;
            Palette.Sky(_theme, height01, out Color top, out Color bottom);
            for (int i = 0; i < Steps; i++)
            {
                float t = i / (Steps - 1f);
                _gradient.SetPixel(0, i, Color.Lerp(bottom, top, Mathf.Pow(t, 0.8f)));
            }
            _gradient.Apply(false);
            _camera.backgroundColor = top;
        }
    }
}
