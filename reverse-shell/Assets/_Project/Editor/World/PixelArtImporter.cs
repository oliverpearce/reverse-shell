using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Any texture under Assets/_Project/Art/ is imported crisp: point filtering, no compression,
    /// no mipmaps, no power-of-two rescaling. Smooth UI atlases under Resources/UI/ are imported for clean scaling.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Project/Art/";
        /// <summary>Smooth UI art loaded at runtime (Tools/IconForge): trilinear and mipmapped, never compressed.</summary>
        public const string UiFolder = "Assets/_Project/Resources/UI/";

        void OnPreprocessTexture()
        {
            if (assetPath.Replace('\\', '/').StartsWith(UiFolder))
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.filterMode = FilterMode.Trilinear;
                ui.mipmapEnabled = true;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.npotScale = TextureImporterNPOTScale.None;
                ui.wrapMode = TextureWrapMode.Clamp;
                ui.alphaIsTransparency = true;
                return;
            }
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
            // Character card portraits (Tools/PortraitForge) are used directly as UI sprites.
            if (path.Contains("/Portraits/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
            }
        }
    }
}
