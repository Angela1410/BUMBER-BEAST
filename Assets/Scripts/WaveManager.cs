using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class EnemyWave
{
    public string waveName = "Wave";
    public Enemy[] enemyPrefabs;
    public ElementType element = ElementType.Grass;
    public int enemyCount = 3;
    public float spawnInterval = 0.75f;
    public int extraHealth = 0;
    public float speedMultiplier = 1f;
    public bool isBoss = false;
    public bool bossSpawnsLast = false;
    public int bossSpawnIndex = -1;
    public int bossHealthBonus = 0;
    public string bossName = "";

    [Header("Enemy Variety (optional)")]
    [Tooltip("Overrides the regular enemies' sprite for this wave. The boss keeps its own look.")]
    public Sprite enemySprite;
    [Tooltip("Size multiplier for the regular enemies in this wave. 1 = prefab size.")]
    public float enemyScale = 1f;
    [Tooltip("Planning-card name for the regular enemies (e.g. \"Spark Mite\" on a boss wave that reuses " +
        "another prefab's body). Empty = taken from the prefab or the wave name.")]
    public string enemyDisplayName = "";

    [Header("Mixed Elements (optional)")]
    [Tooltip("Element per spawn, repeating: enemy i gets elementPattern[i % length]. Give it the same " +
        "length as Enemy Prefabs to pair each enemy type with its element. Empty = Element / random pool.")]
    public ElementType[] elementPattern;

    [Header("Timed Spawning (optional)")]
    [Tooltip("Seconds of spawning. Above 0 this is a timed wave: random enemies from the Spawn Table keep " +
        "coming until the time runs out, and the wave ends once the field is clear. 0 = classic wave of " +
        "Enemy Count enemies from Enemy Prefabs.")]
    public float spawnDuration = 0f;
    public EnemySpawnEntry[] spawnTable;
    [Tooltip("Random seconds between spawns (x = shortest, y = longest).")]
    public Vector2 spawnGap = new Vector2(2f, 2.8f);
    [Tooltip("Spawning pauses while this many of the wave's enemies are on the field. 0 = no cap.")]
    public int maxAlive = 5;

    public bool IsTimed => spawnDuration > 0f && spawnTable != null && spawnTable.Length > 0;
}

// one kind of enemy a timed wave can spawn. the element should match the art
// (rocky creatures are Earth, thorny critters are Grass) so you can read the matchups quickly
[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("Name on the planning card. Empty = taken from the prefab name.")]
    public string displayName = "";
    public Enemy prefab;
    public ElementType element = ElementType.Grass;
    [Tooltip("0 = random filler, picked by Weight for the whole wave. Above 0 = exactly this many spawn, " +
        "spread across the wave's time.")]
    public int fixedCount = 0;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Optional sprite (e.g. one of the Chapter 1 Grass critters on the shared Enemy_1 prefab).")]
    public Sprite spriteOverride;
    [Tooltip("Size multiplier. 1 = prefab size.")]
    public float scale = 1f;
    [Tooltip("Extra health on top of the wave's (tough kinds such as Volt Beetles).")]
    public int extraHealth = 0;
    [Tooltip("Speed multiplier on top of the wave's (quick kinds such as Ember Imps).")]
    public float speedMultiplier = 1f;
}

public class WaveManager : MonoBehaviour
{
    public Transform spawnPoint;
    public EnemyWave[] waves;
    public TMP_Text waveText;
    public UnityEngine.UI.Text legacyWaveText;
    public float delayBeforeFirstWave = 1f;
    public float delayBetweenWaves = 1f;
    public float spawnWidth = 5f;
    public float spawnHeightJitter = 0.35f;
    public float minimumHorizontalSpacing = 1.1f;
    public bool randomizeFormation = true;
    public bool randomizeElements = true;
    
    [Header("Routing")]
    public Transform[] routeWaypoints;
    public bool useCenterLanes;
    public float centerLaneOffset = 0.95f;
    
    [Header("Spawning pacing")]
    public bool randomizeSpawnBursts = true;
    public int maximumSpawnBatchSize = 2;
    public float minimumSpawnDelay = 1.5f;
    public float maximumSpawnDelay = 2f;
    public ElementType[] availableElements;

    [Header("Planning")]
    [Tooltip("Before each wave, show what is coming (enemy types, elements, and which squad Beast " +
        "beats them) and wait for the player to tap READY. The field is clear while planning, so " +
        "the player can swap Beasts and line up the first shot.")]
    public bool planBeforeEachWave = true;

    // which StageWaveLayouts version wrote this scene's waves (0 = made by hand). the editor reads it
    // to update stages 1-8 by itself, once per layout version, so no menu step is needed
    [HideInInspector] public int timedLayoutVersion;

    private const int MaxPlanRows = 4;
    private const float PlanCardDelay = 0.6f;
    // the only Grass enemy: the shared critter prefab (Enemy_1, it only has a number as a name). boss wave
    // minions and timed waves both show this name, so the card never lists it under two names
    private const string SharedCritterName = "Thorn Pup";

    private int currentWaveIndex = -1;
    private readonly List<ElementType> plannedElements = new List<ElementType>(16);
    private readonly List<PlanGroup> planGroups = new List<PlanGroup>(MaxPlanRows + 1);
    private bool planConfirmed;
    private bool planVisible;
    private PadSwapSystem padSwap;
    private BeastId planShownBeast;
    private bool planBeastShown;
    private GameObject planCard;
    private RectTransform planCardRect;
    private TMP_Text planTitleText;
    private TMP_Text planHintText;
    private RectTransform planButtonRect;
    private readonly RectTransform[] planRowRects = new RectTransform[MaxPlanRows];
    private readonly Image[] planRowIcons = new Image[MaxPlanRows];
    private readonly TMP_Text[] planRowNames = new TMP_Text[MaxPlanRows];
    private readonly TMP_Text[] planRowDetails = new TMP_Text[MaxPlanRows];
    private readonly Image[] planRowBacks = new Image[MaxPlanRows];
    private readonly Image[] planRowPills = new Image[MaxPlanRows];
    private readonly Outline[] planRowPillOutlines = new Outline[MaxPlanRows];
    private readonly TMP_Text[] planRowPillTexts = new TMP_Text[MaxPlanRows];
    private Outline planCardOutline;
    private TMP_Text planButtonLabel;
    private static readonly Color32 PlanRowColor = new Color32(50, 60, 45, 255);
    private static readonly Color32 PlanRowStrongColor = new Color32(84, 74, 36, 255);
    private Vector2 planLaidOutCanvasSize;

    private sealed class PlanGroup
    {
        public string name;
        public ElementType element;
        public bool boss;
        public int count; // 0 = random filler of a timed wave
        public float share; // how much of the filler this is, 0..1
        public int kinds; // enemy kinds merged into this row (rows merged by element)
        public string trait; // "TOUGH" / "FAST" for kinds with their own stats, else null
    }

    private struct ScheduledSpawn
    {
        public float time;
        public int entry;
    }

    private const float TimedSpawnClearance = 0.95f;
    private const float BurstSpawnGap = 0.55f;
    private readonly List<ScheduledSpawn> fixedSchedule = new List<ScheduledSpawn>(16);
    private static readonly System.Comparison<ScheduledSpawn> ByTime = (a, b) => a.time.CompareTo(b.time);
    private bool timedWaveActive;
    private float timedWaveRemaining;
    private float timedWaveDuration;

    // small wave status chip shown at the top while a wave runs (the big banner only shows
    // for a moment at the start)
    private RectTransform waveChipRect;
    private CanvasGroup waveChipGroup;
    private Image waveChipImage;
    private Outline waveChipOutline;
    private TMP_Text waveChipLeftText;
    private TMP_Text waveChipRightText;
    private RectTransform waveChipBarFill;
    private Image waveChipBarImage;
    private float waveChipAlpha;
    private int chipShownSecond = int.MinValue;
    private int chipShownEnemies = int.MinValue;
    private int chipShownWave = int.MinValue;
    private bool chipShownTimed;
    private bool waveInProgress;
    private bool allWavesComplete;
    private bool waveSpawnFailed;
    private float nextWaveTimer;
    private readonly List<Enemy> activeWaveEnemies = new List<Enemy>();
    private RectTransform waveCanvasRect;
    private RectTransform wavePanelRect;
    private Image wavePanelImage;
    private Outline wavePanelOutline;
    private CanvasGroup wavePanelGroup;
    private TMP_Text waveTitleText;
    private TMP_Text waveEnemiesText;
    private Vector2 waveTargetPosition;
    private Vector2 waveHiddenPosition;
    private Vector2 waveAnimationStartPosition;
    private float waveAnimationElapsed;
    private float waveHideTime;
    private float lastCanvasWidth;
    private float lastCanvasHeight;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Rect lastSafeArea;
    private int lastDisplayedEnemyCount = int.MinValue;
    private int lastDisplayedSecond = int.MinValue;
    private int lastDisplayedWaveIndex = int.MinValue;
    private bool lastDisplayedWaveInProgress;
    private bool lastDisplayedAllWavesComplete;
    private string lastDisplayedTitle;
    private string lastDisplayedDetail;
    private bool waveHudLayoutApplied;
    private bool waveHudVisible;
    private bool waveAnimationPlaying;
    private bool lastAppliedBossStyle;

