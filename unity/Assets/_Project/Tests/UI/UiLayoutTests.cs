using NUnit.Framework;
using StackIt.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StackIt.Tests
{
    /// <summary>
    /// UiKit layout behaviour, run through uGUI's real layout pass (Unity only: these need the
    /// engine, so they live outside EditMode, which also runs under plain .NET).
    /// </summary>
    public class UiLayoutTests
    {
        private RectTransform _root;

        [SetUp]
        public void SetUp()
        {
            _root = UiKit.Rect("Root", null);
            // TMP only lays text out under a canvas; world space keeps the size set here.
            _root.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _root.sizeDelta = new Vector2(1000, 2000);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root.gameObject);

        [Test]
        public void FixedHeightRowInAColumnKeepsItsHeight()
        {
            // The daily gift's day strip: a Row (children force-expanded) used to report itself as
            // flexible, so the column stretched it to fill the screen.
            UiKit.Column(_root, 0);
            RectTransform row = UiKit.Rect("Row", _root);
            UiKit.Size(row, height: 230);
            UiKit.Row(row, 12);
            UiKit.Rect("Cell", row);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            Assert.AreEqual(230f, row.rect.height, 0.01f);
        }

        [Test]
        public void ScrollListFillsAParentWithoutALayoutGroup()
        {
            // Settings: the list used to keep a new RectTransform's default 100 x 100 size.
            RectTransform content = UiKit.ScrollList(_root, 20);
            var viewport = (RectTransform)content.parent;

            Assert.AreEqual(1000f, viewport.rect.width, 0.01f);
            Assert.AreEqual(2000f, viewport.rect.height, 0.01f);
        }

        [Test]
        public void DoubleWidthTileLinesUpWithTheRowAbove()
        {
            // Daily gift grid: days 1-4 above days 5, 6 and a double-width day 7.
            const float gap = 24f;
            UiKit.Column(_root, gap);
            RectTransform[] top = Cells(4, gap);
            RectTransform[] bottom = Cells(3, gap);
            UiKit.Size(bottom[2], width: gap, flexibleWidth: 2);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            Assert.AreEqual(top[0].rect.width, bottom[0].rect.width, 0.01f);
            Assert.AreEqual(top[1].anchoredPosition.x, bottom[1].anchoredPosition.x, 0.01f);
            Assert.AreEqual(Left(top[2]), Left(bottom[2]), 0.01f);
            Assert.AreEqual(Right(top[3]), Right(bottom[2]), 0.01f);
        }

        [Test]
        public void IconButtonShowsItsGlyph()
        {
            // The header's back button rendered empty: the glyph was wider than the padded label
            // and the ellipsis overflow truncated it away.
            Button button = UiKit.IconButton(_root, Icons.Back, () => { });
            TMPro.TextMeshProUGUI label = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            label.ForceMeshUpdate(true);

            Assert.AreSame(Fonts.Icons, label.font, "icon font asset missing (run Stack IT ▸ Setup Project)");
            Assert.IsFalse(label.isTextTruncated);
            Assert.AreEqual(1, label.textInfo.characterCount);
            Assert.IsTrue(label.textInfo.characterInfo[0].isVisible);
        }

        private RectTransform[] Cells(int count, float gap)
        {
            RectTransform row = UiKit.Rect("Row", _root);
            UiKit.Size(row, height: 100);
            UiKit.Row(row, gap);
            var cells = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                cells[i] = UiKit.Rect("Cell" + i, row);
                // Text of different widths must not skew the shares.
                UiKit.Size(cells[i], width: 0, flexibleWidth: 1);
            }
            return cells;
        }

        private static float Left(RectTransform rt) => rt.anchoredPosition.x - rt.rect.width * rt.pivot.x;
        private static float Right(RectTransform rt) => rt.anchoredPosition.x + rt.rect.width * (1 - rt.pivot.x);
    }
}
