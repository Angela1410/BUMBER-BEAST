using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class BumperBeastProjectSetup
{
    private const string EnemyPrefabPath = "Assets/Enemy_1.prefab";
    private const string BumperPrefabPath = "Assets/Bumper_Left.prefab";
    private const string ChomaPhysicsPath = "Assets/materials/ChomaBounce.physicsMaterial2D";
    private const string EnemyPhysicsPath = "Assets/materials/EnemyNoBounce.physicsMaterial2D";
    private const string ActiveSceneSetupKey = "BumperBeast.ActiveSceneSetupComplete";
    private static Sprite bumperSprite;

    static BumperBeastProjectSetup()
    {
        EditorApplication.delayCall += SetupActiveSceneOnce;
    }

    private static void SetupActiveSceneOnce()
    {
        if (Application.isBatchMode || SessionState.GetBool(ActiveSceneSetupKey, false))
            return;

        SessionState.SetBool(ActiveSceneSetupKey, true);

        if (!EditorSceneManager.GetActiveScene().IsValid())
            return;

        SetupEnemyPrefab();
        SetupScene(EditorSceneManager.GetActiveScene(), EditorSceneManager.GetActiveScene().path);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Bumper Beast/Build Complete Wave Gameplay")]
    public static void BuildAllStages()
    {
        SetupEnemyPrefab();

        string[] scenePaths = Directory.GetFiles("Assets/Scenes", "*.unity");
        System.Array.Sort(scenePaths);

        foreach (string scenePath in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SetupScene(scene, scenePath);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Bumper Beast wave gameplay was added to all stages.");
    }

    [MenuItem("Bumper Beast/Setup Active Stage")]
    public static void SetupActiveStage()
    {
        SetupEnemyPrefab();
        Scene activeScene = EditorSceneManager.GetActiveScene();
        SetupScene(activeScene, activeScene.path);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Bumper Beast gameplay was added to the active stage.");
    }

    [MenuItem("Bumper Beast/Repair Grass Stages 1-4")]
    public static void RepairGrassStages()
    {
        SetupEnemyPrefab();

        for (int stageNumber = 1; stageNumber <= 4; stageNumber++)
        {
            string scenePath = "Assets/Scenes/stage-" + stageNumber + ".unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError("Missing Grass stage scene: " + scenePath);
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SetupScene(scene, scenePath);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Grass stages 1-4 were repaired.");
    }

    [MenuItem("Bumper Beast/Copy Stage 1 Bumper Setup To Stages 2-4")]
    public static void CopyStageOneBumperSetupToStagesTwoThroughFour()
    {
        for (int stageNumber = 2; stageNumber <= 4; stageNumber++)
        {
            string scenePath = "Assets/Scenes/stage-" + stageNumber + ".unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError("Missing Grass stage scene: " + scenePath);
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureArenaBumpers(scenePath);
            EnsureBumperRespawnSetup(scenePath);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Stage 1 bumper gameplay setup was copied to stages 2-4.");
    }

    [MenuItem("Bumper Beast/Apply Stage Layouts 1-4")]
    public static void ApplyCoreStageLayouts()
    {
        for (int stageNumber = 1; stageNumber <= 4; stageNumber++)
        {
            string scenePath = "Assets/Scenes/stage-" + stageNumber + ".unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError("Missing level layout scene: " + scenePath);
                continue;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureArenaBumpers(scenePath);
            EnsureBumperRespawnSetup(scenePath);

            Transform[] waypoints = EnsureGrassEnemyRoute(scenePath);
            WaveManager waveManager = Object.FindAnyObjectByType<WaveManager>();
            if (waveManager == null)
            {
                Debug.LogError("No WaveManager found in " + scenePath + ".");
                continue;
            }

            waveManager.routeWaypoints = waypoints;
            waveManager.useCenterLanes = false;
            waveManager.randomizeFormation = false;
            waveManager.randomizeSpawnBursts = false;
            EditorUtility.SetDirty(waveManager);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Straightaway, Weave, S-Curve, and Fortress layouts were applied to stages 1-4.");
    }

    [MenuItem("Bumper Beast/Apply Stage 4 Route and Bumper Colors")]
    public static void ApplyStage4RouteAndBumperColors()
    {
        string scenePath = "Assets/Scenes/stage-4.unity";
        if (!File.Exists(scenePath))
        {
            Debug.LogError("Missing Stage 4 scene: " + scenePath);
            return;
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EnsureStartingBumperLayout(4);

        Transform[] waypoints = EnsureGrassEnemyRoute(scenePath);
        WaveManager waveManager = Object.FindAnyObjectByType<WaveManager>();
        if (waveManager == null)
        {
            Debug.LogError("No WaveManager found in " + scenePath + ".");
            return;
        }

        waveManager.routeWaypoints = waypoints;
        waveManager.useCenterLanes = false;
        EditorUtility.SetDirty(waveManager);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Stage 4 route and consistent bumper colors were applied.");
    }

    [MenuItem("Bumper Beast/Build Full Game")]
    public static void BuildFullGame()
    {
        BuildAllStages();
        RegisterAllScenes();
        CreateHubScene();
        SetPortraitSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Bumper Beast full game map, shop, progression, and stages are ready.");
    }

    private static void SetupEnemyPrefab()
    {
        PhysicsMaterial2D enemyPhysics = GetPhysicsMaterial(EnemyPhysicsPath, "EnemyNoBounce", 0f, 0f);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);

        if (prefabRoot.GetComponent<Enemy>() == null)
            prefabRoot.AddComponent<Enemy>();

        ThornmawAbility thornmawAbility = prefabRoot.GetComponent<ThornmawAbility>();
        if (thornmawAbility == null)
            thornmawAbility = prefabRoot.AddComponent<ThornmawAbility>();

        thornmawAbility.bramblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BumperPrefabPath);
        if (thornmawAbility.bramblePrefab == null)
            Debug.LogError("Thornmaw setup requires " + BumperPrefabPath + ".");

        Rigidbody2D body = prefabRoot.GetComponent<Rigidbody2D>();

        if (body == null)
            body = prefabRoot.AddComponent<Rigidbody2D>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        prefabRoot.tag = "Enemy";

        Collider2D enemyCollider = prefabRoot.GetComponent<Collider2D>();
        if (enemyCollider != null)
        {
            enemyCollider.isTrigger = false;
            enemyCollider.sharedMaterial = enemyPhysics;
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static void SetupScene(Scene scene, string scenePath)
    {
        int stageNumber = GetStageNumber(scenePath);
        bool grassStage = stageNumber >= 1 && stageNumber <= 4;
        RemoveBossObjectsFromScene();

        if (grassStage)
            RemoveTestBumpers(scene);

        LaunchController launcher = Object.FindAnyObjectByType<LaunchController>();

        if (launcher == null)
        {
            Debug.LogWarning("No LaunchController found in " + scenePath);
            return;
        }

        launcher.unlimitedLaunches = true;
        launcher.launchesLeft = launcher.maxLaunches;
        launcher.launchPower = 3.6f;
        launcher.maxDragDistance = 2.5f;
        launcher.maxLaunchSpeed = 9f;
        RemoveLaunchCounter();
        HideSeparateBeastDisplays(launcher.gameObject);
        if (!grassStage)
        {
            launcher.transform.position = new Vector3(0f, -7.1f, 0f);
            ConfigurePortraitArena();
        }
        EnsureArenaBumpers(scenePath);
        if (grassStage)
            EnsureBumperRespawnSetup(scenePath);

        GameObject barrierObject = GameObject.Find("Square");
        Collider2D barrierCollider = barrierObject != null
            ? barrierObject.GetComponent<Collider2D>()
            : null;

        if (barrierObject != null)
        {
            SpriteRenderer barrierRenderer = barrierObject.GetComponent<SpriteRenderer>();
            if (barrierRenderer != null && barrierRenderer.sprite == null)
                barrierRenderer.sprite = CreateSolidSquareSprite(new Color(0.7f, 0.72f, 0.82f, 1f));
        }

        if (barrierCollider != null)
            barrierCollider.isTrigger = true;

        ConfigureScenePhysics(launcher, GetPhysicsMaterial(ChomaPhysicsPath, "ChomaBounce", 0.35f, 0f),
            GetPhysicsMaterial(EnemyPhysicsPath, "EnemyNoBounce", 0f, 0f));

        BeastController beast = launcher.GetComponent<BeastController>();

        if (beast == null)
            beast = launcher.gameObject.AddComponent<BeastController>();

        Transform launchPad = FindOrCreateLaunchPad(launcher.transform);
        beast.launchPad = launchPad;

        PadSwapSystem padSwap = launcher.GetComponent<PadSwapSystem>();
        if (padSwap == null)
            padSwap = launcher.gameObject.AddComponent<PadSwapSystem>();
        padSwap.beast = beast;

        WaveManager waveManager = Object.FindAnyObjectByType<WaveManager>();

        if (waveManager == null)
            waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();

        Transform spawnPoint = FindOrCreateSpawnPoint();
        waveManager.spawnPoint = spawnPoint;
        waveManager.routeWaypoints = EnsureGrassEnemyRoute(scenePath);
        waveManager.waves = CreateWaves(scenePath);
        if (grassStage)
            waveManager.useCenterLanes = false;
        waveManager.centerLaneOffset = 0.95f;
        if (grassStage)
            waveManager.randomizeSpawnBursts = false;
        if (stageNumber == 4)
            waveManager.randomizeElements = false;
        waveManager.maximumSpawnBatchSize = 2;
        waveManager.minimumSpawnDelay = 0.7f;
        waveManager.maximumSpawnDelay = 1.4f;
        waveManager.spawnWidth = grassStage ? 0.55f : 3.2f;
        waveManager.spawnHeightJitter = grassStage ? 0f : 0.35f;
        waveManager.randomizeFormation = !grassStage;
        waveManager.randomizeElements = true;
        waveManager.availableElements = ElementsForStage(scenePath);
        waveManager.minimumHorizontalSpacing = 1.25f;
        waveManager.delayBetweenWaves = grassStage ? 3f : 1f;
        if (!grassStage)
            ArrangeExistingEnemies(waveManager.availableElements);

        StageManager stageManager = Object.FindAnyObjectByType<StageManager>();

        if (stageManager == null)
            stageManager = new GameObject("StageManager").AddComponent<StageManager>();

        stageManager.waveManager = waveManager;
        stageManager.stageNumber = GetStageNumber(scenePath);
        stageManager.stageReward = 25 + stageManager.stageNumber * 5;
        stageManager.barrierMaxHealth = 5;
        stageManager.barrierY = barrierObject != null
            ? barrierObject.transform.position.y
            : -5.2f;
        stageManager.barrierContactOffset = 0.35f;

        if (stageManager.winPanel != null)
        {
            Button nextButton = stageManager.winPanel.GetComponentInChildren<Button>(true);
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(stageManager.NextStage);
            }
        }

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();

        if (canvas != null)
        {
            CreateWaveLabel(canvas.transform, waveManager);
            CreateSkillButton(canvas.transform, beast);
            CreateBeastSwapButton(canvas.transform, padSwap);
            stageManager.barrierHealthText = CreateBarrierLabel(canvas.transform);
            CreateBossHealthLabel(canvas.transform);
        }

        EditorUtility.SetDirty(launcher);
        EditorUtility.SetDirty(beast);
        EditorUtility.SetDirty(padSwap);
        EditorUtility.SetDirty(waveManager);
        EditorUtility.SetDirty(stageManager);
    }

    private static void ArrangeExistingEnemies(ElementType[] elements)
    {
        Enemy[] enemies = Object.FindObjectsByType<Enemy>();
        System.Array.Sort(enemies, (left, right) => left.transform.position.x.CompareTo(right.transform.position.x));

        if (enemies.Length == 0)
            return;

        int columns = Mathf.Min(5, Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(enemies.Length * 1.35f))));

        for (int index = 0; index < enemies.Length; index++)
        {
            int row = index / columns;
            int column = index % columns;
            float x = Mathf.Lerp(-3.3f, 3.3f, (float)column / (columns - 1));
            float y = 5.8f - row * 1.35f;
            enemies[index].transform.position = new Vector3(x, y, enemies[index].transform.position.z);

            if (elements != null && elements.Length > 0)
                enemies[index].SetElementForEditor(elements[index % elements.Length]);

            EditorUtility.SetDirty(enemies[index]);
        }
    }

    private static void HideSeparateBeastDisplays(GameObject launchObject)
    {
        foreach (BeastId beast in BeastRoster.AllBeasts)
        {
            if (beast == BeastId.Choma)
                continue;

            GameObject display = GameObject.Find(BeastRoster.GetName(beast));
            if (display == null || display == launchObject || display.GetComponent<LaunchController>() != null)
                continue;

            display.SetActive(false);
            EditorUtility.SetDirty(display);
        }
    }

    private static void ConfigureScenePhysics(LaunchController launcher, PhysicsMaterial2D chomaMaterial, PhysicsMaterial2D enemyMaterial)
    {
        Rigidbody2D launcherBody = launcher.GetComponent<Rigidbody2D>();
        if (launcherBody != null)
        {
            launcherBody.bodyType = RigidbodyType2D.Dynamic;
            launcherBody.gravityScale = 0f;
            launcherBody.linearDamping = 0.25f;
            launcherBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            launcherBody.freezeRotation = true;
        }

        Collider2D launcherCollider = launcher.GetComponent<Collider2D>();
        if (launcherCollider != null)
        {
            launcherCollider.isTrigger = false;
            launcherCollider.sharedMaterial = chomaMaterial;
        }

        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>())
        {
            Rigidbody2D enemyBody = enemy.GetComponent<Rigidbody2D>();
            if (enemyBody == null)
                enemyBody = enemy.gameObject.AddComponent<Rigidbody2D>();

            enemyBody.bodyType = RigidbodyType2D.Kinematic;
            enemyBody.gravityScale = 0f;
            enemyBody.freezeRotation = true;
            enemyBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
            if (enemyCollider != null)
            {
                enemyCollider.isTrigger = false;
                enemyCollider.sharedMaterial = enemyMaterial;
            }
        }
    }

    private static PhysicsMaterial2D GetPhysicsMaterial(string path, string materialName, float bounciness, float friction)
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);

        if (material == null)
        {
            material = new PhysicsMaterial2D(materialName);
            AssetDatabase.CreateAsset(material, path);
        }

        material.bounciness = bounciness;
        material.friction = friction;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemoveLaunchCounter()
    {
        GameObject launchesText = GameObject.Find("LaunchesText");

        if (launchesText != null)
            launchesText.SetActive(false);
    }

    private static TMP_Text CreateBarrierLabel(Transform canvas)
    {
        GameObject labelObject = GameObject.Find("BarrierHealthText");

        if (labelObject == null)
        {
            labelObject = new GameObject("BarrierHealthText", typeof(RectTransform));
            labelObject.transform.SetParent(canvas, false);
        }

        if (labelObject.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(labelObject);
            labelObject = new GameObject("BarrierHealthText", typeof(RectTransform));
            labelObject.transform.SetParent(canvas, false);
        }

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(180f, 90f);
        rect.sizeDelta = new Vector2(360f, 55f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = labelObject.AddComponent<TextMeshProUGUI>();

        text.fontSize = 26f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.text = "BARRIER: 5 / 5";
        return text;
    }

    private static TMP_Text CreateBossHealthLabel(Transform canvas)
    {
        GameObject labelObject = GameObject.Find("BossHealthText");

        if (labelObject == null)
        {
            labelObject = new GameObject("BossHealthText", typeof(RectTransform));
            labelObject.transform.SetParent(canvas, false);
        }

        if (labelObject.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(labelObject);
            labelObject = new GameObject("BossHealthText", typeof(RectTransform));
            labelObject.transform.SetParent(canvas, false);
        }

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -190f);
        rect.sizeDelta = new Vector2(550f, 60f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = labelObject.AddComponent<TextMeshProUGUI>();

        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.75f, 0.2f);
        text.text = string.Empty;
        text.enabled = false;
        return text;
    }

    private static int GetStageNumber(string scenePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(scenePath);
        string number = fileName.Replace("stage-", string.Empty);
        int separator = number.IndexOf('_');

        if (separator >= 0)
            number = number.Substring(0, separator);

        return int.TryParse(number, out int stageNumber) ? stageNumber : 1;
    }

    private static void RegisterAllScenes()
    {
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
        var paths = new System.Collections.Generic.List<string>();

        foreach (EditorBuildSettingsScene scene in currentScenes)
            paths.Add(scene.path);

        foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity"))
        {
            if (!paths.Contains(path.Replace('\\', '/')))
                paths.Add(path.Replace('\\', '/'));
        }

        paths.Sort((left, right) =>
        {
            bool leftIsHub = Path.GetFileNameWithoutExtension(left) == "GameHub";
            bool rightIsHub = Path.GetFileNameWithoutExtension(right) == "GameHub";

            if (leftIsHub != rightIsHub)
                return leftIsHub ? -1 : 1;

            int leftStage = GetStageNumber(left);
            int rightStage = GetStageNumber(right);
            int stageComparison = leftStage.CompareTo(rightStage);
            return stageComparison != 0 ? stageComparison : string.CompareOrdinal(left, right);
        });
        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        foreach (string path in paths)
            buildScenes.Add(new EditorBuildSettingsScene(path, true));

        EditorBuildSettings.scenes = buildScenes.ToArray();
    }

    private static void CreateHubScene()
    {
        string hubPath = "Assets/Scenes/GameHub.unity";
        Scene hubScene;

        if (File.Exists(hubPath))
            hubScene = EditorSceneManager.OpenScene(hubPath, OpenSceneMode.Single);
        else
            hubScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject progress = GameObject.Find("GameProgress");

        if (progress == null)
            progress = new GameObject("GameProgress");

        if (progress.GetComponent<GameProgress>() == null)
            progress.AddComponent<GameProgress>();

        GameObject hub = GameObject.Find("GameHubController");

        if (hub == null)
            hub = new GameObject("GameHubController");

        GameHubController controller = hub.GetComponent<GameHubController>();

        if (controller == null)
            controller = hub.AddComponent<GameHubController>();

        if (Object.FindAnyObjectByType<Camera>() == null)
        {
            GameObject cameraObject = new GameObject("HubCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.backgroundColor = new Color(0.06f, 0.16f, 0.19f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        Camera hubCamera = Camera.main;
        if (hubCamera != null)
        {
            hubCamera.orthographic = true;
            hubCamera.orthographicSize = 10f;
        }

        Canvas canvas = CreateHubCanvas();
        CreateHubText(canvas.transform, "HubTitle", "BUMPER BEAST", 38,
            new Vector2(0f, 890f), TextAnchor.MiddleCenter);
        controller.coinsText = CreateHubText(canvas.transform, "CoinsText", "COINS: 0", 28, new Vector2(-300f, 800f), TextAnchor.MiddleCenter);
        controller.unlockedText = CreateHubText(canvas.transform, "UnlockedText", "UNLOCKED: 1 / 20", 24, new Vector2(300f, 800f), TextAnchor.MiddleCenter);
        controller.shopText = CreateHubText(canvas.transform, "ShopText", "SHOP", 24, new Vector2(0f, -250f), TextAnchor.MiddleCenter);

        for (int index = 1; index <= 20; index++)
        {
            int column = (index - 1) % 2;
            int row = (index - 1) / 2;
            CreateHubButton(canvas.transform, "StageButton" + index, "STAGE " + index,
                new Vector2(column == 0 ? -250f : 250f, 610f - row * 74f));
        }

        CreateHubButton(canvas.transform, "BuyLaunchPower", "UPGRADE LAUNCH", new Vector2(-250f, -410f));
        CreateHubButton(canvas.transform, "BuySkillCooldown", "UPGRADE SKILL", new Vector2(250f, -410f));

        for (int index = 0; index < BeastRoster.AllBeasts.Length; index++)
        {
            int column = index % 2;
            int row = index / 2;
            CreateHubButton(canvas.transform, "BeastButton" + index,
                BeastRoster.GetName(BeastRoster.AllBeasts[index]),
                new Vector2(column == 0 ? -250f : 250f, -570f - row * 82f));
        }

        controller.stageCount = 20;
        CreateHubButton(canvas.transform, "ContinueButton", "CONTINUE", new Vector2(0f, 700f));
        EditorSceneManager.SaveScene(hubScene, hubPath);
        RegisterAllScenes();
    }

    private static Canvas CreateHubCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("HubCanvas", typeof(RectTransform));
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (canvas.GetComponent<CanvasScaler>() == null)
            canvas.gameObject.AddComponent<CanvasScaler>();

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        return canvas;
    }

    private static Text CreateHubText(Transform parent, string name, string value, int size, Vector2 position, TextAnchor alignment)
    {
        GameObject textObject = GameObject.Find(name);

        if (textObject == null)
        {
            textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
        }
        else if (textObject.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(textObject);
            textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = name == "ShopText"
            ? new Vector2(980f, 320f)
            : name == "HubTitle"
                ? new Vector2(800f, 100f)
                : new Vector2(440f, 70f);
        Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private static Button CreateHubButton(Transform parent, string name, string label, Vector2 position)
    {
        GameObject buttonObject = GameObject.Find(name);

        if (buttonObject == null)
        {
            buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<Image>().color = new Color(0.12f, 0.25f, 0.75f, 0.95f);
            buttonObject.AddComponent<Button>();
        }
        else if (buttonObject.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(buttonObject);
            buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<Image>().color = new Color(0.12f, 0.25f, 0.75f, 0.95f);
            buttonObject.AddComponent<Button>();
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        if (name.StartsWith("StageButton"))
            rect.sizeDelta = new Vector2(220f, 60f);
        else if (name.StartsWith("BeastButton"))
            rect.sizeDelta = new Vector2(300f, 70f);
        else if (name == "ContinueButton")
            rect.sizeDelta = new Vector2(300f, 72f);
        else
            rect.sizeDelta = new Vector2(300f, 76f);
        Text text = CreateHubText(buttonObject.transform, name + "Label", label, 18, Vector2.zero, TextAnchor.MiddleCenter);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return buttonObject.GetComponent<Button>();
    }

    private static void SetPortraitSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.defaultScreenWidth = 1080;
        PlayerSettings.defaultScreenHeight = 1920;
    }

    private static void ConfigurePortraitArena()
    {
        SetArenaWall("wall-left", new Vector3(-4.5f, 0f, 0f), new Vector3(0.5f, 16.4f, 1f));
        SetArenaWall("wall-right", new Vector3(4.5f, 0f, 0f), new Vector3(0.5f, 16.4f, 1f));
        SetArenaWall("wall-top", new Vector3(0f, 8.2f, 0f), new Vector3(9f, 0.5f, 1f));
        SetArenaWall("wall-bottom", new Vector3(0f, -8.2f, 0f), new Vector3(9f, 0.5f, 1f));

        GameObject spawn = GameObject.Find("EnemySpawnPoint");
        if (spawn != null)
            spawn.transform.position = new Vector3(0f, 7.2f, 0f);

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        CanvasScaler scaler = Object.FindAnyObjectByType<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
        }
    }

    private static void SetArenaWall(string objectName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.Find(objectName);
        if (wall == null)
            return;

        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    private static void EnsureArenaBumpers(string scenePath)
    {
        int stageNumber = GetStageNumber(scenePath);
        if (stageNumber < 1 || stageNumber > 4)
            return;

        EnsureStartingBumperLayout(stageNumber);
    }

    private static void EnsureStartingBumperLayout(int stageNumber)
    {
        Vector3[] positions;
        switch (stageNumber)
        {
            case 1:
                positions = new[]
                {
                    new Vector3(-1.8f, 1f, 0f),
                    new Vector3(1.8f, 1f, 0f)
                };
                break;
            case 2:
                positions = new[]
                {
                    new Vector3(-2.2f, 2.5f, 0f),
                    new Vector3(2.2f, 0.5f, 0f),
                    new Vector3(-2.2f, -1.5f, 0f)
                };
                break;
            case 3:
                positions = new[]
                {
                    new Vector3(0f, 2.5f, 0f),
                    new Vector3(-1.5f, 2.5f, 0f),
                    new Vector3(1.5f, -1.5f, 0f)
                };
                break;
            case 4:
                positions = new[]
                {
                    new Vector3(-1.5f, 0.5f, 0f),
                    new Vector3(0f, 0.5f, 0f),
                    new Vector3(1.5f, 0.5f, 0f)
                };
                break;
            default:
                return;
        }

        System.Collections.Generic.List<GameObject> bumpers =
            new System.Collections.Generic.List<GameObject>();
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>())
        {
            if (renderer != null &&
                renderer.gameObject.name.StartsWith("Bumper_", System.StringComparison.Ordinal))
                bumpers.Add(renderer.gameObject);
        }

        bumpers.Sort(CompareStartingBumpers);

        GameObject bumperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BumperPrefabPath);
        if (bumperPrefab == null)
        {
            Debug.LogError("Starting bumper layout requires " + BumperPrefabPath + ".");
            return;
        }

        while (bumpers.Count > positions.Length)
        {
            GameObject extra = bumpers[bumpers.Count - 1];
            bumpers.RemoveAt(bumpers.Count - 1);
            Object.DestroyImmediate(extra);
        }

        while (bumpers.Count < positions.Length)
        {
            GameObject bumper = PrefabUtility.InstantiatePrefab(bumperPrefab) as GameObject;
            if (bumper == null)
                bumper = Object.Instantiate(bumperPrefab);
            bumpers.Add(bumper);
        }

        for (int index = 0; index < positions.Length; index++)
        {
            GameObject bumper = bumpers[index];
            bumper.name = "Bumper_" + (index + 1).ToString("00");
            bumper.transform.position = positions[index];
            ConfigureBumper(bumper);
            EditorUtility.SetDirty(bumper.transform);
        }
    }

    private static int CompareStartingBumpers(GameObject left, GameObject right)
    {
        const string prefix = "Bumper_";
        int leftIndex = 0;
        int rightIndex = 0;
        bool leftHasLayoutIndex = left.name.StartsWith(prefix, System.StringComparison.Ordinal) &&
            int.TryParse(left.name.Substring(prefix.Length), out leftIndex);
        bool rightHasLayoutIndex = right.name.StartsWith(prefix, System.StringComparison.Ordinal) &&
            int.TryParse(right.name.Substring(prefix.Length), out rightIndex);

        if (leftHasLayoutIndex && rightHasLayoutIndex)
            return leftIndex.CompareTo(rightIndex);

        if (leftHasLayoutIndex != rightHasLayoutIndex)
            return leftHasLayoutIndex ? -1 : 1;

        int byPosition = left.transform.position.x.CompareTo(right.transform.position.x);
        return byPosition != 0 ? byPosition : string.CompareOrdinal(left.name, right.name);
    }

    private static void ConfigureBumper(GameObject bumper)
    {
        SpriteRenderer renderer = bumper.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = bumper.AddComponent<SpriteRenderer>();

        renderer.sprite = CreateBumperSprite();
        renderer.color = Color.white;

        CircleCollider2D bumperCollider = bumper.GetComponent<CircleCollider2D>();
        Collider2D existingCollider = bumper.GetComponent<Collider2D>();
        if (existingCollider == null)
        {
            bumperCollider = bumper.AddComponent<CircleCollider2D>();
            bumperCollider.radius = 0.46f;
            existingCollider = bumperCollider;
        }

        existingCollider.isTrigger = false;

        Rigidbody2D bumperBody = bumper.GetComponent<Rigidbody2D>();
        if (bumperBody == null)
            bumperBody = bumper.AddComponent<Rigidbody2D>();

        bumperBody.bodyType = RigidbodyType2D.Static;
        bumperBody.gravityScale = 0f;
        bumperBody.freezeRotation = true;

        Bumper bumperLogic = bumper.GetComponent<Bumper>();
        if (bumperLogic == null)
        {
            bumperLogic = bumper.AddComponent<Bumper>();
            bumperLogic.hp = 3;
            bumperLogic.speedMultiplier = 1.2f;
            bumperLogic.minimumBoostSpeed = 12f;
            bumperLogic.maxSpeed = 15f;
        }

        bumperLogic.RefreshVisuals();
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(bumper);
    }

    private static void EnsureBumperRespawnSetup(string scenePath)
    {
        int stageNumber = GetStageNumber(scenePath);
        GameObject bumperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BumperPrefabPath);
        if (bumperPrefab == null)
        {
            Debug.LogError("Bumper respawn setup requires " + BumperPrefabPath + ".");
            return;
        }

        BumperManager manager = Object.FindAnyObjectByType<BumperManager>();
        if (manager == null)
            manager = new GameObject("BumberManager").AddComponent<BumperManager>();

        manager.bumperPrefab = bumperPrefab;
        manager.respawnDelay = 5f;
        manager.checkRadius = 0.5f;

        Vector3[] positions = GetFixedBumperSpawnPositions(stageNumber);
        if (positions == null &&
            (manager.fixedSpawnPoints == null || manager.fixedSpawnPoints.Length == 0))
        {
            positions = new[]
            {
                new Vector3(-3.47f, 5.73f, 0f),
                new Vector3(3.19f, 4.39f, 0f),
                new Vector3(-3.77f, 0.35f, 0f),
                new Vector3(3.49f, -2.69f, 0f)
            };
        }

        if (positions != null)
        {
            const string spawnRootName = "BumperFixedSpawnPoints";

            GameObject spawnRoot = GameObject.Find(spawnRootName);
            if (spawnRoot == null)
                spawnRoot = new GameObject(spawnRootName);

            Transform[] spawnPoints = new Transform[positions.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                string pointName = "BumperSpawnPoint_" + (index + 1).ToString("00");
                Transform point = manager.fixedSpawnPoints != null &&
                    index < manager.fixedSpawnPoints.Length
                    ? manager.fixedSpawnPoints[index]
                    : null;

                if (point == null)
                    point = spawnRoot.transform.Find(pointName);

                if (point == null)
                {
                    GameObject pointObject = new GameObject(pointName);
                    pointObject.transform.SetParent(spawnRoot.transform, false);
                    point = pointObject.transform;
                }

                point.name = pointName;
                point.position = positions[index];
                spawnPoints[index] = point;
                EditorUtility.SetDirty(point.gameObject);
            }

            manager.fixedSpawnPoints = spawnPoints;
            while (spawnRoot.transform.childCount > positions.Length)
                Object.DestroyImmediate(spawnRoot.transform.GetChild(spawnRoot.transform.childCount - 1).gameObject);

            EditorUtility.SetDirty(spawnRoot);
        }

        EditorUtility.SetDirty(manager);
    }

    private static Vector3[] GetFixedBumperSpawnPositions(int stageNumber)
    {
        if (stageNumber == 1)
        {
            return new[]
            {
                new Vector3(-2.2f, 3f, 0f),
                new Vector3(2.2f, 3f, 0f),
                new Vector3(-2.2f, -1f, 0f),
                new Vector3(2.2f, -1f, 0f)
            };
        }

        if (stageNumber == 2)
        {
            return new[]
            {
                new Vector3(2.2f, 4.5f, 0f),
                new Vector3(-2.2f, 0.5f, 0f),
                new Vector3(2.2f, -1.5f, 0f),
                new Vector3(-2.2f, -3.5f, 0f)
            };
        }

        if (stageNumber == 3)
        {
            return new[]
            {
                new Vector3(-2f, 3f, 0f),
                new Vector3(2f, -2f, 0f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0f, -2.5f, 0f)
            };
        }

        if (stageNumber == 4)
        {
            return new[]
            {
                new Vector3(-2.2f, 2f, 0f),
                new Vector3(2.2f, 2f, 0f),
                new Vector3(-2.2f, -1f, 0f),
                new Vector3(2.2f, -1f, 0f)
            };
        }

        return null;
    }

    private static Sprite CreateBumperSprite()
    {
        if (bumperSprite != null)
            return bumperSprite;

        const int textureSize = 64;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color outline = new Color(0.04f, 0.04f, 0.04f, 1f);
        Color body = Color.white;
        Color center = Color.white;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - (textureSize - 1) * 0.5f;
                float dy = y - (textureSize - 1) * 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                Color pixel = distance > 30f
                    ? Color.clear
                    : distance > 24f
                        ? outline
                        : distance > 14f
                            ? body
                            : center;
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        bumperSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f), textureSize);
        bumperSprite.name = "BumperBeastBumper";
        return bumperSprite;
    }

    private static Transform[] EnsureGrassEnemyRoute(string scenePath)
    {
        int stageNumber = GetStageNumber(scenePath);

        if (stageNumber < 1 || stageNumber > 4)
            return new Transform[0];

        Vector3[] routePositions;
        switch (stageNumber)
        {
            case 1:
                routePositions = new[]
                {
                    new Vector3(0f, 4f, 0f),
                    new Vector3(0f, -3f, 0f)
                };
                break;
            case 2:
                routePositions = new[]
                {
                    new Vector3(-1.5f, 4.5f, 0f),
                    new Vector3(1.5f, 2.5f, 0f),
                    new Vector3(-1.5f, 0.5f, 0f),
                    new Vector3(1.5f, -1.5f, 0f)
                };
                break;
            case 3:
                routePositions = new[]
                {
                    new Vector3(2f, 4f, 0f),
                    new Vector3(2f, 1f, 0f),
                    new Vector3(-2f, 0f, 0f),
                    new Vector3(-2f, -3f, 0f)
                };
                break;
            case 4:
                routePositions = new[]
                {
                    new Vector3(0f, 4f, 0f),
                    new Vector3(0f, 2f, 0f),
                    new Vector3(2.2f, 2f, 0f),
                    new Vector3(2.2f, -1f, 0f),
                    new Vector3(0f, -1f, 0f)
                };
                break;
            default:
                return new Transform[0];
        }

        GameObject routeRoot = GameObject.Find("EnemyRoute");
        if (routeRoot == null)
            routeRoot = new GameObject("EnemyRoute");

        routeRoot.transform.position = Vector3.zero;
        Transform[] waypoints = new Transform[routePositions.Length];

        for (int index = 0; index < routePositions.Length; index++)
        {
            string pointName = "RoutePoint_" + (index + 1).ToString("00");
            Transform point = routeRoot.transform.Find(pointName);
            if (point == null)
            {
                GameObject pointObject = new GameObject(pointName);
                pointObject.transform.SetParent(routeRoot.transform, false);
                point = pointObject.transform;
            }

            point.position = routePositions[index];
            waypoints[index] = point;
        }

        while (routeRoot.transform.childCount > routePositions.Length)
            Object.DestroyImmediate(routeRoot.transform.GetChild(routeRoot.transform.childCount - 1).gameObject);

        EditorUtility.SetDirty(routeRoot);
        return waypoints;
    }

    private static void RemoveTestBumpers(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                string normalizedName = item.name.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
                if (normalizedName.Equals("TESTBUMBER", System.StringComparison.OrdinalIgnoreCase) ||
                    normalizedName.Equals("TESTBUMPER", System.StringComparison.OrdinalIgnoreCase))
                    Object.DestroyImmediate(item.gameObject);
            }
        }
    }

    private static void RemoveBossObjectsFromScene()
    {
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>())
        {
            if (enemy != null &&
                (enemy.name.IndexOf("Thornmaw", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (!string.IsNullOrEmpty(enemy.bossName) && enemy.bossName.IndexOf("Thornmaw", System.StringComparison.OrdinalIgnoreCase) >= 0)))
                Object.DestroyImmediate(enemy.gameObject);
        }
    }

    private static Sprite CreateSolidSquareSprite(Color color)
    {
        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[256];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = color;

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 32f);
    }

    private static Transform FindOrCreateLaunchPad(Transform launcher)
    {
        GameObject existing = GameObject.Find("LaunchPad");

        if (existing != null)
        {
            existing.transform.position = launcher.position;
            return existing.transform;
        }

        GameObject pad = new GameObject("LaunchPad");
        pad.transform.position = launcher.position;

        SpriteRenderer launcherRenderer = launcher.GetComponent<SpriteRenderer>();
        SpriteRenderer padRenderer = pad.AddComponent<SpriteRenderer>();

        if (launcherRenderer != null)
        {
            padRenderer.sprite = launcherRenderer.sprite;
            padRenderer.sortingOrder = launcherRenderer.sortingOrder - 1;
        }

        padRenderer.color = new Color(0.15f, 0.2f, 0.55f, 0.85f);
        pad.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
        return pad.transform;
    }

    private static Transform FindOrCreateSpawnPoint()
    {
        GameObject existing = GameObject.Find("EnemySpawnPoint");

        if (existing != null)
            return existing.transform;

        GameObject spawnPoint = new GameObject("EnemySpawnPoint");
        spawnPoint.transform.position = new Vector3(0f, 4.5f, 0f);
        return spawnPoint.transform;
    }

    private static EnemyWave[] CreateWaves(string scenePath)
    {
        Enemy enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath).GetComponent<Enemy>();
        ElementType[] elements = ElementsForStage(Path.GetFileNameWithoutExtension(scenePath));
        int stageNumber = GetStageNumber(scenePath);
        if (stageNumber == 4)
        {
            return new[]
            {
                new EnemyWave
                {
                    waveName = "Opening Wave",
                    enemyPrefabs = new[] { enemyPrefab },
                    element = ElementType.Grass,
                    enemyCount = 3,
                    spawnInterval = 1f
                },
                new EnemyWave
                {
                    waveName = "Thornmaw Boss Wave",
                    enemyPrefabs = new[] { enemyPrefab },
                    element = ElementType.Grass,
                    enemyCount = 5,
                    spawnInterval = 1.2f,
                    bossSpawnIndex = 2,
                    bossHealthBonus = 30,
                    bossName = "Thornmaw"
                }
            };
        }

        bool grassStage = stageNumber >= 1 && stageNumber <= 4;
        bool openingChapter = stageNumber <= 4;
        int openingCount = grassStage ? stageNumber + 2 : 3;
        int pressureCount = grassStage ? stageNumber + 3 : 2;
        int finalCount = grassStage ? stageNumber + 4 : 3;
        int pressureHealth = stageNumber >= 9 ? 1 : 0;
        int finalHealth = stageNumber >= 13 ? 1 : 0;
        float pressureSpeed = Mathf.Min(1.1f, 0.85f + stageNumber * 0.01f);
        float finalSpeed = Mathf.Min(1.2f, pressureSpeed + 0.1f);

        System.Collections.Generic.List<EnemyWave> waveList = new System.Collections.Generic.List<EnemyWave>
        {
            new EnemyWave
            {
                waveName = "Opening Wave",
                enemyPrefabs = new[] { enemyPrefab },
                element = elements[0],
                enemyCount = openingCount,
                spawnInterval = openingChapter ? 1.1f : 0.8f,
                extraHealth = 0,
                speedMultiplier = openingChapter ? 0.75f : 1f
            },
            new EnemyWave
            {
                waveName = "Pressure Wave",
                enemyPrefabs = new[] { enemyPrefab },
                element = elements[Mathf.Min(1, elements.Length - 1)],
                enemyCount = pressureCount,
                spawnInterval = 1f,
                extraHealth = pressureHealth,
                speedMultiplier = pressureSpeed
            }
        };

        if (stageNumber >= 1 && stageNumber <= 4)
        {
            waveList.Add(new EnemyWave
            {
                waveName = "Final Wave",
                enemyPrefabs = new[] { enemyPrefab },
                element = elements[0],
                enemyCount = finalCount,
                spawnInterval = 0.9f,
                extraHealth = finalHealth,
                speedMultiplier = finalSpeed,
            });
        }

        return waveList.ToArray();
    }

    private static ElementType[] ElementsForStage(string sceneName)
    {
        if (sceneName.Contains("Fire"))
            return new[] { ElementType.Grass, ElementType.Water, ElementType.Fire };

        if (sceneName.Contains("Water"))
            return new[] { ElementType.Grass, ElementType.Earth, ElementType.Water };

        if (sceneName.Contains("Electric"))
            return new[] { ElementType.Grass, ElementType.Earth, ElementType.Electric };

        if (sceneName.Contains("Earth"))
            return new[] { ElementType.Grass, ElementType.Earth };

        return new[] { ElementType.Grass };
    }

    private static void CreateWaveLabel(Transform canvas, WaveManager waveManager)
    {
        GameObject labelObject = GameObject.Find("WaveText");

        if (labelObject == null)
        {
            labelObject = new GameObject("WaveText", typeof(RectTransform));
            labelObject.transform.SetParent(canvas, false);
        }

        if (labelObject.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(labelObject);
            labelObject = new GameObject("WaveText", typeof(RectTransform));
            labelObject.transform.SetParent(canvas, false);
        }

        RectTransform rect = labelObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -115f);
        rect.sizeDelta = new Vector2(740f, 130f);

        Text text = labelObject.GetComponent<Text>();
        if (text == null)
            text = labelObject.AddComponent<Text>();

        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        waveManager.legacyWaveText = text;
    }

    private static void CreateSkillButton(Transform canvas, BeastController beast)
    {
        GameObject buttonObject = GameObject.Find("BeastSkillButton");

        if (buttonObject == null || buttonObject.GetComponent<RectTransform>() == null)
        {
            if (buttonObject != null)
                Object.DestroyImmediate(buttonObject);

            buttonObject = new GameObject("BeastSkillButton", typeof(RectTransform));
            buttonObject.transform.SetParent(canvas, false);
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        PortraitButtonLayout.Apply(rect, 0);

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.25f, 0.75f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();

        GameObject titleObject = buttonObject.transform.Find("SkillLabel")?.gameObject;

        if (titleObject == null || titleObject.GetComponent<RectTransform>() == null)
        {
            if (titleObject != null)
                Object.DestroyImmediate(titleObject);

            titleObject = new GameObject("SkillLabel", typeof(RectTransform));
            titleObject.transform.SetParent(buttonObject.transform, false);
        }

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(8f, 8f);
        titleRect.offsetMax = new Vector2(-8f, -8f);

        Text title = titleObject.GetComponent<Text>();
        if (title == null)
            title = titleObject.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 20;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.text = BeastRoster.GetAbilityName(GameProgress.Instance != null
            ? GameProgress.Instance.SelectedBeast
            : BeastId.Choma).ToUpper() + "\nREADY";

        BeastSkillButton skill = buttonObject.GetComponent<BeastSkillButton>();

        if (skill == null)
            skill = buttonObject.AddComponent<BeastSkillButton>();

        skill.beast = beast;
        skill.button = button;
        skill.legacyCooldownText = buttonObject.GetComponentInChildren<Text>();
        EditorUtility.SetDirty(skill);
    }

    private static void CreateBeastSwapButton(Transform canvas, PadSwapSystem padSwap)
    {
        GameObject buttonObject = GameObject.Find("BeastSwapButton");

        if (buttonObject == null || buttonObject.GetComponent<RectTransform>() == null)
        {
            if (buttonObject != null)
                Object.DestroyImmediate(buttonObject);

            buttonObject = new GameObject("BeastSwapButton", typeof(RectTransform));
            buttonObject.transform.SetParent(canvas, false);
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        PortraitButtonLayout.Apply(rect, 1);

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.42f, 0.24f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();

        GameObject labelObject = buttonObject.transform.Find("SwapLabel")?.gameObject;
        if (labelObject == null || labelObject.GetComponent<RectTransform>() == null)
        {
            if (labelObject != null)
                Object.DestroyImmediate(labelObject);

            labelObject = new GameObject("SwapLabel", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
        }

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 8f);
        labelRect.offsetMax = new Vector2(-8f, -8f);

        Text label = labelObject.GetComponent<Text>();
        if (label == null)
            label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "SWAP BEAST";

        padSwap.beast = padSwap.GetComponent<BeastController>();
        padSwap.swapButton = button;
        padSwap.legacySwapLabel = label;
        EditorUtility.SetDirty(padSwap);
    }
}