    public bool IsComplete => allWavesComplete;
    public bool HasSpawnFailed => waveSpawnFailed;
    public int CurrentWaveNumber => currentWaveIndex + 1;
    public int WaveCount => waves != null ? waves.Length : 0;

    // the wave manager of the stage that's running (one per stage scene)
    public static WaveManager Current { get; private set; }

    // no wave is running yet (before the first plan card, while it waits for READY, and between
    // waves): you can't launch until the player taps READY
    public bool IsAwaitingReady => planBeforeEachWave && waveCanvasRect != null && !waveInProgress &&
        !allWavesComplete && !waveSpawnFailed;
    public bool IsWaveInProgress => waveInProgress;
    public RectTransform ReadyButtonRect => planVisible ? planButtonRect : null;

    // wave events for the tutorial and the stage HUD: plan card shown (wave number), wave started
    // (wave number, boss wave), wave cleared (wave number, last wave)
    public static event System.Action<int> PlanShown;
    public static event System.Action<int, bool> WaveStarted;
    public static event System.Action<int, bool> WaveCleared;

    private float readyNudgeUntil;

    void OnEnable()
    {
        Current = this;
    }

    void OnDisable()
    {
        if (Current == this)
            Current = null;
    }

    // someone tried to launch before READY: the button bounces to show where to tap
    public void NudgeReady()
    {
        if (!IsAwaitingReady)
            return;
        if (Time.unscaledTime > readyNudgeUntil)
            GameAudio.Play(Sfx.Click);
        readyNudgeUntil = Time.unscaledTime + 0.9f;
    }

