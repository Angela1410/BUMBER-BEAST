using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameHubController : MonoBehaviour
{
    public Text coinsText;
    public Text unlockedText;
    public Text shopText;
    public int stageCount = 20;

    void Start()
    {
        EnsureProgress();

        for (int index = 1; index <= stageCount; index++)
        {
            Button button = GameObject.Find("StageButton" + index)?.GetComponent<Button>();

            if (button == null)
                continue;

            int stageNumber = index;
            button.interactable = stageNumber <= GameProgress.Instance.HighestUnlockedStage;
            button.onClick.AddListener(() => LoadStage(stageNumber));
        }

        Button launchButton = GameObject.Find("BuyLaunchPower")?.GetComponent<Button>();
        Button skillButton = GameObject.Find("BuySkillCooldown")?.GetComponent<Button>();
        Button continueButton = GameObject.Find("ContinueButton")?.GetComponent<Button>();

        if (launchButton != null)
            launchButton.onClick.AddListener(BuyLaunchPower);

        if (skillButton != null)
            skillButton.onClick.AddListener(BuySkillCooldown);

        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueToNextStage);

        for (int index = 0; index < BeastRoster.AllBeasts.Length; index++)
        {
            Button beastButton = GameObject.Find("BeastButton" + index)?.GetComponent<Button>();

            if (beastButton == null)
                continue;

            BeastId beast = BeastRoster.AllBeasts[index];
            beastButton.onClick.AddListener(() => BuyOrSelectBeast(beast));
        }

        RefreshText();
    }

    public void LoadStage(int stageNumber)
    {
        if (GameProgress.Instance == null ||
            stageNumber > GameProgress.Instance.HighestUnlockedStage)
            return;

        string sceneName = GetStageSceneName(stageNumber);

        if (Application.CanStreamedLevelBeLoaded(sceneName))
            SceneManager.LoadScene(sceneName);
    }

    public void ContinueToNextStage()
    {
        if (GameProgress.Instance == null)
        {
            EnsureProgress();
        }

        if (GameProgress.Instance == null)
            return;

        if (GameProgress.Instance.HighestUnlockedStage > stageCount)
            return;

        int nextStage = Mathf.Clamp(GameProgress.Instance.HighestUnlockedStage, 1, stageCount);
        LoadStage(nextStage);
    }

    public void BuyLaunchPower()
    {
        GameProgress.Instance.BuyLaunchPower();
        RefreshText();
    }

    public void BuySkillCooldown()
    {
        GameProgress.Instance.BuySkillCooldown();
        RefreshText();
    }

    public void BuyOrSelectBeast(BeastId beast)
    {
        if (!GameProgress.Instance.IsBeastUnlocked(beast))
            GameProgress.Instance.UnlockBeast(beast);
        else
            GameProgress.Instance.SelectBeast(beast);

        RefreshText();
    }

    public static string GetStageSceneName(int stageNumber)
    {
        if (stageNumber <= 4)
            return "stage-" + stageNumber;

        if (stageNumber <= 8)
            return "stage-" + stageNumber + "_Earth";

        if (stageNumber <= 12)
            return "stage-" + stageNumber + "_Electric";

        if (stageNumber <= 16)
            return "stage-" + stageNumber + "_Water";

        return "stage-" + stageNumber + "_Fire";
    }

    void RefreshText()
    {
        if (GameProgress.Instance == null)
            return;

        if (coinsText != null)
            coinsText.text = "COINS: " + GameProgress.Instance.Coins;

        if (unlockedText != null)
            unlockedText.text = "UNLOCKED: " + GameProgress.Instance.HighestUnlockedStage + " / " + stageCount;

        Button continueButton = GameObject.Find("ContinueButton")?.GetComponent<Button>();
        if (continueButton != null)
        {
            bool campaignComplete = GameProgress.Instance.HighestUnlockedStage > stageCount;
            continueButton.interactable = !campaignComplete;

            Text continueLabel = continueButton.GetComponentInChildren<Text>();
            if (continueLabel != null)
                continueLabel.text = campaignComplete ? "ALL CHAPTERS COMPLETE" : "CONTINUE";
        }

        if (shopText != null)
        {
            if (GameProgress.Instance.HighestClearedStage >= 4 && !GameProgress.Instance.IsBeastUnlocked(BeastId.Thistle))
            {
                shopText.text = "WILDROOT THICKET COMPLETE" +
                    "\nTHISTLE RECRUITMENT OPEN: " + BeastRoster.GetPrice(BeastId.Thistle) + " COINS" +
                    "\nLAUNCH POWER LV " + GameProgress.Instance.LaunchPowerLevel +
                    "\nSKILL COOLDOWN LV " + GameProgress.Instance.SkillCooldownLevel +
                    "\nSELECTED: " + BeastRoster.GetName(GameProgress.Instance.SelectedBeast);
            }
            else if (GameProgress.Instance.HighestClearedStage >= 4 && GameProgress.Instance.IsBeastUnlocked(BeastId.Thistle))
            {
                shopText.text = "THISTLE UNLOCKED" +
                    "\nLAUNCH POWER LV " + GameProgress.Instance.LaunchPowerLevel +
                    "\nSKILL COOLDOWN LV " + GameProgress.Instance.SkillCooldownLevel +
                    "\nSELECTED: " + BeastRoster.GetName(GameProgress.Instance.SelectedBeast);
            }
            else
            {
                shopText.text = "LAUNCH POWER LV " + GameProgress.Instance.LaunchPowerLevel +
                    "\nSKILL COOLDOWN LV " + GameProgress.Instance.SkillCooldownLevel +
                    "\nSELECTED: " + BeastRoster.GetName(GameProgress.Instance.SelectedBeast);
            }
        }

        Text launchLabel = GameObject.Find("BuyLaunchPower")?.GetComponentInChildren<Text>();
        Text skillLabel = GameObject.Find("BuySkillCooldown")?.GetComponentInChildren<Text>();

        if (launchLabel != null)
            launchLabel.text = "UPGRADE LAUNCH\n" + GameProgress.Instance.LaunchPowerPrice + " COINS";

        if (skillLabel != null)
            skillLabel.text = "UPGRADE SKILL\n" + GameProgress.Instance.SkillCooldownPrice + " COINS";

        for (int index = 0; index < BeastRoster.AllBeasts.Length; index++)
        {
            Button button = GameObject.Find("BeastButton" + index)?.GetComponent<Button>();

            if (button == null)
                continue;

            BeastId beast = BeastRoster.AllBeasts[index];
            bool unlocked = GameProgress.Instance.IsBeastUnlocked(beast);
            bool stageReached = GameProgress.Instance.HighestClearedStage >= BeastRoster.GetUnlockStage(beast);
            button.interactable = unlocked || stageReached;

            Text label = button.GetComponentInChildren<Text>();

            if (label != null)
            {
                string status = unlocked
                    ? beast == GameProgress.Instance.SelectedBeast ? "SELECTED" : "SELECT"
                    : stageReached ? "BUY " + BeastRoster.GetPrice(beast) : "CLEAR STAGE " + BeastRoster.GetUnlockStage(beast);
                label.text = BeastRoster.GetName(beast) + "\n" + status;
            }
        }

    }

    void EnsureProgress()
    {
        if (GameProgress.Instance != null)
            return;

        new GameObject("GameProgress").AddComponent<GameProgress>();
    }
}