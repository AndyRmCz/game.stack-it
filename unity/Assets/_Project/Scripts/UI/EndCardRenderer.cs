using System.Collections.Generic;
using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Game;
using StackIt.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// The challenge card, the image shared with the link, in the player's current look: the logo,
    /// the score to beat, and the player's tower (its blocks in the colours the run had on the way
    /// up) reaching the rival line the friend will race in the game, with Blocky standing on top
    /// under the gold slab. Then "Can you beat me?" and the code in a pill. Drawn offscreen by its
    /// own camera into a 720 x 1280 texture (a story's shape; sizes and positions are pixels from
    /// the top). <see cref="SavePng"/> makes the shared image. Never shown on the device screen.
    /// </summary>
    public sealed class EndCardRenderer : System.IDisposable
    {
        public const int Layer = 31;
        public const int Width = 720;
        public const int Height = 1280;
        private const float Margin = 48f;
        private const float LineY = 760f; // the line to beat, where Blocky stands
        private const int TowerBlocks = 5;

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
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
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
            scaler.referenceResolution = new Vector2(Width, Height);

            UiKit.Background(canvasGo.transform, 0.5f);
            RectTransform content = UiKit.Stretch(UiKit.Rect("Content", canvasGo.transform));
            _group = content.gameObject.AddComponent<CanvasGroup>(); // the content fades in over the background

            TextMeshProUGUI logo = UiKit.Text(content, "milli", TextRole.Logo, size: 84f);
            Place(logo.rectTransform, 132f, 100f);
            TextEffect.Add(logo, TextEffectKind.LogoGlow);
            TextMeshProUGUI score = UiKit.Text(content, card.Score.ToString(), TextRole.BigNumber, T.TextPrimary, size: 220f);
            Place(score.rectTransform, 330f, 240f);
            TextEffect.Add(score, TextEffectKind.LogoGlow);
            _score = score.rectTransform;
            if (!string.IsNullOrEmpty(card.ScoreLabel))
                Place(UiKit.Text(content, card.ScoreLabel, TextRole.Overline, T.TextSecondary, size: 26f).rectTransform, 478f, 36f);

            // The rival line as the game draws it, behind the tower that reaches it.
            RoundedRect line = UiKit.Solid(content, "LineToBeat", T.Accent2, 2f, alpha: 0.85f, glow: T.Accent2Glow);
            Place(line.rectTransform, LineY, 4f, Width);
            Tower(content, card.Score);
            Blocky blocky = Blocky.Create(content, 104f, still: true);
            blocky.SetMood(MascotMood.Cheer);
            var feet = (RectTransform)blocky.transform;
            feet.anchorMin = feet.anchorMax = new Vector2(0.5f, 1f);
            feet.pivot = new Vector2(0.5f, 0f);
            feet.anchoredPosition = new Vector2(0f, -LineY);

            TextMeshProUGUI headline = UiKit.Text(content, card.Headline, TextRole.Title, T.TextPrimary, size: 60f);
            Place(headline.rectTransform, 1062f, 76f);
            if (!string.IsNullOrEmpty(card.Code))
            {
                // The code in a pill, like the app's secondary buttons.
                RectTransform codeLine = UiKit.Rect("CodeLine", content);
                Place(codeLine, 1170f, 104f);
                HorizontalLayoutGroup row = UiKit.Row(codeLine, 0);
                row.childForceExpandWidth = false;
                row.childControlWidth = true;
                RoundedRect pill = UiKit.Shape(codeLine, "Code", 52f);
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

        /// <summary>The player's tower, its top face on the line to beat.</summary>
        private static void Tower(RectTransform parent, int score)
        {
            RectTransform rt = UiKit.Rect("Tower", parent);
            Place(rt, 0f, 400f, 240f);
            var tower = rt.gameObject.AddComponent<IsoTower>();
            tower.raycastTarget = false;
            var blocks = new List<IsoTower.BlockFaces>();
            foreach (int height in ShareImage.TowerBlocks(score, TowerBlocks))
                blocks.Add(IsoTower.FromTop(Palette.BlockTop(null, height, Look.P)));
            tower.SetBlocks(blocks);
            rt.anchoredPosition = new Vector2(0f, -(LineY + tower.TopFaceCentre().y));
        }

        /// <summary>Centres <paramref name="rt"/> <paramref name="fromTop"/> pixels below the card's top edge.</summary>
        private static void Place(RectTransform rt, float fromTop, float height, float width = Width - 2f * Margin)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, -fromTop);
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

        /// <summary>The finished card (score popped, content in) as a PNG at <paramref name="path"/>: the challenge share.</summary>
        public void SavePng(string path)
        {
            var still = (RenderTexture)Render(1f);
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(still.width, still.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = still;
                image.ReadPixels(new Rect(0, 0, still.width, still.height), 0, 0);
                image.Apply(false);
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(image);
            }
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
