using System;
using UnityEditor;

namespace StackIt.EditorTools
{
    /// <summary>
    /// Our drawn pictures (the app icons, Blocky's glass) import as plain 2D textures with their
    /// alpha, full size, no mipmaps, uncompressed: they're UI art, and a .meta without import
    /// settings imports as a cubemap, which Resources.Load&lt;Texture2D&gt; can't load (Blocky
    /// went white). TextureImportTests checks every .meta on disk.
    /// </summary>
    public class PlainTextures : AssetPostprocessor
    {
        private static readonly string[] Paths =
        {
            "Assets/_Project/Art/Icons/",
            "Assets/_Project/Resources/StackIt/BlockyGlass.png",
        };

        // Bumping it re-imports these pictures with the settings below.
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!Array.Exists(Paths, p => assetPath.StartsWith(p, StringComparison.Ordinal))) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
