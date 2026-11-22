#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Full-screen art is imported as a single sprite with no mipmaps. Title/UI art
// stays uncompressed so text in it is crisp; painted stage backgrounds use
// high-quality compression (they fill the screen on mobile, so memory matters).
// Runs automatically whenever Unity imports a texture in these folders.
public sealed class TitleArtPostprocessor : AssetPostprocessor
{
    private const string UiArtFolder = "Assets/Resources/UI/";
    private const string StageBackgroundFolder = "Assets/Sprites/Chapter2/Backgrounds/";

    void OnPreprocessTexture()
    {
        bool uiArt = assetPath.StartsWith(UiArtFolder, System.StringComparison.OrdinalIgnoreCase);
        bool stageBackground = assetPath.StartsWith(StageBackgroundFolder, System.StringComparison.OrdinalIgnoreCase);
        if (!uiArt && !stageBackground)
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = uiArt
            ? TextureImporterCompression.Uncompressed
            : TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = importer.DoesSourceTextureHaveAlpha();
    }
}
#endif
