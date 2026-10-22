using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Any texture under Assets/_Project/Art/ is imported crisp: point filtering, no compression,
    /// no mipmaps, no power-of-two rescaling.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Project/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtFolder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            // Tile atlases are also read on the CPU (battle backdrops are painted from them).
            var path = assetPath.Replace('\\', '/');
            importer.isReadable = path.Contains("/ThirdParty/") || path.Contains("/Tiles/");
        }
    }
}
