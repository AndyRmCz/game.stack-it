using System.Xml;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class DeepLinkManifestTests
    {
        [TestCase("https://stackit.up.railway.app", "stackit.up.railway.app")]
        [TestCase("https://play.stackit.gg/", "play.stackit.gg")]
        [TestCase("http://localhost:8000", "localhost")]
        public void HostComesFromThePublicBaseUrl(string baseUrl, string host)
        {
            Assert.AreEqual(host, DeepLinkManifest.Host(baseUrl));
        }

        [Test]
        public void AndroidManifestDeclaresVerifiedAppLinkSchemeAndFileProvider()
        {
            string xml = DeepLinkManifest.AndroidLibraryManifest(
                "https://stackit.up.railway.app", "stackit", "com.unity3d.player.UnityPlayerGameActivity");

            var doc = new XmlDocument();
            doc.LoadXml(xml); // well-formed
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", "http://schemas.android.com/apk/res/android");

            XmlNode activity = doc.SelectSingleNode("//activity[@android:name='com.unity3d.player.UnityPlayerGameActivity']", ns);
            Assert.IsNotNull(activity);
            XmlNode https = activity.SelectSingleNode("intent-filter[@android:autoVerify='true']/data[@android:scheme='https']", ns);
            Assert.AreEqual("stackit.up.railway.app", https.Attributes["android:host"].Value);
            Assert.AreEqual("/c/", https.Attributes["android:pathPrefix"].Value);
            Assert.IsNotNull(activity.SelectSingleNode("intent-filter/data[@android:scheme='stackit'][@android:host='c']", ns));
            Assert.IsNotNull(doc.SelectSingleNode("//provider[@android:authorities='${applicationId}.stackit.fileprovider']", ns));
        }
    }
}
