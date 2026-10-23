using System;
using StackIt.Client.Theming;
using StackIt.Game;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>Layout of the design language, in points at the 390 × 844 reference canvas.</summary>
    public static class Ui
    {
        public const float RefWidth = 390f;
        public const float RefHeight = 844f;

        public const float Side = 20f;           // side padding
        public const float TopGap = 12f;         // top bar below the safe area
        public const float BottomGap = 28f;      // above the safe area
        public const float PrimaryHeight = 72f;
        public const float PrimaryRadius = 24f;
        public const float Edge = 6f;            // the primary button's pressable bottom edge
        public const float PressDepth = 4f;      // edge 6 → 2 while pressed
        public const float PrimaryIcon = 22f;
        public const float PrimaryGap = 12f;
        public const float PillHeight = 44f;
        public const float PillPad = 18f;
        public const float PillIcon = 18f;
        public const float IconButton = 48f;
        public const float IconButtonRadius = 16f;
        public const float IconSize = 22f;
        public const float CoinPill = 40f;
        public const float CoinIcon = 22f;
        public const float NavRadius = 22f;
        public const float NavPad = 8f;
        public const float NavItem = 56f;
        public const float NavHeight = NavItem + 2 * NavPad;
        public const float CardRadius = 18f;
        public const float CardPad = 16f;
        public const float RowGap = 12f;
        public const float Dot = 10f;
        public const float SheetRadius = 22f;
        public const float Header = 72f;         // top bar height under the safe area
        public const float Banner = 56f;         // banner ad height reserved above the bottom navigation (menus)
    }

    /// <summary>
    /// Code-built uGUI + TextMeshPro widgets (no prefabs, so everything is reviewable in diffs).
    /// Every coloured graphic is bound to look tokens through a themed component; nothing here
    /// holds a raw colour.
    /// </summary>
    public static class UiKit
    {
        /// <summary>Raised on every button press (the App plays the click and a light haptic).</summary>
        public static Action Pressed;

        // --- canvas & layout -------------------------------------------------------------------------

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go); // not allowed in edit-mode tools and tests
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.vertexColorAlwaysGammaSpace = true; // UI colours are sRGB, as in the tokens
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2
                | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Ui.RefWidth, Ui.RefHeight);
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

        /// <summary>Anchors a full-width strip of <paramref name="height"/> at a vertical anchor (0 = bottom).</summary>
        public static RectTransform Band(RectTransform rt, float anchorY, float height, float pivotY = 0.5f, float inset = Ui.Side)
        {
            rt.anchorMin = new Vector2(0, anchorY);
            rt.anchorMax = new Vector2(1, anchorY);
            rt.pivot = new Vector2(0.5f, pivotY);
            rt.sizeDelta = new Vector2(-2 * inset, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Pins a rect of <paramref name="size"/> to a corner/edge anchor with an offset.</summary>
        public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        /// <summary>Anchors to the device safe area (notches, home indicator) on all four sides.</summary>
        public static RectTransform SafeArea(Transform parent)
        {
            RectTransform rt = Stretch(Rect("SafeArea", parent));
            Rect safe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return rt;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            return rt;
        }

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
                // A Row (children force-expanded) reports itself as flexible, so a column would
                // stretch it to fill the screen; a fixed height means fixed.
                element.flexibleHeight = 0;
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

        /// <summary>A spacer that takes the column's leftover height (pushes what follows to the bottom).</summary>
        public static RectTransform Flex(Transform parent)
        {
            RectTransform rt = Rect("Flex", parent);
            rt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            return rt;
        }

        public static void IgnoreLayout(Component c)
        {
            var element = c.GetComponent<LayoutElement>();
            if (element == null) element = c.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        /// <summary>
        /// Vertical scrolling list; add rows to the returned content transform. It fills its parent
        /// (a layout group on the parent sizes it instead).
        /// </summary>
        public static RectTransform ScrollList(Transform parent, float spacing)
        {
            RectTransform viewport = Stretch(Rect("Scroll", parent));
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // catches drags between rows
            IgnoreTheming.Mark(hit, "invisible drag area");
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 12);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            // Room for card shadows at the edges.
            Column(content, spacing, new RectOffset(2, 2, 4, 24));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
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

        // --- surfaces --------------------------------------------------------------------------------

        public static RoundedRect Shape(Transform parent, string name, float radius)
        {
            RectTransform rt = Rect(name, parent);
            var shape = rt.gameObject.AddComponent<RoundedRect>();
            shape.Radius = radius;
            shape.raycastTarget = false;
            return shape;
        }

        /// <summary>A themed surface (card, nav, sunken track, toast) filling its parent's layout slot.</summary>
        public static RoundedRect Surface(Transform parent, string name, SurfaceKind kind, float radius = Ui.CardRadius, string fill = null)
        {
            RoundedRect shape = Shape(parent, name, radius);
            ThemedSurface.Bind(shape, kind, fill);
            return shape;
        }

        /// <summary>A card: surface, border and shadow from the look. Add content (and layout) to it.</summary>
        public static RectTransform Card(Transform parent, string name, float radius = Ui.CardRadius) =>
            Surface(parent, name, SurfaceKind.Card, radius).rectTransform;

        /// <summary>A circle (or rounded chip) in one token colour, optionally ringed or glowing.</summary>
        public static RoundedRect Solid(Transform parent, string name, string fill, float radius, float alpha = 1f,
            string ring = null, float ringWidth = 0f, string glow = null)
        {
            RoundedRect shape = Shape(parent, name, radius);
            ThemedSurface themed = ThemedSurface.Bind(shape, SurfaceKind.Solid, fill, glow);
            themed.FillAlpha = alpha;
            themed.RingToken = ring;
            themed.RingWidth = ringWidth;
            themed.Refresh();
            return shape;
        }

        /// <summary>The look's background gradient (and Neon's centre glow) filling <paramref name="parent"/>.</summary>
        public static RectTransform Background(Transform parent, float glowCentreY = 0.55f)
        {
            RectTransform rt = Stretch(Rect("Background", parent));
            var gradient = rt.gameObject.AddComponent<GradientBackground>();
            gradient.raycastTarget = true; // screens block taps to what is behind them
            RoundedRect glow = Shape(rt, "CentreGlow", 0f);
            Pin(glow.rectTransform, new Vector2(0.5f, glowCentreY), Vector2.zero, Vector2.zero);
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var themed = rt.gameObject.AddComponent<ThemedGradientBackground>();
            themed.Target = gradient;
            themed.CenterGlow = glow;
            ThemedPart.Mark(glow, themed);
            themed.Refresh();
            return rt;
        }

        /// <summary>A full-screen dim layer (overlay.dim) that blocks taps.</summary>
        public static RoundedRect Dim(Transform parent)
        {
            RoundedRect dim = Solid(parent, "Dim", T.OverlayDim, 0f);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            return dim;
        }

        // --- text ------------------------------------------------------------------------------------

        public static TextMeshProUGUI Text(Transform parent, string text, TextRole role, string token = T.TextPrimary,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float size = 0f)
        {
            RectTransform rt = Rect("Text", parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            ThemedFont.Apply(label, role, size);
            label.text = text;
            label.alignment = align;
            label.textWrappingMode = role == TextRole.Body || role == TextRole.Caption ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            if (token != null) ThemedGraphic.Bind(label, token); // null: a themed parent colours it
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

        /// <summary>A single icon glyph (outline set unless <paramref name="filled"/>).</summary>
        public static TextMeshProUGUI Icon(Transform parent, string glyph, float size, string token = T.TextPrimary, bool filled = false)
        {
            RectTransform rt = Rect("Icon", parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = filled ? Fonts.IconsFilled : Fonts.Icons;
            label.fontSize = size;
            label.text = glyph;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            if (token != null) ThemedGraphic.Bind(label, token);
            Size(rt, size, size);
            return label;
        }

        /// <summary>Text with a leading icon from the outline set (the text fonts fall back to it).</summary>
        public static string WithIcon(string icon, string text) =>
            string.IsNullOrEmpty(icon) ? text : $"<size=118%><voffset=-0.14em>{icon}</voffset></size>  {text}";

        // --- buttons ---------------------------------------------------------------------------------

        /// <summary>
        /// Primary button: full width, 72 tall, radius 24, a 6 pt pressable edge in a darker shade and
        /// a soft glow below; leading filled icon. Exactly one per screen. <paramref name="breathe"/>
        /// makes it the idle call to action (Home, Game Over, Challenge Intro).
        /// </summary>
        public static Button Primary(Transform parent, string text, string icon, UnityAction onClick, bool breathe = false,
            ButtonKind kind = ButtonKind.Primary)
        {
            RectTransform root = Rect("Primary", parent);
            Size(root, height: Ui.PrimaryHeight);
            RoundedRect edge = Shape(root, "Edge", Ui.PrimaryRadius);
            Stretch(edge.rectTransform);
            edge.raycastTarget = true;
            RoundedRect face = Shape(root, "Face", Ui.PrimaryRadius);
            Stretch(face.rectTransform, 0, 0, 0, Ui.Edge);

            RectTransform content = Stretch(Rect("Content", face.transform), 20, 0, 20, 0);
            HorizontalLayoutGroup row = Row(content, Ui.PrimaryGap);
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            TextMeshProUGUI glyph = null;
            if (!string.IsNullOrEmpty(icon)) glyph = Icon(content, icon, Ui.PrimaryIcon, null, filled: true);
            TextMeshProUGUI label = Text(content, text, TextRole.ButtonPrimary, null);
            OneLine(label, 18f);

            var themed = root.gameObject.AddComponent<ThemedButton>();
            themed.Kind = kind;
            themed.Edge = edge;
            themed.Face = face;
            themed.Ink = glyph != null ? new Graphic[] { glyph, label } : new Graphic[] { label };
            Parts(themed, edge, face, glyph, label);
            themed.Refresh();

            Button button = Finish(root, edge, onClick, face.rectTransform, Ui.PressDepth);
            if (breathe) root.gameObject.AddComponent<Breathe>().Button = themed;
            return button;
        }

        /// <summary>Secondary pill: 44 tall, fully rounded, outlined, leading outline icon; hugs its text.</summary>
        public static Button Pill(Transform parent, string text, string icon, UnityAction onClick)
        {
            RectTransform root = Rect("Pill", parent);
            RoundedRect face = Shape(root, "Face", Ui.PillHeight / 2f);
            Stretch(face.rectTransform);
            face.raycastTarget = true;
            HorizontalLayoutGroup row = Row(root, 8);
            row.padding = new RectOffset((int)Ui.PillPad, (int)Ui.PillPad, 0, 0);
            row.childForceExpandWidth = false;
            IgnoreLayout(face);
            TextMeshProUGUI glyph = null;
            if (!string.IsNullOrEmpty(icon)) glyph = Icon(root, icon, Ui.PillIcon, null);
            TextMeshProUGUI label = Text(root, text, TextRole.Pill, null);
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Size(root, height: Ui.PillHeight);
            root.sizeDelta = new Vector2(root.sizeDelta.x, Ui.PillHeight);

            var themed = root.gameObject.AddComponent<ThemedButton>();
            themed.Kind = ButtonKind.Secondary;
            themed.Face = face;
            themed.Ink = glyph != null ? new Graphic[] { glyph, label } : new Graphic[] { label };
            Parts(themed, face, glyph, label);
            themed.Refresh();
            return Finish(root, face, onClick, null, 0f);
        }

        /// <summary>Centres a pill on its own line inside a column (pills hug their text).</summary>
        public static Button CenteredPill(Transform column, string text, string icon, UnityAction onClick)
        {
            RectTransform line = Rect("PillLine", column);
            Size(line, height: Ui.PillHeight);
            HorizontalLayoutGroup row = Row(line, 0);
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            return Pill(line, text, icon, onClick);
        }

        /// <summary>Icon button: 48 × 48, radius 16, on a surface.</summary>
        public static Button IconButton(Transform parent, string glyph, UnityAction onClick)
        {
            RectTransform root = Rect("IconButton", parent);
            Size(root, Ui.IconButton, Ui.IconButton);
            root.sizeDelta = new Vector2(Ui.IconButton, Ui.IconButton);
            RoundedRect face = Shape(root, "Face", Ui.IconButtonRadius);
            Stretch(face.rectTransform);
            face.raycastTarget = true;
            TextMeshProUGUI icon = Icon(face.transform, glyph, Ui.IconSize, null);
            Stretch(icon.rectTransform);

            var themed = root.gameObject.AddComponent<ThemedButton>();
            themed.Kind = ButtonKind.Icon;
            themed.Face = face;
            themed.Ink = new Graphic[] { icon };
            Parts(themed, face, icon);
            themed.Refresh();
            return Finish(root, face, onClick, null, 0f);
        }

        private static void Parts(Themed owner, params Graphic[] parts)
        {
            foreach (Graphic g in parts)
                if (g != null) ThemedPart.Mark(g, owner);
        }

        private static Button Finish(RectTransform root, Graphic hit, UnityAction onClick, RectTransform face, float depth)
        {
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // Pressable animates and dims
            button.targetGraphic = hit;
            if (onClick != null) button.onClick.AddListener(onClick);
            var press = root.gameObject.AddComponent<Pressable>();
            press.Button = button;
            press.Face = face;
            press.Depth = depth;
            return button;
        }

        public static void SetLabel(Button button, string text)
        {
            foreach (TextMeshProUGUI label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.font == Fonts.Icons || label.font == Fonts.IconsFilled) continue;
                label.text = text;
                return;
            }
        }

        // --- small pieces ----------------------------------------------------------------------------

        /// <summary>A coin: coin.fill disc with a coin.ring rim.</summary>
        public static RoundedRect Coin(Transform parent, float size)
        {
            RoundedRect coin = Solid(parent, "Coin", T.CoinFill, size / 2f, ring: T.CoinRing, ringWidth: Mathf.Max(2f, size * 0.12f));
            Size(coin.rectTransform, size, size);
            coin.rectTransform.sizeDelta = new Vector2(size, size);
            return coin;
        }

        /// <summary>Coin counter pill (40 tall, coin on the left). Returns the number label.</summary>
        public static TextMeshProUGUI CoinPill(Transform parent)
        {
            RoundedRect bg = Surface(parent, "Coins", SurfaceKind.Card, Ui.CoinPill / 2f);
            RectTransform rt = bg.rectTransform;
            HorizontalLayoutGroup row = Row(rt, 8);
            row.padding = new RectOffset(9, 16, 0, 0);
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Coin(rt, Ui.CoinIcon);
            TextMeshProUGUI label = Text(rt, "0", TextRole.Coin);
            rt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Size(rt, height: Ui.CoinPill);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Ui.CoinPill);
            return label;
        }

        /// <summary>A small solid chip (combo, price, "Challenge"): fill and ink tokens, optional icon.</summary>
        public static TextMeshProUGUI Chip(Transform parent, string text, string fill, string ink, float height = 30f, string icon = null)
        {
            RoundedRect bg = Solid(parent, "Chip", fill, height / 2f);
            RectTransform rt = bg.rectTransform;
            HorizontalLayoutGroup row = Row(rt, 6);
            row.padding = new RectOffset(12, 12, 0, 0);
            row.childForceExpandWidth = false;
            if (!string.IsNullOrEmpty(icon)) Icon(rt, icon, height * 0.55f, ink, filled: true);
            TextMeshProUGUI label = Text(rt, text, TextRole.Pill, ink, size: height * 0.48f);
            rt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Size(rt, height: height);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            return label;
        }

        /// <summary>
        /// Notification dot (10 pt, accent2) on the top-right of <paramref name="host"/>: a white ring
        /// in Pop, a soft glow in Neon. Hidden until <see cref="ShowDot"/>.
        /// </summary>
        public static RoundedRect Dot(Transform host)
        {
            RoundedRect dot = Solid(host, "Dot", T.Accent2, Ui.Dot / 2f + 2f, ring: T.DotRing, ringWidth: 2f, glow: T.Accent2Glow);
            Pin(dot.rectTransform, new Vector2(1f, 1f), new Vector2(Ui.Dot + 4f, Ui.Dot + 4f), new Vector2(3f, 3f));
            dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dot.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
            IgnoreLayout(dot);
            dot.gameObject.SetActive(false);
            return dot;
        }

        /// <summary>Shows or hides a dot; it pops in when it appears.</summary>
        public static void ShowDot(RoundedRect dot, bool visible)
        {
            if (dot == null || dot.gameObject.activeSelf == visible) return;
            dot.gameObject.SetActive(visible);
            if (visible) Tween.PopIn(dot.transform, 0.32f);
        }

        /// <summary>A round avatar with the person's initial.</summary>
        public static RoundedRect Avatar(Transform parent, string name, float size)
        {
            RoundedRect circle = Solid(parent, "Avatar", T.Accent, size / 2f, alpha: 0.2f);
            Size(circle.rectTransform, size, size);
            circle.rectTransform.sizeDelta = new Vector2(size, size);
            string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            TextMeshProUGUI label = Text(circle.transform, initial, TextRole.BigNumber, T.TextPrimary, size: size * 0.46f);
            Stretch(label.rectTransform);
            return circle;
        }

        /// <summary>Gold, silver or bronze chip with the rank.</summary>
        public static RoundedRect Medal(Transform parent, int rank, float size = 32f)
        {
            string fill = rank == 1 ? T.MedalGold : rank == 2 ? T.MedalSilver : T.MedalBronze;
            RoundedRect medal = Solid(parent, "Medal", fill, size / 2f);
            Size(medal.rectTransform, size, size);
            TextMeshProUGUI label = Text(medal.transform, rank.ToString(), TextRole.BigNumber, T.MedalInk, size: size * 0.5f);
            Stretch(label.rectTransform);
            return medal;
        }

        // --- composite controls ----------------------------------------------------------------------

        /// <summary>The floating bottom navigation card; add items with <see cref="NavItem"/>.</summary>
        public static RectTransform NavBar(Transform parent)
        {
            RoundedRect nav = Surface(parent, "Nav", SurfaceKind.Nav, Ui.NavRadius);
            HorizontalLayoutGroup row = Row(nav.rectTransform, 0);
            int pad = (int)Ui.NavPad;
            row.padding = new RectOffset(pad, pad, pad, pad);
            return nav.rectTransform;
        }

        /// <summary>One of the four equal nav columns: 22 pt outline icon over a 12 pt label.</summary>
        public static Button NavItem(Transform nav, string glyph, string text, UnityAction onClick, bool active = false)
        {
            RectTransform item = Rect("NavItem", nav);
            var hit = item.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            IgnoreTheming.Mark(hit, "invisible tap area");
            Column(item, 2, new RectOffset(0, 0, 8, 6), TextAnchor.MiddleCenter).childControlHeight = true;
            string token = active ? T.Accent : T.NavLabel;
            TextMeshProUGUI icon = Icon(item, glyph, Ui.IconSize, token);
            TextMeshProUGUI label = Text(item, text, TextRole.Caption, token);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            OneLine(label, 9f);
            Size(label.rectTransform, height: 16);
            var button = item.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.AddListener(onClick);
            var press = item.gameObject.AddComponent<Pressable>();
            press.Button = button;
            return button;
        }

        /// <summary>
        /// Segmented control: a sunken track with a surface thumb behind the selected option.
        /// Returns the setter that moves the selection (without calling <paramref name="onSelect"/>).
        /// </summary>
        public static Action<int> Segmented(Transform parent, string[] labels, Action<int> onSelect, float height = 40f)
        {
            RoundedRect track = Surface(parent, "Segmented", SurfaceKind.Sunken, height / 2f);
            Size(track.rectTransform, height: height);
            HorizontalLayoutGroup row = Row(track.rectTransform, 0);
            row.padding = new RectOffset(4, 4, 4, 4);
            var thumbs = new RoundedRect[labels.Length];
            var texts = new TextMeshProUGUI[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                RoundedRect thumb = Surface(track.transform, labels[i], SurfaceKind.Card, (height - 8f) / 2f);
                thumb.raycastTarget = true;
                TextMeshProUGUI text = Text(thumb.transform, labels[i], TextRole.Pill, T.TextSecondary);
                Stretch(text.rectTransform, 8, 0, 8, 0);
                OneLine(text, 11f);
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
                    bool on = i == selected;
                    thumbs[i].color = on ? Color.white : Color.clear; // tint: the selected thumb shows its surface
                    ThemedGraphic.Bind(texts[i], on ? T.TextPrimary : T.TextSecondary);
                }
            };
        }

        /// <summary>A settings row: outline icon, label, and a switch (accent when on).</summary>
        public static Toggle Toggle(Transform parent, string icon, string text, bool value, UnityAction<bool> onChange)
        {
            RectTransform row = Rect("Toggle", parent);
            Size(row, height: 52);
            TextMeshProUGUI glyph = Icon(row, icon, Ui.IconSize, T.TextSecondary);
            Pin(glyph.rectTransform, new Vector2(0, 0.5f), new Vector2(Ui.IconSize, Ui.IconSize), Vector2.zero);
            glyph.rectTransform.pivot = new Vector2(0, 0.5f);
            TextMeshProUGUI label = Text(row, text, TextRole.Body, T.TextPrimary, TextAlignmentOptions.Left);
            Stretch(label.rectTransform, Ui.IconSize + 12, 0, 64, 0);
            OneLine(label, 12f);

            RoundedRect track = Solid(row, "Track", T.SurfaceSunken, 14f);
            Pin(track.rectTransform, new Vector2(1, 0.5f), new Vector2(50, 28), Vector2.zero);
            track.raycastTarget = true;
            RoundedRect knob = Shape(track.transform, "Knob", 11f);
            ThemedSurface.Bind(knob, SurfaceKind.Solid, T.Surface);
            knob.rectTransform.sizeDelta = new Vector2(22, 22);
            var knobFill = knob.GetComponent<ThemedSurface>();

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = track;
            void Paint(bool on, bool animate)
            {
                var trackFill = track.GetComponent<ThemedSurface>();
                trackFill.FillToken = on ? T.Accent : T.SurfaceSunken;
                trackFill.Refresh();
                knobFill.FillToken = on ? T.AccentOn : T.TextSecondary;
                knobFill.Refresh();
                RectTransform k = knob.rectTransform;
                k.anchorMin = k.anchorMax = new Vector2(0.5f, 0.5f);
                float to = on ? 11f : -11f;
                if (!animate || Look.ReduceMotion)
                {
                    k.anchoredPosition = new Vector2(to, 0f);
                    return;
                }
                float from = k.anchoredPosition.x;
                Tween.Run(k, 0.22f, t =>
                {
                    if (k != null) k.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, Tween.EaseOutBack(t)), 0f);
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

        /// <summary>Text field on a sunken surface. <paramref name="big"/>: the large code field.</summary>
        public static TMP_InputField Input(Transform parent, string placeholder, int characterLimit,
            TMP_InputField.CharacterValidation validation = TMP_InputField.CharacterValidation.None, bool big = false)
        {
            float height = big ? 72f : 48f;
            RoundedRect bg = Surface(parent, "Input", SurfaceKind.Card, big ? Ui.PrimaryRadius : 14f);
            RectTransform root = bg.rectTransform;
            Size(root, height: height);
            bg.raycastTarget = true;
            RectTransform area = Stretch(Rect("Text Area", root), 16, 6, 16, 6);
            area.gameObject.AddComponent<RectMask2D>();

            TextRole role = big ? TextRole.Title : TextRole.Body;
            TextMeshProUGUI hint = Text(area, placeholder, role, T.TextSecondary, big ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
            ThemedGraphic.Bind(hint, T.TextSecondary, 0.7f);
            Stretch(hint.rectTransform);
            OneLine(hint, 12f);
            TextMeshProUGUI text = Text(area, "", role, T.TextPrimary, big ? TextAlignmentOptions.Center : TextAlignmentOptions.Left,
                big ? 36f : 0f);
            Stretch(text.rectTransform);
            if (big) text.characterSpacing = 30f;

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = characterLimit;
            field.characterValidation = validation;
            field.caretWidth = 2;
            field.customCaretColor = true;
            var caret = field.gameObject.AddComponent<ThemedInputCaret>();
            caret.Field = field;
            caret.Refresh();
            return field;
        }

        /// <summary>A list row card: horizontal content, 16 pt padding.</summary>
        public static RectTransform ListRow(Transform parent, float height = 64f)
        {
            RectTransform row = Card(parent, "Row");
            Size(row, height: height);
            HorizontalLayoutGroup layout = Row(row, 12);
            layout.padding = new RectOffset((int)Ui.CardPad, (int)Ui.CardPad, 8, 8);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A label that takes the row's free width.</summary>
        public static TextMeshProUGUI Grow(TextMeshProUGUI label)
        {
            Size(label.rectTransform, flexibleWidth: 1);
            return label;
        }
    }
}
