using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// Ink on paper over a pastel sky: calm, high-contrast UI with tactile buttons (the family-chores
    /// look). Sizes are in canvas units of a 1080-wide portrait screen.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Ink = Hex(0x16213e);
        public static readonly Color InkSoft = Hex(0x4a5578);
        public static readonly Color InkDeep = Hex(0x060b1a);   // lip under ink buttons
        public static readonly Color Paper = Hex(0xffffff);
        public static readonly Color Enamel = Hex(0xedf2f5);    // sheet background
        public static readonly Color Line = Hex(0xd3dce3);      // borders, lip under paper buttons
        public static readonly Color Sun = Hex(0xffc93c);       // coins and rewards
        public static readonly Color SunDeep = Hex(0xd99a0b);
        public static readonly Color Pool = Hex(0x2d7ff9);      // challenges and rivals
        public static readonly Color PoolDeep = Hex(0x1857c4);
        public static readonly Color Tomato = Hex(0xe8473f);    // destructive
        public static readonly Color Mint = Hex(0x1fae6b);      // done / on
        public static readonly Color Glass = new Color(1f, 1f, 1f, 0.82f);          // paper over the sky
        public static readonly Color Scrim = new Color(0.086f, 0.129f, 0.243f, 0.38f);
        public static readonly Color ShadowColor = new Color(0.086f, 0.129f, 0.243f, 0.16f);

        public const float Gutter = 56f;
        public const float Radius = 40f;
        public const float ButtonHeight = 150f;
        public const float Lip = 12f;

        public const float TitleSize = 64f;
        public const float BodySize = 44f;
        public const float SmallSize = 36f;
        public const float CaptionSize = 32f;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    public enum ButtonStyle
    {
        Primary,   // ink: the one main action
        Rival,     // pool: challenge actions
        Reward,    // sun: coins
        Secondary, // paper
        Ghost,     // text only
        Danger,    // paper with tomato text
    }

    /// <summary>Code-built uGUI + TextMeshPro widgets (no prefabs, so everything is reviewable in diffs).</summary>
    public static class UiKit
    {
        /// <summary>Raised on every button press (the App plays the click and a light haptic).</summary>
        public static Action Pressed;

        private const int SpriteRadius = 48;
        private static Sprite _rounded;
        private static Sprite _shadow;

        /// <summary>9-sliced rounded rectangle generated at runtime (no sprite assets needed).</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = RoundedSprite(SpriteRadius, 0f);

        /// <summary>Soft blurred rounded rectangle, drawn under cards.</summary>
        private static Sprite SoftShadow => _shadow != null ? _shadow : _shadow = RoundedSprite(SpriteRadius, 28f);

        private static Sprite RoundedSprite(int radius, float blur)
        {
            int pad = Mathf.CeilToInt(blur);
            int size = 2 * (radius + pad) + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float inner = radius + pad;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to the rounded rectangle whose corners are centred `inner` from the edges.
                float dx = Mathf.Max(inner - x - 0.5f, 0f, x + 0.5f - (size - inner));
                float dy = Mathf.Max(inner - y - 0.5f, 0f, y + 0.5f - (size - inner));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float alpha = blur <= 0f ? Mathf.Clamp01(0.5f - d) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blur * 0.5f, blur, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();
            float border = radius + pad + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
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
            // Portrait phones: always 1080 units wide, so rows never get narrower than designed.
            // Wider than 9:16 (tablets): fit the height instead.
            scaler.matchWidthOrHeight = (float)Screen.width / Screen.height > 1080f / 1920f ? 1f : 0f;
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

        /// <summary>Anchors a full-width strip of <paramref name="height"/> at a vertical anchor (0 = bottom).</summary>
        public static RectTransform Band(RectTransform rt, float anchorY, float height, float pivotY = 0.5f, float inset = Theme.Gutter)
        {
            rt.anchorMin = new Vector2(0, anchorY);
            rt.anchorMax = new Vector2(1, anchorY);
            rt.pivot = new Vector2(0.5f, pivotY);
            rt.sizeDelta = new Vector2(-2 * inset, height);
            rt.anchoredPosition = Vector2.zero;
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

        public static Image Panel(Transform parent, string name, Color color, bool rounded = false, float radius = Theme.Radius)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded) Round(image, radius);
            return image;
        }

        public static void Round(Image image, float radius)
        {
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(1f, radius);
        }

        /// <summary>
        /// A paper card with a soft shadow. Returns the card's root: add content (and layout groups)
        /// to it; the shadow and surface ignore layout.
        /// </summary>
        public static RectTransform Card(Transform parent, string name, Color? color = null, float radius = Theme.Radius, bool shadow = true)
        {
            RectTransform root = Rect(name, parent);
            if (shadow)
            {
                Image s = Panel(root, "Shadow", Theme.ShadowColor);
                s.sprite = SoftShadow;
                s.type = Image.Type.Sliced;
                s.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(1f, radius);
                Stretch(s.rectTransform, -24, -8, -24, -40);
                IgnoreLayout(s.rectTransform);
            }
            Image surface = Panel(root, "Surface", color ?? Theme.Paper, rounded: true, radius: radius);
            Stretch(surface.rectTransform);
            IgnoreLayout(surface.rectTransform);
            return root;
        }

        private static void IgnoreLayout(RectTransform rt) => rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            RectTransform rt = Rect("Label", parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = bold ? Fonts.BodyBold : Fonts.Body;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Numbers and titles in Fredoka.</summary>
        public static TextMeshProUGUI Display(Transform parent, string text, float size, Color color, bool shadow = false)
        {
            TextMeshProUGUI label = Label(parent, text, size, color);
            label.font = Fonts.Display;
            label.characterSpacing = -1f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (shadow) label.gameObject.AddComponent<TextEffect>().Shadow = true;
            return label;
        }

        /// <summary>Small spaced capitals for eyebrows and section labels.</summary>
        public static TextMeshProUGUI Caps(Transform parent, string text, Color color, float size = Theme.CaptionSize,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI label = Label(parent, text, size, color, align, bold: true);
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 8f;
            return label;
        }

        /// <summary>A single icon glyph.</summary>
        public static TextMeshProUGUI Icon(Transform parent, string icon, float size, Color color)
        {
            TextMeshProUGUI label = Label(parent, icon, size, color);
            label.font = Fonts.Icons;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>One line that shrinks (down to <paramref name="minSize"/>) instead of wrapping.</summary>
        public static TextMeshProUGUI OneLine(TextMeshProUGUI label, float minSize)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = minSize;
            return label;
        }

        /// <summary>Text with an optional leading icon (icons render from the text fonts' fallback).</summary>
        public static string WithIcon(string icon, string text) =>
            string.IsNullOrEmpty(icon) ? text : $"<size=120%><voffset=-0.12em>{icon}</voffset></size>  {text}";

        private static void Colors(ButtonStyle style, out Color fill, out Color lip, out Color ink)
        {
            switch (style)
            {
                case ButtonStyle.Primary: fill = Theme.Ink; lip = Theme.InkDeep; ink = Theme.Paper; break;
                case ButtonStyle.Rival: fill = Theme.Pool; lip = Theme.PoolDeep; ink = Theme.Paper; break;
                case ButtonStyle.Reward: fill = Theme.Sun; lip = Theme.SunDeep; ink = Theme.Ink; break;
                case ButtonStyle.Secondary: fill = Theme.Paper; lip = Theme.Line; ink = Theme.Ink; break;
                case ButtonStyle.Danger: fill = Theme.Paper; lip = Theme.Line; ink = Theme.Tomato; break;
                default: fill = Color.clear; lip = Color.clear; ink = Theme.Ink; break;
            }
        }

        /// <summary>
        /// Tactile button: a rounded face resting on a darker lip; pressing sinks the face. Ghost
        /// buttons have no lip and squish instead.
        /// </summary>
        public static Button Button(Transform parent, string text, ButtonStyle style, UnityAction onClick,
            float height = Theme.ButtonHeight, string icon = null)
        {
            Colors(style, out Color fill, out Color lipColor, out Color ink);
            bool lip = style != ButtonStyle.Ghost;

            RectTransform root = Rect("Button", parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // touch area covers face and lip
            Size(root, height: height);

            RectTransform face = root;
            if (lip)
            {
                Image under = Panel(root, "Lip", lipColor, rounded: true, radius: height * 0.3f);
                Stretch(under.rectTransform, 0, Theme.Lip, 0, 0);
                Image top = Panel(root, "Face", fill, rounded: true, radius: height * 0.3f);
                face = Stretch(top.rectTransform, 0, 0, 0, Theme.Lip);
            }

            float textSize = height >= 170 ? 60 : height >= 130 ? 48 : 40;
            TextMeshProUGUI label = Label(face, WithIcon(icon, text), textSize, ink, TextAlignmentOptions.Center, bold: true);
            if (style == ButtonStyle.Primary || style == ButtonStyle.Rival) label.font = Fonts.Display;
            Stretch(label.rectTransform, 32, 0, 32, 0);
            OneLine(label, textSize * 0.6f);

            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // Pressable animates and dims
            button.targetGraphic = hit;
            button.onClick.AddListener(onClick);

            var press = root.gameObject.AddComponent<Pressable>();
            press.Button = button;
            press.Face = lip ? face : null;
            press.Depth = Theme.Lip;
            return button;
        }

        /// <summary>Square (or round) button holding just an icon.</summary>
        public static Button IconButton(Transform parent, string icon, ButtonStyle style, UnityAction onClick, float size = 120f)
        {
            Button button = Button(parent, "", style, onClick, size);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.font = Fonts.Icons;
            label.enableAutoSizing = false;
            label.fontSize = size * 0.48f;
            label.text = icon;
            Size((RectTransform)button.transform, width: size);
            return button;
        }

        /// <summary>Icon over a short label: one tab of the home dock.</summary>
        public static Button DockItem(Transform parent, string icon, string text, UnityAction onClick)
        {
            RectTransform root = Rect("Dock " + text, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            TextMeshProUGUI glyph = Icon(root, icon, 64, Theme.Ink);
            glyph.rectTransform.anchorMin = new Vector2(0, 0.42f);
            glyph.rectTransform.anchorMax = new Vector2(1, 1);
            glyph.rectTransform.offsetMin = glyph.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI label = Label(root, text, 30, Theme.InkSoft, TextAlignmentOptions.Top, bold: true);
            label.rectTransform.anchorMin = new Vector2(0, 0);
            label.rectTransform.anchorMax = new Vector2(1, 0.42f);
            label.rectTransform.offsetMin = new Vector2(4, 0);
            label.rectTransform.offsetMax = new Vector2(-4, 0);
            OneLine(label, 22);

            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.AddListener(onClick);
            root.gameObject.AddComponent<Pressable>().Button = button;
            return button;
        }

        /// <summary>
        /// Rounded pill that hugs its text, e.g. the coin balance or "Perfect ×3". Inside a layout
        /// group the group sizes it; on its own, a ContentSizeFitter does (set its anchors/position).
        /// </summary>
        public static TextMeshProUGUI Pill(Transform parent, string text, Color fill, Color ink, float height = 96f, string icon = null)
        {
            bool inLayout = parent.GetComponent<LayoutGroup>() != null;
            Image bg = Panel(parent, "Pill", fill, rounded: true, radius: height / 2f);
            Size(bg.rectTransform, height: height);
            var layout = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            int pad = Mathf.RoundToInt(height * 0.36f);
            layout.padding = new RectOffset(pad, pad, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            if (!inLayout)
            {
                var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                bg.rectTransform.sizeDelta = new Vector2(0, height);
            }
            TextMeshProUGUI label = Label(bg.transform, WithIcon(icon, text), height * 0.44f, ink, TextAlignmentOptions.Center, bold: true);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>
        /// Segmented control (tabs): a sunken track with a paper thumb behind the selected option.
        /// Returns the setter that moves the selection (without calling <paramref name="onSelect"/>).
        /// </summary>
        public static Action<int> Segmented(Transform parent, string[] labels, Action<int> onSelect, float height = 116f)
        {
            Image track = Panel(parent, "Segmented", Theme.Line, rounded: true, radius: height / 2f);
            Size(track.rectTransform, height: height);
            HorizontalLayoutGroup row = Row(track.rectTransform, 0);
            row.padding = new RectOffset(8, 8, 8, 8);
            var thumbs = new Image[labels.Length];
            var texts = new TextMeshProUGUI[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                Image thumb = Panel(track.transform, labels[i], Color.clear, rounded: true, radius: (height - 16f) / 2f);
                thumb.raycastTarget = true;
                TextMeshProUGUI text = Label(thumb.transform, labels[i], 40, Theme.InkSoft, TextAlignmentOptions.Center, bold: true);
                Stretch(text.rectTransform, 12, 0, 12, 0);
                OneLine(text, 28);
                var button = thumb.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    Pressed?.Invoke();
                    onSelect(index);
                });
                thumbs[i] = thumb;
                texts[i] = text;
            }
            return selected =>
            {
                for (int i = 0; i < thumbs.Length; i++)
                {
                    thumbs[i].color = i == selected ? Theme.Paper : Color.clear;
                    texts[i].color = i == selected ? Theme.Ink : Theme.InkSoft;
                }
            };
        }

        /// <summary>Round badge with a centred glyph or initial, e.g. a friend's avatar or a rank.</summary>
        public static TextMeshProUGUI Badge(Transform parent, string text, Color fill, Color ink, float size = 96f, bool icon = false)
        {
            Image circle = Panel(parent, "Badge", fill, rounded: true, radius: size / 2f);
            Size(circle.rectTransform, width: size, height: size);
            TextMeshProUGUI label = icon ? Icon(circle.transform, text, size * 0.5f, ink) : Display(circle.transform, text, size * 0.46f, ink);
            Stretch(label.rectTransform);
            return label;
        }

        public static void SetText(Button button, string text, string icon = null) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = WithIcon(icon, text);

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
            if (width >= 0)
            {
                element.preferredWidth = width;
                element.minWidth = width;
            }
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
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            RectTransform content = Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            // Room for card shadows at the edges.
            Column(content, spacing, new RectOffset(4, 4, 8, 48));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            return content;
        }

        public static TMP_InputField Input(Transform parent, string placeholder, int characterLimit,
            TMP_InputField.CharacterValidation validation = TMP_InputField.CharacterValidation.None)
        {
            RectTransform root = Rect("Input", parent);
            Size(root, height: 140);
            Image border = Panel(root, "Border", Theme.Line, rounded: true, radius: 36);
            Stretch(border.rectTransform);
            Image bg = Panel(root, "Field", Theme.Paper, rounded: true, radius: 32);
            Stretch(bg.rectTransform, 4, 4, 4, 4);
            bg.raycastTarget = true;
            RectTransform area = Stretch(Rect("Text Area", bg.transform), 40, 12, 40, 12);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Label(area, placeholder, Theme.BodySize, Theme.WithAlpha(Theme.InkSoft, 0.55f), TextAlignmentOptions.Left);
            Stretch(hint.rectTransform);
            OneLine(hint, 30);
            TextMeshProUGUI text = Label(area, "", 48, Theme.Ink, TextAlignmentOptions.Left, bold: true);
            Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = characterLimit;
            field.characterValidation = validation;
            field.caretColor = Theme.Pool;
            field.caretWidth = 4;
            field.selectionColor = Theme.WithAlpha(Theme.Pool, 0.25f);
            return field;
        }

        public static Toggle Toggle(Transform parent, string icon, string text, bool value, UnityAction<bool> onChange)
        {
            RectTransform row = Rect("Toggle", parent);
            Size(row, height: 120);
            TextMeshProUGUI label = Label(row, WithIcon(icon, text), Theme.BodySize, Theme.Ink, TextAlignmentOptions.Left, bold: true);
            Stretch(label.rectTransform, 0, 0, 200, 0);

            Image track = Panel(row, "Track", Theme.Line, rounded: true, radius: 42);
            track.raycastTarget = true;
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.pivot = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(150, 84);
            Image knob = Panel(track.transform, "Knob", Theme.Paper, rounded: true, radius: 34);
            knob.rectTransform.sizeDelta = new Vector2(68, 68);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = track;
            void Paint(bool on, bool animate)
            {
                track.color = on ? Theme.Mint : Theme.Line;
                RectTransform k = knob.rectTransform;
                k.anchorMin = k.anchorMax = new Vector2(0.5f, 0.5f);
                float to = on ? 33f : -33f;
                if (!animate)
                {
                    k.anchoredPosition = new Vector2(to, 0f);
                    return;
                }
                float from = k.anchoredPosition.x;
                StackIt.Game.Tween.Run(k, 0.25f, t =>
                {
                    if (k != null) k.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, StackIt.Game.Tween.EaseOutBack(t)), 0f);
                });
            }
            toggle.isOn = value;
            Paint(value, false);
            toggle.onValueChanged.AddListener(on =>
            {
                Pressed?.Invoke();
                Paint(on, true);
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
