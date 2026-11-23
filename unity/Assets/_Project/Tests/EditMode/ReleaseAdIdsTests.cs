using System;
using System.Collections.Generic;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class ReleaseAdIdsTests
    {
        private const string App = "ca-app-pub-1305259028857953~1234567890";
        private const string Unit = "ca-app-pub-1305259028857953/";

        private static Func<string, string> Env(Dictionary<string, string> values) =>
            name => values.TryGetValue(name, out string value) ? value : null;

        private static Dictionary<string, string> AllSet() => new Dictionary<string, string>
        {
            ["ADMOB_ANDROID_APP_ID"] = App,
            ["ADMOB_ANDROID_REWARDED"] = Unit + "1111111111",
            ["ADMOB_ANDROID_INTERSTITIAL"] = Unit + "2222222222",
            ["ADMOB_ANDROID_BANNER"] = Unit + "3333333333",
        };

        [Test]
        public void NothingSetMeansATestBuild()
        {
            Assert.IsNull(ReleaseAdIds.Read("ANDROID", Env(new Dictionary<string, string>())));
        }

        [Test]
        public void AllFourAreReadForThePlatform()
        {
            ReleaseAdIds ids = ReleaseAdIds.Read("ANDROID", Env(AllSet()));

            Assert.AreEqual(App, ids.AppId);
            Assert.AreEqual(Unit + "1111111111", ids.Rewarded);
            Assert.AreEqual(Unit + "2222222222", ids.Interstitial);
            Assert.AreEqual(Unit + "3333333333", ids.Banner);
        }

        [Test]
        public void ValuesAreTrimmed()
        {
            Dictionary<string, string> values = AllSet();
            values["ADMOB_ANDROID_BANNER"] = "  " + Unit + "3333333333\r\n";

            Assert.AreEqual(Unit + "3333333333", ReleaseAdIds.Read("ANDROID", Env(values)).Banner);
        }

        [Test]
        public void AMissingUnitFailsAndNamesIt()
        {
            Dictionary<string, string> values = AllSet();
            values.Remove("ADMOB_ANDROID_INTERSTITIAL");

            var error = Assert.Throws<InvalidOperationException>(() => ReleaseAdIds.Read("ANDROID", Env(values)));
            StringAssert.Contains("ADMOB_ANDROID_INTERSTITIAL", error.Message);
        }

        [Test]
        public void GooglesTestIdsAreRefused()
        {
            Dictionary<string, string> values = AllSet();
            values["ADMOB_ANDROID_REWARDED"] = "ca-app-pub-3940256099942544/5224354917";

            var error = Assert.Throws<InvalidOperationException>(() => ReleaseAdIds.Read("ANDROID", Env(values)));
            StringAssert.Contains("ADMOB_ANDROID_REWARDED", error.Message);
        }

        [TestCase("ADMOB_ANDROID_APP_ID", Unit + "1111111111")] // a unit pasted as the app ID
        [TestCase("ADMOB_ANDROID_BANNER", App)] // the app ID pasted as a unit
        [TestCase("ADMOB_ANDROID_BANNER", "pub-1305259028857953")]
        public void APasteInTheWrongPlaceIsRefused(string name, string value)
        {
            Dictionary<string, string> values = AllSet();
            values[name] = value;

            var error = Assert.Throws<InvalidOperationException>(() => ReleaseAdIds.Read("ANDROID", Env(values)));
            StringAssert.Contains(name, error.Message);
        }

        [Test]
        public void EachPlatformReadsOnlyItsOwnNames()
        {
            Assert.IsNull(ReleaseAdIds.Read("IOS", Env(AllSet())));
        }

        private const string TestApp = "ca-app-pub-3940256099942544~1458002511";
        private const string TestUnit = "ca-app-pub-3940256099942544/1712485313";

        [Test]
        public void IdsFromOneAccountPass()
        {
            Assert.DoesNotThrow(() => ReleaseAdIds.CheckSameAccount("IOS", App, Unit + "1111111111", Unit + "2222222222"));
            Assert.DoesNotThrow(() => ReleaseAdIds.CheckSameAccount("IOS", TestApp, TestUnit));
        }

        // A cloud build without the ADMOB_IOS_* variables kept Google's test App ID next to the
        // real units already in AdsConfig: AdMob refused every request, and TestFlight showed no ads.
        [Test]
        public void RealUnitsWithGooglesTestAppIdFailAndSayWhatToSet()
        {
            var error = Assert.Throws<InvalidOperationException>(
                () => ReleaseAdIds.CheckSameAccount("IOS", TestApp, TestUnit, Unit + "1111111111"));
            StringAssert.Contains("ADMOB_IOS_", error.Message);
            StringAssert.Contains(Unit + "1111111111", error.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("pub-1305259028857953")]
        public void AnAppIdWithoutAnAccountFails(string appId)
        {
            Assert.Throws<InvalidOperationException>(() => ReleaseAdIds.CheckSameAccount("IOS", appId, Unit + "1111111111"));
        }
    }
}
