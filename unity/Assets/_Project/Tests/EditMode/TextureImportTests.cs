using System.IO;
using System.Linq;
using NUnit.Framework;

namespace StackIt.Tests
{
    /// <summary>
    /// Our pictures are plain 2D textures. A .meta without import settings (only a guid, as a
    /// script writes it) imports as a cubemap: Resources.Load&lt;Texture2D&gt; then returns null and
    /// a RawImage draws a white square, which is how Blocky's glass went white.
    /// </summary>
    public class TextureImportTests
    {
        [Test]
        public void EveryPictureImportsAsA2DTexture()
        {
            string project = Directory.GetParent(TestPaths.ScriptsDir()).FullName;
            string[] offenders = Directory.GetFiles(project, "*.png", SearchOption.AllDirectories)
                .Where(png => !File.ReadAllText(png + ".meta").Contains("textureShape: 1"))
                .Select(png => png.Substring(project.Length + 1).Replace('\\', '/'))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }
    }
}
