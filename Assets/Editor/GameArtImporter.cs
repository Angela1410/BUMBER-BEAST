using System.IO;
using UnityEditor;
using UnityEngine;

// import settings for the painted art in Assets/Resources/Art that GameArt loads in the game
// (barrier states, launcher, Terrathorn and its boulder). runs on every import so the files
// always come in as clean, compressed sprites with no mipmaps
public class GameArtImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Art/";

    void OnPreprocessTexture()
    {
        if (assetPath == BumperBeastBuild.IconPath)
        {
            // the app icon stays lossless, the build makes all the icon sizes from it
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

    // Terrathorn's 1024 x 836 picture covers the same 1.63 x 1.33 units as the old sprite,
    // so the boss keeps its size and collider. the rest are placed by code (any value works)
    private static float PixelsPerUnit(string name)
    {
        return name == "Boss_Terrathorn" ? 1024f / 1.63f : 100f;
    }
}
