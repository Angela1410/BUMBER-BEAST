using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// saves the PLACEHOLDER sprites (drawn by PlaceholderPainter) to Assets/Resources/Placeholders,
// where the game loads them by name (PlaceholderArt). files that already exist are never overwritten,
// so to swap in real art you just save a PNG with the same file name
public static class PlaceholderArtGenerator
{
    public const string Folder = "Assets/Resources/Placeholders";
    private const string LegacySparkMitePath = "Assets/Sprites/Placeholders/Placeholder_SparkMite.png";
    private const string LegacyFolder = "Assets/Sprites/Placeholders";
    public const string SparkMite = "Enemy_SparkMite";
    private const string DustScoutPrefab = "Assets/Prefab/Enemy_DustScout.prefab";
    private const string ChomaPrefab = "Assets/Prefab/Choma.prefab";

    public static string AssetPath(string name)
    {
        return Folder + "/" + name + ".png";
    }

    // makes sure every placeholder exists and returns them all by name
    public static Dictionary<string, Sprite> EnsureAll(List<string> problems)
    {
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources", "Placeholders");

        MoveLegacySparkMite(problems);

        float enemySize = EnemyWorldSize();
        float beastSize = BeastWorldSize();
        List<string> names = new List<string>();
        names.AddRange(PlaceholderPainter.EnemyNames);
        names.AddRange(PlaceholderPainter.BossNames);
        names.AddRange(PlaceholderPainter.BeastNames);

        foreach (string name in names)
        {
            bool boss = name.StartsWith("Boss_");
            bool beast = name.StartsWith("Beast_");
            int size = boss ? 384 : 256;
            float worldSize = beast ? beastSize : enemySize * (IsTough(name) ? 1.15f : 1f);
            Sprite sprite = EnsureSprite(name, size, worldSize, problems);
            if (sprite != null)
                sprites[name] = sprite;
        }
        FitBeastFrames(beastSize, problems);
        return sprites;
    }

