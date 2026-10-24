using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Any texture under Assets/_Project/Art/ is imported crisp: point filtering, no compression,
    /// no mipmaps, no power-of-two rescaling. Smooth UI atlases under Resources/UI/ are imported for clean scaling.
    /// The pixel UI font is imported unsmoothed (Hinted Raster).
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Project/Art/";
        /// <summary>Smooth UI art loaded at runtime (Tools/IconForge): trilinear and mipmapped, never compressed.</summary>
        public const string UiFolder = "Assets/_Project/Resources/UI/";

        /// <summary>Battle effect sprite sheets (Super Pixel Effects Gigapack), loaded at runtime by FxLibrary: crisp like the art.</summary>
        public const string FxFolder = "Assets/_Project/Resources/Fx/";
        /// <summary>Pixel card pieces (Tools/CardForge), loaded at runtime by CardArt: crisp like the art.</summary>
        public const string CardsFolder = "Assets/_Project/Resources/Cards/";

        /// <summary>The pixel UI font (monogram): rendered without anti-aliasing so its glyphs stay sharp.</summary>
        public const string PixelFontPath = "Assets/_Project/Resources/Fonts/monogram";

        void OnPreprocessAsset()
        {
            var font = assetImporter as TrueTypeFontImporter;
            if (font == null || !assetPath.Replace('\\', '/').StartsWith(PixelFontPath)) return;
            font.fontRenderingMode = FontRenderingMode.HintedRaster;
            font.fontTextureCase = FontTextureCase.Dynamic;
            font.includeFontData = true;
        }

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
            var runtimePixels = assetPath.Replace('\\', '/');
            if (runtimePixels.StartsWith(FxFolder) || runtimePixels.StartsWith(CardsFolder))
            {
                var fx = (TextureImporter)assetImporter;
                fx.textureType = TextureImporterType.Default;
                fx.filterMode = FilterMode.Point;
                fx.mipmapEnabled = false;
                fx.textureCompression = TextureImporterCompression.Uncompressed;
                fx.npotScale = TextureImporterNPOTScale.None;
                fx.wrapMode = TextureWrapMode.Clamp;
                fx.alphaIsTransparency = true;
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
