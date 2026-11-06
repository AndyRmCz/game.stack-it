using StackIt.Client;
using StackIt.Client.Theming;
using StackIt.Game;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>
    /// What ties the 3D Blocky to the Home screen: a tap area and a speech bubble that follow it
    /// around the screen, kept where the bubble can be read. Under the rest of the screen, so
    /// Play always wins a tap.
    /// </summary>
    public sealed class HomeBlocky : MonoBehaviour
    {
        private const float Width = 96f, Height = 112f;
        private const float MarginTop = 190f, MarginBottom = 230f, MarginSide = 56f; // clear of the logo and Play

        private static int _turn;

        private readonly BlockyPokes _pokes = new BlockyPokes();
        private Mascot _mascot;
        private RectTransform _area;
        private SpeechBubble _bubble;

        public static HomeBlocky Create(RectTransform parent, Mascot mascot)
        {
            RoundedRect hit = UiKit.Shape(parent, "Blocky", 0f);
            hit.Fill = Color.clear;
            hit.raycastTarget = true;
            IgnoreTheming.Mark(hit, "invisible tap area over the 3D Blocky");
            RectTransform area = hit.rectTransform;
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(Width, Height);

            HomeBlocky home = area.gameObject.AddComponent<HomeBlocky>();
            home._mascot = mascot;
            home._area = area;
            home._bubble = new SpeechBubble(area, 0.7f);
            var tap = area.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(home.Poke);
            return home;
        }

        private void LateUpdate()
        {
            Camera view = _mascot.ViewCamera;
            if (view == null) return;
            Vector3 screen = view.WorldToScreenPoint(_mascot.Centre);
            var space = (RectTransform)_area.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out Vector2 local)) return;
            Rect r = space.rect;
            local.x = Mathf.Clamp(local.x, r.xMin + MarginSide, r.xMax - MarginSide);
            local.y = Mathf.Clamp(local.y, r.yMin + MarginBottom, Mathf.Max(r.yMin + MarginBottom, r.yMax - MarginTop));
            _area.anchoredPosition = local - (Vector2)r.center;
        }

        private void Poke() => React(_pokes.Poke(Time.unscaledTime));

        /// <summary>The moment's face on the 3D Blocky and one of its lines, then whatever comes next.</summary>
        public void React(BlockyMoment moment)
        {
            _mascot.React(BlockyMoments.Mood(moment));
            BlockyMoment? next = BlockyMoments.Followup(moment);
            _bubble.Say(L.T(BlockyMoments.LineKey(moment, _turn++)), () =>
            {
                if (next.HasValue && isActiveAndEnabled) React(next.Value);
            });
        }
    }
}
