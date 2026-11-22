using System.IO;
using UnityEditor;
using UnityEngine;

// Import settings for the painted stage art in Assets/Resources/Art, which GameArt loads at
// runtime (barrier states, launcher, Terrathorn and its boulder). Applied on every import so
// the files always come in as clean, compressed, un-mipmapped sprites.
public class GameArtImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Art/";

    void OnPreprocessTexture()
    {
        if (assetPath == BumperBeastBuild.IconPath)
        {
            // The app icon: kept lossless, the build scales it to every icon size.
            TextureImporter icon = (TextureImporter)assetImporter;
            icon.textureType = TextureImporterType.Default;
            icon.npotScale = TextureImporterNPOTScale.None;
            icon.mipmapEnabled = false;
            icon.alphaIsTransparency = true;
            icon.textureCompression = TextureImporterCompression.Uncompressed;
            icon.maxTextureSize = 1024;
            return;
        }

        if (!assetPath.StartsWith(Folder))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit(Path.GetFileNameWithoutExtension(assetPath));
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
    }

    // Terrathorn's 1024 x 836 canvas covers the same 1.63 x 1.33 units as the sprite it replaces,
    // so the boss keeps its size and collider. The rest are laid out by code (any value works).
    private static float PixelsPerUnit(string name)
    {
        return name == "Boss_Terrathorn" ? 1024f / 1.63f : 100f;
    }
}
