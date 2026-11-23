using System;
using System.Text.RegularExpressions;

namespace StackIt.Client
{
    /// <summary>
    /// The real AdMob IDs of a release build, read from the build machine's environment
    /// (ADMOB_ANDROID_* or ADMOB_IOS_*), so they never sit in the repo. None set is a test build,
    /// which keeps Google's test IDs. Some set, a test ID, or an ID pasted in the wrong place fails,
    /// as a release that shows test ads, or none, earns nothing.
    /// </summary>
    public sealed class ReleaseAdIds
    {
        private const string GoogleTestPublisher = "3940256099942544";
        private static readonly Regex AppIdShape = new Regex(@"^ca-app-pub-\d{16}~\d{10}$");
        private static readonly Regex UnitShape = new Regex(@"^ca-app-pub-\d{16}/\d{10}$");

        public string AppId { get; private set; }
        public string Rewarded { get; private set; }
        public string Interstitial { get; private set; }
        public string Banner { get; private set; }

        /// <param name="platform">"ANDROID" or "IOS", as in ADMOB_ANDROID_APP_ID.</param>
        public static ReleaseAdIds Read(string platform, Func<string, string> env)
        {
            string prefix = "ADMOB_" + platform + "_";
            string[] names = { prefix + "APP_ID", prefix + "REWARDED", prefix + "INTERSTITIAL", prefix + "BANNER" };
            if (Array.TrueForAll(names, n => string.IsNullOrWhiteSpace(env(n)))) return null;

            return new ReleaseAdIds
            {
                AppId = Checked(names[0], env, AppIdShape),
                Rewarded = Checked(names[1], env, UnitShape),
                Interstitial = Checked(names[2], env, UnitShape),
                Banner = Checked(names[3], env, UnitShape),
            };
        }

        /// <summary>
        /// Fails unless the App ID and every ad unit come from one AdMob account: AdMob refuses every
        /// request from a unit of another account, so that build shows no ads at all.
        /// </summary>
        /// <param name="platform">"ANDROID" or "IOS", for the variables the message names.</param>
        public static void CheckSameAccount(string platform, string appId, params string[] units)
        {
            string account = Account(appId);
            if (account == null) throw new InvalidOperationException("The " + platform + " AdMob App ID '" + appId + "' doesn't look like ca-app-pub-…~…");
            foreach (string unit in units)
            {
                if (Account(unit) == account) continue;
                throw new InvalidOperationException(
                    "The ad unit " + unit + " is from another AdMob account than the App ID " + appId +
                    ", so AdMob would refuse every ad. Set all four ADMOB_" + platform + "_* variables on the build, " +
                    "or put Google's test IDs back in AdsConfig.");
            }
        }

        /// <summary>The 16-digit publisher number in ca-app-pub-…~… or ca-app-pub-…/…, or null.</summary>
        private static string Account(string id)
        {
            const string prefix = "ca-app-pub-";
            if (id == null || !id.StartsWith(prefix, StringComparison.Ordinal)) return null;
            int end = id.IndexOfAny(new[] { '~', '/' }, prefix.Length);
            return end < 0 ? null : id.Substring(prefix.Length, end - prefix.Length);
        }

        private static string Checked(string name, Func<string, string> env, Regex shape)
        {
            string value = env(name)?.Trim();
            if (string.IsNullOrEmpty(value)) throw new InvalidOperationException(name + " is not set");
            if (value.Contains(GoogleTestPublisher)) throw new InvalidOperationException(name + " is one of Google's test IDs");
            if (!shape.IsMatch(value)) throw new InvalidOperationException(name + " doesn't look like " + (shape == AppIdShape ? "an app ID (ca-app-pub-…~…)" : "an ad unit ID (ca-app-pub-…/…)"));
            return value;
        }
    }
}
