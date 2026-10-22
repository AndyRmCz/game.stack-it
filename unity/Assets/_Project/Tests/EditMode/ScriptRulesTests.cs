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
    }
}
