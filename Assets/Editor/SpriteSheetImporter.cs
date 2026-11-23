#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpriteSheetImporter
{
    private const float ThistleVisibleHeightPixels = 420f;
    private const float ThistleFallbackPixelsPerUnit = 382f;
    private const string ThistlePath = "Assets/Sprites/Beasts/Thistle_Sheet.png";
    private const string ChomaPrefabPath = "Assets/Prefab/Choma.prefab";
    private const string StageOnePath = "Assets/Scenes/stage-1.unity";

    private sealed class SheetDefinition
    {
        public readonly string path;
        public readonly int columns;
        public readonly int rows;
        // 0 = work it out from the cell height (one cell = one world unit), otherwise use this number
        public readonly float pixelsPerUnit;
        // pivot shared by every cell, as 0-1 (Unity counts from the bottom left)
        public readonly Vector2 pivot;

        public SheetDefinition(string path, int columns, int rows, float pixelsPerUnit = 0f)
            : this(path, columns, rows, pixelsPerUnit, new Vector2(0.5f, 0.5f))
        {
        }

        public SheetDefinition(string path, int columns, int rows, float pixelsPerUnit, Vector2 pivot)
        {
            this.path = path;
            this.columns = columns;
            this.rows = rows;
            this.pixelsPerUnit = pixelsPerUnit;
            this.pivot = pivot;
        }
    }

    // Earth barrier pivot: (512, 512) px from the cell's top left. every frame is drawn centered at
    // x 576 with its base on row 736, so this pivot keeps the wall exactly where the Square transform
    // (that we tuned by hand) shows it, the same on every frame
    private static readonly Vector2 EarthBarrierPivot = new Vector2(512f / 1152f, 256f / 768f);

    private static readonly SheetDefinition[] Sheets =
    {
        new SheetDefinition(ThistlePath, 4, 4),
        new SheetDefinition("Assets/Sprites/Beasts/Mossback_Sheet.png", 4, 4),
        new SheetDefinition("Assets/Sprites/Chapter2/Ch2_Enemies_Sheet.png", 3, 2),
        new SheetDefinition("Assets/Sprites/Chapter2/Terrathorn_Sheet.png", 2, 2),
        new SheetDefinition("Assets/Sprites/Chapter2/Ch2_Bumpers_Sheet.png", 3, 2),
        // Earth barrier: 1152x768 cells at PPU 300 are the same 3.84x2.56 units as the chapter 1 log
        // sheets (384x256 at PPU 100), so the Square prefab's scale still works
        new SheetDefinition("Assets/Sprites/Chapter2/Earth_Barrier_Hit.png", 2, 2, 300f, EarthBarrierPivot),
        new SheetDefinition("Assets/Sprites/Chapter2/Earth_Barrier_Destroyed.png", 2, 2, 300f, EarthBarrierPivot)
    };

    // has a version number so a fixed hook runs again even if the old one already ran this session
    private const string AutoImportSessionKey = "BumperBeast.ThistleAutoImport.Done.v2";

    // after a script reload, slice and set up the Thistle sheet by itself if it hasn't been done yet,
    // so the art works without clicking a menu. only the texture import and the Choma prefab are
    // changed here, the stage 1 test unlock (which saves a scene) stays in the menu item
    [InitializeOnLoadMethod]
    private static void AutoImportThistleOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                SessionState.GetBool(AutoImportSessionKey, false) ||
                !File.Exists(ThistlePath))
                return;

            // Unity hasn't imported it yet (no importer), so try again after the next reload
            if (!(AssetImporter.GetAtPath(ThistlePath) is TextureImporter importer))
                return;

            // only our grid slices count. Unity's Automatic slicing (or slicing by hand) makes rects with
            // other names that ConfigureThistlePrefab can't use
            SessionState.SetBool(AutoImportSessionKey, true);
            if (importer.spriteImportMode == SpriteImportMode.Multiple && HasGridSlices(Sheets[0]))
                return;

            try
            {
                float pixelsPerUnit = GetThistlePixelsPerUnit();
                ImportGridSheet(Sheets[0], pixelsPerUnit);
                AssetDatabase.SaveAssets();
                bool configured = ConfigureThistlePrefab();
                Debug.Log("Bumper Beast: Thistle sheet sliced automatically (PPU " +
                    pixelsPerUnit.ToString("0") + "); Choma prefab " +
                    (configured ? "updated with the Thistle animation set." : "was NOT updated; see errors above."));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    // slices one of the known sheets if its grid slices are missing. returns false if the file
    // is unknown, missing or not imported yet. the chapter 2 art setup uses this
    public static bool EnsureGridSlices(string path)
    {
        SheetDefinition sheet = null;
        foreach (SheetDefinition candidate in Sheets)
        {
            if (string.Equals(candidate.path, path, StringComparison.OrdinalIgnoreCase))
                sheet = candidate;
        }
        if (sheet == null || !File.Exists(sheet.path))
            return false;
        if (!(AssetImporter.GetAtPath(sheet.path) is TextureImporter importer))
            return false;
        if (importer.spriteImportMode == SpriteImportMode.Multiple && HasGridSlices(sheet))
            return true;

        ImportGridSheet(sheet, GetThistlePixelsPerUnit());
        AssetDatabase.SaveAssets();
        return HasGridSlices(sheet);
    }

    [MenuItem("Bumper Beast/Import Sprite Sheets")]
    public static void ImportSpriteSheets()
    {
        AssetDatabase.Refresh();

        float thistlePixelsPerUnit = GetThistlePixelsPerUnit();
        List<string> missingSheets = new List<string>();
        int importedSheets = 0;

        foreach (SheetDefinition sheet in Sheets)
        {
            if (!File.Exists(sheet.path))
            {
                missingSheets.Add(Path.GetFileNameWithoutExtension(sheet.path));
                continue;
            }

            ImportGridSheet(sheet, thistlePixelsPerUnit);
            importedSheets++;
        }

        AssetDatabase.SaveAssets();
        bool configuredThistle = ConfigureThistlePrefab();
        bool configuredStageOne = EnableStageOneBeastTesting();

        string missingText = missingSheets.Count == 0
            ? "All configured sheets were found."
            : "Not found (left unchanged): " + string.Join(", ", missingSheets);
        string result = "Imported " + importedSheets + " sprite sheet(s)." +
            "\nThistle PPU: " + thistlePixelsPerUnit.ToString("0") +
            " (matches Choma's current idle sprite height)." +
            "\nThistle prefab setup: " + (configuredThistle ? "complete" : "not applied") +
            "\nStage 1 test unlock: " + (configuredStageOne ? "enabled" : "not applied") +
            "\n" + missingText;

        Debug.Log("Bumper Beast sprite import finished. " + result);
        EditorUtility.DisplayDialog("Bumper Beast Sprite Import", result, "OK");
    }

    private static float GetThistlePixelsPerUnit()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChomaPrefabPath);
        LaunchController launcher = prefab != null
            ? prefab.GetComponent<LaunchController>()
            : null;
        Sprite chomaIdle = launcher != null ? launcher.idleSprite : null;
        TextureImporter chomaImporter = chomaIdle != null
            ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(chomaIdle)) as TextureImporter
            : null;

        if (chomaIdle == null || chomaImporter == null ||
            chomaImporter.spritePixelsPerUnit <= 0f ||
            prefab.transform.lossyScale.y <= 0f)
        {
            Debug.LogWarning(
                "Could not measure Choma's idle sprite; using the 382 PPU fallback for Thistle.");
            return ThistleFallbackPixelsPerUnit;
        }

        float chomaWorldHeight = chomaIdle.rect.height /
            chomaImporter.spritePixelsPerUnit * prefab.transform.lossyScale.y;
        return Mathf.Max(1f, Mathf.Round(ThistleVisibleHeightPixels / chomaWorldHeight));
    }

    private static void ImportGridSheet(SheetDefinition sheet, float thistlePixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(sheet.path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Texture importer unavailable for " + sheet.path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = sheet.pixelsPerUnit > 0f
            ? sheet.pixelsPerUnit
            : sheet.path == ThistlePath
                ? thistlePixelsPerUnit
                : GetDefaultPixelsPerUnit(sheet.path, importer.maxTextureSize, sheet.rows);
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.path);
        if (texture == null)
            throw new InvalidOperationException("Imported texture unavailable at " + sheet.path);

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(texture);
        if (provider == null)
            throw new InvalidOperationException("Sprite data provider unavailable for " + sheet.path);

        provider.InitSpriteEditorDataProvider();
        // keep the IDs of slices that already exist so prefab and scene references don't break on re-import
        Dictionary<string, SpriteRect> existingByName =
            new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
        foreach (SpriteRect existingRect in provider.GetSpriteRects())
        {
            if (!string.IsNullOrEmpty(existingRect.name))
                existingByName[existingRect.name] = existingRect;
        }

        // sprite rects use the SOURCE pixels. texture.width/height are the imported size, which
        // maxTextureSize shrinks for sheets over 2048 px, so we can't use them here
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
        bool centred = sheet.pivot == new Vector2(0.5f, 0.5f);
        SpriteRect[] spriteRects = new SpriteRect[sheet.columns * sheet.rows];

        for (int row = 0; row < sheet.rows; row++)
        {
            for (int column = 0; column < sheet.columns; column++)
            {
                int index = row * sheet.columns + column;
                string spriteName = Path.GetFileNameWithoutExtension(sheet.path) +
                    "_" + index.ToString("00");
                SpriteRect rect = new SpriteRect
                {
                    name = spriteName,
                    rect = GridCellRect(sheet, sourceWidth, sourceHeight, row, column),
                    alignment = centred ? SpriteAlignment.Center : SpriteAlignment.Custom,
                    pivot = sheet.pivot
                };
                // a new SpriteRect makes its own ID the first time spriteID is read,
                // so we only copy IDs over for slices that already exist
                if (existingByName.TryGetValue(spriteName, out SpriteRect existing))
                    rect.spriteID = existing.spriteID;
                spriteRects[index] = rect;
            }
        }

        provider.SetSpriteRects(spriteRects);
        provider.Apply();
        AssetDatabase.ImportAsset(sheet.path, ImportAssetOptions.ForceUpdate);
    }

    private static Rect GridCellRect(SheetDefinition sheet, int sourceWidth, int sourceHeight, int row, int column)
    {
        int cellWidth = sourceWidth / sheet.columns;
        int cellHeight = sourceHeight / sheet.rows;
        return new Rect(column * cellWidth, sourceHeight - (row + 1) * cellHeight, cellWidth, cellHeight);
    }

    // true if every cell of the grid exists as a slice with our naming, the right source-pixel rect
    // and pivot (so a sheet sliced wrong, like by the old shrunk-size slicing, gets sliced again)
    private static bool HasGridSlices(SheetDefinition sheet)
    {
        if (!(AssetImporter.GetAtPath(sheet.path) is TextureImporter importer))
            return false;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.path);
        if (texture == null)
            return false;

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(texture);
        if (provider == null)
            return false;
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, SpriteRect> byName = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
        foreach (SpriteRect spriteRect in provider.GetSpriteRects())
        {
            if (!string.IsNullOrEmpty(spriteRect.name))
                byName[spriteRect.name] = spriteRect;
        }

        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
        string prefix = Path.GetFileNameWithoutExtension(sheet.path) + "_";
        for (int row = 0; row < sheet.rows; row++)
        {
            for (int column = 0; column < sheet.columns; column++)
            {
                string spriteName = prefix + (row * sheet.columns + column).ToString("00");
                if (!byName.TryGetValue(spriteName, out SpriteRect existing))
                    return false;
                if (existing.rect != GridCellRect(sheet, sourceWidth, sourceHeight, row, column) ||
                    (existing.pivot - sheet.pivot).sqrMagnitude > 1e-8f)
                    return false;
            }
        }
        return true;
    }

    private static float GetDefaultPixelsPerUnit(string path, int maxTextureSize, int rows)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int sourceHeight = texture != null ? texture.height : maxTextureSize;
        int cellHeight = sourceHeight / Mathf.Max(1, rows);
        return Mathf.Max(1f, cellHeight);
    }

    private static bool ConfigureThistlePrefab()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(ThistlePath)
            .OfType<Sprite>()
            .ToArray();
        if (sprites.Length < 16)
        {
            Debug.LogError(
                "Thistle sheet must contain 16 slices before its animation can be configured.");
            return false;
        }

        Dictionary<string, Sprite> spritesByName = sprites.ToDictionary(
            sprite => sprite.name,
            StringComparer.Ordinal);
        string prefix = Path.GetFileNameWithoutExtension(ThistlePath) + "_";
        Sprite[] frames = new Sprite[16];
        for (int index = 0; index < frames.Length; index++)
        {
            string spriteName = prefix + index.ToString("00");
            if (!spritesByName.TryGetValue(spriteName, out frames[index]))
            {
                Debug.LogError("Thistle sprite slice is missing: " + spriteName);
                return false;
            }
        }

        GameObject prefab = PrefabUtility.LoadPrefabContents(ChomaPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Unable to load the Choma prefab at " + ChomaPrefabPath);
            return false;
        }

        try
        {
            LaunchController launcher = prefab.GetComponent<LaunchController>();
            if (launcher == null)
            {
                Debug.LogError("Choma prefab has no LaunchController component.");
                return false;
            }

            BeastSpriteSet thistleSpriteSet = FindOrCreateSpriteSet(launcher, BeastId.Thistle);
            thistleSpriteSet.idle = frames[0];
            thistleSpriteSet.ball = frames[4];

            BeastAnimSet thistleAnimationSet = FindOrCreateAnimationSet(launcher, BeastId.Thistle);
            thistleAnimationSet.idle = CopyFrames(frames, 0, 4);
            thistleAnimationSet.curl = CopyFrames(frames, 4, 4);
            thistleAnimationSet.spin = CopyFrames(frames, 8, 4);
            thistleAnimationSet.skillEffect = CopyFrames(frames, 12, 4);
            thistleAnimationSet.fps = 10f;

            EditorUtility.SetDirty(launcher);
            PrefabUtility.SaveAsPrefabAsset(prefab, ChomaPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static BeastSpriteSet FindOrCreateSpriteSet(LaunchController launcher, BeastId beast)
    {
        if (launcher.beastSprites != null)
        {
            foreach (BeastSpriteSet set in launcher.beastSprites)
            {
                if (set != null && set.beast == beast)
                    return set;
            }
        }

        BeastSpriteSet created = new BeastSpriteSet { beast = beast };
        int oldLength = launcher.beastSprites != null ? launcher.beastSprites.Length : 0;
        Array.Resize(ref launcher.beastSprites, oldLength + 1);
        launcher.beastSprites[oldLength] = created;
        return created;
    }

    private static BeastAnimSet FindOrCreateAnimationSet(LaunchController launcher, BeastId beast)
    {
        if (launcher.beastAnimations != null)
        {
            foreach (BeastAnimSet set in launcher.beastAnimations)
            {
                if (set != null && set.beast == beast)
                    return set;
            }
        }

        BeastAnimSet created = new BeastAnimSet { beast = beast };
        int oldLength = launcher.beastAnimations != null ? launcher.beastAnimations.Length : 0;
        Array.Resize(ref launcher.beastAnimations, oldLength + 1);
        launcher.beastAnimations[oldLength] = created;
        return created;
    }

    private static Sprite[] CopyFrames(Sprite[] frames, int start, int count)
    {
        Sprite[] result = new Sprite[count];
        Array.Copy(frames, start, result, 0, count);
        return result;
    }

    private static bool EnableStageOneBeastTesting()
    {
        if (!File.Exists(StageOnePath))
        {
            Debug.LogError("Stage 1 scene is missing: " + StageOnePath);
            return false;
        }

        Scene originalScene = SceneManager.GetActiveScene();
        Scene stageOne = EditorSceneManager.OpenScene(StageOnePath, OpenSceneMode.Additive);
        if (!stageOne.IsValid() || !stageOne.isLoaded)
        {
            Debug.LogError("Unable to open the Stage 1 scene for the testing override.");
            return false;
        }

        bool foundSwapSystem = false;

        foreach (GameObject root in stageOne.GetRootGameObjects())
        {
            PadSwapSystem[] systems = root.GetComponentsInChildren<PadSwapSystem>(true);
            foreach (PadSwapSystem system in systems)
            {
                if (system == null)
                    continue;

                system.unlockAllForTesting = true;
                EditorUtility.SetDirty(system);
                foundSwapSystem = true;
            }
        }

        if (!foundSwapSystem)
        {
            EditorSceneManager.CloseScene(stageOne, true);
            Debug.LogError("Stage 1 has no PadSwapSystem to enable for testing.");
            return false;
        }

        EditorSceneManager.MarkSceneDirty(stageOne);
        bool saved = EditorSceneManager.SaveScene(stageOne);
        EditorSceneManager.CloseScene(stageOne, true);
        if (originalScene.IsValid() && originalScene.isLoaded)
            SceneManager.SetActiveScene(originalScene);
        if (!saved)
            Debug.LogError("Unable to save the Stage 1 testing override.");
        return saved;
    }
}
#endif
