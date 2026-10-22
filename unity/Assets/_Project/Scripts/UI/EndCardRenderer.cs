using StackIt.Services;
using StackIt.Services.Native;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// The clip's closing card ("48 — Can you beat me? Code K7QX9PA"), drawn offscreen by its own
    /// camera straight into the recorder's render texture. Never shown on the device screen.
    /// </summary>
    public sealed class EndCardRenderer : IEndCardRenderer
    {
        public const int Layer = 31;

        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly RectTransform _score;
        private readonly CanvasGroup _group;

        public EndCardRenderer(EndCard card)
        {
            _root = new GameObject("EndCard");
            _root.layer = Layer;
            _camera = _root.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Theme.Enamel;
            _camera.cullingMask = 1 << Layer;
            _camera.orthographic = true;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.layer = Layer;
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RecorderService.Width, RecorderService.Height);
            _group = canvasGo.AddComponent<CanvasGroup>();

            RectTransform col = UiKit.Stretch(UiKit.Rect("Col", canvasGo.transform), 48, 0, 48, 0);
            UiKit.Column(col, 18, null, TextAnchor.MiddleCenter);
            UiKit.Caps(col, "STACK IT", Theme.InkSoft, 40);
            TextMeshProUGUI score = UiKit.Display(col, card.Score.ToString(), 230, Theme.Ink);
            UiKit.Size(score.rectTransform, height: 250);
            _score = score.rectTransform;
            UiKit.Label(col, card.Headline, 60, Theme.Ink, TextAlignmentOptions.Center, true);
            if (!string.IsNullOrEmpty(card.Code))
            {
                TextMeshProUGUI code = UiKit.Display(col, card.Code, 72, Theme.Pool);
                code.characterSpacing = 14;
            }
            SetLayer(_root.transform);
        }

        public void Render(RenderTexture target, float progress)
        {
            float pop = Mathf.Clamp01(progress * 4f);
            float s = Mathf.LerpUnclamped(0.6f, 1f, StackIt.Game.Tween.EaseOutBack(pop));
            _score.localScale = new Vector3(s, s, 1f);
            _group.alpha = Mathf.Clamp01(progress * 6f);
            _camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            _camera.targetTexture = null;
        }

        public void Dispose() => Object.Destroy(_root);

        private static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform child in t) SetLayer(child);
        }
    }
}
