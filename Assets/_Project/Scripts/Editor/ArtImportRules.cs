using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Applies the project's HD painted art settings the first time an image is imported
    /// into Assets/_Project/Art, so new art drops in with the right scale and filtering.
    /// Settings changed by hand afterwards are kept (rules run only on first import).
    ///
    /// Rules:
    /// - Sprite, 256 pixels per unit, bilinear, high quality compression, no mipmaps.
    /// - Art/Environment/Tiles: pivot at the center (isometric ground tiles, 256x128 px).
    /// - Art/Characters and Art/Environment: pivot at the bottom center (feet / trunk base).
    /// - File names ending in "_Sheet" import as Multiple, ready for slicing in the Sprite Editor.
    /// - Art/UI, Art/Placeholder and Art/Flare are left alone (UI has its own rules, placeholders are generated, the Flare
    ///   atlases are set up by the scene builder).
    /// </summary>
    public class ArtImportRules : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";
        public const int PixelsPerUnit = 256;

        private void OnPreprocessTexture()
        {
            if (!assetImporter.importSettingsMissing) return;
            if (!assetPath.StartsWith(ArtRoot)) return;
            // UI and placeholders have their own settings; the Flare atlases are set up by the scene builder (FlareAtlas),
            // so they are not first imported as sprites and then imported again.
            if (assetPath.StartsWith(ArtRoot + "UI/") || assetPath.StartsWith(ArtRoot + "Placeholder/") || assetPath.StartsWith(ArtRoot + "Flare/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            bool isSheet = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_Sheet");
            importer.spriteImportMode = isSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = assetPath.StartsWith(ArtRoot + "Environment/Tiles/")
                ? (int)SpriteAlignment.Center
                : (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
        }
    }
}
