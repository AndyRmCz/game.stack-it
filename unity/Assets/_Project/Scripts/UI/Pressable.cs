using StackIt.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Tactile press for code-built buttons: the face sinks onto its lip (or the whole button
    /// squishes when it has no lip), with a click and a light haptic through UiKit.Pressed.
    /// Also dims the button while it is not interactable.
    /// </summary>
    public sealed class Pressable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform Face;
        public float Depth;
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
            Animate(1f, 0.06f, false);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!_down) return;
            _down = false;
            Animate(0f, 0.22f, true);
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
                Apply(overshoot ? Mathf.LerpUnclamped(from, to, Tween.EaseOutBack(t)) : Mathf.Lerp(from, to, t));
            });
        }

        /// <summary>0 = at rest, 1 = fully pressed (overshoot may briefly go below 0).</summary>
        private void Apply(float amount)
        {
            _amount = amount;
            if (Face != null)
            {
                var shift = new Vector2(0f, -Depth * 0.8f * amount);
                Face.offsetMin = _min + shift;
                Face.offsetMax = _max + shift;
            }
            else
            {
                float s = 1f - 0.05f * amount;
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
