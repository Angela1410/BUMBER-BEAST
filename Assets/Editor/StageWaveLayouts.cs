#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// writes timed waves into all 20 stages. each normal wave keeps spawning random enemies from a
// table until its time is up. every enemy's element matches its art:
// Grass: Thorn Pup (Enemy_1, the chapter 1 critter), the only Grass enemy
// Earth: Dust Scout, Burrower, Rollstone (chapter 2 rock creatures)
// Electric: Spark Mite (fast), Volt Beetle (tough), PLACEHOLDER art
// Water: Drip Slime, Shell Crab (tough), PLACEHOLDER art
// Fire: Ember Imp (fast), Magma Brute (tough), PLACEHOLDER art
// each chapter mixes its own element with the one before it, so the Beast you rescued in the last
// chapter still has a job, and each boss stage gives a preview of the next chapter's element:
// Ch1 Grass (stage 4 adds four Earth), Ch2 Earth + Grass (stage 8 adds Electric),
// Ch3 Electric + Earth (12 adds Water), Ch4 Water + Electric (16 adds Fire),
// Ch5 Fire + Water, and the finale mixes everything.
// bosses 12/16/20 (Voltrex, Maelith, Pyrax) get generated boss waves, Thornmaw and Terrathorn
// keep the boss waves their scenes already have.
// it runs by itself after every script reload: makes the placeholder art, builds stages 9-20
// from stage 5 (ChapterBuilder), and rewrites any stage whose waves are older than its layout
// version, once. the menu item does it again on demand. it only changes
// WaveManager.waves / randomizeElements / timedLayoutVersion
[InitializeOnLoad]
public static class StageWaveLayouts
{
    private const string Critter = "Assets/Prefab/Enemy_1.prefab";
    private const string DustScoutPrefab = "Assets/Prefab/Enemy_DustScout.prefab";
    private const string BurrowerPrefab = "Assets/Prefab/Enemy_Burrower.prefab";
    private const string RollstonePrefab = "Assets/Prefab/Enemy_Rollstone.prefab";

    private enum Kind
    {
        ThornPup, DustScout, Burrower, Rollstone,
        SparkMite, VoltBeetle, DripSlime, ShellCrab, EmberImp, MagmaBrute
    }

    private sealed class KindInfo
    {
        public readonly string prefab;
        public readonly ElementType element;
        public readonly string name;
        public readonly string spriteName; // null = use the prefab's own sprite
        public readonly int extraHealth;
        public readonly float speed;

        public KindInfo(string prefab, ElementType element, string name, string spriteName, int extraHealth, float speed)
        {
            this.prefab = prefab;
            this.element = element;
            this.name = name;
            this.spriteName = spriteName;
            this.extraHealth = extraHealth;
            this.speed = speed;
        }
    }

    // placeholder enemies use the Dust Scout's body (its light tint keeps the art's own colors)
    private static readonly Dictionary<Kind, KindInfo> Kinds = new Dictionary<Kind, KindInfo>
    {
        { Kind.ThornPup, new KindInfo(Critter, ElementType.Grass, "Thorn Pup", null, 0, 1f) },
        { Kind.DustScout, new KindInfo(DustScoutPrefab, ElementType.Earth, "Dust Scout", null, 0, 1f) },
        { Kind.Burrower, new KindInfo(BurrowerPrefab, ElementType.Earth, "Burrower", null, 0, 1f) },
        { Kind.Rollstone, new KindInfo(RollstonePrefab, ElementType.Earth, "Rollstone", null, 0, 1f) },
        { Kind.SparkMite, new KindInfo(DustScoutPrefab, ElementType.Electric, "Spark Mite", PlaceholderArtGenerator.SparkMite, 0, 1.25f) },
        { Kind.VoltBeetle, new KindInfo(DustScoutPrefab, ElementType.Electric, "Volt Beetle", "Enemy_VoltBeetle", 2, 0.8f) },
        { Kind.DripSlime, new KindInfo(DustScoutPrefab, ElementType.Water, "Drip Slime", "Enemy_DripSlime", 0, 1f) },
        { Kind.ShellCrab, new KindInfo(DustScoutPrefab, ElementType.Water, "Shell Crab", "Enemy_ShellCrab", 2, 0.78f) },
        { Kind.EmberImp, new KindInfo(DustScoutPrefab, ElementType.Fire, "Ember Imp", "Enemy_EmberImp", 0, 1.3f) },
        { Kind.MagmaBrute, new KindInfo(DustScoutPrefab, ElementType.Fire, "Magma Brute", "Enemy_MagmaBrute", 3, 0.75f) },
    };

