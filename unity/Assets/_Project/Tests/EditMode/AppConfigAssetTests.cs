using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace StackIt.Tests
{
    /// <summary>Checks the shipped AppConfig asset, which the Inspector saves without validation.</summary>
    public class AppConfigAssetTests
    {
        private static string Field(string name)
        {
            string asset = File.ReadAllText(Path.Combine(TestPaths.ResourcesDir(), "AppConfig.asset"));
            Match m = Regex.Match(asset, @"^\s*" + name + @":[ \t]*(.*)$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, name + " missing from AppConfig.asset");
            return m.Groups[1].Value.Trim();
        }

        [Test]
        public void ApiBaseUrlIsAnAbsoluteHttpUrl()
        {
            // A bare host ("x.up.railway.app") makes every request and the privacy/terms links fail.
            string url = Field("apiBaseUrl");
            Assert.IsTrue(Uri.TryCreate(url, UriKind.Absolute, out Uri uri), "not an absolute URL: " + url);
            Assert.That(uri.Scheme, Is.EqualTo("https").Or.EqualTo("http"), url);
            Assert.IsFalse(url.EndsWith("/"), "paths are appended with a leading slash: " + url);
        }

        [Test]
        public void BundleIdIsThePackageRegisteredOnGooglePlay()
        {
            // The Play Console app was created as game.milli.com, and a package can never change:
            // a bundle with any other id is refused on upload.
            Assert.AreEqual("game.milli.com", Field("bundleId"));
        }
    }
}
