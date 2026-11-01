using NUnit.Framework;
using StackIt.Client;
using StackIt.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StackIt.Tests
{
    public class BlockyTests
    {
        private RectTransform _sheet;

        [SetUp]
        public void SetUp() => _sheet = UiKit.Rect("Sheet", null);

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_sheet.gameObject);

        [Test]
        public void OnlyACheerWearsTheGoldSlab()
        {
            Blocky blocky = Blocky.Perch(_sheet);
            GameObject hat = blocky.transform.Find("Body/Hat").gameObject;
            blocky.SetMood(MascotMood.Cheer);
            Assert.IsTrue(hat.activeSelf);
            blocky.SetMood(MascotMood.Rally);
            Assert.IsFalse(hat.activeSelf);
            blocky.SetMood(MascotMood.Happy);
            Assert.IsFalse(hat.activeSelf);
        }

        [Test]
        public void ARallySquintsAndAHappyRunOpensUp()
        {
            Blocky blocky = Blocky.Create(_sheet, 64f);
            var eye = (RectTransform)blocky.transform.Find("Body/Eye");
            blocky.SetMood(MascotMood.Rally);
            float squint = eye.sizeDelta.y;
            blocky.SetMood(MascotMood.Happy);
            Assert.Less(squint, eye.sizeDelta.y);
        }

        [Test]
        public void ThePerchSitsOnTheSheetsTopEdgeOutsideItsLayout()
        {
            Blocky blocky = Blocky.Perch(_sheet);
            var root = (RectTransform)blocky.transform;
            Assert.AreEqual(1f, root.anchorMin.y);
            Assert.AreEqual(0f, root.pivot.y);
            Assert.IsTrue(root.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout);
        }

        [Test]
        public void PerchingLeavesTheSheetsColoursAlone()
        {
            Blocky blocky = Blocky.Perch(_sheet);
            blocky.SetMood(MascotMood.Cheer);
            Assert.AreEqual(1, _sheet.childCount, "only Blocky is added: no tint behind the content");
        }

        [Test]
        public void AStillBlockyStaysFullSizeForTheCard()
        {
            // The challenge card is one rendered frame: a pop-in would catch Blocky half grown.
            Blocky blocky = Blocky.Create(_sheet, 64f, still: true);
            blocky.SetMood(MascotMood.Cheer);
            Assert.AreEqual(Vector3.one, blocky.transform.localScale);
        }
    }
}
