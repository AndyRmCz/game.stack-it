using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>Colors and sizes of the UI, matching the web landing page (backend/app/web/pages.py).</summary>
    public static class Theme
    {
        public static readonly Color Night = Hex(0x140a2e);
        public static readonly Color Dusk = Hex(0x3b1060);
        public static readonly Color Magenta = Hex(0xff3d9a);
        public static readonly Color Cyan = Hex(0x2de2e6);
        public static readonly Color Amber = Hex(0xffb84d);
        public static readonly Color Moon = Hex(0xf4f0ff);
        public static readonly Color Lavender = Hex(0xb9a8e0);
        public static readonly Color Scrim = new Color(0.03f, 0.01f, 0.09f, 0.82f);
        public static readonly Color Card = new Color(0.23f, 0.06f, 0.38f, 0.92f);
        public static readonly Color Line = new Color(0.96f, 0.94f, 1f, 0.22f);

        public const float Gutter = 48f;
        public const float ButtonHeight = 140f;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }

    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Ghost,
        Rival,
    }

    /// <summary>Code-built uGUI + TextMeshPro widgets (no prefabs, so everything is reviewable in diffs).</summary>
    public static class UiKit
    {
        private static Sprite _rounded;

        /// <summary>9-sliced rounded rectangle generated at runtime (no sprite assets needed).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64;
                const float radius = 24f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, 0f, x + 0.5f - (size - radius));
                    float dy = Mathf.Max(radius - y - 0.5f, 0f, y + 0.5f - (size - radius));
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                tex.Apply();
                var border = new Vector4(radius, radius, radius, radius);
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                return _rounded;
            }
        }

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors to the device safe area (notches, home indicator).</summary>
        public static RectTransform SafeArea(Transform parent)
        {
            RectTransform rt = Stretch(Rect("SafeArea", parent));
            Rect safe = Screen.safeArea;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = false)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2f;
            }
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            RectTransform rt = Rect("Label", parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Display numerals: tight, bold, with a soft glow (defaults to the text color).</summary>
        public static TextMeshProUGUI Display(Transform parent, string text, float size, Color color,
            Color? glow = null, float glowWidth = 0.12f)
        {
            TextMeshProUGUI label = Label(parent, text, size, color, TextAlignmentOptions.Center, true);
            label.characterSpacing = -2f;
            var textGlow = label.gameObject.AddComponent<TextGlow>();
            textGlow.Width = glowWidth;
            textGlow.Color = glow ?? new Color(color.r, color.g, color.b, 0.35f);
            return label;
        }

        public static Button Button(Transform parent, string text, ButtonStyle style, UnityAction onClick, float height = Theme.ButtonHeight)
        {
            Color fill, ink;
            switch (style)
            {
                case ButtonStyle.Primary: fill = Theme.Magenta; ink = Color.white; break;
                case ButtonStyle.Rival: fill = Theme.Cyan; ink = Theme.Night; break;
                case ButtonStyle.Secondary: fill = new Color(1f, 1f, 1f, 0.12f); ink = Theme.Moon; break;
                default: fill = new Color(0, 0, 0, 0); ink = Theme.Lavender; break;
            }
            Image bg = Panel(parent, "Button", fill, rounded: true);
            bg.raycastTarget = true;
            if (style == ButtonStyle.Ghost)
            {
                var outline = bg.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Line;
                outline.effectDistance = new Vector2(2, -2);
            }
            var button = bg.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Size(bg.rectTransform, height: height);

            TextMeshProUGUI label = Label(bg.transform, text, style == ButtonStyle.Primary ? 52 : 44, ink, TextAlignmentOptions.Center, true);
            label.characterSpacing = 2f;
            Stretch(label.rectTransform, 24, 0, 24, 0);
            return button;
        }

        public static void SetText(Button button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperCenter)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static LayoutElement Size(RectTransform rt, float width = -1, float height = -1, float flexibleWidth = -1)
        {
            // Explicit == null: Unity's fake-null objects defeat the ?? operator.
            var element = rt.GetComponent<LayoutElement>();
            if (element == null) element = rt.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) element.preferredWidth = width;
            if (height >= 0)
            {
                element.preferredHeight = height;
                element.minHeight = height;
            }
            if (flexibleWidth >= 0) element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static RectTransform Spacer(Transform parent, float height)
        {
            RectTransform rt = Rect("Spacer", parent);
            Size(rt, height: height);
            return rt;
        }

        /// <summary>Vertical scrolling list; add rows to the returned content transform.</summary>
        public static RectTransform ScrollList(Transform parent, float spacing)
        {
            Image viewport = Panel(parent, "Scroll", new Color(0, 0, 0, 0));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            RectTransform content = Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            Column(content, spacing);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            return content;
        }

        public static TMP_InputField Input(Transform parent, string placeholder, int characterLimit,
            TMP_InputField.CharacterValidation validation = TMP_InputField.CharacterValidation.None)
        {
            Image bg = Panel(parent, "Input", new Color(1f, 1f, 1f, 0.1f), rounded: true);
            Size(bg.rectTransform, height: 130);
            RectTransform area = Stretch(Rect("Text Area", bg.transform), 36, 12, 36, 12);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Label(area, placeholder, 46, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Left);
            Stretch(hint.rectTransform);
            TextMeshProUGUI text = Label(area, "", 50, Theme.Moon, TextAlignmentOptions.Left, true);
            Stretch(text.rectTransform);

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = characterLimit;
            field.characterValidation = validation;
            field.caretColor = Theme.Cyan;
            field.selectionColor = new Color(Theme.Cyan.r, Theme.Cyan.g, Theme.Cyan.b, 0.35f);
            return field;
        }

        public static Toggle Toggle(Transform parent, string text, bool value, UnityAction<bool> onChange)
        {
            RectTransform row = Rect("Toggle", parent);
            Size(row, height: 120);
            TextMeshProUGUI label = Label(row, text, 46, Theme.Moon, TextAlignmentOptions.Left);
            Stretch(label.rectTransform, 0, 0, 220, 0);

            Image track = Panel(row, "Track", new Color(1f, 1f, 1f, 0.15f), rounded: true);
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.pivot = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(170, 84);
            Image knob = Panel(track.transform, "Knob", Theme.Moon, rounded: true);
            knob.rectTransform.sizeDelta = new Vector2(68, 68);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = track;
            void Paint(bool on)
            {
                track.color = on ? Theme.Magenta : new Color(1f, 1f, 1f, 0.15f);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 0.75f : 0.25f, 0.5f);
                knob.rectTransform.anchoredPosition = Vector2.zero;
            }
            toggle.isOn = value;
            Paint(value);
            toggle.onValueChanged.AddListener(on =>
            {
                Paint(on);
                onChange(on);
            });
            return toggle;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static string Format(string template, params object[] args)
        {
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }
}