    // the Beasts' idle animation frames (Placeholders/Frames/Beast_<name>_Idle_NN.png) get the same
    // size on screen as the Beast's idle picture: pixels per unit from the width
    private static void FitBeastFrames(float beastSize, List<string> problems)
    {
        string folder = Folder + "/Frames";
        if (!Directory.Exists(folder))
            return;
        foreach (string file in Directory.GetFiles(folder, "Beast_*_Idle_*.png"))
        {
            string path = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;
            if (!TryReadPngSize(path, out int width, out int height))
            {
                problems.Add(path + " is not a readable PNG; its size was left as it is.");
                continue;
            }
            float wanted = width / beastSize;
            int largest = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                Mathf.Abs(importer.spritePixelsPerUnit - wanted) > wanted * 0.005f ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                importer.maxTextureSize < largest;
            if (!changed)
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = wanted;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, largest);
            importer.SaveAndReimport();
        }
    }

    // real art saved over a placeholder (same file name) stays the same size on screen: its pixels per unit
    // follows the file's width, so a bigger, sharper picture fills the same space. real art also gets compressed
    private static void FitReplacedArt(string path, int placeholderSize, float worldSize, List<string> problems)
    {
        if (!TryReadPngSize(path, out int width, out int height))
        {
            problems.Add(path + " is not a readable PNG; its size was left as it is.");
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        bool changed = false;
        float wanted = width / worldSize;
        if (Mathf.Abs(importer.spritePixelsPerUnit - wanted) > wanted * 0.005f)
        {
            importer.spritePixelsPerUnit = wanted;
            changed = true;
        }

        if (width != placeholderSize || height != placeholderSize)
        {
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }
            int largest = Mathf.Max(width, height);
            if (importer.maxTextureSize < largest)
            {
                importer.maxTextureSize = Mathf.NextPowerOfTwo(largest);
                changed = true;
            }
        }

        if (changed)
            importer.SaveAndReimport();
    }

    // width and height from the PNG header (bytes 16-23 of the IHDR chunk)
    private static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = height = 0;
        byte[] header = new byte[24];
        try
        {
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) < header.Length)
                    return false;
            }
        }
        catch (IOException)
        {
            return false;
        }

        if (header[0] != 0x89 || header[1] != (byte)'P' || header[2] != (byte)'N' || header[3] != (byte)'G')
            return false;
        width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        return width > 0 && height > 0;
    }

    private static bool IsTough(string name)
    {
        return name == "Enemy_VoltBeetle" || name == "Enemy_ShellCrab" || name == "Enemy_MagmaBrute";
    }

    // the Spark Mite was first saved in Sprites/Placeholders. moving it keeps its GUID,
    // so the stage 8 waves that already use it still work
    private static void MoveLegacySparkMite(List<string> problems)
    {
        string target = AssetPath(SparkMite);
        if (!File.Exists(LegacySparkMitePath) || File.Exists(target))
            return;

        string error = AssetDatabase.MoveAsset(LegacySparkMitePath, target);
        if (!string.IsNullOrEmpty(error))
        {
            problems.Add("Could not move " + LegacySparkMitePath + " to " + target + ": " + error);
            return;
        }

        if (AssetDatabase.IsValidFolder(LegacyFolder) && Directory.GetFiles(LegacyFolder).Length == 0 &&
            Directory.GetDirectories(LegacyFolder).Length == 0)
            AssetDatabase.DeleteAsset(LegacyFolder);
    }

    private static Sprite EnsureSprite(string name, int size, float worldSize, List<string> problems)
    {
        string path = AssetPath(name);
        float pixelsPerUnit = size / worldSize;
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
        {
            // already made, or replaced with real art: keep it, at the placeholder's size
            FitReplacedArt(path, size, worldSize, problems);
            Sprite fitted = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return fitted != null ? fitted : existing;
        }
        if (File.Exists(path))
        {
            problems.Add(path + " exists but is not imported as a Sprite: set its Texture Type to Sprite (2D and UI).");
            return null;
        }

        PlaceholderPainter.Sketch sketch = PlaceholderPainter.Draw(name);
        if (sketch == null)
        {
            problems.Add("PlaceholderPainter has no drawing called " + name + ".");
            return null;
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.LoadRawTextureData(sketch.Render(size));
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            problems.Add("Unable to import " + path + ".");
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Sprite created = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (created == null)
            problems.Add(path + " was written but did not import as a Sprite.");
        return created;
    }

    // same size on screen as the Dust Scout, whose body the placeholder enemies use
    // (matched by area, because the placeholders are round)
    private static float EnemyWorldSize()
    {
        Enemy scout = AssetDatabase.LoadAssetAtPath<Enemy>(DustScoutPrefab);
        SpriteRenderer renderer = scout != null ? scout.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return 0.8f;
        Vector3 size = renderer.sprite.bounds.size;
        return Mathf.Max(0.2f, Mathf.Sqrt(size.x * size.y) * 1.1f);
    }

    // same size as Choma, so swapped-in Beasts fill the launch pad the same way
    private static float BeastWorldSize()
    {
        GameObject choma = AssetDatabase.LoadAssetAtPath<GameObject>(ChomaPrefab);
        LaunchController launcher = choma != null ? choma.GetComponent<LaunchController>() : null;
        Sprite idle = launcher != null ? launcher.idleSprite : null;
        if (idle == null && choma != null)
        {
            SpriteRenderer renderer = choma.GetComponent<SpriteRenderer>();
            idle = renderer != null ? renderer.sprite : null;
        }
        if (idle == null)
            return 1f;
        Vector3 size = idle.bounds.size;
        return Mathf.Max(0.2f, Mathf.Max(size.x, size.y));
    }
}

// art dropped into the placeholder folder while the editor is open gets fitted to the
// placeholder's size right away (otherwise it happens on the next script reload)
public class PlaceholderArtWatcher : AssetPostprocessor
{
    private static bool scheduled;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (scheduled || Application.isBatchMode)
            return;

        foreach (string path in imported)
        {
            if (path.StartsWith(PlaceholderArtGenerator.Folder + "/") && path.EndsWith(".png"))
            {
                scheduled = true;
                EditorApplication.delayCall += Refit;
                return;
            }
        }
    }

    private static void Refit()
    {
        scheduled = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        List<string> problems = new List<string>();
        PlaceholderArtGenerator.EnsureAll(problems);
        foreach (string problem in problems)
            Debug.LogWarning("Bumper Beast art: " + problem);
    }
}
