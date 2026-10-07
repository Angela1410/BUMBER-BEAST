using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    private int currentWaveIndex = -1;
    private bool waveInProgress;
    private bool allWavesComplete;
    private float nextWaveTimer;
    private readonly List<Enemy> activeWaveEnemies = new List<Enemy>();

    public bool IsComplete => allWavesComplete;
    public int CurrentWaveNumber => currentWaveIndex + 1;

    void Update()
    {
        if (nextWaveTimer > 0f)
            nextWaveTimer = Mathf.Max(0f, nextWaveTimer - Time.deltaTime);

        if (waveInProgress)
            UpdateWaveText();
    }

    void Start()
    {
        EnsureElementPool();
        ClearBossHealthLabel();

        if (useCenterLanes)
            centerLaneOffset = Mathf.Max(centerLaneOffset, 0.95f);

        if (spawnPoint == null)
        {
            Debug.LogError("WaveManager needs a Spawn Point.", this);
            enabled = false;
            return;
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("WaveManager needs at least one wave.", this);
            enabled = false;
            return;
        }
        
        // Ensure route waypoints are assigned in the inspector
        if (routeWaypoints == null || routeWaypoints.Length == 0)
        {
            Debug.LogWarning("WaveManager has no routeWaypoints assigned! Enemies will not move.", this);
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
            waveInProgress = true;
            UpdateWaveText();

            yield return StartCoroutine(SpawnWave(waves[index]));

            while (HasActiveWaveEnemies())
                yield return null;

            waveInProgress = false;

            if (index < waves.Length - 1)
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
        if (wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
        {
            Debug.LogError("A wave has no enemy prefabs assigned.", this);
            yield break;
        }

        int amount = Mathf.Max(0, wave.enemyCount);
        activeWaveEnemies.Clear();
        Enemy previousSpawnedEnemy = null;

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
                    Debug.LogError("The previous enemy did not clear the spawn point; stopping this wave to prevent enemy overlap.", this);
                    yield break;
                }
            }

            Enemy prefab = wave.enemyPrefabs[i % wave.enemyPrefabs.Length];
            if (prefab == null)
            {
                Debug.LogError("Wave contains a null enemy prefab at index " + (i % wave.enemyPrefabs.Length) + ".", this);
            }
            else
            {
                Vector3 spawnPosition = spawnPoint.position;
                Debug.Log($"Spawning enemy {i} at time {Time.time}");
                Enemy enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);

                enemy.ApplyWaveData(PickElement(wave.element), wave.extraHealth, wave.speedMultiplier);
                enemy.SetRoute(routeWaypoints, 0f);
                previousSpawnedEnemy = enemy;

                bool enemyIsBoss = (wave.bossSpawnIndex >= 0 && i == wave.bossSpawnIndex) ||
                    (wave.bossSpawnsLast && i == amount - 1) ||
                    (wave.isBoss && amount == 1);
                if (enemyIsBoss)
                {
                    enemy.SetBoss(wave.bossName, wave.bossHealthBonus, wave.speedMultiplier);
                    enemy.transform.localScale = Vector3.one * 1.5f;
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
                yield return new WaitForSeconds(spawnDelay);
            }
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

    void UpdateWaveText()
    {
        if (waveText == null)
        {
            if (legacyWaveText == null)
                return;

            legacyWaveText.text = GetWaveText();
            return;
        }

        waveText.text = GetWaveText();
    }

    string GetWaveText()
    {
        if (allWavesComplete)
            return "All Waves Complete";

        int enemiesRemaining = CountActiveWaveEnemies();

        if (nextWaveTimer > 0f)
            return "Next wave in " + Mathf.CeilToInt(nextWaveTimer) + "s";

        if (waveInProgress)
        {
            if (waves != null && currentWaveIndex >= 0 && currentWaveIndex < waves.Length && waves[currentWaveIndex].isBoss)
                return "BOSS: " + waves[currentWaveIndex].bossName + "\nWave " + CurrentWaveNumber + " / " + waves.Length + "\nEnemies: " + enemiesRemaining;

            if (waves != null && currentWaveIndex >= 0 && currentWaveIndex < waves.Length && waves[currentWaveIndex].bossSpawnsLast)
                return "Wave " + CurrentWaveNumber + " / " + waves.Length + "\nThornmaw arrives last\nEnemies: " + enemiesRemaining;

            return "Wave " + CurrentWaveNumber + " / " + waves.Length +
                "\nEnemies: " + enemiesRemaining + "  |  Types: " + GetActiveElements();
        }

        return "Preparing Waves";
    }

    string GetActiveElements()
    {
        var elements = new HashSet<ElementType>();

        foreach (Enemy enemy in activeWaveEnemies)
        {
            if (enemy != null)
                elements.Add(enemy.element);
        }

        return elements.Count == 0 ? "None" : string.Join(", ", elements);
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

    void OnDrawGizmosSelected()
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
                if (previous != null)
                    Gizmos.DrawLine(previous.position + Vector3.right * laneOffset, lanePosition);

                previous = waypoint;
            }
        }
    }
}