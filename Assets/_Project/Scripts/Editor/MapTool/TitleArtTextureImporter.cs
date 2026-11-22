using UnityEditor;
using UnityEngine;

namespace MyGame.Editor
{
    // UI art must retain its source aspect, including non-power-of-two sizes.
    public sealed class TitleArtTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/TitleArt/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = assetPath.EndsWith("Logo.png");
        }
    }
}
