using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class BubbleSizeTests
    {
        private static readonly BubbleSize Bubble = new BubbleSize(maxWidth: 216f, padX: 14f, padY: 9f, fontSize: 14f);

        [Test]
        public void AShortLineHugsItsText()
        {
            (float width, float height) = Bubble.Fit("Hi!", 30f, 18f);

            Assert.AreEqual(30f + 1f + 28f, width, 0.01f);
            Assert.AreEqual(18f + 18f, height, 0.01f);
        }

        [Test]
        public void ALongLineWrapsAtTheMaximumWidth()
        {
            (float width, _) = Bubble.Fit("A much longer line than fits", 400f, 54f);

            Assert.AreEqual(216f, width, 0.01f);
        }

        [Test]
        public void AnUnmeasuredLineGetsTheFullWidthNotAOneLetterColumn()
        {
            // TextMeshPro measures 0 x 0 before it has woken up: the bubble shrank to its padding
            // and the words stacked one letter a line.
            (float width, float height) = Bubble.Fit("Nice stack! Keep going", 0f, 0f);

            Assert.AreEqual(216f, width, 0.01f);
            Assert.Greater(height, 14f + 18f, "room for at least one line");
        }

        [Test]
        public void AnUnmeasuredLongLineGetsRoomForItsLines()
        {
            string line = new string('x', 80); // ~ 80 x 7.7 px = 3+ lines of 188 px
            (_, float oneLine) = Bubble.Fit("Hi", 0f, 0f);
            (_, float several) = Bubble.Fit(line, 0f, 0f);

            Assert.Greater(several, 2.5f * (oneLine - 18f) + 18f);
        }
    }
}
