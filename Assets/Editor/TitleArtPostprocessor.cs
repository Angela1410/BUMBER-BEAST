#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// full screen art is imported as one sprite with no mipmaps. title/UI art stays uncompressed
// so the text in it stays sharp. painted stage backgrounds use high quality compression
// (they fill the whole phone screen, so memory matters). runs by itself whenever Unity
// imports a texture in these folders
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
