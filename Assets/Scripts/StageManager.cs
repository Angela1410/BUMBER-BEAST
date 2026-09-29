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

    private bool stageFinished = false;
    private int barrierHealth;

    void Awake()
    {
        if (GameProgress.Instance == null)
            new GameObject("GameProgress").AddComponent<GameProgress>();

        GameObject barrierObject = GameObject.Find("Square");
        if (barrierObject != null)
        {
            barrierObject.transform.localScale = new Vector3(5.6f, 0.3f, 1f);

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

        UpdateBarrierText();
    }

    void Update()
    {
        if (stageFinished)
            return;

        if (waveManager != null)
        {
            if (EnemyReachedBarrier())
                StageFailed();
            else if (waveManager.IsComplete)
                StageComplete();

            return;
        }

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        // WIN
        if (enemies.Length == 0)
        {
            StageComplete();
            return;
        }

        // LOSE
        if (launchController != null && launchController.launchesLeft <= 0)
        {
            StageFailed();
        }
    }

    bool EnemyReachedBarrier()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        foreach (Enemy enemy in enemies)
        {
            Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
            float enemyBottom = enemyCollider != null
                ? enemyCollider.bounds.min.y
                : enemy.transform.position.y;

            if (enemyBottom <= barrierY + barrierContactOffset)
            {
                barrierHealth--;
                Destroy(enemy);
                UpdateBarrierText();

                if (barrierHealth <= 0)
                    return true;
            }
        }

        return false;
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
            winPanel.SetActive(true);
    }

    void StageFailed()
    {
        stageFinished = true;

        if (losePanel != null)
            losePanel.SetActive(true);
    }

    public void RetryStage()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void NextStage()
    {
        if (Application.CanStreamedLevelBeLoaded("GameHub"))
            SceneManager.LoadScene("GameHub");
        else
            Debug.LogError("GameHub is missing from Build Settings. Run Bumper Beast > Build Full Game.");
    }
}