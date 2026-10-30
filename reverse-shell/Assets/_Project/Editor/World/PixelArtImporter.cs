using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Any texture under Assets/_Project/Art/ is imported crisp: point filtering, no compression,
    /// no mipmaps, no power-of-two rescaling. Smooth UI atlases under Resources/UI/ are imported for clean scaling.
    /// The pixel UI font is imported unsmoothed (Hinted Raster). The nature kit under Resources/NatureKit/ gets crisp
    /// tiling textures and plain models, and so do the single props under Resources/Props/.
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

        /// <summary>The UI font (monogram): hinted smooth: anti-aliased edges, but the glyphs snap to the pixel grid so
        /// they stay sharp rather than soft.</summary>
        public const string PixelFontPath = "Assets/_Project/Resources/Fonts/monogram";

        /// <summary>The Stylized Nature MegaKit (Track C), loaded at runtime by NatureKit: crisp, tiling textures (with
        /// mipmaps, so far-off leaves don't shimmer) and plain models (no animation, cameras, lights or colliders).</summary>
        public const string NatureKitFolder = "Assets/_Project/Resources/NatureKit/";

        /// <summary>Track C's per-world ruin atlases (Tools/TileForge/ruins_atlas.py --theme), loaded by name by a WorldTheme.</summary>
        public const string TilesFolder = "Assets/_Project/Resources/Tiles/";

        /// <summary>Single props loaded at runtime (the office's Retro PC, OfficeKit): imported like the nature kit, but
        /// readable, so OfficeKit can find the monitor glass's own triangles.</summary>
        public const string PropsFolder = "Assets/_Project/Resources/Props/";

        static bool IsKit(string path)
        {
            path = path.Replace('\\', '/');
            return path.StartsWith(NatureKitFolder) || path.StartsWith(PropsFolder);
        }

        void OnPreprocessModel()
        {
            if (!IsKit(assetPath)) return;
            var model = (ModelImporter)assetImporter;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importCameras = false;
            model.importLights = false;
            model.importBlendShapes = false;
            model.importVisibility = false;
            model.addCollider = false;
            model.isReadable = assetPath.Replace('\\', '/').StartsWith(PropsFolder);
            // The kit's material names (Bark_NormalTree, Leaves_Pine, ...) pick NatureKit's own toon materials.
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        }

        void OnPreprocessAsset()
        {
            var font = assetImporter as TrueTypeFontImporter;
            if (font == null || !assetPath.Replace('\\', '/').StartsWith(PixelFontPath)) return;
            font.fontRenderingMode = FontRenderingMode.HintedSmooth;
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
            if (assetPath.Replace('\\', '/').StartsWith(TilesFolder))
            {
                // Track C's per-world ruin atlases: crisp like the forest one, and readable (battle backdrops paint from them).
                var tiles = (TextureImporter)assetImporter;
                tiles.textureType = TextureImporterType.Default;
                tiles.filterMode = FilterMode.Point;
                tiles.mipmapEnabled = false;
                tiles.textureCompression = TextureImporterCompression.Uncompressed;
                tiles.npotScale = TextureImporterNPOTScale.None;
                tiles.wrapMode = TextureWrapMode.Clamp;
                tiles.alphaIsTransparency = true;
                tiles.isReadable = true;
                return;
            }
            if (IsKit(assetPath))
            {
                var kit = (TextureImporter)assetImporter;
                kit.textureType = TextureImporterType.Default;
                kit.filterMode = FilterMode.Point;
                kit.mipmapEnabled = true;
                kit.textureCompression = TextureImporterCompression.Uncompressed;
                kit.wrapMode = TextureWrapMode.Repeat;
                kit.alphaIsTransparency = true;
                kit.maxTextureSize = 512;
                // Leaf cards keep their cut-out coverage in the smaller mipmaps (no thinning or blobbing far away).
                kit.mipMapsPreserveCoverage = true;
                kit.alphaTestReferenceValue = 0.5f;
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
