using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace StackIt.Tests
{
    /// <summary>
    /// Unity rules the C# compiler cannot catch, checked on the source text so they also fail in
    /// dotnet test / CI, not only at Play time.
    /// </summary>
    public class ScriptRulesTests
    {
        private static readonly Regex ScriptableObjectClass =
            new Regex(@"class\s+(\w+)\s*:\s*ScriptableObject\b");

        // A field (starts with an access modifier) whose initializer constructs a native Unity
        // object: these run in the MonoBehaviour constructor, where Unity throws.
        private static readonly Regex NativeObjectFieldInitializer = new Regex(
            @"^\s*(private|protected|internal|public)\b[^(=]*=\s*new\s+" +
            @"(MaterialPropertyBlock|Material|Mesh|Texture2D|RenderTexture|GameObject|ComputeBuffer)\s*\(",
            RegexOptions.Multiline);

        // TMP properties that touch the font material, which TMP only loads in Awake. Screens build
        // under an inactive root, so Awake has not run yet: only TextEffect (applied in Start) may set them.
        private static readonly Regex TmpMaterialSetter =
            new Regex(@"\.(outlineWidth|outlineColor|fontMaterial|fontSharedMaterial)\s*=[^=]");

        private static IEnumerable<string> RuntimeScripts() =>
            Directory.GetFiles(TestPaths.ScriptsDir(), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/Core/") && !f.Replace('\\', '/').Contains("/Client/"));

        [Test]
        public void ScriptableObjectsLiveInAFileNamedAfterTheClass()
        {
            // Otherwise assets lose their script reference ("The referenced script ... is missing").
            var offenders = new List<string>();
            foreach (string file in RuntimeScripts())
            foreach (Match m in ScriptableObjectClass.Matches(File.ReadAllText(file)))
            {
                if (m.Groups[1].Value != Path.GetFileNameWithoutExtension(file))
                    offenders.Add(m.Groups[1].Value + " in " + Path.GetFileName(file));
            }
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void NoNativeUnityObjectsInFieldInitializers()
        {
            var offenders = new List<string>();
            foreach (string file in RuntimeScripts())
            foreach (Match m in NativeObjectFieldInitializer.Matches(File.ReadAllText(file)))
            {
                offenders.Add(Path.GetFileName(file) + ": " + m.Value.Trim());
            }
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void TmpMaterialPropertiesAreOnlySetByTextEffect()
        {
            var offenders = new List<string>();
            foreach (string file in RuntimeScripts().Where(f => Path.GetFileName(f) != "TextEffect.cs"))
            foreach (Match m in TmpMaterialSetter.Matches(File.ReadAllText(file)))
            {
                offenders.Add(Path.GetFileName(file) + ": " + m.Value.Trim());
            }
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void TextEffectShaderVariantsShipInTheBuild()
        {
            // TMP's mobile SDF shader compiles outline and underlay as shader_feature: a player
            // build strips every keyword combination no built material uses, so TextEffect's
            // runtime keywords would silently do nothing on the phone. A material in Resources
            // with each combination keeps it.
            const string mobileSdf = "guid: fe393ace9b354375a9cb14cdbbc28be4";
            List<string> materials = Directory.GetFiles(TestPaths.ResourcesDir(), "*.mat", SearchOption.AllDirectories)
                .Select(File.ReadAllText).Where(t => t.Contains(mobileSdf)).ToList();
            foreach (string[] combo in new[] { new[] { "UNDERLAY_ON" }, new[] { "OUTLINE_ON", "UNDERLAY_ON" } })
            {
                bool shipped = materials.Any(t =>
                {
                    List<string> keywords = Regex.Matches(t, @"^\s*-\s*(\w+_ON)\s*$", RegexOptions.Multiline)
                        .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                    return keywords.OrderBy(k => k).SequenceEqual(combo);
                });
                Assert.IsTrue(shipped, "no material in Resources keeps " + string.Join(" + ", combo));
            }
        }
    }
}
