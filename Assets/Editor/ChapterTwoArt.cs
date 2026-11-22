#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Wires the painted Stone Hollow art into the Earth stages:
//  - stage5Bg..stage8Bg onto each scene's EarthEnvironment;
//  - the Ch2_Enemies sheet (3 columns x 2 rows: normal / damaged) into three prefab
//    variants of Enemy_1, which the matching waves then spawn.
// Run from the menu after Unity has imported the PNGs. Missing or not-yet-imported
// files are reported and skipped; anything already correct is left untouched.
public static class ChapterTwoArt
{
    private const string BackgroundFolder = "Assets/Sprites/Chapter2/Backgrounds/";
    private const string EnemySheetPath = "Assets/Sprites/Chapter2/Ch2_Enemies_Sheet.png";
    private const string BaseEnemyPrefabPath = "Assets/Prefab/Enemy_1.prefab";

    // Earth barrier and launch pad: the sprites are pre-fitted to the Chapter 1 canvases
    // (same world size under the prefabs' scales), so only clips, controllers and sprites change.
    private const string BarrierHitSheetPath = "Assets/Sprites/Chapter2/Earth_Barrier_Hit.png";
    private const string BarrierDestroyedSheetPath = "Assets/Sprites/Chapter2/Earth_Barrier_Destroyed.png";
    // Pad frames (painted sheet): 00 rest, 01-03 rope pulled down further each frame with the
    // pillar tops bending in, 04 release (rope whips up), 05 settle.
    private static readonly string[] PadFramePaths =
    {
        "Assets/Sprites/Chapter2/Earth_LaunchPad.png",
        "Assets/Sprites/Chapter2/Earth_LaunchPad_01.png",
        "Assets/Sprites/Chapter2/Earth_LaunchPad_02.png",
        "Assets/Sprites/Chapter2/Earth_LaunchPad_03.png",
        "Assets/Sprites/Chapter2/Earth_LaunchPad_04.png",
        "Assets/Sprites/Chapter2/Earth_LaunchPad_05.png"
    };
    private const float PadPixelsPerUnit = 300f;
    private const string BarrierControllerPath = "Assets/Sprites/Square.controller";
    private const string PadControllerPath = "Assets/Sprites/LaunchPad.controller";
    private const string GeneratedFolder = "Assets/Sprites/Chapter2/Generated";
    private const string BarrierOverridePath = GeneratedFolder + "/Earth_Barrier.overrideController";
    private const string PadOverridePath = GeneratedFolder + "/Earth_LaunchPad.overrideController";
    private const string BarrierObjectName = "Square";
    private const string PadObjectName = "LaunchPad";
    // The Earth hit frames are subtler than the log's big starburst, so the hit plays at a
    // slower beat: key times stretched by this factor, and the barrier returns to idle to match.
    private const float BarrierHitTimeScale = 1.6f;
    private const float BarrierHitReturnDelay = 0.6f;

    private static readonly string[] EarthScenes =
    {
        "Assets/Scenes/stage-5_Earth.unity",
        "Assets/Scenes/stage-6_Earth.unity",
        "Assets/Scenes/stage-7_Earth.unity",
        "Assets/Scenes/stage-8_Earth.unity"
    };

    private sealed class EnemyVariant
    {
        public readonly string name;
        public readonly int normalSlice;
        public readonly int damagedSlice;
        public readonly string[] waveNames;

        public EnemyVariant(string name, int normalSlice, int damagedSlice, params string[] waveNames)
        {
            this.name = name;
            this.normalSlice = normalSlice;
            this.damagedSlice = damagedSlice;
            this.waveNames = waveNames;
        }

        public string PrefabPath => "Assets/Prefab/Enemy_" + name + ".prefab";
    }

    // Slice order is row-major: top row normal, bottom row damaged.
    private static readonly EnemyVariant[] Variants =
    {
        new EnemyVariant("DustScout", 0, 3, "Dust Scouts"),
        new EnemyVariant("Burrower", 1, 4, "Burrowers"),
        new EnemyVariant("Rollstone", 2, 5, "Rollstones", "Boulderbacks")
    };