    private struct Pick
    {
        public Kind kind;
        public float weight;
        public int fixedCount;
    }

    private static Pick W(Kind kind, float weight) { return new Pick { kind = kind, weight = weight }; }
    private static Pick Fixed(Kind kind, int count) { return new Pick { kind = kind, fixedCount = count }; }

    private sealed class WaveSpec
    {
        public readonly string name;
        public readonly float seconds;
        public readonly Vector2 gap;
        public readonly int maxAlive;
        public readonly Pick[] picks;
        // difficulty set by hand (new stages). if not set, health and speed come from the wave that
        // was in the same slot before (we tuned stages 1-8 by hand)
        public readonly bool tuned;
        public readonly int extraHealth;
        public readonly float speed;

        public WaveSpec(string name, float seconds, float gapMin, float gapMax, int maxAlive, params Pick[] picks)
        {
            this.name = name;
            this.seconds = seconds;
            gap = new Vector2(gapMin, gapMax);
            this.maxAlive = maxAlive;
            this.picks = picks;
            speed = 1f;
        }

        public WaveSpec(string name, float seconds, float gapMin, float gapMax, int maxAlive, int extraHealth, float speed,
            params Pick[] picks)
            : this(name, seconds, gapMin, gapMax, maxAlive, picks)
        {
            tuned = true;
            this.extraHealth = extraHealth;
            this.speed = speed;
        }
    }

    // a generated boss wave: the minions walk in first, the boss comes last
    private sealed class BossSpec
    {
        public readonly string name;
        public readonly ElementType element;
        public readonly Kind minion;
        public readonly int minionCount;
        public readonly int extraHealth;
        public readonly float spawnInterval;

        public BossSpec(string name, ElementType element, Kind minion, int minionCount, int extraHealth, float spawnInterval)
        {
            this.name = name;
            this.element = element;
            this.minion = minion;
            this.minionCount = minionCount;
            this.extraHealth = extraHealth;
            this.spawnInterval = spawnInterval;
        }
    }

    private sealed class StageSpec
    {
        public readonly int stage;
        public readonly int version; // change this to rewrite this stage's waves once
        public readonly BossSpec boss; // null: keep the scene's own boss waves
        public readonly WaveSpec[] waves;

        public StageSpec(int stage, int version, BossSpec boss, params WaveSpec[] waves)
        {
            this.stage = stage;
            this.version = version;
            this.boss = boss;
            this.waves = waves;
        }

        public string ScenePath => "Assets/Scenes/" + GameHubController.GetStageSceneName(stage) + ".unity";
    }

