using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class StageManager : MonoBehaviour
{
    public GameObject winPanel;
    public GameObject losePanel;
    public LaunchController launchController;
    public WaveManager waveManager;
    public float loseY = -5.5f;
    public int stageReward = 25;
    public int stageNumber = 1;
    public int barrierMaxHealth = 5;
    public float barrierY = -3.5f;
    public float barrierContactOffset = 0.35f;
    public TMPro.TMP_Text barrierHealthText;
    public TMPro.TMP_Text winTitleText;
    public TMPro.TMP_Text loseTitleText;

    private bool stageFinished = false;
    private int barrierHealth;

    void Awake()
    {
        if (GameProgress.Instance == null)
            new GameObject("GameProgress").AddComponent<GameProgress>();

        GameObject barrierObject = GameObject.Find("Square");
        if (barrierObject != null)
        {
            Collider2D barrierCollider = barrierObject.GetComponent<Collider2D>();
            if (barrierCollider != null)
                barrierCollider.isTrigger = true;

            barrierY = barrierObject.transform.position.y;
        }

        barrierHealth = barrierMaxHealth;

        if (barrierHealthText == null)
        {
            GameObject barrierTextObject = GameObject.Find("BarrierHealthText");

            if (barrierTextObject != null)
                barrierHealthText = barrierTextObject.GetComponent<TMP_Text>();
        }

        if (winTitleText == null && winPanel != null)
            winTitleText = winPanel.GetComponentInChildren<TMP_Text>();

        if (loseTitleText == null && losePanel != null)
            loseTitleText = losePanel.GetComponentInChildren<TMP_Text>();

        UpdateBarrierText();
    }

    void Update()
    {
        if (stageFinished)
            return;

        if (waveManager != null)
        {
            if (EnemyReachedBarrier())
            {
                StageFailed();
                return;
            }

            if (waveManager.IsComplete && !HasActiveBoss())
            {
                StageComplete();
                return;
            }

            return;
        }

        Enemy[] enemies = FindObjectsByType<Enemy>();

        // WIN
        if (enemies.Length == 0)
        {
            StageComplete();
            return;
        }

        // LOSE
        if (launchController != null && !launchController.unlimitedLaunches && launchController.launchesLeft <= 0)
        {
            StageFailed();
        }
    }

    bool HasActiveBoss()
    {
        foreach (Enemy enemy in FindObjectsByType<Enemy>())
        {
            if (enemy != null && enemy.isBoss)
                return true;
        }

        return false;
    }

    bool EnemyReachedBarrier()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>();
        bool barrierDestroyed = false;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.HasReachedBarrier)
                continue;

            Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
            float enemyBottom = enemyCollider != null
                ? enemyCollider.bounds.min.y
                : enemy.transform.position.y;

            if (enemyBottom <= barrierY + barrierContactOffset)
            {
                enemy.MarkBarrierReached();
                barrierHealth--;
                UpdateBarrierText();

                if (enemy != null)
                    Destroy(enemy.gameObject);

                if (barrierHealth <= 0)
                {
                    barrierDestroyed = true;
                    break;
                }
            }
        }

        return barrierDestroyed;
    }

    void UpdateBarrierText()
    {
        if (barrierHealthText != null)
            barrierHealthText.text = "BARRIER: " + barrierHealth + " / " + barrierMaxHealth;
    }

    void StageComplete()
    {
        stageFinished = true;

        if (GameProgress.Instance != null)
            GameProgress.Instance.CompleteStage(stageNumber, stageReward);

        if (winPanel != null)
        {
            if (winTitleText == null)
                winTitleText = winPanel.GetComponentInChildren<TMP_Text>();

            if (winTitleText != null)
            {
                if (stageNumber == 4)
                    winTitleText.text = "WILDROOT THICKET COMPLETE";
                else if (stageNumber % 4 == 0)
                    winTitleText.text = stageNumber >= 20 ? "CAMPAIGN COMPLETE" : "CHAPTER COMPLETE";
                else
                    winTitleText.text = "STAGE COMPLETE";
            }

            winPanel.SetActive(true);
        }
    }

    void StageFailed()
    {
        stageFinished = true;

        if (losePanel != null)
        {
            if (loseTitleText == null)
                loseTitleText = losePanel.GetComponentInChildren<TMP_Text>();

            if (loseTitleText != null)
                loseTitleText.text = "BARRIER DESTROYED";

            losePanel.SetActive(true);
        }
    }

    public void RetryStage()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void NextStage()
    {
        if (stageNumber % 4 == 0 && Application.CanStreamedLevelBeLoaded("GameHub"))
        {
            SceneManager.LoadScene("GameHub");
            return;
        }

        int nextStageNumber = stageNumber + 1;
        string nextSceneName = GameHubController.GetStageSceneName(nextStageNumber);

        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        if (Application.CanStreamedLevelBeLoaded("GameHub"))
        {
            SceneManager.LoadScene("GameHub");
            return;
        }

        if (stageNumber >= 4)
        {
            Debug.Log("Wildroot Thicket complete. Returning to the chapter hub.");
            if (Application.CanStreamedLevelBeLoaded("GameHub"))
                SceneManager.LoadScene("GameHub");
            return;
        }

        Debug.LogError("No valid next stage or hub scene is available in Build Settings.");
    }
}