    [MenuItem("Bumper Beast/Wire Chapter 2 Art")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Bumper Beast: leave Play Mode before wiring Chapter 2 art.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        List<string> problems = new List<string>();
        Dictionary<string, Enemy> variantPrefabs = BuildEnemyVariants(problems);
        AnimatorOverrideController barrierOverride = BuildBarrierOverride(problems, out Sprite barrierIdleSprite);
        AnimatorOverrideController padOverride = BuildPadOverride(problems, out Sprite padSprite);

        int backgroundsWired = 0, wavesWired = 0, scenesSaved = 0, propsWired = 0;
        Scene originalScene = SceneManager.GetActiveScene();

        for (int stageIndex = 0; stageIndex < EarthScenes.Length; stageIndex++)
        {
            string scenePath = EarthScenes[stageIndex];
            if (!File.Exists(scenePath))
            {
                problems.Add(scenePath + " is missing.");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                problems.Add("Unable to open " + scenePath + ".");
                continue;
            }

            bool dirty = false;

            // Background.
            string spritePath = BackgroundFolder + "stage" + (stageIndex + 5) + "Bg.png";
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (background == null)
            {
                problems.Add(File.Exists(spritePath)
                    ? spritePath + " exists but is not imported as a Sprite yet (let Unity finish importing, then run again)."
                    : spritePath + " is missing.");
            }
            else
            {
                EarthEnvironment environment = FindInScene<EarthEnvironment>(scene);
                if (environment == null)
                    problems.Add(scenePath + " has no EarthEnvironment component.");
                else if (environment.background != background)
                {
                    Undo.RecordObject(environment, "Wire Chapter 2 Background");
                    environment.background = background;
                    EditorUtility.SetDirty(environment);
                    backgroundsWired++;
                    dirty = true;
                }
            }

            // Waves.
            if (variantPrefabs.Count > 0)
            {
                WaveManager waveManager = FindInScene<WaveManager>(scene);
                if (waveManager == null || waveManager.waves == null)
                    problems.Add(scenePath + " has no WaveManager with waves.");
                else
                {
                    bool wavesChanged = false;
                    foreach (EnemyWave wave in waveManager.waves)
                    {
                        if (wave == null || wave.isBoss)
                            continue;
                        EnemyVariant variant = FindVariantForWave(wave.waveName);
                        if (variant == null || !variantPrefabs.TryGetValue(variant.name, out Enemy prefab))
                            continue;
                        bool prefabsMatch = wave.enemyPrefabs != null && wave.enemyPrefabs.Length == 1 && wave.enemyPrefabs[0] == prefab;
                        if (prefabsMatch && wave.enemySprite == null)
                            continue;
                        if (!wavesChanged)
                            Undo.RecordObject(waveManager, "Wire Chapter 2 Enemies");
                        wave.enemyPrefabs = new[] { prefab };
                        // A wave sprite override would replace the painted art at spawn (and then
                        // flip to the painted damaged frame at half health), so painted variants never use one.
                        wave.enemySprite = null;
                        wavesChanged = true;
                        wavesWired++;
                    }
                    if (wavesChanged)
                    {
                        EditorUtility.SetDirty(waveManager);
                        dirty = true;
                    }
                }
            }

            // Barrier and launch pad art.
            if (barrierOverride != null &&
                ApplyProp(scene, BarrierObjectName, barrierOverride, barrierIdleSprite, "Wire Earth Barrier", problems, scenePath))
            {
                propsWired++;
                dirty = true;
            }
            if (padOverride != null &&
                ApplyProp(scene, PadObjectName, padOverride, padSprite, "Wire Earth Launch Pad", problems, scenePath))
            {
                propsWired++;
                dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                    scenesSaved++;
                else
                    problems.Add("Unable to save " + scenePath + ".");
            }
            EditorSceneManager.CloseScene(scene, true);
        }

        if (originalScene.IsValid() && originalScene.isLoaded)
            SceneManager.SetActiveScene(originalScene);

        string summary = "Bumper Beast: Chapter 2 art wired. Backgrounds set: " + backgroundsWired +
            ", enemy variants ready: " + variantPrefabs.Count + "/" + Variants.Length +
            ", waves re-pointed: " + wavesWired +
            ", barrier/pad props updated: " + propsWired +
            ", scenes saved: " + scenesSaved + ".";
        if (problems.Count == 0)
            Debug.Log(summary);
        else
            Debug.LogWarning(summary + "\n" + string.Join("\n", problems));
    }