    private static readonly StageSpec[] Stages =
    {
        // ---- Chapter 1: Thorn Pups only, slow pace (just Choma, Fire beats Grass) ----
        // stage 1 is for learning: Pups have 2 HP (one Choma hit), walk slower and come one or two
        // at a time. stage 2 keeps that for two waves, stage 3 brings back full strength Pups a bit
        // slower, and from stage 4 the normal pace stays
        new StageSpec(1, 4, null,
            new WaveSpec("Thorn Pups", 18f, 3.4f, 4.2f, 2, -2, 0.68f, W(Kind.ThornPup, 1f)),
            new WaveSpec("Pup Pack", 22f, 3.2f, 4.0f, 3, -2, 0.74f, W(Kind.ThornPup, 1f)),
            new WaveSpec("Thicket Rush", 25f, 3.0f, 3.8f, 3, -2, 0.8f, W(Kind.ThornPup, 1f))),
        new StageSpec(2, 4, null,
            new WaveSpec("Thorn Pups", 22f, 3.0f, 3.8f, 3, -2, 0.72f, W(Kind.ThornPup, 1f)),
            new WaveSpec("Pup Pack", 26f, 2.8f, 3.6f, 3, -2, 0.8f, W(Kind.ThornPup, 1f)),
            new WaveSpec("Overgrowth", 30f, 2.6f, 3.4f, 4, 0, 0.9f, W(Kind.ThornPup, 1f))),
        new StageSpec(3, 4, null,
            new WaveSpec("Thorn Pups", 28f, 2.6f, 3.4f, 4, 0, 0.75f, W(Kind.ThornPup, 1f)),
            new WaveSpec("Bramble Pack", 32f, 2.4f, 3.2f, 4, 0, 0.86f, W(Kind.ThornPup, 1f)),
            new WaveSpec("Wild Thicket", 36f, 2.2f, 3.0f, 5, 0, 0.95f, W(Kind.ThornPup, 1f))),
        new StageSpec(4, 2, null,
            new WaveSpec("Thicket Rush", 30f, 2.2f, 3.0f, 4, W(Kind.ThornPup, 1f)),
            new WaveSpec("Strange Tracks", 30f, 2.2f, 3.0f, 4,
                W(Kind.ThornPup, 1f), Fixed(Kind.DustScout, 2), Fixed(Kind.Burrower, 2))),

        // ---- Chapter 2: Earth + Grass (Thistle beats Earth, Choma beats Grass) ----
        new StageSpec(5, 2, null,
            new WaveSpec("Dust Scouts", 30f, 2.2f, 3.0f, 4, W(Kind.DustScout, 3f), W(Kind.ThornPup, 1f)),
            new WaveSpec("Thorn Pack", 35f, 2.1f, 2.9f, 5, W(Kind.ThornPup, 3f), W(Kind.DustScout, 1f)),
            new WaveSpec("Tunnel Ambush", 40f, 2.0f, 2.7f, 5,
                W(Kind.DustScout, 2f), W(Kind.Burrower, 1f), W(Kind.ThornPup, 2f))),
        new StageSpec(6, 2, null,
            new WaveSpec("Burrowers", 35f, 2.0f, 2.8f, 5, W(Kind.Burrower, 2f), W(Kind.DustScout, 1f), W(Kind.ThornPup, 1f)),
            new WaveSpec("Thorn Stragglers", 35f, 2.0f, 2.8f, 5, W(Kind.ThornPup, 3f), W(Kind.Burrower, 1f)),
            new WaveSpec("Rockslide", 45f, 1.9f, 2.6f, 6,
                Fixed(Kind.Rollstone, 3), W(Kind.DustScout, 2f), W(Kind.ThornPup, 2f))),
        new StageSpec(7, 2, null,
            new WaveSpec("Scout Swarm", 40f, 1.8f, 2.5f, 6, W(Kind.DustScout, 2f), W(Kind.ThornPup, 2f)),
            new WaveSpec("Boulderbacks", 40f, 2.0f, 2.7f, 5, W(Kind.Rollstone, 1f), W(Kind.Burrower, 1f), W(Kind.ThornPup, 1f)),
            new WaveSpec("Stampede", 50f, 1.7f, 2.4f, 7,
                W(Kind.Rollstone, 1f), W(Kind.DustScout, 1f), W(Kind.Burrower, 1f), W(Kind.ThornPup, 2f))),
        // boss stage: three Electric Spark Mites (placeholder art) come before Terrathorn
        new StageSpec(8, 3, null,
            new WaveSpec("Last Tunnel", 45f, 1.8f, 2.5f, 6,
                W(Kind.DustScout, 1f), W(Kind.Burrower, 1f), W(Kind.Rollstone, 1f), W(Kind.ThornPup, 2f),
                Fixed(Kind.SparkMite, 3))),

        // ---- Chapter 3: Electric + Earth (Mossback beats Electric, Thistle beats Earth) ----
        new StageSpec(9, 1, null,
            new WaveSpec("Spark Storm", 40f, 2.1f, 2.8f, 5, 0, 1f, W(Kind.SparkMite, 3f), W(Kind.DustScout, 1f)),
            new WaveSpec("Rock and Volt", 40f, 2.0f, 2.7f, 5, 0, 1f,
                W(Kind.SparkMite, 2f), W(Kind.Burrower, 1f), W(Kind.DustScout, 1f)),
            new WaveSpec("Beetle March", 45f, 1.9f, 2.6f, 6, 0, 1f,
                Fixed(Kind.VoltBeetle, 3), W(Kind.SparkMite, 2f), W(Kind.DustScout, 2f))),
        new StageSpec(10, 1, null,
            new WaveSpec("Charged Scouts", 40f, 2.0f, 2.7f, 5, 0, 1f, W(Kind.SparkMite, 2f), W(Kind.DustScout, 2f)),
            new WaveSpec("Volt Beetles", 45f, 2.0f, 2.7f, 5, 0, 1f,
                W(Kind.VoltBeetle, 2f), W(Kind.Rollstone, 1f), W(Kind.SparkMite, 1f)),
            new WaveSpec("Thunderhead", 50f, 1.9f, 2.5f, 6, 0, 1.02f,
                W(Kind.SparkMite, 2f), W(Kind.VoltBeetle, 1f), W(Kind.Burrower, 1f), W(Kind.Rollstone, 1f))),
        new StageSpec(11, 1, null,
            new WaveSpec("Spark Swarm", 45f, 1.8f, 2.4f, 6, 0, 1.02f, W(Kind.SparkMite, 3f), W(Kind.Burrower, 1f)),
            new WaveSpec("Storm Rockslide", 45f, 1.9f, 2.5f, 6, 0, 1.02f,
                Fixed(Kind.Rollstone, 3), W(Kind.VoltBeetle, 1f), W(Kind.SparkMite, 2f), W(Kind.DustScout, 1f)),
            new WaveSpec("Overload", 55f, 1.8f, 2.4f, 7, 1, 1.04f,
                W(Kind.SparkMite, 2f), W(Kind.VoltBeetle, 1f), W(Kind.DustScout, 1f), W(Kind.Burrower, 1f), W(Kind.Rollstone, 1f))),
        // boss stage: three Drip Slimes preview the Water chapter, then Voltrex
        new StageSpec(12, 1, new BossSpec("Voltrex", ElementType.Electric, Kind.SparkMite, 6, 1, 1.2f),
            new WaveSpec("Storm Front", 45f, 1.9f, 2.5f, 6, 1, 1.04f,
                W(Kind.SparkMite, 2f), W(Kind.VoltBeetle, 1f), W(Kind.DustScout, 1f), Fixed(Kind.DripSlime, 3))),

        // ---- Chapter 4: Water + Electric (Voltis beats Water, Mossback beats Electric) ----
        new StageSpec(13, 1, null,
            new WaveSpec("Drip Drop", 40f, 2.1f, 2.8f, 5, 1, 1.03f, W(Kind.DripSlime, 3f), W(Kind.SparkMite, 1f)),
            new WaveSpec("Tide Pools", 45f, 2.0f, 2.7f, 5, 1, 1.03f,
                W(Kind.DripSlime, 2f), W(Kind.VoltBeetle, 1f), W(Kind.SparkMite, 1f)),
            new WaveSpec("Shell Wall", 45f, 1.9f, 2.6f, 6, 1, 1.03f,
                Fixed(Kind.ShellCrab, 3), W(Kind.DripSlime, 2f), W(Kind.SparkMite, 1f))),
        new StageSpec(14, 1, null,
            new WaveSpec("Slime Tide", 45f, 1.9f, 2.6f, 6, 1, 1.04f, W(Kind.DripSlime, 3f), W(Kind.SparkMite, 2f)),
            new WaveSpec("Crab Line", 45f, 2.0f, 2.6f, 5, 1, 1.04f,
                W(Kind.ShellCrab, 2f), W(Kind.DripSlime, 1f), W(Kind.VoltBeetle, 1f)),
            new WaveSpec("Undertow", 50f, 1.8f, 2.4f, 7, 1, 1.05f,
                W(Kind.DripSlime, 2f), W(Kind.ShellCrab, 1f), W(Kind.SparkMite, 2f), W(Kind.VoltBeetle, 1f))),
        new StageSpec(15, 1, null,
            new WaveSpec("Riptide", 45f, 1.8f, 2.4f, 6, 1, 1.05f, W(Kind.DripSlime, 3f), W(Kind.SparkMite, 2f)),
            new WaveSpec("Static Surf", 50f, 1.8f, 2.4f, 6, 1, 1.05f,
                W(Kind.SparkMite, 2f), W(Kind.VoltBeetle, 1f), W(Kind.DripSlime, 2f)),
            new WaveSpec("Flood", 55f, 1.7f, 2.3f, 7, 1, 1.06f,
                Fixed(Kind.ShellCrab, 3), W(Kind.DripSlime, 2f), W(Kind.SparkMite, 1f), W(Kind.VoltBeetle, 1f))),
        // boss stage: three Ember Imps preview the Fire chapter, then Maelith
        new StageSpec(16, 1, new BossSpec("Maelith", ElementType.Water, Kind.DripSlime, 6, 1, 1.2f),
            new WaveSpec("Grotto Guard", 50f, 1.8f, 2.4f, 6, 1, 1.06f,
                W(Kind.DripSlime, 2f), W(Kind.ShellCrab, 1f), W(Kind.SparkMite, 1f), Fixed(Kind.EmberImp, 3))),

        // ---- Chapter 5: Fire + Water (Ripple beats Fire, Voltis beats Water) ----
        new StageSpec(17, 1, null,
            new WaveSpec("Ember Rush", 40f, 2.0f, 2.7f, 5, 1, 1.06f, W(Kind.EmberImp, 3f), W(Kind.DripSlime, 1f)),
            new WaveSpec("Steam Vents", 45f, 1.9f, 2.6f, 6, 1, 1.06f, W(Kind.EmberImp, 2f), W(Kind.DripSlime, 2f)),
            new WaveSpec("Magma March", 45f, 1.9f, 2.6f, 6, 1, 1.06f,
                Fixed(Kind.MagmaBrute, 3), W(Kind.EmberImp, 2f), W(Kind.ShellCrab, 1f))),
        new StageSpec(18, 1, null,
            new WaveSpec("Ash Swarm", 45f, 1.8f, 2.5f, 6, 1, 1.07f, W(Kind.EmberImp, 3f), W(Kind.ThornPup, 1f)),
            new WaveSpec("Brute Squad", 45f, 1.9f, 2.6f, 6, 1, 1.07f,
                W(Kind.MagmaBrute, 2f), W(Kind.EmberImp, 1f), W(Kind.DripSlime, 1f)),
            new WaveSpec("Firestorm", 50f, 1.7f, 2.3f, 7, 1, 1.08f,
                W(Kind.EmberImp, 2f), W(Kind.MagmaBrute, 1f), W(Kind.ShellCrab, 1f), W(Kind.DripSlime, 1f))),
        new StageSpec(19, 1, null,
            new WaveSpec("Lava Flow", 45f, 1.7f, 2.3f, 6, 1, 1.08f, W(Kind.EmberImp, 3f), W(Kind.DripSlime, 2f)),
            new WaveSpec("Tide and Flame", 50f, 1.7f, 2.3f, 7, 1, 1.08f,
                W(Kind.ShellCrab, 1f), W(Kind.DripSlime, 1f), W(Kind.EmberImp, 2f), W(Kind.MagmaBrute, 1f)),
            new WaveSpec("Inferno", 55f, 1.6f, 2.2f, 7, 2, 1.1f,
                Fixed(Kind.MagmaBrute, 3), W(Kind.EmberImp, 2f), W(Kind.DripSlime, 1f), W(Kind.ShellCrab, 1f))),
        // the finale: every element at once, then Pyrax
        new StageSpec(20, 1, new BossSpec("Pyrax", ElementType.Fire, Kind.EmberImp, 6, 2, 1.1f),
            new WaveSpec("Last Stand", 55f, 1.6f, 2.2f, 7, 2, 1.1f,
                W(Kind.EmberImp, 2f), W(Kind.MagmaBrute, 1f), W(Kind.DripSlime, 1f), W(Kind.SparkMite, 1f),
                W(Kind.DustScout, 1f), W(Kind.ThornPup, 1f)))
    };

