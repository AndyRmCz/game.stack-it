using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Isometric orthographic camera. Three framings, eased between: home (the base block resting low
    /// in a wide sky), play (follows the top of the tower) and game over (pulls back over the whole
    /// tower, high on screen above the results sheet).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private const float Distance = 30f;
        private const float FollowSpeed = 4f;
        private const float FramingSpeed = 2.2f;
        private const float MinOrthoSize = 6.5f;
        private const float HalfWidthNeeded = 3.9f; // moving block travel, projected on screen
        private static readonly Quaternion View = Quaternion.Euler(30f, 45f, 0f);

        public Camera Camera { get; private set; }

        private float _targetY;
        private float _y;
        private float _shake;
        private float _seed;
        private float _zoomTarget;
        private float _zoom;
        private float _homeTarget = 1f;
        private float _home = 1f;

        public static CameraRig Create()
        {
            var go = new GameObject("Camera");
            var rig = go.AddComponent<CameraRig>();
            rig.Camera = go.AddComponent<Camera>();
            rig.Camera.orthographic = true;
            rig.Camera.nearClipPlane = 0.1f;
            rig.Camera.farClipPlane = 200f;
            rig.Camera.clearFlags = CameraClearFlags.SolidColor;
            rig.Camera.backgroundColor = new Color(0.79f, 0.76f, 0.94f);
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            rig._seed = Random.value * 100f;
            return rig;
        }

        public void Follow(float topY, bool snap = false)
        {
            _targetY = topY;
            if (snap) _y = topY;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        /// <summary>0 = gameplay framing, 1 = pulled back to show the whole tower (game over).</summary>
        public void SetZoomOut(float amount, bool snap = false)
        {
            _zoomTarget = amount;
            if (snap) _zoom = amount;
        }

        /// <summary>Home framing: the base block small and low, under the title.</summary>
        public void SetHome(bool home, bool snap = false)
        {
            _homeTarget = home ? 1f : 0f;
            if (snap) _home = _homeTarget;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _y = Mathf.Lerp(_y, _targetY, 1f - Mathf.Exp(-FollowSpeed * dt));
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-FramingSpeed * dt));
            _home = Mathf.Lerp(_home, _homeTarget, 1f - Mathf.Exp(-FramingSpeed * 1.5f * dt));

            float aspect = Mathf.Max(0.3f, Camera.aspect);
            float baseSize = Mathf.Max(MinOrthoSize, HalfWidthNeeded / aspect);

            // Play: the top of the tower a bit below centre, the moving block above it.
            float size = baseSize;
            float focusY = _y + 1.5f;
            // Game over: the whole tower in the upper half of the screen.
            float towerSize = Mathf.Max(baseSize, _y + 3f);
            size = Mathf.Lerp(size, towerSize, _zoom);
            focusY = Mathf.Lerp(focusY, _y * 0.5f - 0.5f * towerSize, _zoom);
            // Home: wider, the base resting at about a third of the screen's height.
            float homeSize = baseSize * 1.25f;
            size = Mathf.Lerp(size, homeSize, _home);
            focusY = Mathf.Lerp(focusY, _y + 0.32f * homeSize, _home);

            Camera.orthographicSize = size;
            Vector3 position = new Vector3(0f, focusY, 0f) - View * Vector3.forward * Distance;

            if (_shake > 0.001f)
            {
                float t = Time.unscaledTime * 30f;
                position += View * new Vector3(Mathf.PerlinNoise(_seed, t) - 0.5f, Mathf.PerlinNoise(t, _seed) - 0.5f, 0f) * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 2f);
            }
            transform.SetPositionAndRotation(position, View);
        }
    }
}