    // Creates or updates Enemy_DustScout / Enemy_Burrower / Enemy_Rollstone as prefab
    // variants of Enemy_1 (so stats, abilities and colliders stay inherited) and returns
    // the Enemy component of each one that is ready.
    private static Dictionary<string, Enemy> BuildEnemyVariants(List<string> problems)
    {
        Dictionary<string, Enemy> result = new Dictionary<string, Enemy>();

        if (!File.Exists(EnemySheetPath))
        {
            problems.Add(EnemySheetPath + " is missing; enemy variants skipped.");
            return result;
        }
        if (!SpriteSheetImporter.EnsureGridSlices(EnemySheetPath))
        {
            problems.Add(EnemySheetPath + " is not imported/sliced yet (let Unity finish importing, then run again); enemy variants skipped.");
            return result;
        }

        Dictionary<string, Sprite> slices = AssetDatabase.LoadAllAssetRepresentationsAtPath(EnemySheetPath)
            .OfType<Sprite>()
            .ToDictionary(sprite => sprite.name, sprite => sprite, System.StringComparer.Ordinal);
        string prefix = Path.GetFileNameWithoutExtension(EnemySheetPath) + "_";

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPrefabPath);
        if (basePrefab == null)
        {
            problems.Add(BaseEnemyPrefabPath + " is missing; enemy variants skipped.");
            return result;
        }

        foreach (EnemyVariant variant in Variants)
        {
            if (!slices.TryGetValue(prefix + variant.normalSlice.ToString("00"), out Sprite normal) ||
                !slices.TryGetValue(prefix + variant.damagedSlice.ToString("00"), out Sprite damaged))
            {
                problems.Add("Enemy sheet slices for " + variant.name + " are missing.");
                continue;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(variant.PrefabPath);
            if (prefabAsset == null)
                prefabAsset = CreateVariant(basePrefab, variant.PrefabPath, problems);
            if (prefabAsset == null)
                continue;

            if (ApplySprites(variant.PrefabPath, normal, damaged))
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(variant.PrefabPath);

            Enemy enemy = prefabAsset != null ? prefabAsset.GetComponent<Enemy>() : null;
            if (enemy == null)
                problems.Add(variant.PrefabPath + " has no Enemy component.");
            else
                result[variant.name] = enemy;
        }

        return result;
    }

    private static GameObject CreateVariant(GameObject basePrefab, string path, List<string> problems)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        if (instance == null)
        {
            problems.Add("Unable to instantiate " + BaseEnemyPrefabPath + " to create " + path + ".");
            return null;
        }
        try
        {
            instance.name = Path.GetFileNameWithoutExtension(path);
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // Returns true when the prefab was changed and saved.
    private static bool ApplySprites(string prefabPath, Sprite normal, Sprite damaged)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            bool changed = false;
            SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Debug.LogError(prefabPath + " has no SpriteRenderer.");
                return false;
            }
            if (renderer.sprite != normal)
            {
                renderer.sprite = normal;
                changed = true;
            }

