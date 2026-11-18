using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class AppIconLayersTests
    {
        private static readonly AppIconLayers<string> Icons = new AppIconLayers<string>("icon", "night", "blocky");

        [Test]
        public void AnAdaptiveSlotGetsTheNightBehindBlocky()
        {
            // Android's adaptive icon: layer 0 is the background, layer 1 the foreground.
            CollectionAssert.AreEqual(new[] { "night", "blocky" }, Icons.For(2));
        }

        [Test]
        public void AOneLayerSlotGetsTheWholeIcon()
        {
            // Android's round and legacy icons, which the launcher shows as they are.
            CollectionAssert.AreEqual(new[] { "icon" }, Icons.For(1));
        }

        [Test]
        public void WithoutTheLayersAnAdaptiveSlotIsLeftEmptyRatherThanCroppingTheWholeIcon()
        {
            var noLayers = new AppIconLayers<string>("icon", null, null);

            CollectionAssert.IsEmpty(noLayers.For(2));
            CollectionAssert.AreEqual(new[] { "icon" }, noLayers.For(1));
        }
    }
}
