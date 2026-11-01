using NUnit.Framework;
using StackIt.UI;
using UnityEngine;

namespace StackIt.Tests
{
    public class IsoTowerTests
    {
        private RectTransform _root;

        [SetUp]
        public void SetUp() => _root = UiKit.Rect("Root", null);

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root.gameObject);

        private IsoTower Tower(int blocks, float width, float height)
        {
            RectTransform rt = UiKit.Rect("Tower", _root);
            rt.sizeDelta = new Vector2(width, height);
            var tower = rt.gameObject.AddComponent<IsoTower>();
            var faces = new IsoTower.BlockFaces[blocks];
            tower.SetBlocks(faces);
            return tower;
        }

        [Test]
        public void TheTopFaceRisesOneBlockPerBlock()
        {
            // Where the challenge card stands Blocky: on the top block's top face.
            float three = Tower(3, 240, 600).TopFaceCentre().y;
            float four = Tower(4, 240, 600).TopFaceCentre().y;
            Assert.Greater(four, three);
            Assert.AreEqual(0f, Tower(4, 240, 600).TopFaceCentre().x, 0.001f, "centred");
        }

        [Test]
        public void TheTopFaceStaysInsideAShortRect()
        {
            // A rect too short for the tower scales the blocks down, top face included.
            IsoTower tower = Tower(20, 240, 300);
            Rect r = tower.rectTransform.rect;
            Assert.Less(tower.TopFaceCentre().y, r.yMax);
        }
    }
}
