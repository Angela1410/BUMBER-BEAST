using UnityEngine;
using UnityEngine.SceneManagement;

public class GameProgress : MonoBehaviour
{
    public static GameProgress Instance { get; private set; }

    public int Coins { get; private set; }
    public int HighestUnlockedStage { get; private set; }
    public int HighestClearedStage { get; private set; }
    public int LaunchPowerLevel { get; private set; }
    public int SkillCooldownLevel { get; private set; }
    public BeastId SelectedBeast { get; private set; }
    public int LaunchPowerPrice => 50 + LaunchPowerLevel * 25;
    public int SkillCooldownPrice => 75 + SkillCooldownLevel * 35;

    const string CoinsKey = "BumperBeast.Coins";
    const string StageKey = "BumperBeast.HighestUnlockedStage";
    const string ClearedStageKey = "BumperBeast.HighestClearedStage";
    const string RewardedStagesKey = "BumperBeast.RewardedStages";
    const string LaunchKey = "BumperBeast.LaunchPowerLevel";
    const string SkillKey = "BumperBeast.SkillCooldownLevel";
    const string BeastKey = "BumperBeast.SelectedBeast";
    const string UnlockedBeastsKey = "BumperBeast.UnlockedBeasts";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        HideSeparateBeastDisplays();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
    }

    public void CompleteStage(int stageNumber, int reward)
    {
        HighestClearedStage = Mathf.Max(HighestClearedStage, stageNumber);
        HighestUnlockedStage = Mathf.Max(HighestUnlockedStage, stageNumber + 1);
        int rewardedStages = PlayerPrefs.GetInt(RewardedStagesKey, 0);
        int stageBit = 1 << (stageNumber - 1);

        if ((rewardedStages & stageBit) == 0)
        {
            Coins += Mathf.Max(0, reward);
            PlayerPrefs.SetInt(RewardedStagesKey, rewardedStages | stageBit);
        }

        Save();
    }

    public bool SpendCoins(int amount)
    {
        if (Coins < amount)
            return false;

        Coins -= amount;
        Save();
        return true;
    }

    public bool BuyLaunchPower()
    {
        int price = LaunchPowerPrice;

        if (!SpendCoins(price))
            return false;

        LaunchPowerLevel++;
        Save();
        return true;
    }

    public bool BuySkillCooldown()
    {
        int price = SkillCooldownPrice;

        if (!SpendCoins(price))
            return false;

        SkillCooldownLevel++;
        Save();
        return true;
    }

    public bool IsBeastUnlocked(BeastId beast)
    {
        if (beast == BeastId.Choma)
            return true;

        bool purchased = (PlayerPrefs.GetInt(UnlockedBeastsKey, 1) & (1 << (int)beast)) != 0;
        return purchased && HighestClearedStage >= BeastRoster.GetUnlockStage(beast);
    }

    public bool UnlockBeast(BeastId beast)
    {
        if (IsBeastUnlocked(beast))
            return true;

        if (HighestClearedStage < BeastRoster.GetUnlockStage(beast) ||
            !SpendCoins(BeastRoster.GetPrice(beast)))
            return false;

        int unlocked = PlayerPrefs.GetInt(UnlockedBeastsKey, 1) | (1 << (int)beast);
        PlayerPrefs.SetInt(UnlockedBeastsKey, unlocked);
        Save();
        return true;
    }

    public void SelectBeast(BeastId beast)
    {
        if (!IsBeastUnlocked(beast))
            return;

        SelectedBeast = beast;
        PlayerPrefs.SetInt(BeastKey, (int)beast);
        Save();
    }

    public void ApplySelectedBeast(GameObject beastObject)
    {
        BeastElement element = beastObject.GetComponent<BeastElement>();

        if (element != null)
            element.element = BeastRoster.GetElement(GetSelectedBeastOrChoma());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideSeparateBeastDisplays();
    }

    void HideSeparateBeastDisplays()
    {
        foreach (BeastId beast in BeastRoster.AllBeasts)
        {
            if (beast == BeastId.Choma)
                continue;

            GameObject display = GameObject.Find(BeastRoster.GetName(beast));
            if (display != null && display.GetComponent<LaunchController>() == null)
                display.SetActive(false);
        }
    }

    BeastId GetSelectedBeastOrChoma()
    {
        return IsBeastUnlocked(SelectedBeast) ? SelectedBeast : BeastId.Choma;
    }

    public void ApplySelectedBeastVisual(GameObject beastObject)
    {
        SpriteRenderer sprite = beastObject.GetComponent<SpriteRenderer>();
        BeastId activeBeast = GetSelectedBeastOrChoma();

        if (sprite != null)
            sprite.color = BeastRoster.GetColor(activeBeast);

        beastObject.name = BeastRoster.GetName(activeBeast);
    }

    public void ResetProgress()
    {
        Coins = 0;
        HighestUnlockedStage = 1;
        HighestClearedStage = 0;
        LaunchPowerLevel = 0;
        SkillCooldownLevel = 0;
        PlayerPrefs.SetInt(UnlockedBeastsKey, 1);
        PlayerPrefs.SetInt(RewardedStagesKey, 0);
        PlayerPrefs.SetInt(BeastKey, (int)BeastId.Choma);
        Save();
    }

    void Load()
    {
        Coins = PlayerPrefs.GetInt(CoinsKey, 0);
        HighestUnlockedStage = PlayerPrefs.GetInt(StageKey, 1);
        HighestClearedStage = PlayerPrefs.GetInt(ClearedStageKey, 0);
        LaunchPowerLevel = PlayerPrefs.GetInt(LaunchKey, 0);
        SkillCooldownLevel = PlayerPrefs.GetInt(SkillKey, 0);
        SelectedBeast = (BeastId)PlayerPrefs.GetInt(BeastKey, 0);

        if (!IsBeastUnlocked(SelectedBeast))
        {
            SelectedBeast = BeastId.Choma;
            PlayerPrefs.SetInt(BeastKey, (int)BeastId.Choma);
            PlayerPrefs.Save();
        }
    }

    void Save()
    {
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.SetInt(StageKey, HighestUnlockedStage);
        PlayerPrefs.SetInt(ClearedStageKey, HighestClearedStage);
        PlayerPrefs.SetInt(LaunchKey, LaunchPowerLevel);
        PlayerPrefs.SetInt(SkillKey, SkillCooldownLevel);
        PlayerPrefs.SetInt(BeastKey, (int)SelectedBeast);
        PlayerPrefs.Save();
    }
}