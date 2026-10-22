using System.IO;
using System.Linq;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class StringsCsvTests
    {
        [Test]
        public void ParsesQuotedFieldsWithCommasQuotesAndNewlines()
        {
            const string csv = "key,en,es\n" +
                               "home.play,PLAY,JUGAR\n" +
                               "hud.passed,\"YOU PASSED @{0}!\",\"¡SUPERASTE A @{0}!\"\n" +
                               "intro.rules,\"Same blocks, same speed.\",\"Mismos bloques, \"\"misma\"\" velocidad.\"\n" +
                               "multi,\"line one\nline two\",\"uno\r\ndos\"\r\n";

            StringsCsv table = StringsCsv.Parse(csv);

            CollectionAssert.AreEqual(new[] { "en", "es" }, table.Languages);
            Assert.AreEqual("JUGAR", table.Get("es", "home.play"));
            Assert.AreEqual("¡SUPERASTE A @{0}!", table.Get("es", "hud.passed"));
            Assert.AreEqual("Same blocks, same speed.", table.Get("en", "intro.rules"));
            Assert.AreEqual("Mismos bloques, \"misma\" velocidad.", table.Get("es", "intro.rules"));
            Assert.AreEqual("line one\nline two", table.Get("en", "multi"));
            Assert.AreEqual("uno\ndos", table.Get("es", "multi"));
            Assert.IsNull(table.Get("es", "missing"));
        }

        [Test]
        public void ShippedStringTableIsCompleteInBothLanguages()
        {
            // Every key must have English and Spanish text: no hardcoded or missing UI strings.
            string path = Path.Combine(Path.GetDirectoryName(TestPaths.SimVectorsDir()), "..", "unity",
                "Assets", "_Project", "Resources", "StackIt", "strings.csv");
            StringsCsv table = StringsCsv.Parse(File.ReadAllText(path));

            Assert.Greater(table.Keys.Count(), 50);
            foreach (string key in table.Keys)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(table.Get("en", key)), "missing en: " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(table.Get("es", key)), "missing es: " + key);
                Assert.AreEqual(Placeholders(table.Get("en", key)), Placeholders(table.Get("es", key)), "placeholders differ: " + key);
            }
        }

        private static string Placeholders(string text) =>
            string.Join(",", System.Text.RegularExpressions.Regex.Matches(text, @"\{\d+\}")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v));
    }
}
