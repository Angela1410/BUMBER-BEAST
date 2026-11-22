using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds stages 9-20 (chapters 3-5: Electric, Water, Fire) from the finished stage-5 layout, so
// every chapter plays like the tuned ones (route, bumpers, barrier, launch pad, HUD). The old
// prototype scenes are overwritten file-for-file; each keeps its own .meta (GUID), so the Build
// Settings stay valid. Each copy is then themed for its chapter: generated backdrop and motes,
// tinted barrier / launch pad / rock decorations, rope colour, stage number and coin reward.
// Waves are written next by StageWaveLayouts (until then the copy keeps stage 5's waves).
// Runs automatically (once per BuildVersion) from StageWaveLayouts' editor hook; stage scenes
// open in the editor are skipped and reported. Stages 1-8 are never touched.
// Each built stage also gets its painted background (Assets/Sprites/Chapter3-5/Backgrounds) once
// per PaintedBackdropVersion, without rebuilding the scene; the chapter's own barrier and launcher
// art are picked at runtime by BarrierArt / LauncherArt.
public static class ChapterBuilder
{
    public const int BuildVersion = 1;
    public const int FirstBuiltStage = 9;
    // Bumped when the painted backgrounds of stages 9-20 should be assigned again.
    public const int PaintedBackdropVersion = 1;
    private const string TemplateScene = "Assets/Scenes/stage-5_Earth.unity";
    // Stage-5 rock decorations copied into every built stage; they belong to the Earth canyon, so
    // they are switched off where a chapter's own painted background is shown (no colliders).
    private const string EarthDecorPrefix = "BumperBeast1_";

    private static readonly Regex BuildPattern = new Regex(@"^\s*stageBuildVersion:\s*(\d+)", RegexOptions.Multiline);
    private static readonly Regex BackdropPattern = new Regex(@"^\s*paintedBackdropVersion:\s*(\d+)", RegexOptions.Multiline);
    private static readonly Regex RewardPattern = new Regex(@"^\s*stageReward:\s*(\d+)", RegexOptions.Multiline);

    private struct Look
    {
        public BackdropTheme theme;
        public Color barrierTint;
        public Color padTint;
        public Color ropeColor;
        public Color decorTint;
        public Color moteColor;
        public int moteCount;
    }

    // Stages 1-8 are always ready for waves; 9-20 once they have been built from the template.
    public static bool IsReady(int stage, string sceneText)
    {
        return stage < FirstBuiltStage || ReadInt(BuildPattern, sceneText, 0) >= BuildVersion;
    }

    public static List<string> BuildOutdatedStages(List<string> problems, bool force)
    {
        List<string> rebuilt = new List<string>();
        if (!File.Exists(TemplateScene))
        {
            problems.Add("The template " + TemplateScene + " is missing, so stages 9-20 were not built.");
            return rebuilt;
        }

        for (int stage = FirstBuiltStage; stage <= ChapterData.StageCount; stage++)
        {
            string path = "Assets/Scenes/" + GameHubController.GetStageSceneName(stage) + ".unity";
            string label = Path.GetFileNameWithoutExtension(path);
            if (!File.Exists(path))
            {
                problems.Add(path + " is missing (each stage needs its scene in Assets/Scenes and the Build Settings).");
                continue;
            }

            string text = File.ReadAllText(path);
            if (!force && ReadInt(BuildPattern, text, 0) >= BuildVersion)
                continue;

            Scene open = SceneManager.GetSceneByPath(path);
            if (open.IsValid() && open.isLoaded)
            {
                problems.Add(label + " is open in the editor, so it was not built yet: open another scene " +
                    "(for example GameHub) and it builds itself.");
                continue;
            }

            int reward = ReadInt(RewardPattern, text, 25 + stage * 5);
            File.Copy(TemplateScene, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                problems.Add("Unable to open the rebuilt " + path + ".");
                continue;
            }

            Customize(scene, stage, reward, problems);
            if (EditorSceneManager.SaveScene(scene))
                rebuilt.Add(label);
            else
                problems.Add("Unable to save " + path + ".");
            EditorSceneManager.CloseScene(scene, true);
        }

        return rebuilt;
    }

