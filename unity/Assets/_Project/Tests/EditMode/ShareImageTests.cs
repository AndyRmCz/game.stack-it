using System;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class ShareImageTests
    {
        private static readonly DateTime At = new DateTime(2026, 10, 31, 21, 5, 9, DateTimeKind.Utc);

        [Test]
        public void TheEndCardIsAPngNamedAfterTheCode()
        {
            Assert.AreEqual("milli-K7Q2XZ-20261031-210509.png", ShareImage.FileName("K7Q2XZ", At));
        }

        [Test]
        public void EveryShareGetsAFreshNameSoAppsDontShowACachedCard()
        {
            Assert.AreNotEqual(ShareImage.FileName("K7Q2XZ", At), ShareImage.FileName("K7Q2XZ", At.AddSeconds(1)));
        }

        [Test]
        public void ANameStaysFileSafeWhateverTheCode()
        {
            string name = ShareImage.FileName("../a b?", At);
            StringAssert.IsMatch("^milli-[A-Za-z0-9]*-[0-9]{8}-[0-9]{6}\\.png$", name);
            StringAssert.StartsWith(ShareImage.Prefix, ShareImage.FileName(null, At));
        }

        [Test]
        public void OnlyOurOwnCardsMatchTheCleanUpPattern()
        {
            Assert.AreEqual("milli-*.png", ShareImage.Pattern);
        }
    }
}
