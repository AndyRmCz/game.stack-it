using System.Linq;
using NUnit.Framework;
using StackIt.Client.Music;

namespace StackIt.Tests
{
    public class VoicePoolTests
    {
        [Test]
        public void AFinishedVoiceIsReusedBeforeAnyNoteIsCut()
        {
            var pool = new VoicePool(3);
            int pad = pool.Take(0, 4.0);   // a long pad
            int hit = pool.Take(0, 0.2);   // a short drum hit
            int other = pool.Take(0, 3.0);
            Assert.AreEqual(3, new[] { pad, hit, other }.Distinct().Count());

            // At 1 s only the drum hit has finished: its voice is the one taken, the pad keeps ringing.
            Assert.AreEqual(hit, pool.Take(1.0, 1.5));
        }

        [Test]
        public void WhenEveryVoiceIsBusyTheOneClosestToFinishingIsCut()
        {
            var pool = new VoicePool(2);
            int longer = pool.Take(0, 4.0);
            int sooner = pool.Take(0, 2.0);
            Assert.AreEqual(sooner, pool.Take(1.0, 3.0));
            Assert.AreNotEqual(longer, sooner);
            Assert.AreEqual(3.0, pool.EndOf(sooner));
        }
    }
}
