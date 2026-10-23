using StackIt.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Tactile press for code-built buttons. With a <see cref="Face"/> (primary buttons) the face
    /// moves down <see cref="Depth"/> over 60 ms, so its 6 pt edge shrinks to 2, then springs back
    /// over 140 ms with a slight overshoot; other buttons squish a little. Fires UiKit.Pressed
    /// (click and light haptic) and dims the button while it is not interactable.
    /// </summary>
    public sealed class Pressable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float DownSeconds = 0.06f;
        private const float UpSeconds = 0.14f;

        public RectTransform Face;
        public float Depth = 4f;
        public Button Button;

        private CanvasGroup _group;
        private Vector2 _min, _max;
        private bool _captured;
        private bool _down;
        private float _amount;
        private bool _lastInteractable = true;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Button != null && !Button.IsInteractable()) return;
            _down = true;
            UiKit.Pressed?.Invoke();
            Animate(1f, DownSeconds, false);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!_down) return;
            _down = false;
            Animate(0f, UpSeconds, true);
        }

        private void Animate(float to, float duration, bool overshoot)
        {
            if (Face != null && !_captured)
            {
                _min = Face.offsetMin;
                _max = Face.offsetMax;
                _captured = true;
            }
            float from = _amount;
            Transform target = Face != null ? Face : transform;
            Tween.Run(target, duration, t =>
            {
                if (this == null) return;
                Apply(overshoot ? Mathf.LerpUnclamped(from, to, Tween.EaseOutBack(t)) : Mathf.Lerp(from, to, Tween.EaseOutCubic(t)));
            });
        }

        /// <summary>0 = at rest, 1 = fully pressed (overshoot may briefly go below 0).</summary>
        private void Apply(float amount)
        {
            _amount = amount;
            if (Face != null)
            {
                var shift = new Vector2(0f, -Depth * amount);
                Face.offsetMin = _min + shift;
                Face.offsetMax = _max + shift;
            }
            else
            {
                float s = 1f - 0.04f * amount;
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        private void LateUpdate()
        {
            bool interactable = Button == null || Button.IsInteractable();
            if (interactable == _lastInteractable) return;
            _lastInteractable = interactable;
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = interactable ? 1f : 0.45f;
        }
    }
}