            EnemyDamagedSprite damagedComponent = contents.GetComponent<EnemyDamagedSprite>();
            if (damagedComponent == null)
            {
                damagedComponent = contents.AddComponent<EnemyDamagedSprite>();
                changed = true;
            }
            if (damagedComponent.damagedSprite != damaged)
            {
                damagedComponent.damagedSprite = damaged;
                changed = true;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ---------------------------------------------------------------- barrier / pad

    // Earth barrier: the Chapter 1 Square controller with its three clips replaced by
    // copies whose sprite keys point at the Earth sheets (same cell order, same timing).
    private static AnimatorOverrideController BuildBarrierOverride(List<string> problems, out Sprite idleSprite)
    {
        idleSprite = null;
        if (!File.Exists(BarrierHitSheetPath) || !File.Exists(BarrierDestroyedSheetPath))
        {
            problems.Add("Earth barrier sheets are missing (" + BarrierHitSheetPath + ", " + BarrierDestroyedSheetPath + "); barrier skipped.");
            return null;
        }
        if (!SpriteSheetImporter.EnsureGridSlices(BarrierHitSheetPath) ||
            !SpriteSheetImporter.EnsureGridSlices(BarrierDestroyedSheetPath))
        {
            problems.Add("Earth barrier sheets are not imported/sliced yet (let Unity finish importing, then run again); barrier skipped.");
            return null;
        }

        Dictionary<string, Sprite> hit = SlicesByName(BarrierHitSheetPath);
        Dictionary<string, Sprite> destroyed = SlicesByName(BarrierDestroyedSheetPath);
        hit.TryGetValue("Earth_Barrier_Hit_00", out idleSprite);
        if (idleSprite == null)
        {
            problems.Add("Earth_Barrier_Hit_00 slice is missing; barrier skipped.");
            return null;
        }

        // Chapter 1 clip sprites are "Analysis output 2_<i>" (idle + hit) and "Analysis output 1_<i>" (destroyed).
        Sprite Map(Sprite original)
        {
            if (original == null)
                return null;
            string name = original.name;
            int underscore = name.LastIndexOf('_');
            if (underscore < 0 || !int.TryParse(name.Substring(underscore + 1), out int index))
                return null;
            string key = "_" + index.ToString("00");
            if (name.StartsWith("Analysis output 2", System.StringComparison.Ordinal))
                return hit.TryGetValue("Earth_Barrier_Hit" + key, out Sprite h) ? h : null;
            if (name.StartsWith("Analysis output 1", System.StringComparison.Ordinal))
                return destroyed.TryGetValue("Earth_Barrier_Destroyed" + key, out Sprite d) ? d : null;
            return null;
        }

        return BuildOverride(BarrierControllerPath, BarrierOverridePath, "Earth_",
            (clipName, keyIndex, keyCount, original) => Map(original), problems,
            clipName => clipName.Contains("Hit") ? BarrierHitTimeScale : 1f);
    }

    // Earth launch pad: the Chapter 1 pad clips (idle / pulling back / launching) keep their
    // timing, with each key mapped onto the five Earth frames by its position in the clip.
    private static AnimatorOverrideController BuildPadOverride(List<string> problems, out Sprite padSprite)
    {
        padSprite = null;
        Sprite[] frames = new Sprite[PadFramePaths.Length];
        for (int index = 0; index < PadFramePaths.Length; index++)
        {
            string path = PadFramePaths[index];
            if (!File.Exists(path))
            {
                problems.Add(path + " is missing; launch pad skipped.");
                return null;
            }
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                problems.Add(path + " is not imported yet (let Unity finish importing, then run again); launch pad skipped.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, PadPixelsPerUnit) ||
                importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PadPixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            frames[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[index] == null)
            {
                problems.Add(path + " did not import as a Sprite; launch pad skipped.");
                return null;
            }
        }
        padSprite = frames[0];

        Sprite Map(string clipName, int keyIndex, int keyCount, Sprite original)
        {
            if (original == null)
                return null;
            float position = keyCount > 1 ? keyIndex / (float)(keyCount - 1) : 0f;
            if (clipName.Contains("PullingBack"))
                return frames[Mathf.Clamp(Mathf.RoundToInt(position * 3f), 0, 3)];
            if (clipName.Contains("Launching"))
            {
                // Release: rope whips up, settles, back to rest.
                if (position < 0.5f) return frames[4];
                if (keyIndex < keyCount - 1) return frames[5];
                return frames[0];
            }
            return frames[0];
        }

        return BuildOverride(PadControllerPath, PadOverridePath, "Earth_", Map, problems);
    }

    private static Dictionary<string, Sprite> SlicesByName(string sheetPath)
    {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
        foreach (Sprite sprite in AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath).OfType<Sprite>())
            result[sprite.name] = sprite;
        return result;
    }

    // Clones every clip of the base controller with its sprite keys remapped, and returns an
    // override controller that swaps them in. Assets are created once and updated in place.
    // map(clipName, keyIndex, keyCount, originalSprite) -> Earth sprite, or null to leave the key unchanged.
    // timeScale(clipName) -> factor applied to every key time (1 = keep the Chapter 1 timing).
    private static AnimatorOverrideController BuildOverride(string controllerPath, string overridePath, string clipPrefix,
        System.Func<string, int, int, Sprite, Sprite> map, List<string> problems,
        System.Func<string, float> timeScale = null)
    {
        RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (baseController == null)
        {
            problems.Add(controllerPath + " is missing; override skipped.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(GeneratedFolder).Replace('\\', '/'), Path.GetFileName(GeneratedFolder));

        List<KeyValuePair<AnimationClip, AnimationClip>> pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        foreach (AnimationClip original in baseController.animationClips.Distinct())
        {
            if (original == null)
                continue;
            string clipPath = GeneratedFolder + "/" + clipPrefix + original.name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool isNew = clip == null;
            if (isNew)
            {
                clip = Object.Instantiate(original);
                clip.name = clipPrefix + original.name;
            }
            else
            {
                // Refresh from the original so timing/loop changes in Chapter 1 carry over.
                EditorUtility.CopySerialized(original, clip);
                clip.name = clipPrefix + original.name;
            }

            bool missing = false;
            float scale = timeScale != null ? Mathf.Max(0.01f, timeScale(original.name)) : 1f;
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                for (int index = 0; index < keys.Length; index++)
                {
                    Sprite mapped = map(original.name, index, keys.Length, keys[index].value as Sprite);
                    if (mapped == null)
                    {
                        missing = true;
                        continue;
                    }
                    keys[index].value = mapped;
                    keys[index].time *= scale;
                }
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }
            if (missing)
                problems.Add("Some sprite keys of " + original.name + " had no Earth equivalent and were left unchanged.");

            if (isNew)
                AssetDatabase.CreateAsset(clip, clipPath);
            else
                EditorUtility.SetDirty(clip);
            pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, clip));
        }

        AnimatorOverrideController overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(overrideController, overridePath);
        }
        else
        {
            overrideController.runtimeAnimatorController = baseController;
        }
        overrideController.ApplyOverrides(pairs);
        EditorUtility.SetDirty(overrideController);
        AssetDatabase.SaveAssets();
        return overrideController;
    }

    // Points the named scene object's Animator at the override and its renderer at the resting sprite.
    private static bool ApplyProp(Scene scene, string objectName, RuntimeAnimatorController controller, Sprite restingSprite,
        string undoLabel, List<string> problems, string scenePath)
    {
        GameObject target = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                target = root;
                break;
            }
            Transform child = root.transform.Find(objectName);
            if (child != null)
            {
                target = child.gameObject;
                break;
            }
        }
        if (target == null)
        {
            problems.Add(scenePath + " has no object named '" + objectName + "'.");
            return false;
        }

        bool changed = false;
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
            problems.Add(sceneName + ": '" + objectName + "' has no Animator; its animation cannot be overridden.");
        else if (animator.runtimeAnimatorController != controller)
        {
            Undo.RecordObject(animator, undoLabel);
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            changed = true;
            Debug.Log("Bumper Beast: " + sceneName + " / " + objectName + " -> animator now uses " + controller.name + ".");
        }

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
            problems.Add(sceneName + ": '" + objectName + "' has no SpriteRenderer.");
        else if (restingSprite != null && renderer.sprite != restingSprite)
        {
            Undo.RecordObject(renderer, undoLabel);
            renderer.sprite = restingSprite;
            EditorUtility.SetDirty(renderer);
            changed = true;
            Debug.Log("Bumper Beast: " + sceneName + " / " + objectName + " -> resting sprite now " + restingSprite.name + ".");
        }

        // Earth barrier: hold the hit pose as long as the slower Earth hit clip runs.
        BarrierVisuals barrierVisuals = target.GetComponent<BarrierVisuals>();
        if (barrierVisuals != null && !Mathf.Approximately(barrierVisuals.hitReturnDelay, BarrierHitReturnDelay))
        {
            Undo.RecordObject(barrierVisuals, undoLabel);
            barrierVisuals.hitReturnDelay = BarrierHitReturnDelay;
            EditorUtility.SetDirty(barrierVisuals);
            changed = true;
            Debug.Log("Bumper Beast: " + sceneName + " / " + objectName + " -> hit return delay " + BarrierHitReturnDelay + "s.");
        }

        if (!changed && animator != null && renderer != null)
            Debug.Log("Bumper Beast: " + sceneName + " / " + objectName + " already wired (" + controller.name + ", " +
                (renderer.sprite != null ? renderer.sprite.name : "no sprite") + ").");
        return changed;
    }

    private static EnemyVariant FindVariantForWave(string waveName)
    {
        if (string.IsNullOrEmpty(waveName))
            return null;
        foreach (EnemyVariant variant in Variants)
            foreach (string name in variant.waveNames)
                if (string.Equals(name, waveName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return variant;
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}
#endif
