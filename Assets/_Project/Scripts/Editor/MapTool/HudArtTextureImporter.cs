using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public sealed class HudArtTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/HudArt/")) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;
            importer.maxTextureSize=256;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;
        }
    }
}