    private static readonly Regex VersionPattern =
        new Regex(@"^\s*timedLayoutVersion:\s*(\d+)", RegexOptions.Multiline);

    static StageWaveLayouts()
    {
        if (Application.isBatchMode)
            return;
        EditorApplication.update += AutoApplyWhenIdle;
    }

    // runs once after each script reload, as soon as the editor is idle and not playing
    private static void AutoApplyWhenIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        EditorApplication.update -= AutoApplyWhenIdle;
        ApplyPendingChanges();
    }

    // everything the editor does by itself after a script reload (placeholder art, stages 9-20,
    // their painted backgrounds, stage waves). the build script calls it too, because batch mode
    // skips the hook. every step has a version, so running it again changes nothing
    public static void ApplyPendingChanges()
    {
        List<string> problems = new List<string>();
        PlaceholderArtGenerator.EnsureAll(problems);
        List<string> rebuilt = ChapterBuilder.BuildOutdatedStages(problems, false);
        List<string> painted = ChapterBuilder.ApplyPaintedBackdrops(problems);
        if (painted.Count > 0)
            Debug.Log("Bumper Beast: painted backgrounds set on " + string.Join(", ", painted) +
                " (their generated backdrop and the stage-5 rock decorations are switched off).");

        List<StageSpec> outdated = new List<StageSpec>();
        foreach (StageSpec stage in Stages)
        {
            string path = stage.ScenePath;
            if (!File.Exists(path))
                continue;
            string text = File.ReadAllText(path);
            if (ReadVersion(text) < stage.version && ChapterBuilder.IsReady(stage.stage, text))
                outdated.Add(stage);
        }

        if (outdated.Count > 0 || rebuilt.Count > 0 || problems.Count > 0)
            Run(outdated, "updated automatically", problems, rebuilt);
    }

    private static int ReadVersion(string sceneText)
    {
        Match match = VersionPattern.Match(sceneText);
        return match.Success && int.TryParse(match.Groups[1].Value, out int version) ? version : 0;
    }

    [MenuItem("Bumper Beast/Apply Timed Waves (All Stages)")]
    public static void ApplyFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Bumper Beast: leave Play Mode before changing the stage waves.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        List<string> problems = new List<string>();
        List<string> rebuilt = ChapterBuilder.BuildOutdatedStages(problems, false);
        List<StageSpec> ready = new List<StageSpec>();
        foreach (StageSpec stage in Stages)
        {
            string path = stage.ScenePath;
            if (File.Exists(path) && ChapterBuilder.IsReady(stage.stage, File.ReadAllText(path)))
                ready.Add(stage);
        }
        Run(ready, "applied", problems, rebuilt);
    }

    [MenuItem("Bumper Beast/Rebuild Stages 9-20 From Stage 5")]
    public static void RebuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Bumper Beast: leave Play Mode before rebuilding stages.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Rebuild stages 9-20?",
                "Stages 9-20 are replaced by copies of stage 5's layout (themed per chapter) with new waves. " +
                "Any hand edits in those twelve scenes are lost. Stages 1-8 are not touched.", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        List<string> problems = new List<string>();
        List<string> rebuilt = ChapterBuilder.BuildOutdatedStages(problems, true);
        List<StageSpec> stages = new List<StageSpec>();
        foreach (StageSpec stage in Stages)
        {
            if (stage.stage >= ChapterBuilder.FirstBuiltStage && File.Exists(stage.ScenePath) &&
                ChapterBuilder.IsReady(stage.stage, File.ReadAllText(stage.ScenePath)))
                stages.Add(stage);
        }
        Run(stages, "rebuilt", problems, rebuilt);
    }

    private static void Run(List<StageSpec> stages, string verb, List<string> problems, List<string> rebuilt)
    {
        Dictionary<string, Enemy> prefabs = LoadPrefabs(problems);
        Dictionary<string, Sprite> sprites = prefabs.Count == 4 ? LoadSprites(problems) : null;
        if (prefabs.Count < 4 || sprites == null)
        {
            Debug.LogError("Bumper Beast: stage waves NOT changed.\n" + string.Join("\n", problems));
            return;
        }

        StringBuilder report = new StringBuilder();
        int scenesSaved = 0;
        foreach (StageSpec stage in stages)
        {
            string path = stage.ScenePath;
            if (!File.Exists(path))
            {
                problems.Add(path + " is missing.");
                continue;
            }

            // if a stage is already open we edit it there (we never close or reload it on you)
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool hadUnsavedEdits = wasOpen && scene.isDirty;
            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    problems.Add("Unable to open " + path + ".");
                    continue;
                }
            }

            string stageName = Path.GetFileNameWithoutExtension(path);
            WaveManager waveManager = FindInScene<WaveManager>(scene);
            if (waveManager == null)
            {
                problems.Add(path + " has no WaveManager.");
            }
            else
            {
                if (wasOpen)
                    Undo.RecordObject(waveManager, "Apply Timed Waves");
                WriteWaves(stage, waveManager, prefabs, sprites);
                EditorUtility.SetDirty(waveManager);
                EditorSceneManager.MarkSceneDirty(scene);
                report.Append('\n').Append(stageName).Append(": ").Append(Describe(waveManager.waves));

                if (hadUnsavedEdits)
                    problems.Add(stageName + " is open with unsaved changes, so its new waves are only in the open " +
                        "scene: save it (Ctrl+S) to keep them.");
                else if (EditorSceneManager.SaveScene(scene))
                    scenesSaved++;
                else
                    problems.Add("Unable to save " + path + ".");
            }

            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);
        }

        string summary = "Bumper Beast: timed waves " + verb + ", scenes saved: " + scenesSaved +
            (rebuilt.Count > 0 ? ". Stages built from the stage-5 layout: " + string.Join(", ", rebuilt) : string.Empty) +
            "." + report;
        if (problems.Count == 0)
            Debug.Log(summary);
        else
            Debug.LogWarning(summary + "\n\nNeeds attention:\n" + string.Join("\n", problems));
    }

    private static Dictionary<string, Enemy> LoadPrefabs(List<string> problems)
    {
        Dictionary<string, Enemy> prefabs = new Dictionary<string, Enemy>();
        foreach (string path in new[] { Critter, DustScoutPrefab, BurrowerPrefab, RollstonePrefab })
        {
            Enemy enemy = AssetDatabase.LoadAssetAtPath<Enemy>(path);
            if (enemy == null)
                problems.Add(path + " is missing or has no Enemy component (run Bumper Beast/Wire Chapter 2 Art first).");
            else
                prefabs[path] = enemy;
        }
        return prefabs;
    }

    private static Dictionary<string, Sprite> LoadSprites(List<string> problems)
    {
        Dictionary<string, Sprite> sprites = PlaceholderArtGenerator.EnsureAll(problems);
        bool complete = true;
        foreach (KindInfo info in Kinds.Values)
        {
            if (info.spriteName != null && !sprites.ContainsKey(info.spriteName))
            {
                problems.Add("No placeholder sprite " + info.spriteName + " (needed for " + info.name + ").");
                complete = false;
            }
        }
        return complete ? sprites : null;
    }

    private static void WriteWaves(StageSpec stage, WaveManager waveManager, Dictionary<string, Enemy> prefabs,
        Dictionary<string, Sprite> sprites)
    {
        EnemyWave[] oldWaves = waveManager.waves ?? new EnemyWave[0];
        List<EnemyWave> regular = new List<EnemyWave>();
        List<EnemyWave> bosses = new List<EnemyWave>();
        Dictionary<Enemy, float> sceneScales = new Dictionary<Enemy, float>();
        foreach (EnemyWave wave in oldWaves)
        {
            if (wave == null)
                continue;
            if (IsBossWave(wave))
            {
                MatchMinionElementsToArt(wave, prefabs[Critter]);
                bosses.Add(wave);
                continue;
            }
            regular.Add(wave);
            CollectSceneScales(wave, sceneScales);
        }

        List<EnemyWave> newWaves = new List<EnemyWave>();
        for (int index = 0; index < stage.waves.Length; index++)
        {
            // health / speed tuning comes from the wave that was in this slot before
            EnemyWave tuning = index < regular.Count ? regular[index]
                : regular.Count > 0 ? regular[regular.Count - 1] : null;
            newWaves.Add(BuildWave(stage.waves[index], tuning, prefabs, sprites, sceneScales));
        }

        if (stage.boss != null)
            newWaves.Add(BuildBossWave(stage.boss, prefabs, sprites));
        else
            newWaves.AddRange(bosses);

        waveManager.waves = newWaves.ToArray();
        // elements come from each enemy kind, random ones would break the art/element match
        waveManager.randomizeElements = false;
        waveManager.timedLayoutVersion = stage.version;
    }

    // boss wave minions made from the shared critter are Thorn Pups, which look like Grass, so they're
    // set to Grass even if the boss is another element (Terrathorn is Earth). the boss keeps the wave's
    // element, and prefabs, counts and the boss itself don't change
    private static void MatchMinionElementsToArt(EnemyWave wave, Enemy critter)
    {
        if (wave.element == ElementType.Grass || wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
            return;
        foreach (Enemy prefab in wave.enemyPrefabs)
        {
            if (prefab != critter)
                return; // other art: leave its elements alone
        }

        int count = Mathf.Max(1, wave.enemyCount);
        ElementType[] pattern = new ElementType[count];
        for (int i = 0; i < count; i++)
            pattern[i] = ElementType.Grass;
        int bossIndex = wave.bossSpawnIndex >= 0 && wave.bossSpawnIndex < count ? wave.bossSpawnIndex
            : wave.bossSpawnsLast || wave.isBoss ? count - 1
            : -1;
        if (bossIndex >= 0)
            pattern[bossIndex] = wave.element;
        wave.elementPattern = pattern;
    }

    // keeps any size the scene already used per prefab (like Rollstones at 0.85), whether it came
    // from an older wave or a previous timed layout
    private static void CollectSceneScales(EnemyWave wave, Dictionary<Enemy, float> sceneScales)
    {
        if (wave.IsTimed)
        {
            foreach (EnemySpawnEntry entry in wave.spawnTable)
            {
                if (entry != null && entry.prefab != null && entry.spriteOverride == null &&
                    entry.scale > 0.01f && !sceneScales.ContainsKey(entry.prefab))
                    sceneScales[entry.prefab] = entry.scale;
            }
        }
        else if (wave.enemyPrefabs != null && wave.enemyPrefabs.Length == 1 && wave.enemyPrefabs[0] != null &&
            wave.enemySprite == null && wave.enemyScale > 0.01f && !sceneScales.ContainsKey(wave.enemyPrefabs[0]))
        {
            sceneScales[wave.enemyPrefabs[0]] = wave.enemyScale;
        }
    }

    private static EnemyWave BuildWave(WaveSpec spec, EnemyWave tuning, Dictionary<string, Enemy> prefabs,
        Dictionary<string, Sprite> sprites, Dictionary<Enemy, float> sceneScales)
    {
        EnemySpawnEntry[] table = new EnemySpawnEntry[spec.picks.Length];
        List<Enemy> distinctPrefabs = new List<Enemy>();
        List<ElementType> distinctElements = new List<ElementType>();
        for (int i = 0; i < spec.picks.Length; i++)
        {
            KindInfo info = Kinds[spec.picks[i].kind];
            Enemy prefab = prefabs[info.prefab];
            table[i] = new EnemySpawnEntry
            {
                displayName = info.name,
                prefab = prefab,
                element = info.element,
                fixedCount = spec.picks[i].fixedCount,
                weight = spec.picks[i].fixedCount > 0 ? 0f : spec.picks[i].weight,
                spriteOverride = info.spriteName != null ? sprites[info.spriteName] : null,
                scale = info.spriteName == null && sceneScales.TryGetValue(prefab, out float scale) ? scale : 1f,
                extraHealth = info.extraHealth,
                speedMultiplier = info.speed
            };
            if (info.spriteName == null && !distinctPrefabs.Contains(prefab))
            {
                distinctPrefabs.Add(prefab);
                distinctElements.Add(info.element);
            }
        }
        if (distinctPrefabs.Count == 0)
        {
            distinctPrefabs.Add(table[0].prefab);
            distinctElements.Add(table[0].element);
        }

        float averageGap = (spec.gap.x + spec.gap.y) * 0.5f;
        return new EnemyWave
        {
            waveName = spec.name,
            spawnDuration = spec.seconds,
            spawnTable = table,
            spawnGap = spec.gap,
            maxAlive = spec.maxAlive,
            // old fallback, only used if Spawn Duration gets set back to 0 in the Inspector
            enemyPrefabs = distinctPrefabs.ToArray(),
            elementPattern = distinctElements.ToArray(),
            element = distinctElements[0],
            enemyCount = Mathf.Max(1, Mathf.RoundToInt(spec.seconds / averageGap)),
            spawnInterval = !spec.tuned && tuning != null ? tuning.spawnInterval : averageGap,
            extraHealth = spec.tuned ? spec.extraHealth : tuning != null ? tuning.extraHealth : 0,
            speedMultiplier = spec.tuned ? spec.speed : tuning != null ? tuning.speedMultiplier : 1f,
            enemySprite = null,
            enemyScale = 1f
        };
    }

    private static EnemyWave BuildBossWave(BossSpec boss, Dictionary<string, Enemy> prefabs, Dictionary<string, Sprite> sprites)
    {
        KindInfo minion = Kinds[boss.minion];
        int count = boss.minionCount + 1;
        ElementType[] pattern = new ElementType[count];
        for (int i = 0; i < count; i++)
            pattern[i] = minion.element;
        pattern[count - 1] = boss.element;

        return new EnemyWave
        {
            waveName = boss.name + " Boss Wave",
            enemyPrefabs = new[] { prefabs[minion.prefab] },
            element = boss.element,
            enemyCount = count,
            spawnInterval = boss.spawnInterval,
            extraHealth = boss.extraHealth,
            speedMultiplier = 1f,
            isBoss = false,
            bossSpawnsLast = true,
            bossSpawnIndex = -1,
            bossHealthBonus = 2,
            bossName = boss.name,
            enemySprite = minion.spriteName != null ? sprites[minion.spriteName] : null,
            enemyScale = 1f,
            enemyDisplayName = minion.name,
            elementPattern = pattern,
            spawnDuration = 0f,
            spawnTable = new EnemySpawnEntry[0]
        };
    }

    private static string Describe(EnemyWave[] waves)
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < waves.Length; i++)
        {
            EnemyWave wave = waves[i];
            if (i > 0)
                text.Append(", ");
            text.Append(wave.waveName);
            text.Append(wave.IsTimed ? " (" + Mathf.RoundToInt(wave.spawnDuration) + "s)" : " (boss, x" + wave.enemyCount + ")");
        }
        return text.ToString();
    }

    private static bool IsBossWave(EnemyWave wave)
    {
        return wave.isBoss || wave.bossSpawnsLast || wave.bossSpawnIndex >= 0 ||
            !string.IsNullOrWhiteSpace(wave.bossName);
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