    private static void Customize(Scene scene, int stage, int reward, List<string> problems)
    {
        string label = scene.name;
        Look look = LookFor(ChapterData.ChapterOfStage(stage));

        StageManager stageManager = FindInScene<StageManager>(scene);
        if (stageManager == null)
        {
            problems.Add(label + ": the stage-5 copy has no StageManager.");
        }
        else
        {
            stageManager.stageNumber = stage;
            stageManager.stageReward = reward;
            stageManager.stageBuildVersion = BuildVersion;
            MarkChanged(stageManager);
        }

        EarthEnvironment environment = FindInScene<EarthEnvironment>(scene);
        if (environment == null)
        {
            problems.Add(label + ": the stage-5 copy has no EarthEnvironment backdrop.");
        }
        else
        {
            environment.theme = look.theme;
            environment.variant = (stage - 1) % ChapterData.StagesPerChapter;
            environment.background = null;   // generated landscape until painted art exists
            environment.tint = Color.white;
            environment.dustColor = look.moteColor;
            environment.dustCount = look.moteCount;
            environment.gameObject.name = "StageBackdrop";
            MarkChanged(environment);
            MarkChanged(environment.gameObject);
        }

        // Barrier, launch pad and the rock decorations take the chapter's colour (placeholder look).
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string objectName = renderer.gameObject.name;
                if (objectName == "Square")
                    renderer.color = look.barrierTint;
                else if (objectName == "LaunchPad")
                    renderer.color = look.padTint;
                else if (objectName.StartsWith("BumperBeast1_"))
                    renderer.color = look.decorTint;
                else
                    continue;
                MarkChanged(renderer);
            }
        }

        LaunchPadFeedback pad = FindInScene<LaunchPadFeedback>(scene);
        if (pad != null)
        {
            pad.vineColor = look.ropeColor;
            MarkChanged(pad);
        }

        // The stage's painted background, when it is in the project (otherwise the generated one).
        Sprite painted = LoadPaintedBackdrop(stage, problems);
        if (painted != null)
            ApplyPaintedBackdrop(scene, painted, problems, false);

        WaveManager waveManager = FindInScene<WaveManager>(scene);
        if (waveManager == null)
        {
            problems.Add(label + ": the stage-5 copy has no WaveManager.");
        }
        else
        {
            // StageWaveLayouts writes this stage's own waves next.
            waveManager.timedLayoutVersion = 0;
            MarkChanged(waveManager);
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    // Painted background of a built stage: Assets/Sprites/Chapter<N>/Backgrounds/stage<S>Bg.png,
    // next to the chapter-2 ones (stage5Bg-stage8Bg).
    public static string PaintedBackdropPath(int stage)
    {
        return "Assets/Sprites/Chapter" + (ChapterData.ChapterOfStage(stage) + 1) + "/Backgrounds/stage" + stage + "Bg.png";
    }

    // Gives every built stage (9-20) its painted background once per PaintedBackdropVersion.
    // A stage open in the editor is updated in place; it is saved unless it has unsaved edits.
    // Returns the scenes that changed.
    public static List<string> ApplyPaintedBackdrops(List<string> problems)
    {
        List<string> applied = new List<string>();
        for (int stage = FirstBuiltStage; stage <= ChapterData.StageCount; stage++)
        {
            string path = "Assets/Scenes/" + GameHubController.GetStageSceneName(stage) + ".unity";
            if (!File.Exists(path))
                continue;   // BuildOutdatedStages reports missing scenes

            string text = File.ReadAllText(path);
            if (!IsReady(stage, text) || ReadInt(BackdropPattern, text, 0) >= PaintedBackdropVersion)
                continue;

            Sprite painted = LoadPaintedBackdrop(stage, problems);
            if (painted == null)
                continue;

            string label = Path.GetFileNameWithoutExtension(path);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool hadUnsavedEdits = wasOpen && scene.isDirty;
            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    problems.Add("Unable to open " + path + " to set its painted background.");
                    continue;
                }
            }

            if (ApplyPaintedBackdrop(scene, painted, problems, wasOpen))
            {
                if (hadUnsavedEdits)
                    problems.Add(label + " is open with unsaved changes, so its painted background is only in the " +
                        "open scene: save it (Ctrl+S) to keep it.");
                else if (EditorSceneManager.SaveScene(scene))
                    applied.Add(label);
                else
                    problems.Add("Unable to save " + path + ".");
            }

            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);
        }
        return applied;
    }

    private static Sprite LoadPaintedBackdrop(int stage, List<string> problems)
    {
        string path = PaintedBackdropPath(stage);
        if (!File.Exists(path))
            return null;   // no painted art for this stage yet: it keeps the generated backdrop

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            problems.Add(path + " is not imported as a sprite: set its Texture Type to Sprite (2D and UI).");
        return sprite;
    }

    private static bool ApplyPaintedBackdrop(Scene scene, Sprite painted, List<string> problems, bool recordUndo)
    {
        EarthEnvironment environment = FindInScene<EarthEnvironment>(scene);
        if (environment == null)
        {
            problems.Add(scene.name + " has no EarthEnvironment backdrop, so its painted background was not set.");
            return false;
        }

        if (recordUndo)
            Undo.RecordObject(environment, "Painted Stage Background");
        environment.background = painted;
        environment.tint = Color.white;
        environment.paintedBackdropVersion = PaintedBackdropVersion;
        MarkChanged(environment);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                GameObject decor = renderer.gameObject;
                if (!decor.name.StartsWith(EarthDecorPrefix) || !decor.activeSelf)
                    continue;
                if (recordUndo)
                    Undo.RecordObject(decor, "Painted Stage Background");
                decor.SetActive(false);
                MarkChanged(decor);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static Look LookFor(int chapter)
    {
        switch (chapter)
        {
            case 2:
                return new Look
                {
                    theme = BackdropTheme.Electric,
                    barrierTint = new Color(0.8f, 0.82f, 1f, 1f),
                    padTint = new Color(0.88f, 0.88f, 1f, 1f),
                    ropeColor = new Color(0.95f, 0.8f, 0.22f, 1f),
                    decorTint = new Color(0.66f, 0.66f, 0.88f, 1f),
                    moteColor = new Color(1f, 0.92f, 0.45f, 0.5f),
                    moteCount = 18
                };
            case 3:
                return new Look
                {
                    theme = BackdropTheme.Water,
                    barrierTint = new Color(0.74f, 0.9f, 1f, 1f),
                    padTint = new Color(0.82f, 0.93f, 1f, 1f),
                    ropeColor = new Color(0.2f, 0.52f, 0.78f, 1f),
                    decorTint = new Color(0.56f, 0.8f, 0.88f, 1f),
                    moteColor = new Color(0.72f, 0.92f, 1f, 0.45f),
                    moteCount = 16
                };
            default:
                return new Look
                {
                    theme = BackdropTheme.Fire,
                    barrierTint = new Color(1f, 0.8f, 0.7f, 1f),
                    padTint = new Color(1f, 0.86f, 0.78f, 1f),
                    ropeColor = new Color(0.6f, 0.2f, 0.1f, 1f),
                    decorTint = new Color(0.88f, 0.56f, 0.46f, 1f),
                    moteColor = new Color(1f, 0.55f, 0.2f, 0.6f),
                    moteCount = 22
                };
        }
    }

    // Saves a change made from code; prefab instances (barrier, pad, Beast) need it recorded as
    // an override or Unity drops it on save.
    private static void MarkChanged(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static int ReadInt(Regex pattern, string text, int fallback)
    {
        Match match = pattern.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : fallback;
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
