using UnityEditor;

namespace PointOfOrigin.EditorTools
{
    /// <summary>
    /// The Blender-rendered PNGs under Resources/Sprites are tiny pixel sprites:
    /// keep them crisp (point filter), uncompressed and transparent.
    /// </summary>
    public class SpriteImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Sprites/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
