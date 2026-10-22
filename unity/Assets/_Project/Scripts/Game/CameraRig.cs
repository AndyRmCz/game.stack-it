using UnityEngine;

namespace StackIt.Game
{
    /// <summary>Isometric orthographic camera that follows the top of the tower and shakes on impact.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private const float Distance = 30f;
        private const float FollowSpeed = 4f;
        private const float MinOrthoSize = 6.5f;
        private const float HalfWidthNeeded = 3.9f; // moving block travel, projected on screen

        public Camera Camera { get; private set; }

        private float _targetY;
        private float _y;
        private float _shake;
        private float _seed;
        private float _zoomOut;

        public static CameraRig Create()
        {
            var go = new GameObject("Camera");
            var rig = go.AddComponent<CameraRig>();
            rig.Camera = go.AddComponent<Camera>();
            rig.Camera.orthographic = true;
            rig.Camera.nearClipPlane = 0.1f;
            rig.Camera.farClipPlane = 200f;
            rig.Camera.clearFlags = CameraClearFlags.SolidColor;
            rig.Camera.backgroundColor = new Color(0.08f, 0.04f, 0.18f);
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
        public void SetZoomOut(float amount) => _zoomOut = amount;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _y = Mathf.Lerp(_y, _targetY, 1f - Mathf.Exp(-FollowSpeed * dt));

            float aspect = Mathf.Max(0.3f, Camera.aspect);
            float baseSize = Mathf.Max(MinOrthoSize, HalfWidthNeeded / aspect);
            float towerSize = Mathf.Max(baseSize, _y * 0.75f + 2f);
            Camera.orthographicSize = Mathf.Lerp(baseSize, towerSize, _zoomOut);

            Quaternion rotation = Quaternion.Euler(30f, 45f, 0f);
            // Keep the top of the tower a bit below the screen centre, the moving block above it.
            float focusY = Mathf.Lerp(_y + 1.5f, _y * 0.5f, _zoomOut);
            Vector3 focus = new Vector3(0f, focusY, 0f);
            Vector3 position = focus - rotation * Vector3.forward * Distance;

            if (_shake > 0.001f)
            {
                float t = Time.unscaledTime * 35f;
                position += rotation * new Vector3(Mathf.PerlinNoise(_seed, t) - 0.5f, Mathf.PerlinNoise(t, _seed) - 0.5f, 0f) * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.5f);
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
