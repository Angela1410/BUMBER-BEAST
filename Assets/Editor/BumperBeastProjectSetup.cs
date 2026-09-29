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
    private const string ChomaPhysicsPath = "Assets/materials/ChomaBounce.physicsMaterial2D";
    private const string EnemyPhysicsPath = "Assets/materials/EnemyNoBounce.physicsMaterial2D";
    private const string ActiveSceneSetupKey = "BumperBeast.ActiveSceneSetupComplete";

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
        launcher.transform.position = new Vector3(0f, -7.1f, 0f);
        ConfigurePortraitArena();

        GameObject barrierObject = GameObject.Find("Square");
        Collider2D barrierCollider = barrierObject != null
            ? barrierObject.GetComponent<Collider2D>()
            : null;

        if (barrierCollider != null)
            barrierCollider.isTrigger = true;

        ConfigureScenePhysics(launcher, GetPhysicsMaterial(ChomaPhysicsPath, "ChomaBounce", 0.35f, 0f),
            GetPhysicsMaterial(EnemyPhysicsPath, "EnemyNoBounce", 0f, 0f));

        BeastController beast = launcher.GetComponent<BeastController>();

        if (beast == null)
            beast = launcher.gameObject.AddComponent<BeastController>();

        Transform launchPad = FindOrCreateLaunchPad(launcher.transform);
        beast.launchPad = launchPad;

        WaveManager waveManager = Object.FindAnyObjectByType<WaveManager>();

        if (waveManager == null)
            waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();

        Transform spawnPoint = FindOrCreateSpawnPoint();
        waveManager.spawnPoint = spawnPoint;
        waveManager.waves = CreateWaves(scenePath);
        waveManager.spawnWidth = 3.2f;
        waveManager.spawnHeightJitter = 0.35f;
        waveManager.randomizeFormation = true;
        waveManager.useExistingEnemiesAsFirstWave = true;
        waveManager.randomizeElements = true;
        waveManager.availableElements = ElementsForStage(scenePath);
        waveManager.minimumHorizontalSpacing = 1.25f;
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
            stageManager.barrierHealthText = CreateBarrierLabel(canvas.transform);
        }

        EditorUtility.SetDirty(launcher);
        EditorUtility.SetDirty(beast);
        EditorUtility.SetDirty(waveManager);
        EditorUtility.SetDirty(stageManager);
    }

    private static void ArrangeExistingEnemies(ElementType[] elements)
    {
        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
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

        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
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

        GameObject barrier = GameObject.Find("Square");
        if (barrier != null)
        {
            barrier.transform.position = new Vector3(0f, -5.2f, barrier.transform.position.z);
            barrier.transform.localScale = new Vector3(5.6f, 0.3f, 1f);
        }

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
        bool openingChapter = stageNumber <= 4;
        int pressureCount = 2;
        int finalCount = 3;
        int pressureHealth = stageNumber >= 9 ? 1 : 0;
        int finalHealth = stageNumber >= 13 ? 1 : 0;
        float pressureSpeed = Mathf.Min(1.1f, 0.85f + stageNumber * 0.01f);
        float finalSpeed = Mathf.Min(1.2f, pressureSpeed + 0.1f);

        return new[]
        {
            new EnemyWave
            {
                waveName = "Opening Wave",
                enemyPrefabs = new[] { enemyPrefab },
                element = elements[0],
                enemyCount = openingChapter ? 2 : 3,
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
            },
            new EnemyWave
            {
                waveName = "Final Wave",
                enemyPrefabs = new[] { enemyPrefab },
                element = elements[elements.Length - 1],
                enemyCount = finalCount,
                spawnInterval = 0.9f,
                extraHealth = finalHealth,
                speedMultiplier = finalSpeed
            }
        };
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
        rect.anchoredPosition = new Vector2(0f, -45f);
        rect.sizeDelta = new Vector2(720f, 90f);

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
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-180f, 100f);
        rect.sizeDelta = new Vector2(300f, 90f);

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
        title.fontSize = 24;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.text = BeastRoster.GetAbilityName(GameProgress.Instance != null
            ? GameProgress.Instance.SelectedBeast
            : BeastId.Choma) + "\nREADY";

        BeastSkillButton skill = buttonObject.GetComponent<BeastSkillButton>();

        if (skill == null)
            skill = buttonObject.AddComponent<BeastSkillButton>();

        skill.beast = beast;
        skill.button = button;
        skill.legacyCooldownText = buttonObject.GetComponentInChildren<Text>();
        EditorUtility.SetDirty(skill);
    }
}