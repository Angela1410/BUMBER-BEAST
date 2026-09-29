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
    public bool useExistingEnemiesAsFirstWave = true;
    public bool randomizeElements = true;
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

        int firstWaveIndex = 0;
        List<Enemy> existingEnemies = FindExistingEnemies();

        if (useExistingEnemiesAsFirstWave && existingEnemies.Count > 0)
        {
            currentWaveIndex = 0;
            waveInProgress = true;
            activeWaveEnemies.Clear();
            ConfigureExistingEnemies(existingEnemies, waves[0]);
            UpdateWaveText();

            while (HasActiveWaveEnemies())
                yield return null;

            waveInProgress = false;
            firstWaveIndex = 1;
        }

        for (int index = firstWaveIndex; index < waves.Length; index++)
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
        float interval = Mathf.Max(0f, wave.spawnInterval);
        activeWaveEnemies.Clear();

        for (int index = 0; index < amount; index++)
        {
            Enemy prefab = wave.enemyPrefabs[index % wave.enemyPrefabs.Length];

            if (prefab != null)
            {
                float horizontalPosition = randomizeFormation
                    ? GetSpawnX(index)
                    : amount == 1
                        ? 0f
                        : Mathf.Lerp(-spawnWidth, spawnWidth, (float)index / (amount - 1));
                float verticalOffset = randomizeFormation
                    ? Random.Range(-spawnHeightJitter, spawnHeightJitter)
                    : 0f;
                Vector3 spawnPosition = spawnPoint.position +
                    new Vector3(horizontalPosition, verticalOffset, 0f);
                Enemy enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);
                enemy.ApplyWaveData(PickElement(wave.element), wave.extraHealth, wave.speedMultiplier);
                activeWaveEnemies.Add(enemy);
            }

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
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

    List<Enemy> FindExistingEnemies()
    {
        return new List<Enemy>(FindObjectsByType<Enemy>(FindObjectsSortMode.None));
    }

    void ConfigureExistingEnemies(List<Enemy> enemies, EnemyWave wave)
    {
        foreach (Enemy enemy in enemies)
        {
            enemy.ApplyWaveData(PickElement(wave.element), wave.extraHealth, wave.speedMultiplier);
            activeWaveEnemies.Add(enemy);
        }
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

        return waveInProgress
            ? "Wave " + CurrentWaveNumber + " / " + waves.Length +
              "\nEnemies: " + enemiesRemaining + "  |  Types: " + GetActiveElements()
            : "Preparing Waves";
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
}