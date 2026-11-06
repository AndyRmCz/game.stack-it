using NUnit.Framework;
using StackIt.Client;
using StackIt.Services;
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
        public void BlockyIsGlassWithItsFaceOnTop()
        {
            Blocky blocky = Blocky.Create(_sheet, 64f);
            Transform glass = blocky.transform.Find("Body/Glass");
            Assert.IsNotNull(glass.GetComponent<UnityEngine.UI.RawImage>().texture, BlockyGlass.Resource);
            Assert.Less(glass.GetSiblingIndex(), blocky.transform.Find("Body/Face").GetSiblingIndex());
        }

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
            blocky.SetMood(MascotMood.Rally);
            float squint = blocky.Expression.EyeOpen;
            blocky.SetMood(MascotMood.Happy);
            Assert.Less(squint, blocky.Expression.EyeOpen);
        }

        [Test]
        public void TheCardsSlabOutlastsTheMoodThatDoesNotWearOne()
        {
            Blocky blocky = Blocky.Create(_sheet, 64f, still: true);
            blocky.SetMood(MascotMood.Smug);
            blocky.WearSlab();
            Assert.IsTrue(blocky.transform.Find("Body/Hat").gameObject.activeSelf);
        }

        [Test]
        public void AStillBlockyDrawsItsMoodRightAway()
        {
            Blocky blocky = Blocky.Create(_sheet, 64f, still: true);
            blocky.SetMood(MascotMood.Agony);
            Assert.AreEqual(FacePoses.For(MascotMood.Agony), blocky.GetComponentInChildren<BlockyFace>().Pose);
        }

        [Test]
        public void APerchedBlockySpeaksUpAndOnlyThatOne()
        {
            Blocky perched = Blocky.Perch(_sheet);
            GameObject bubble = perched.transform.Find("Bubble").gameObject;
            Assert.IsFalse(bubble.activeSelf);
            perched.React(BlockyMoment.NewBest);
            Assert.IsTrue(bubble.activeSelf);
            Assert.AreEqual(MascotMood.Cheer, BlockyMoments.Mood(BlockyMoment.NewBest));

            Blocky quiet = Blocky.Create(_sheet, 64f);
            Assert.DoesNotThrow(() => quiet.React(BlockyMoment.NewBest));
            Assert.IsNull(quiet.transform.Find("Bubble"));
        }

        [Test]
        public void TheBubbleNeverTakesATap()
        {
            Blocky blocky = Blocky.Perch(_sheet);
            Assert.IsFalse(blocky.transform.Find("Bubble").GetComponent<CanvasGroup>().blocksRaycasts);
        }

        [Test]
        public void APerchedBlockyCanBePoked()
        {
            Blocky blocky = Blocky.Perch(_sheet);
            blocky.transform.Find("Body/Front").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(FacePoses.For(MascotMood.Joy), blocky.Expression, "the first poke is a giggle");
            Assert.IsTrue(blocky.transform.Find("Bubble").gameObject.activeSelf);
        }

        private sealed class MemoryStore : IKeyValueStore
        {
            private readonly System.Collections.Generic.Dictionary<string, string> _values =
                new System.Collections.Generic.Dictionary<string, string>();
            public string GetString(string key) => _values.TryGetValue(key, out string v) ? v : null;
            public void SetString(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        [Test]
        public void VisitsCountCalendarDaysNotHours()
        {
            var save = new SaveService(new MemoryStore());
            Assert.IsNull(save.Visit(new System.DateTime(2026, 11, 4, 23, 50, 0)), "the very first visit");
            Assert.AreEqual(0, save.Visit(new System.DateTime(2026, 11, 4, 23, 59, 0)));
            Assert.AreEqual(1, save.Visit(new System.DateTime(2026, 11, 5, 0, 5, 0)), "ten minutes, but a new day");
            Assert.AreEqual(4, save.Visit(new System.DateTime(2026, 11, 9, 12, 0, 0)));
        }

        [Test]
        public void AClockSetBackCountsAsNoTimeAway()
        {
            var save = new SaveService(new MemoryStore());
            save.Visit(new System.DateTime(2026, 11, 9));
            Assert.AreEqual(0, save.Visit(new System.DateTime(2026, 11, 4)));
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
