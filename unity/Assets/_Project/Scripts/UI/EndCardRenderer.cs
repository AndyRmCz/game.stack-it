using StackIt.Client.Theming;
using StackIt.Services;
using StackIt.Services.Native;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// The clip's closing card in the player's current look: the logo, the big score, "Can you beat
    /// me?" and the challenge code in a pill, drawn offscreen by its own camera into its own
    /// 720 × 1280 texture, so the layout has the video's shape rather than the phone screen's
    /// (sizes are in pixels; the score is 220 px so it stays readable in a phone feed). The
    /// recorder copies it into the video the right way up. Never shown on the device screen.
    /// </summary>
    public sealed class EndCardRenderer : IEndCardRenderer
    {
        public const int Layer = 31;

        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly RenderTexture _target;
        private readonly RectTransform _score;
        private readonly CanvasGroup _group;

        public EndCardRenderer(EndCard card)
        {
            _root = new GameObject("EndCard");
            _root.layer = Layer;
            _camera = _root.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Look.C(T.TextPrimary); // hidden behind the gradient
            _camera.cullingMask = 1 << Layer;
            _camera.orthographic = true;
            _target = new RenderTexture(RecorderService.Width, RecorderService.Height, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target; // from the start: the canvas sizes itself to it

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.layer = Layer;
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.vertexColorAlwaysGammaSpace = true;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RecorderService.Width, RecorderService.Height);

            UiKit.Background(canvasGo.transform, 0.5f);
            RectTransform col = UiKit.Stretch(UiKit.Rect("Col", canvasGo.transform), 48, 0, 48, 0);
            _group = col.gameObject.AddComponent<CanvasGroup>(); // the content fades in over the background
            UiKit.Column(col, 22, null, TextAnchor.MiddleCenter);
            TextMeshProUGUI logo = UiKit.Text(col, "STACK IT", TextRole.Logo, size: 96f);
            UiKit.Size(logo.rectTransform, height: 110);
            TextEffect.Add(logo, TextEffectKind.LogoGlow);
            TextMeshProUGUI score = UiKit.Text(col, card.Score.ToString(), TextRole.BigNumber, T.TextPrimary, size: 220f);
            UiKit.Size(score.rectTransform, height: 240);
            TextEffect.Add(score, TextEffectKind.LogoGlow);
            _score = score.rectTransform;
            TextMeshProUGUI headline = UiKit.Text(col, card.Headline, TextRole.Title, T.TextPrimary, size: 60f);
            UiKit.Size(headline.rectTransform, height: 76);
            if (!string.IsNullOrEmpty(card.Code))
            {
                // The code in a pill, like the app's secondary buttons.
                RectTransform line = UiKit.Rect("CodeLine", col);
                UiKit.Size(line, height: 104);
                HorizontalLayoutGroup row = UiKit.Row(line, 0);
                row.childForceExpandWidth = false;
                row.childControlWidth = true;
                RoundedRect pill = UiKit.Shape(line, "Code", 52f);
                var themed = pill.gameObject.AddComponent<ThemedButton>();
                themed.Kind = ButtonKind.Secondary;
                themed.Face = pill;
                HorizontalLayoutGroup inside = UiKit.Row(pill.rectTransform, 0);
                inside.padding = new RectOffset(48, 48, 0, 0);
                inside.childForceExpandWidth = false;
                TextMeshProUGUI code = UiKit.Text(pill.transform, card.Code, TextRole.BigNumber, null, size: 64f);
                code.characterSpacing = 14f;
                themed.Ink = new Graphic[] { code };
                ThemedPart.Mark(code, themed);
                themed.Refresh();
                pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            SetLayer(_root.transform);
        }

        public Texture Render(float progress)
        {
            float pop = Mathf.Clamp01(progress * 4f);
            float s = Mathf.LerpUnclamped(0.6f, 1f, StackIt.Game.Tween.EaseOutBack(pop));
            _score.localScale = new Vector3(s, s, 1f);
            _group.alpha = Mathf.Clamp01(progress * 6f);
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            return _target;
        }

        public void Dispose()
        {
            _camera.targetTexture = null;
            _target.Release();
            Object.Destroy(_target);
            Object.Destroy(_root);
        }

        private static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform child in t) SetLayer(child);
        }
    }
}
