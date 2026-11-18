using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class OfflineTests
    {
        [Test]
        public void ARequestThatGotNoAnswerIsOffline()
        {
            Assert.IsTrue(Offline.Is(0, Offline.Code));
        }

        [Test]
        public void AFailedSignInOnThePhoneIsNotOffline()
        {
            // Google's own sign-in failing has no HTTP status either, but the network may be fine:
            // "No connection" sent players checking their wifi instead of retrying.
            Assert.IsFalse(Offline.Is(0, "sign_in_failed"));
            Assert.IsFalse(Offline.Is(0, "sign_in_cancelled"));
            Assert.IsFalse(Offline.Is(0, "provider_unavailable"));
        }

        [Test]
        public void AServerAnswerIsNotOffline()
        {
            Assert.IsFalse(Offline.Is(503, "http_503"));
        }
    }
}