    void AnimateReadyNudge()
    {
        if (planButtonRect == null)
            return;
        float left = readyNudgeUntil - Time.unscaledTime;
        float scale = left > 0f ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(left * 14f)) * Mathf.Clamp01(left / 0.9f) : 1f;
        planButtonRect.localScale = new Vector3(scale, scale, 1f);
    }

    void Update()
    {
        if (nextWaveTimer > 0f)
            nextWaveTimer = Mathf.Max(0f, nextWaveTimer - Time.deltaTime);

        RefreshWaveHudLayout(false);
        UpdateWaveBannerAnimation();
        UpdateWaveChip();
        if (planVisible)
        {
            UpdatePlanCard();
            AnimateReadyNudge();
        }
        if (waveInProgress)
            UpdateWaveText();
        else if (nextWaveTimer > 0f)
            UpdateWaveText();
    }

    void Start()
    {
        CreateWaveHud();
        UpdateWaveText(true);
        EnsureElementPool();
        if (planBeforeEachWave && waveCanvasRect == null)
            Debug.LogWarning("WaveManager: no WaveText on a canvas was found, so the wave planning card " +
                "is skipped and waves start without a READY step.", this);
        ClearBossHealthLabel();

        if (useCenterLanes)
            centerLaneOffset = Mathf.Max(centerLaneOffset, 0.95f);

        if (spawnPoint == null)
        {
            waveSpawnFailed = true;
            Debug.LogError("WaveManager needs a Spawn Point.", this);
            enabled = false;
            return;
        }

        if (waves == null || waves.Length == 0)
        {
            waveSpawnFailed = true;
            Debug.LogError("WaveManager needs at least one wave.", this);
            enabled = false;
            return;
        }

        if (routeWaypoints == null || routeWaypoints.Length == 0)
        {
            waveSpawnFailed = true;
            Debug.LogError("WaveManager needs at least one route waypoint; enemies would otherwise remain stationary.", this);
            enabled = false;
            return;
        }

        StartCoroutine(RunWaves());
    }

    void EnsureElementPool()
    {
        if (availableElements != null && availableElements.Length > 0)
            return;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (sceneName.Contains("Fire"))
            availableElements = new[] { ElementType.Grass, ElementType.Earth, ElementType.Electric, ElementType.Water, ElementType.Fire };
        else if (sceneName.Contains("Water"))
            availableElements = new[] { ElementType.Grass, ElementType.Earth, ElementType.Electric, ElementType.Water };
        else if (sceneName.Contains("Electric"))
            availableElements = new[] { ElementType.Grass, ElementType.Earth, ElementType.Electric };
        else if (sceneName.Contains("Earth"))
            availableElements = new[] { ElementType.Grass, ElementType.Earth };
        else
            availableElements = new[] { ElementType.Grass };
    }

    IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(delayBeforeFirstWave);

        for (int index = 0; index < waves.Length; index++)
        {
            currentWaveIndex = index;
            PlanWaveElements(waves[index]);

            if (planBeforeEachWave && waves[index] != null && waveCanvasRect != null)
            {
                if (index > 0)
                    yield return new WaitForSeconds(PlanCardDelay);
                ShowPlanCard(waves[index], index);
                if (PlanShown != null)
                    PlanShown(index);
                while (!planConfirmed)
                    yield return null;
                HidePlanCard();
            }

            waveInProgress = true;
            if (WaveStarted != null)
                WaveStarted(index, IsBossWave(waves[index]));
            bool timed = waves[index] != null && waves[index].IsTimed;
            timedWaveActive = timed;
            timedWaveDuration = timed ? waves[index].spawnDuration : 0f;
            timedWaveRemaining = timedWaveDuration;
            ShowWaveBanner(IsBossWave(waves[index]));
            UpdateWaveText(true);

            if (waves[index] != null && waves[index].IsTimed)
                yield return StartCoroutine(SpawnTimedWave(waves[index]));
            else
                yield return StartCoroutine(SpawnWave(waves[index]));

            while (HasActiveWaveEnemies())
                yield return null;

            if (waveSpawnFailed)
            {
                waveInProgress = false;
                UpdateWaveText(true);
                yield break;
            }

            waveInProgress = false;

            // wave cleared: the Beast flies back to the pad, ready for the next plan card.
            // the last wave goes straight to the stage result instead
            bool lastWave = index == waves.Length - 1;
            if (!lastWave)
            {
                if (BeastController.Active != null)
                    BeastController.Active.RecallToPad();
                StageToast.Show("WAVE CLEAR!", UiTheme.Gold, 1.4f);
                GameAudio.Play(Sfx.WaveClear);
            }
            if (WaveCleared != null)
                WaveCleared(index, lastWave);

            // with planning on, the plan card (and its READY button) replaces the countdown
            if (index < waves.Length - 1 && !planBeforeEachWave)
            {
                nextWaveTimer = delayBetweenWaves;
                while (nextWaveTimer > 0f)
                {
                    UpdateWaveText();
                    yield return null;
                }
            }
        }

        allWavesComplete = true;
        UpdateWaveText();   
    }

    IEnumerator SpawnWave(EnemyWave wave)
    {
        waveSpawnFailed = false;

        if (wave == null)
        {
            waveSpawnFailed = true;
            Debug.LogError("Wave configuration contains an empty wave entry.", this);
            yield break;
        }

        if (wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
        {
            waveSpawnFailed = true;
            Debug.LogError("A wave has no enemy prefabs assigned.", this);
            yield break;
        }

        int amount = Mathf.Max(0, wave.enemyCount);
        if (amount == 0)
        {
            waveSpawnFailed = true;
            Debug.LogError("A wave must have at least one enemy.", this);
            yield break;
        }

        activeWaveEnemies.Clear();
        Enemy previousSpawnedEnemy = null;
        int spawnedInBurst = 0;
        int maximumBatchSize = Mathf.Max(1, maximumSpawnBatchSize);
        int targetBurstSize = randomizeSpawnBursts
            ? Random.Range(1, maximumBatchSize + 1)
            : maximumBatchSize;

        for (int i = 0; i < amount; i++)
        {
            float requiredSpacing = Mathf.Max(0f, minimumHorizontalSpacing);
            if (previousSpawnedEnemy != null)
            {
                Collider2D previousCollider = previousSpawnedEnemy.GetComponent<Collider2D>();
                Enemy nextPrefab = wave.enemyPrefabs[i % wave.enemyPrefabs.Length];
                Collider2D nextCollider = nextPrefab != null ? nextPrefab.GetComponent<Collider2D>() : null;
                if (previousCollider != null && nextCollider != null)
                {
                    requiredSpacing = Mathf.Max(requiredSpacing,
                        Mathf.Max(previousCollider.bounds.extents.x + nextCollider.bounds.extents.x,
                            previousCollider.bounds.extents.y + nextCollider.bounds.extents.y));
                }

                float clearanceDeadline = Time.time + 10f;
                yield return new WaitUntil(() => previousSpawnedEnemy == null ||
                    Vector2.Distance(previousSpawnedEnemy.transform.position, spawnPoint.position) >= requiredSpacing ||
                    Time.time >= clearanceDeadline);

                if (previousSpawnedEnemy != null &&
                    Vector2.Distance(previousSpawnedEnemy.transform.position, spawnPoint.position) < requiredSpacing)
                {
                    waveSpawnFailed = true;
                    Debug.LogError("The previous enemy did not clear the spawn point; stopping this wave to prevent enemy overlap.", this);
                    yield break;
                }
            }

            Enemy prefab = wave.enemyPrefabs[i % wave.enemyPrefabs.Length];
            if (prefab == null)
            {
                waveSpawnFailed = true;
                Debug.LogError("Wave contains a null enemy prefab at index " + (i % wave.enemyPrefabs.Length) + ".", this);
                yield break;
            }
            else
            {
                Vector3 spawnPosition = spawnPoint.position;
                if (randomizeFormation)
                {
                    spawnPosition.x += GetSpawnX(i);
                    spawnPosition.y += Random.Range(-spawnHeightJitter, spawnHeightJitter);
                }

                Enemy enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);

                ElementType spawnElement = i < plannedElements.Count
                    ? plannedElements[i]
                    : PickElement(wave.element);
                enemy.ApplyWaveData(spawnElement, wave.extraHealth, wave.speedMultiplier);
                enemy.SetRoute(routeWaypoints, 0f);
                previousSpawnedEnemy = enemy;

                bool enemyIsBoss = (wave.bossSpawnIndex >= 0 && i == wave.bossSpawnIndex) ||
                    (wave.bossSpawnsLast && i == amount - 1) ||
                    (wave.isBoss && amount == 1);
                if (enemyIsBoss)
                {
                    enemy.SetBoss(wave.bossName, wave.bossHealthBonus, wave.speedMultiplier);
                    enemy.transform.localScale = Vector3.one * 1.5f;
                    // Voltrex / Maelith / Pyrax (chapters 3-5), the other bosses bring their own ability
                    ChapterBossAbility.AttachIfChapterBoss(enemy, this, wave);
                }
                else
                {
                    ApplyWaveVariety(enemy, wave);
                }

                activeWaveEnemies.Add(enemy);
            }

            if (i < amount - 1)
            {
                float enemySpeed = previousSpawnedEnemy != null ? previousSpawnedEnemy.moveSpeed : 0f;
                float spacingDelay = enemySpeed > 0f
                    ? requiredSpacing / enemySpeed
                    : 0f;
                float spawnDelay = Mathf.Max(0.8f, wave.spawnInterval, spacingDelay);

                if (randomizeSpawnBursts)
                {
                    spawnedInBurst++;
                    if (spawnedInBurst >= targetBurstSize)
                    {
                        float minimumDelay = Mathf.Max(0f, minimumSpawnDelay);
                        float maximumDelay = Mathf.Max(minimumDelay, maximumSpawnDelay);
                        spawnDelay = Mathf.Max(spawnDelay, Random.Range(minimumDelay, maximumDelay));
                        spawnedInBurst = 0;
                        targetBurstSize = Random.Range(1, maximumBatchSize + 1);
                    }
                }

                yield return new WaitForSeconds(spawnDelay);
            }
        }
    }

    // keeps spawning random enemies from the wave's table until its time runs out.
    // fixed count entries (like "exactly 4 Earth scouts") are spread over the time and
    // always all spawn, even if the limit on the field delays them past the end
    IEnumerator SpawnTimedWave(EnemyWave wave)
    {
        waveSpawnFailed = false;
        activeWaveEnemies.Clear();
        fixedSchedule.Clear();

        float fillerWeight = 0f;
        for (int e = 0; e < wave.spawnTable.Length; e++)
        {
            EnemySpawnEntry entry = wave.spawnTable[e];
            if (entry == null || entry.prefab == null)
            {
                waveSpawnFailed = true;
                timedWaveActive = false;
                Debug.LogError("Wave '" + wave.waveName + "' has a Spawn Table entry without a prefab (slot " + e + ").", this);
                yield break;
            }

            if (entry.fixedCount > 0)
            {
                for (int k = 0; k < entry.fixedCount; k++)
                {
                    float slot = (k + Random.Range(0.2f, 0.8f)) / entry.fixedCount;
                    fixedSchedule.Add(new ScheduledSpawn { time = wave.spawnDuration * slot, entry = e });
                }
            }
            else
            {
                fillerWeight += Mathf.Max(0f, entry.weight);
            }
        }

        if (fillerWeight <= 0f && fixedSchedule.Count == 0)
        {
            waveSpawnFailed = true;
            timedWaveActive = false;
            Debug.LogError("Wave '" + wave.waveName + "' is timed but no Spawn Table entry has a Weight or Fixed Count.", this);
            yield break;
        }
        fixedSchedule.Sort(ByTime);

        float gapMin = Mathf.Max(0.4f, Mathf.Min(wave.spawnGap.x, wave.spawnGap.y));
        float gapMax = Mathf.Max(gapMin, Mathf.Max(wave.spawnGap.x, wave.spawnGap.y));
        float elapsed = 0f;
        float nextSpawn = 0f;
        int burstLeft = 0;
        int nextFixed = 0;
        timedWaveDuration = wave.spawnDuration;
        timedWaveRemaining = wave.spawnDuration;
        timedWaveActive = true;

        // the enemies come in small groups with a short break between them (GameTuning: Waves).
        // during a break nothing spawns and the wave clock waits too, so the wave still sends the same
        // number of enemies, just spread out more, and you get a moment to catch up
        GameTuning tuning = GameTuning.Get;
        int groupSize = Mathf.Max(0, tuning.enemiesPerGroup);
        float groupBreak = Mathf.Max(0f, tuning.breakBetweenGroups);
        int spawnedInGroup = 0;
        float breakLeft = 0f;

        while (elapsed < wave.spawnDuration || nextFixed < fixedSchedule.Count)
        {
            yield return null;
            if (breakLeft > 0f)
            {
                breakLeft -= Time.deltaTime;
                continue;
            }
            elapsed += Time.deltaTime;
            timedWaveRemaining = Mathf.Max(0f, wave.spawnDuration - elapsed);

            if (elapsed < nextSpawn)
                continue;
            if (wave.maxAlive > 0 && CountActiveWaveEnemies() >= wave.maxAlive)
                continue;

            bool timeLeft = elapsed < wave.spawnDuration;
            bool fixedDue = nextFixed < fixedSchedule.Count &&
                (fixedSchedule[nextFixed].time <= elapsed || !timeLeft);
            int entryIndex = fixedDue
                ? fixedSchedule[nextFixed].entry
                : timeLeft && fillerWeight > 0f ? PickFillerEntry(wave.spawnTable, fillerWeight) : -1;
            if (entryIndex < 0)
                continue;

            EnemySpawnEntry chosen = wave.spawnTable[entryIndex];
            if (!TryFindTimedSpawnPosition(out Vector3 position))
            {
                nextSpawn = elapsed + 0.25f; // the spawn area is crowded, try again soon
                continue;
            }

            Enemy enemy = Instantiate(chosen.prefab, position, Quaternion.identity);
            enemy.ApplyWaveData(chosen.element, wave.extraHealth + Mathf.Max(0, chosen.extraHealth),
                wave.speedMultiplier * Mathf.Max(0.1f, chosen.speedMultiplier));
            enemy.SetRoute(routeWaypoints, 0f);
            ApplyVariety(enemy, chosen.spriteOverride, chosen.scale);
            activeWaveEnemies.Add(enemy);
            if (fixedDue)
                nextFixed++;

            if (burstLeft > 0)
            {
                burstLeft--;
                nextSpawn = elapsed + BurstSpawnGap;
            }
            else if (randomizeSpawnBursts && maximumSpawnBatchSize > 1 && Random.value < 0.3f)
            {
                burstLeft = Random.Range(1, maximumSpawnBatchSize) - 1;
                nextSpawn = elapsed + BurstSpawnGap;
            }
            else
            {
                nextSpawn = elapsed + Random.Range(gapMin, gapMax);
            }

            // a full group: take a break before the next one
            spawnedInGroup++;
            if (groupSize > 0 && groupBreak > 0f && spawnedInGroup >= groupSize)
            {
                spawnedInGroup = 0;
                burstLeft = 0;
                nextSpawn = elapsed + Random.Range(gapMin, gapMax);
                breakLeft = groupBreak;
            }
        }

        timedWaveRemaining = 0f;
        timedWaveActive = false;
    }

    static int PickFillerEntry(EnemySpawnEntry[] table, float totalWeight)
    {
        float roll = Random.value * totalWeight;
        int last = -1;
        for (int i = 0; i < table.Length; i++)
        {
            if (table[i].fixedCount > 0 || table[i].weight <= 0f)
                continue;
            last = i;
            roll -= table[i].weight;
            if (roll <= 0f)
                return i;
        }
        return last;
    }

    bool TryFindTimedSpawnPosition(out Vector3 position)
    {
        float clearance = Mathf.Max(minimumHorizontalSpacing, TimedSpawnClearance);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 candidate = spawnPoint.position;
            if (randomizeFormation)
            {
                candidate.x += Random.Range(-spawnWidth, spawnWidth);
                candidate.y += Random.Range(-spawnHeightJitter, spawnHeightJitter);
            }

            bool clear = true;
            for (int i = 0; i < activeWaveEnemies.Count; i++)
            {
                Enemy other = activeWaveEnemies[i];
                if (other != null && Vector2.Distance(other.transform.position, candidate) < clearance)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
            {
                position = candidate;
                return true;
            }
        }

        position = spawnPoint.position;
        return false;
    }

    // an enemy that joins the running wave in the middle of the path (Pyrax's Ember Call).
    // the wave waits for it like any other enemy
    public Enemy SpawnReinforcement(Enemy prefab, ElementType element, Sprite sprite, float scale,
        Vector3 position, int routeIndex)
    {
        if (prefab == null)
            return null;

        Enemy enemy = Instantiate(prefab, position, Quaternion.identity);
        EnemyWave wave = currentWaveIndex >= 0 && currentWaveIndex < waves.Length ? waves[currentWaveIndex] : null;
        enemy.ApplyWaveData(element, wave != null ? wave.extraHealth : 0, wave != null ? wave.speedMultiplier : 1f);
        enemy.SetRoute(routeWaypoints, 0f, routeIndex);
        ApplyVariety(enemy, sprite, scale);
        activeWaveEnemies.Add(enemy);
        return enemy;
    }

    static void ApplyWaveVariety(Enemy enemy, EnemyWave wave)
    {
        if (wave != null)
            ApplyVariety(enemy, wave.enemySprite, wave.enemyScale);
    }

    static void ApplyVariety(Enemy enemy, Sprite spriteOverride, float enemyScale)
    {
        if (enemy == null)
            return;

        if (spriteOverride != null)
        {
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = spriteOverride;
            // a cracked "damaged" sprite belongs to the prefab's own art, not the override
            EnemyDamagedSprite damaged = enemy.GetComponent<EnemyDamagedSprite>();
            if (damaged != null)
                damaged.damagedSprite = null;
            // override art is already painted in its element's colors, an element tint would just make it muddy
            enemy.elementTintStrength = 0f;
            enemy.SetElementForEditor(enemy.element);
        }

        float scale = enemyScale > 0.01f ? enemyScale : 1f;
        if (!Mathf.Approximately(scale, 1f))
        {
            enemy.transform.localScale *= scale;
            EnemyHitFeedback hitFeedback = enemy.GetComponent<EnemyHitFeedback>();
            if (hitFeedback != null)
                hitFeedback.SetBasePose(enemy.transform.localScale, enemy.transform.localRotation);
        }
    }

    float GetSpawnX(int index)
    {
        float candidate = Random.Range(-spawnWidth, spawnWidth);
        int attempts = 0;

        while (attempts < 8)
        {
            bool hasSpacing = true;

            for (int spawnedIndex = 0; spawnedIndex < index && spawnedIndex < activeWaveEnemies.Count; spawnedIndex++)
            {
                Enemy previous = activeWaveEnemies[spawnedIndex];

                if (previous != null && Mathf.Abs(previous.transform.position.x - (spawnPoint.position.x + candidate)) < minimumHorizontalSpacing)
                {
                    hasSpacing = false;
                    break;
                }
            }

            if (hasSpacing)
                break;

            candidate = Random.Range(-spawnWidth, spawnWidth);
            attempts++;
        }

        return candidate;
    }

    void ClearBossHealthLabel()
    {
        GameObject labelObject = GameObject.Find("BossHealthText");
        TMP_Text label = labelObject != null ? labelObject.GetComponent<TMP_Text>() : null;

        if (label == null)
            return;

        label.text = string.Empty;
        label.enabled = false;
    }

    ElementType PickElement(ElementType fallback)
    {
        if (!randomizeElements || availableElements == null || availableElements.Length == 0)
            return fallback;

        return availableElements[Random.Range(0, availableElements.Length)];
    }

    // picks each spawn's element first, so the plan card shows exactly what will come
    void PlanWaveElements(EnemyWave wave)
    {
        plannedElements.Clear();
        if (wave == null || wave.IsTimed)
            return;

        bool hasPattern = wave.elementPattern != null && wave.elementPattern.Length > 0;
        int amount = Mathf.Max(0, wave.enemyCount);
        for (int i = 0; i < amount; i++)
        {
            plannedElements.Add(hasPattern
                ? wave.elementPattern[i % wave.elementPattern.Length]
                : PickElement(wave.element));
        }
    }

    static bool SpawnIsBoss(EnemyWave wave, int spawnIndex, int amount)
    {
        return (wave.bossSpawnIndex >= 0 && spawnIndex == wave.bossSpawnIndex) ||
            (wave.bossSpawnsLast && spawnIndex == amount - 1) ||
            (wave.isBoss && amount == 1);
    }

    // "Enemy_DustScout" -> "Dust Scout". a wave with only one type and its own name
    // ("Thicket Stragglers") uses that name instead
    static string GetEnemyDisplayName(EnemyWave wave, Enemy prefab)
    {
        if (!string.IsNullOrWhiteSpace(wave.enemyDisplayName))
            return wave.enemyDisplayName.Trim();

        string waveName = wave.waveName != null ? wave.waveName.Trim() : string.Empty;
        bool namedWave = waveName.Length > 0 && waveName != "Wave" && !waveName.EndsWith(" Wave");
        if (namedWave && wave.enemyPrefabs.Length == 1)
            return waveName.EndsWith("s") ? waveName.Substring(0, waveName.Length - 1) : waveName;

        string raw = prefab != null ? prefab.name : string.Empty;
        if (raw.StartsWith("Enemy_"))
            raw = raw.Substring("Enemy_".Length);
        bool hasLetter = false;
        foreach (char character in raw)
            hasLetter |= char.IsLetter(character);
        if (!hasLetter)
            return SharedCritterName;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length + 4);
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                builder.Append(' ');
            builder.Append(raw[i] == '_' ? ' ' : raw[i]);
        }
        return builder.ToString();
    }

    static string GetEntryDisplayName(EnemySpawnEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.displayName))
            return entry.displayName.Trim();
        string raw = entry.prefab != null ? entry.prefab.name : string.Empty;
        if (raw.StartsWith("Enemy_"))
            raw = raw.Substring("Enemy_".Length);
        System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length + 4);
        bool hasLetter = false;
        for (int i = 0; i < raw.Length; i++)
        {
            hasLetter |= char.IsLetter(raw[i]);
            if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                builder.Append(' ');
            builder.Append(raw[i] == '_' ? ' ' : raw[i]);
        }
        return hasLetter ? builder.ToString() : SharedCritterName;
    }

    void BuildTimedPlanGroups(EnemyWave wave)
    {
        float fillerWeight = 0f;
        foreach (EnemySpawnEntry entry in wave.spawnTable)
        {
            if (entry != null && entry.prefab != null && entry.fixedCount <= 0)
                fillerWeight += Mathf.Max(0f, entry.weight);
        }

        foreach (EnemySpawnEntry entry in wave.spawnTable)
        {
            if (entry == null || entry.prefab == null || (entry.fixedCount <= 0 && entry.weight <= 0f))
                continue;
            string name = GetEntryDisplayName(entry);
            bool isFixed = entry.fixedCount > 0;

            PlanGroup group = null;
            foreach (PlanGroup existing in planGroups)
            {
                if (existing.element == entry.element && existing.name == name && (existing.count > 0) == isFixed)
                {
                    group = existing;
                    break;
                }
            }
            if (group == null)
            {
                group = new PlanGroup { name = name, element = entry.element, trait = EntryTrait(entry) };
                planGroups.Add(group);
            }
            if (isFixed)
                group.count += entry.fixedCount;
            else if (fillerWeight > 0f)
                group.share += entry.weight / fillerWeight;
        }
    }

    static string EntryTrait(EnemySpawnEntry entry)
    {
        if (entry.extraHealth >= 2)
            return "TOUGH";
        if (entry.speedMultiplier >= 1.2f)
            return "FAST";
        return null;
    }

    // more enemy kinds than rows: show one row per element instead ("Dust Scout & Burrower +1"),
    // so an element (like a couple of Electric scouts) never gets hidden under another one
    void MergePlanGroupsByElement()
    {
        if (planGroups.Count <= MaxPlanRows)
            return;

        List<PlanGroup> merged = new List<PlanGroup>(MaxPlanRows + 1);
        foreach (PlanGroup group in planGroups)
        {
            PlanGroup target = null;
            foreach (PlanGroup existing in merged)
            {
                if (existing.element == group.element && existing.boss == group.boss)
                {
                    target = existing;
                    break;
                }
            }
            if (target == null)
            {
                target = new PlanGroup { name = group.name, element = group.element, boss = group.boss, trait = group.trait };
                merged.Add(target);
            }
            else
            {
                if (target.kinds < 2)
                    target.name += " & " + group.name;
                if (target.trait != group.trait)
                    target.trait = null; // a mix of tough and fast kinds: no single trait to show
            }
            target.kinds++;
            target.count += group.count;
            target.share += group.share;
        }

        foreach (PlanGroup group in merged)
        {
            if (group.kinds > 2)
                group.name += " +" + (group.kinds - 2);
        }
        planGroups.Clear();
        planGroups.AddRange(merged);
    }

    void BuildPlanGroups(EnemyWave wave)
    {
        planGroups.Clear();
        if (wave.IsTimed)
        {
            BuildTimedPlanGroups(wave);
            MergePlanGroupsByElement();
            return;
        }
        if (wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
            return;

        int amount = plannedElements.Count;
        for (int i = 0; i < amount; i++)
        {
            bool boss = SpawnIsBoss(wave, i, amount);
            string name = boss
                ? GetBossDisplayName(wave)
                : GetEnemyDisplayName(wave, wave.enemyPrefabs[i % wave.enemyPrefabs.Length]);
            ElementType element = plannedElements[i];

            PlanGroup group = null;
            foreach (PlanGroup existing in planGroups)
            {
                if (existing.boss == boss && existing.element == element && existing.name == name)
                {
                    group = existing;
                    break;
                }
            }
            if (group == null)
            {
                group = new PlanGroup { name = name, element = element, boss = boss };
                planGroups.Add(group);
            }
            group.count++;
        }

        // bosses first (they decide the wave), everything else keeps its spawn order
        int insertAt = 0;
        for (int i = 0; i < planGroups.Count; i++)
        {
            if (!planGroups[i].boss)
                continue;
            PlanGroup bossGroup = planGroups[i];
            planGroups.RemoveAt(i);
            planGroups.Insert(insertAt++, bossGroup);
        }
    }

    void ShowPlanCard(EnemyWave wave, int waveIndex)
    {
        planConfirmed = false;
        BuildPlanCard();
        if (planCard == null)
        {
            planConfirmed = true;
            return;
        }

        if (padSwap == null)
            padSwap = Object.FindAnyObjectByType<PadSwapSystem>();

        BuildPlanGroups(wave);
        string waveName = wave.waveName != null ? wave.waveName.Trim() : string.Empty;
        bool namedWave = waveName.Length > 0 && waveName != "Wave" && !waveName.EndsWith(" Wave");
        string subtitle = namedWave ? waveName.ToUpperInvariant() : string.Empty;
        if (wave.IsTimed)
            subtitle += (subtitle.Length > 0 ? "  ·  " : string.Empty) + Mathf.RoundToInt(wave.spawnDuration) + " SECONDS";
        planTitleText.text = (IsBossWave(wave) ? "BOSS WAVE" : "WAVE " + (waveIndex + 1) + " / " + waves.Length) +
            (subtitle.Length > 0 ? "\n<size=58%><color=#E8DDB8>" + subtitle + "</color></size>" : string.Empty);

        planCard.SetActive(true);
        planCard.transform.SetAsLastSibling();
        planVisible = true;
        planBeastShown = false;
        LayoutPlanCard(true);
        UpdatePlanCard();
    }

    void HidePlanCard()
    {
        planVisible = false;
        if (planCard != null)
            planCard.SetActive(false);
    }

    void ConfirmPlan()
    {
        planConfirmed = true;
    }

    // refreshes the rows when the player swaps Beasts, so the "ON PAD" pill always shows who's on the pad
    void UpdatePlanCard()
    {
        LayoutPlanCard(false);

        BeastController beast = BeastController.Active;
        BeastId active = beast != null ? beast.ActiveBeast : BeastId.Choma;
        if (planBeastShown && active == planShownBeast)
            return;
        planBeastShown = true;
        planShownBeast = active;
        ElementType activeElement = BeastRoster.GetElement(active);

        int shownGroups = Mathf.Min(planGroups.Count, MaxPlanRows);
        for (int row = 0; row < MaxPlanRows; row++)
        {
            bool used = row < shownGroups;
            planRowRects[row].gameObject.SetActive(used);
            if (!used)
                continue;

            PlanGroup group = planGroups[row];
            int count = group.count;
            float share = group.share;
            // put anything past the last row into it, so nothing is hidden
            if (row == MaxPlanRows - 1 && planGroups.Count > MaxPlanRows)
            {
                for (int extra = MaxPlanRows; extra < planGroups.Count; extra++)
                {
                    count += planGroups[extra].count;
                    share += planGroups[extra].share;
                }
            }

            ElementType counter = ElementVisuals.GetCounter(group.element);
            bool hasCounter = FindSquadBeastWithElement(counter, out BeastId counterBeast);
            bool strong = ElementChart.IsSuperEffective(activeElement, group.element);

            string often = share >= 0.5f ? "lots" : share >= 0.25f ? "often" : "a few";
            string amount = count > 0 && share > 0f
                ? "  <color=#FFD54D>x" + count + "</color><color=#C9BE98> + " + often + "</color>"
                : count > 0 ? "  <color=#FFD54D>x" + count + "</color>"
                : "  <color=#C9BE98>" + often + "</color>";
            planRowIcons[row].sprite = ElementVisuals.GetIcon(group.element);
            planRowNames[row].text = (group.boss ? "<color=#FF8A6B>BOSS</color>  " : string.Empty) + group.name + amount;
            planRowDetails[row].text =
                "<color=#" + ElementVisuals.GetHex(group.element) + ">" + ElementVisuals.GetName(group.element) + "</color>" +
                "<color=#C9BE98>  ·  weak to  </color>" +
                "<color=#" + ElementVisuals.GetHex(counter) + ">" + ElementVisuals.GetName(counter) + "</color>" +
                (string.IsNullOrEmpty(group.trait) ? string.Empty : "<color=#C9BE98>  ·  </color><color=#FFB070>" + group.trait + "</color>");

            planRowBacks[row].color = strong ? (Color)PlanRowStrongColor : (Color)PlanRowColor;
            if (hasCounter)
            {
                Color pill = ElementVisuals.GetColor(counter);
                planRowPills[row].color = Color.Lerp(pill, Color.black, 0.28f);
                planRowPillOutlines[row].effectColor = strong ? (Color)UiTheme.Gold : (Color)UiTheme.Ink;
                planRowPillTexts[row].text = "<size=58%>" + (strong ? "ON PAD" : "USE") + "</size>\n" +
                    BeastRoster.GetName(counterBeast).ToUpperInvariant();
            }
            else
            {
                planRowPills[row].color = new Color32(70, 74, 66, 255);
                planRowPillOutlines[row].effectColor = UiTheme.Ink;
                planRowPillTexts[row].text = "<size=58%>NO BEAST</size>\nYET";
            }
        }

        planHintText.text = "On the pad: <color=#" + ElementVisuals.GetHex(activeElement) + ">" +
            BeastRoster.GetName(active).ToUpperInvariant() + "</color>   ·   " +
            (padSwap != null && padSwap.ActiveSquad.Count > 1 ? "SWAP to change, then READY" : "Tap READY when you're set");
    }

    bool FindSquadBeastWithElement(ElementType element, out BeastId found)
    {
        found = BeastId.Choma;
        if (padSwap == null)
            return false;
        IReadOnlyList<BeastId> squadMembers = padSwap.ActiveSquad;
        for (int i = 0; i < squadMembers.Count; i++)
        {
            if (BeastRoster.GetElement(squadMembers[i]) == element)
            {
                found = squadMembers[i];
                return true;
            }
        }
        return false;
    }

    void BuildPlanCard()
    {
        if (planCard != null || waveCanvasRect == null)
            return;

        planCard = new GameObject("WavePlanCard");
        planCard.transform.SetParent(waveCanvasRect, false);
        planCardRect = planCard.AddComponent<RectTransform>();
        planCardRect.anchorMin = new Vector2(0.5f, 0.5f);
        planCardRect.anchorMax = new Vector2(0.5f, 0.5f);
        planCardRect.pivot = new Vector2(0.5f, 0.5f);
        Image cardImage = planCard.AddComponent<Image>();
        cardImage.sprite = HudVisualUtility.RoundedSprite();
        cardImage.type = Image.Type.Sliced;
        cardImage.color = new Color32(34, 42, 31, 246);
        cardImage.raycastTarget = true; // taps on the card never start a launch
        planCardOutline = HudVisualUtility.AddOutline(planCard, UiTheme.PanelBorder, new Vector2(5f, -5f));
        Shadow cardShadow = UiTheme.GetOrAddEffect<Shadow>(planCard);
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        cardShadow.effectDistance = new Vector2(0f, -12f);
        planCard.AddComponent<UiPopIn>();

        planTitleText = HudVisualUtility.AddTmpText(planCard, "PlanTitle", "WAVE", 60f, TextAlignmentOptions.Center);
        planTitleText.richText = true;
        planTitleText.textWrappingMode = TextWrappingModes.Normal;
        TopAnchor(planTitleText.rectTransform);

        for (int row = 0; row < MaxPlanRows; row++)
        {
            GameObject rowObject = new GameObject("PlanRow" + row);
            rowObject.transform.SetParent(planCard.transform, false);
            planRowRects[row] = rowObject.AddComponent<RectTransform>();
            TopAnchor(planRowRects[row]);
            planRowBacks[row] = rowObject.AddComponent<Image>();
            planRowBacks[row].sprite = HudVisualUtility.RoundedSprite();
            planRowBacks[row].type = Image.Type.Sliced;
            planRowBacks[row].color = PlanRowColor;
            planRowBacks[row].raycastTarget = false;

            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(rowObject.transform, false);
            planRowIcons[row] = iconObject.AddComponent<Image>();
            planRowIcons[row].raycastTarget = false;
            planRowIcons[row].preserveAspect = true;
            RectTransform iconRect = planRowIcons[row].rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);

            planRowNames[row] = HudVisualUtility.AddTmpText(rowObject, "Name", string.Empty, 40f, TextAlignmentOptions.BottomLeft);
            planRowNames[row].richText = true;
            planRowNames[row].overflowMode = TextOverflowModes.Ellipsis;
            planRowDetails[row] = HudVisualUtility.AddTmpText(rowObject, "Detail", string.Empty, 30f, TextAlignmentOptions.TopLeft);
            planRowDetails[row].richText = true;
            planRowDetails[row].overflowMode = TextOverflowModes.Ellipsis;

            Image pill = HudVisualUtility.AddImage(rowObject, "BeastPill", UiTheme.Teal);
            pill.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            pill.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            pill.rectTransform.pivot = new Vector2(1f, 0.5f);
            planRowPills[row] = pill;
            planRowPillOutlines[row] = HudVisualUtility.AddOutline(pill.gameObject, UiTheme.Ink, new Vector2(4f, -4f));
            planRowPillTexts[row] = HudVisualUtility.AddTmpText(pill.gameObject, "Label", string.Empty, 34f, TextAlignmentOptions.Center);
            planRowPillTexts[row].richText = true;
            planRowPillTexts[row].lineSpacing = -18f;
        }

        planHintText = HudVisualUtility.AddTmpText(planCard, "PlanHint", string.Empty, 30f, TextAlignmentOptions.Center);
        planHintText.richText = true;
        planHintText.color = new Color32(232, 221, 184, 255);
        TopAnchor(planHintText.rectTransform);

        GameObject buttonObject = new GameObject("ReadyButton");
        buttonObject.transform.SetParent(planCard.transform, false);
        planButtonRect = buttonObject.AddComponent<RectTransform>();
        TopAnchor(planButtonRect);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = HudVisualUtility.RoundedSprite();
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = UiTheme.Orange;
        buttonImage.raycastTarget = true;
        HudVisualUtility.AddOutline(buttonObject, UiTheme.Ink, new Vector2(5f, -5f));
        Shadow buttonShadow = UiTheme.GetOrAddEffect<Shadow>(buttonObject);
        buttonShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        buttonShadow.effectDistance = new Vector2(0f, -8f);
        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(ConfirmPlan);
        buttonObject.AddComponent<UiButtonFeedback>();
        planButtonLabel = HudVisualUtility.AddTmpText(buttonObject, "Label", "READY!", 54f, TextAlignmentOptions.Center);

        planCard.SetActive(false);
    }

    static void TopAnchor(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
    }

    // same 1080x1920 reference scaling as the wave banner. text never auto-sizes below about
    // 24 reference px, so it stays readable on a phone
    void LayoutPlanCard(bool force)
    {
        if (planCardRect == null || waveCanvasRect == null)
            return;

        Vector2 canvasSize = waveCanvasRect.rect.size;
        if (!force && canvasSize == planLaidOutCanvasSize)
            return;
        planLaidOutCanvasSize = canvasSize;

        float scale = Mathf.Min(canvasSize.x / 1080f, canvasSize.y / 1920f);
        int rows = Mathf.Clamp(planGroups.Count, 1, MaxPlanRows);
        float width = Mathf.Min(940f * scale, canvasSize.x - 48f * scale);
        float pad = 32f * scale;
        float inner = width - pad * 2f;
        float titleHeight = 136f * scale;
        float rowHeight = 120f * scale;
        float rowGap = 12f * scale;
        float hintHeight = 72f * scale;
        float buttonHeight = 118f * scale;
        float height = pad + titleHeight + rows * (rowHeight + rowGap) + hintHeight + 12f * scale + buttonHeight + pad;

        planCardRect.sizeDelta = new Vector2(width, height);
        planCardRect.anchoredPosition = new Vector2(0f, 150f * scale);
        planCardOutline.effectDistance = new Vector2(5f * scale, -5f * scale);

        float y = pad;
        Place(planTitleText.rectTransform, inner, titleHeight, ref y);
        SizeText(planTitleText, 62f * scale, 34f * scale);

        float iconSize = 88f * scale;
        float rowInset = 16f * scale;
        float pillWidth = 214f * scale;
        float pillHeight = 88f * scale;
        float textLeft = rowInset + iconSize + 20f * scale;
        float textRight = rowInset + pillWidth + 18f * scale;
        for (int row = 0; row < MaxPlanRows; row++)
        {
            Place(planRowRects[row], inner, rowHeight, ref y, row < rows);
            if (row < rows)
                y += rowGap;

            RectTransform iconRect = planRowIcons[row].rectTransform;
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(rowInset, 0f);

            RectTransform nameRect = planRowNames[row].rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(textLeft, 0f);
            nameRect.offsetMax = new Vector2(-textRight, -8f * scale);
            SizeText(planRowNames[row], 42f * scale, 28f * scale);

            RectTransform detailRect = planRowDetails[row].rectTransform;
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 0.5f);
            detailRect.offsetMin = new Vector2(textLeft, 8f * scale);
            detailRect.offsetMax = new Vector2(-textRight, -2f * scale);
            SizeText(planRowDetails[row], 31f * scale, 24f * scale);

            RectTransform pillRect = planRowPills[row].rectTransform;
            pillRect.sizeDelta = new Vector2(pillWidth, pillHeight);
            pillRect.anchoredPosition = new Vector2(-rowInset, 0f);
            RectTransform pillLabel = planRowPillTexts[row].rectTransform;
            pillLabel.offsetMin = new Vector2(8f * scale, 4f * scale);
            pillLabel.offsetMax = new Vector2(-8f * scale, -4f * scale);
            SizeText(planRowPillTexts[row], 36f * scale, 24f * scale);
        }

        Place(planHintText.rectTransform, inner, hintHeight, ref y);
        SizeText(planHintText, 31f * scale, 24f * scale);
        y += 12f * scale;
        Place(planButtonRect, 500f * scale, buttonHeight, ref y);
        SizeText(planButtonLabel, 56f * scale, 34f * scale);
    }

    static void Place(RectTransform rect, float width, float height, ref float y, bool advance = true)
    {
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, -y);
        if (advance)
            y += height;
    }

    static void SizeText(TMP_Text text, float maxSize, float minSize)
    {
        text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(12f, maxSize);
        text.fontSizeMin = Mathf.Clamp(minSize, 10f, text.fontSizeMax);
        text.fontSize = text.fontSizeMax;
    }

    private void CreateWaveHud()
    {
        if (waveText == null && legacyWaveText == null)
        {
            GameObject sourceObject = GameObject.Find("WaveText");
            if (sourceObject != null)
            {
                waveText = sourceObject.GetComponent<TMP_Text>();
                if (waveText == null)
                    legacyWaveText = sourceObject.GetComponent<UnityEngine.UI.Text>();
            }
        }

        RectTransform sourceRect = waveText != null
            ? waveText.rectTransform
            : legacyWaveText != null
                ? legacyWaveText.rectTransform
                : null;
        if (sourceRect == null)
            return;

        waveCanvasRect = sourceRect.parent as RectTransform;
        if (waveCanvasRect == null)
            return;

        Transform panelTransform = waveCanvasRect.Find("WaveInfoPanel");
        GameObject panel = panelTransform != null ? panelTransform.gameObject : new GameObject("WaveInfoPanel");
        panel.transform.SetParent(waveCanvasRect, false);
        wavePanelRect = panel.GetComponent<RectTransform>();
        if (wavePanelRect == null)
            wavePanelRect = panel.AddComponent<RectTransform>();

        wavePanelImage = panel.GetComponent<Image>();
        if (wavePanelImage == null)
            wavePanelImage = panel.AddComponent<Image>();
        wavePanelImage.sprite = HudVisualUtility.RoundedSprite();
        wavePanelImage.type = Image.Type.Sliced;
        wavePanelImage.raycastTarget = false;
        wavePanelOutline = HudVisualUtility.AddOutline(
            panel, new Color32(138, 143, 115, 255), new Vector2(6f, -6f));

        wavePanelGroup = panel.GetComponent<CanvasGroup>();
        if (wavePanelGroup == null)
            wavePanelGroup = panel.AddComponent<CanvasGroup>();
        wavePanelGroup.blocksRaycasts = false;
        wavePanelGroup.interactable = false;
        wavePanelGroup.alpha = 0f;

        waveTitleText = HudVisualUtility.AddTmpText(
            panel, "Title", "WAVE 1 / 3", 52f, TextAlignmentOptions.Center);
        waveEnemiesText = HudVisualUtility.AddTmpText(
            panel, "Enemies", "Enemies left: 0", 34f, TextAlignmentOptions.Center);
        RectTransform titleRect = waveTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.48f);
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(20f, 0f);
        titleRect.offsetMax = new Vector2(-20f, -10f);
        RectTransform enemiesRect = waveEnemiesText.rectTransform;
        enemiesRect.anchorMin = Vector2.zero;
        enemiesRect.anchorMax = new Vector2(1f, 0.48f);
        enemiesRect.offsetMin = new Vector2(20f, 14f);
        enemiesRect.offsetMax = new Vector2(-20f, 0f);
        waveTitleText.enableAutoSizing = true;
        waveTitleText.fontSizeMin = 30f;
        waveTitleText.fontSizeMax = 56f;
        waveEnemiesText.enableAutoSizing = true;
        waveEnemiesText.fontSizeMin = 26f;
        waveEnemiesText.fontSizeMax = 36f;
        waveEnemiesText.color = new Color32(232, 221, 184, 255);

        if (waveText != null)
            waveText.enabled = false;
        if (legacyWaveText != null)
            legacyWaveText.enabled = false;
        CreateWaveChip();
        RefreshWaveHudLayout(true);
    }

    private void CreateWaveChip()
    {
        Transform existing = waveCanvasRect.Find("WaveStatusChip");
        GameObject chip = existing != null ? existing.gameObject : new GameObject("WaveStatusChip");
        chip.transform.SetParent(waveCanvasRect, false);
        waveChipRect = chip.GetComponent<RectTransform>();
        if (waveChipRect == null)
            waveChipRect = chip.AddComponent<RectTransform>();
        waveChipRect.anchorMin = new Vector2(0.5f, 1f);
        waveChipRect.anchorMax = new Vector2(0.5f, 1f);
        waveChipRect.pivot = new Vector2(0.5f, 1f);

        waveChipImage = chip.GetComponent<Image>();
        if (waveChipImage == null)
            waveChipImage = chip.AddComponent<Image>();
        waveChipImage.sprite = HudVisualUtility.RoundedSprite();
        waveChipImage.type = Image.Type.Sliced;
        waveChipImage.color = new Color32(34, 42, 31, 228);
        waveChipImage.raycastTarget = false;
        waveChipOutline = HudVisualUtility.AddOutline(chip, UiTheme.PanelBorder, new Vector2(4f, -4f));

        waveChipGroup = chip.GetComponent<CanvasGroup>();
        if (waveChipGroup == null)
            waveChipGroup = chip.AddComponent<CanvasGroup>();
        waveChipGroup.blocksRaycasts = false;
        waveChipGroup.interactable = false;
        waveChipGroup.alpha = 0f;

        waveChipLeftText = HudVisualUtility.AddTmpText(chip, "Wave", "WAVE 1/3", 40f, TextAlignmentOptions.Left);
        waveChipRightText = HudVisualUtility.AddTmpText(chip, "Status", string.Empty, 44f, TextAlignmentOptions.Right);
        waveChipRightText.richText = true;

        Image track = HudVisualUtility.AddImage(chip, "TimeTrack", new Color32(18, 22, 16, 255));
        track.rectTransform.anchorMin = new Vector2(0f, 0f);
        track.rectTransform.anchorMax = new Vector2(1f, 0f);
        track.rectTransform.pivot = new Vector2(0.5f, 0f);
        waveChipBarImage = HudVisualUtility.AddImage(track.gameObject, "TimeFill", new Color32(120, 200, 90, 255));
        waveChipBarFill = waveChipBarImage.rectTransform;
        waveChipBarFill.anchorMin = Vector2.zero;
        waveChipBarFill.anchorMax = Vector2.one;
        waveChipBarFill.offsetMin = Vector2.zero;
        waveChipBarFill.offsetMax = Vector2.zero;
        chip.transform.SetAsFirstSibling(); // under buttons and cards
    }

    private void LayoutWaveChip(float scale)
    {
        if (waveChipRect == null)
            return;

        float width = 560f * scale;
        float height = 100f * scale;
        waveChipRect.sizeDelta = new Vector2(width, height);
        waveChipRect.anchoredPosition = waveTargetPosition;
        waveChipOutline.effectDistance = new Vector2(4f * scale, -4f * scale);

        float side = 28f * scale;
        RectTransform left = waveChipLeftText.rectTransform;
        left.anchorMin = new Vector2(0f, 0f);
        left.anchorMax = new Vector2(0.55f, 1f);
        left.offsetMin = new Vector2(side, 16f * scale);
        left.offsetMax = new Vector2(0f, -8f * scale);
        SizeText(waveChipLeftText, 42f * scale, 28f * scale);

        RectTransform right = waveChipRightText.rectTransform;
        right.anchorMin = new Vector2(0.45f, 0f);
        right.anchorMax = new Vector2(1f, 1f);
        right.offsetMin = new Vector2(0f, 16f * scale);
        right.offsetMax = new Vector2(-side, -8f * scale);
        SizeText(waveChipRightText, 46f * scale, 28f * scale);

        RectTransform track = waveChipBarFill.parent as RectTransform;
        track.sizeDelta = new Vector2(-2f * side, 10f * scale);
        track.anchoredPosition = new Vector2(0f, 12f * scale);
    }

    // wave status that stays up: time left while a timed wave is still spawning,
    // then the number of enemies left to clear
    private void UpdateWaveChip()
    {
        if (waveChipGroup == null)
            return;

        bool show = waveInProgress && !waveHudVisible && !allWavesComplete && !waveSpawnFailed;
        float targetAlpha = show ? 1f : 0f;
        waveChipAlpha = Mathf.MoveTowards(waveChipAlpha, targetAlpha, Time.unscaledDeltaTime * 5f);
        waveChipGroup.alpha = waveChipAlpha;
        if (waveChipAlpha <= 0f)
            return;

        int second = timedWaveActive ? Mathf.CeilToInt(timedWaveRemaining) : -1;
        int enemies = CountActiveWaveEnemies();
        if (second != chipShownSecond || enemies != chipShownEnemies ||
            currentWaveIndex != chipShownWave || timedWaveActive != chipShownTimed)
        {
            bool boss = currentWaveIndex >= 0 && waves != null && currentWaveIndex < waves.Length &&
                IsBossWave(waves[currentWaveIndex]);
            waveChipLeftText.text = boss ? "BOSS WAVE" : "WAVE " + CurrentWaveNumber + "/" + waves.Length;
            if (timedWaveActive)
            {
                string time = (second / 60) + ":" + (second % 60).ToString("00");
                waveChipRightText.text = second <= 5 ? "<color=#FFD54D>" + time + "</color>" : time;
            }
            else
            {
                waveChipRightText.text = enemies + " LEFT";
            }
            chipShownSecond = second;
            chipShownEnemies = enemies;
            chipShownWave = currentWaveIndex;
            chipShownTimed = timedWaveActive;
        }

        float fill = timedWaveActive && timedWaveDuration > 0f ? Mathf.Clamp01(timedWaveRemaining / timedWaveDuration) : 0f;
        waveChipBarFill.anchorMax = new Vector2(fill, 1f);
        waveChipBarImage.enabled = fill > 0f;
        waveChipBarImage.color = fill > 0.2f ? new Color32(120, 200, 90, 255) : new Color32(255, 204, 66, 255);
    }

    private void UpdateWaveText(bool force = false)
    {
        if (waveTitleText == null || waveEnemiesText == null)
            return;

        int enemyCount = CountActiveWaveEnemies();
        int displayedSecond = nextWaveTimer > 0f ? Mathf.CeilToInt(nextWaveTimer) : -1;
        bool textChanged = force ||
            currentWaveIndex != lastDisplayedWaveIndex ||
            enemyCount != lastDisplayedEnemyCount ||
            displayedSecond != lastDisplayedSecond ||
            waveInProgress != lastDisplayedWaveInProgress ||
            allWavesComplete != lastDisplayedAllWavesComplete;
        if (!textChanged)
            return;

        bool bossWave = waveInProgress && currentWaveIndex >= 0 &&
            waves != null && currentWaveIndex < waves.Length && IsBossWave(waves[currentWaveIndex]);
        string title;
        string detail;

        if (allWavesComplete)
        {
            title = "ALL WAVES COMPLETE";
            detail = string.Empty;
        }
        else if (waveSpawnFailed)
        {
            title = "WAVE BLOCKED";
            detail = "Check wave configuration";
        }
        else if (nextWaveTimer > 0f)
        {
            title = "NEXT WAVE";
            detail = "Starting in " + displayedSecond + "s";
        }
        else if (waveInProgress && waves != null &&
                 currentWaveIndex >= 0 && currentWaveIndex < waves.Length)
        {
            if (bossWave)
            {
                title = "BOSS WAVE";
                detail = GetBossDisplayName(waves[currentWaveIndex]);
            }
            else
            {
                title = "WAVE " + CurrentWaveNumber + " / " + waves.Length;
                string waveName = waves[currentWaveIndex] != null ? waves[currentWaveIndex].waveName : null;
                // show wave names we wrote ("Rollstones") but not the default ones
                bool namedWave = !string.IsNullOrWhiteSpace(waveName) &&
                    waveName != "Wave" && !waveName.EndsWith(" Wave");
                detail = (namedWave ? waveName + "  ·  " : string.Empty) +
                    (timedWaveActive ? "Survive " + Mathf.CeilToInt(timedWaveDuration) + "s" : "Enemies left: " + enemyCount);
            }
        }
        else
        {
            title = "PREPARING WAVES";
            detail = string.Empty;
        }

        if (force || title != lastDisplayedTitle || currentWaveIndex != lastDisplayedWaveIndex)
        {
            waveTitleText.text = title;
            lastDisplayedTitle = title;
        }
        if (force || detail != lastDisplayedDetail || enemyCount != lastDisplayedEnemyCount ||
            displayedSecond != lastDisplayedSecond)
        {
            waveEnemiesText.text = detail;
            lastDisplayedDetail = detail;
        }

        if (force || bossWave != lastAppliedBossStyle)
        {
            if (wavePanelImage != null)
                wavePanelImage.color = bossWave
                    ? new Color32(58, 24, 28, 235)
                    : new Color32(42, 51, 38, 235);
            if (wavePanelOutline != null)
                wavePanelOutline.effectColor = bossWave
                    ? new Color32(196, 90, 70, 255)
                    : new Color32(138, 143, 115, 255);
            lastAppliedBossStyle = bossWave;
        }

        lastDisplayedWaveIndex = currentWaveIndex;
        lastDisplayedEnemyCount = enemyCount;
        lastDisplayedSecond = displayedSecond;
        lastDisplayedWaveInProgress = waveInProgress;
        lastDisplayedAllWavesComplete = allWavesComplete;

        if (waveText != null)
            waveText.text = title + (string.IsNullOrEmpty(detail) ? string.Empty : "\n" + detail);
        if (legacyWaveText != null)
            legacyWaveText.text = title + (string.IsNullOrEmpty(detail) ? string.Empty : "\n" + detail);
    }

    private static bool IsBossWave(EnemyWave wave)
    {
        return wave != null && (wave.isBoss || wave.bossSpawnsLast || wave.bossSpawnIndex >= 0);
    }

    private static string GetBossDisplayName(EnemyWave wave)
    {
        if (!string.IsNullOrWhiteSpace(wave.bossName))
            return wave.bossName.ToUpperInvariant();

        if (wave.enemyPrefabs != null && wave.enemyPrefabs.Length > 0)
        {
            int spawnIndex = wave.bossSpawnsLast
                ? Mathf.Max(0, wave.enemyCount - 1)
                : Mathf.Max(0, wave.bossSpawnIndex);
            int prefabIndex = spawnIndex % wave.enemyPrefabs.Length;
            Enemy prefab = wave.enemyPrefabs[prefabIndex];
            if (prefab != null && !string.IsNullOrWhiteSpace(prefab.bossName))
                return prefab.bossName.ToUpperInvariant();
        }

        return "THORNMAW";
    }

    private void ShowWaveBanner(bool bossWave)
    {
        GameAudio.Play(bossWave ? Sfx.BossWave : Sfx.WaveStart);
        if (wavePanelGroup == null || wavePanelRect == null)
            return;

        waveHudVisible = true;
        wavePanelGroup.alpha = 1f;
        waveAnimationElapsed = 0f;
        waveHideTime = Time.unscaledTime + 2.3f;
        waveAnimationPlaying = true;
        waveAnimationStartPosition = waveHiddenPosition;
        wavePanelRect.anchoredPosition = waveAnimationStartPosition;
        wavePanelRect.localScale = Vector3.one * 1.15f;
        lastAppliedBossStyle = !bossWave;
    }

    private void UpdateWaveBannerAnimation()
    {
        if (waveHudVisible && !waveAnimationPlaying && Time.unscaledTime >= waveHideTime)
        {
            waveHudVisible = false;
            if (wavePanelGroup != null)
                wavePanelGroup.alpha = 0f;
        }

        if (!waveAnimationPlaying || wavePanelRect == null)
            return;

        waveAnimationElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(waveAnimationElapsed / 0.3f);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        wavePanelRect.anchoredPosition = Vector2.LerpUnclamped(
            waveAnimationStartPosition, waveTargetPosition, eased);
        wavePanelRect.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, eased);
        if (progress >= 1f)
        {
            wavePanelRect.anchoredPosition = waveTargetPosition;
            wavePanelRect.localScale = Vector3.one;
            waveAnimationPlaying = false;
        }
    }

    private void RefreshWaveHudLayout(bool force)
    {
        if (waveCanvasRect == null || wavePanelRect == null)
            return;

        Rect safe = Screen.safeArea;
        float canvasWidth = waveCanvasRect.rect.width;
        float canvasHeight = waveCanvasRect.rect.height;
        bool changed = Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            canvasWidth != lastCanvasWidth ||
            canvasHeight != lastCanvasHeight ||
            !safe.Equals(lastSafeArea);
        if (!force && waveHudLayoutApplied && !changed)
            return;

        float scale = Mathf.Min(canvasWidth / 1080f, canvasHeight / 1920f);
        float widthScale = canvasWidth / Mathf.Max(1f, Screen.width);
        float heightScale = canvasHeight / Mathf.Max(1f, Screen.height);
        float safeLeft = safe.x * widthScale;
        float safeRight = (Screen.width - safe.xMax) * widthScale;
        float safeTop = (Screen.height - safe.yMax) * heightScale;
        float width = Mathf.Min(640f * scale, Mathf.Max(0f, canvasWidth - safeLeft - safeRight));
        float height = 140f * scale;

        wavePanelRect.anchorMin = new Vector2(0.5f, 1f);
        wavePanelRect.anchorMax = new Vector2(0.5f, 1f);
        wavePanelRect.pivot = new Vector2(0.5f, 1f);
        wavePanelRect.sizeDelta = new Vector2(width, height);
        waveTargetPosition = new Vector2((safeLeft - safeRight) * 0.5f, -36f * scale - safeTop);
        waveHiddenPosition = waveTargetPosition + Vector2.up * (height + safeTop + 8f * scale);
        if (wavePanelOutline != null)
            wavePanelOutline.effectDistance = new Vector2(6f * scale, -6f * scale);
        LayoutWaveChip(scale);

        if (waveAnimationPlaying)
        {
            waveAnimationStartPosition = waveHiddenPosition;
            wavePanelRect.anchoredPosition = waveAnimationStartPosition;
        }
        else
        {
            wavePanelRect.anchoredPosition = waveHudVisible ? waveTargetPosition : waveHiddenPosition;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastSafeArea = safe;
        waveHudLayoutApplied = true;
    }

    bool HasActiveWaveEnemies()
    {
        return CountActiveWaveEnemies() > 0;
    }

    int CountActiveWaveEnemies()
    {
        int count = 0;

        for (int index = activeWaveEnemies.Count - 1; index >= 0; index--)
        {
            if (activeWaveEnemies[index] == null)
                activeWaveEnemies.RemoveAt(index);
            else
                count++;
        }

        return count;
    }

    void OnDrawGizmos()
    {
        if (routeWaypoints == null)
            return;

        int laneCount = useCenterLanes ? 2 : 1;
        for (int lane = 0; lane < laneCount; lane++)
        {
            float laneOffset = !useCenterLanes ? 0f : lane == 0 ? -centerLaneOffset : centerLaneOffset;
            Transform previous = null;
            Gizmos.color = lane == 0 ? new Color(0.4f, 0.9f, 1f, 1f) : new Color(1f, 0.75f, 0.2f, 1f);

            foreach (Transform waypoint in routeWaypoints)
            {
                if (waypoint == null)
                    continue;

                Vector3 lanePosition = waypoint.position + Vector3.right * laneOffset;
                Gizmos.DrawSphere(lanePosition, 0.12f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(lanePosition + Vector3.up * 0.25f, waypoint.name);
#endif
                if (previous != null)
                    Gizmos.DrawLine(previous.position + Vector3.right * laneOffset, lanePosition);

                previous = waypoint;
            }
        }

#if UNITY_EDITOR
        if (spawnPoint != null)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 1f);
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
            UnityEditor.Handles.Label(
                spawnPoint.position + Vector3.up * 0.55f,
                "Enemy Spawn Point");
        }
#endif
    }
}
