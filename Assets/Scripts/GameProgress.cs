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
    public const int MaxLaunchPowerLevel = 10;
    public const int MaxSkillCooldownLevel = 10;
    public bool CanUpgradeLaunchPower => LaunchPowerLevel < MaxLaunchPowerLevel;
    public bool CanUpgradeSkillCooldown => SkillCooldownLevel < MaxSkillCooldownLevel;
    public int LaunchPowerPrice => 50 + LaunchPowerLevel * 25;
    public int SkillCooldownPrice => 75 + SkillCooldownLevel * 35;

    // the names (keys) we save each value under in PlayerPrefs
    const string CoinsKey = "BumperBeast.Coins";
    const string StageKey = "BumperBeast.HighestUnlockedStage";
    const string ClearedStageKey = "BumperBeast.HighestClearedStage";
    const string RewardedStagesKey = "BumperBeast.RewardedStages";
    const string LaunchKey = "BumperBeast.LaunchPowerLevel";
    const string SkillKey = "BumperBeast.SkillCooldownLevel";
    const string BeastKey = "BumperBeast.SelectedBeast";
    const string UnlockedBeastsKey = "BumperBeast.UnlockedBeasts";
    const string SquadKey = "BumperBeast.Squad";

    public const int MaxSquadSize = 3;

    public enum SquadChange { Added, Removed, Full, NotOwned, LastMember }

    // up to three Beasts you own go into every stage, the first one starts on the pad
    private readonly System.Collections.Generic.List<BeastId> squad = new System.Collections.Generic.List<BeastId>(MaxSquadSize);
    public System.Collections.Generic.IReadOnlyList<BeastId> Squad => squad;

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

    // called when you win a stage: it unlocks the next stage and pays the coins, but only the first time
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

    // true once a stage's first clear coins have been paid
    public bool IsStageRewarded(int stageNumber)
    {
        if (stageNumber < 1 || stageNumber > 31)
            return false;

        return (PlayerPrefs.GetInt(RewardedStagesKey, 0) & (1 << (stageNumber - 1))) != 0;
    }

    // bonus coins (new stars, combos, the daily gift)
    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;
        Coins += amount;
        Save();
    }

    // --- stars ---
    // best stars for each stage (0-3), saved as one digit per stage like "3210..."
    const string StarsKey = "BumperBeast.Stars";
    public const int MaxStarsPerStage = 3;

    public int GetStars(int stageNumber)
    {
        string saved = PlayerPrefs.GetString(StarsKey, string.Empty);
        int index = stageNumber - 1;
        if (index < 0 || index >= saved.Length)
            return 0;
        int value = saved[index] - '0';
        return value >= 0 && value <= MaxStarsPerStage ? value : 0;
    }

    // keeps the best result and returns how many stars are NEW (0 if you didn't beat your record)
    public int RecordStars(int stageNumber, int stars)
    {
        stars = Mathf.Clamp(stars, 0, MaxStarsPerStage);
        int previous = GetStars(stageNumber);
        if (stageNumber < 1 || stars <= previous)
            return 0;

        char[] digits = PlayerPrefs.GetString(StarsKey, string.Empty).ToCharArray();
        if (digits.Length < stageNumber)
        {
            char[] grown = new char[stageNumber];
            for (int i = 0; i < grown.Length; i++)
                grown[i] = i < digits.Length ? digits[i] : '0';
            digits = grown;
        }
        digits[stageNumber - 1] = (char)('0' + stars);
        PlayerPrefs.SetString(StarsKey, new string(digits));
        PlayerPrefs.Save();
        return stars - previous;
    }

    public int TotalStars
    {
        get
        {
            string saved = PlayerPrefs.GetString(StarsKey, string.Empty);
            int total = 0;
            foreach (char digit in saved)
            {
                int value = digit - '0';
                if (value > 0 && value <= MaxStarsPerStage)
                    total += value;
            }
            return total;
        }
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
        if (!CanUpgradeLaunchPower)
            return false;

        int price = LaunchPowerPrice;

        if (!SpendCoins(price))
            return false;

        LaunchPowerLevel++;
        Save();
        return true;
    }

    public bool BuySkillCooldown()
    {
        if (!CanUpgradeSkillCooldown)
            return false;

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
        // a newly recruited Beast joins the squad if there's room
        if (!squad.Contains(beast) && squad.Count < MaxSquadSize)
            squad.Add(beast);
        Save();
        return true;
    }

    // makes the Beast the squad's leader (it starts on the pad). it joins the squad if needed,
    // taking the last slot if the squad is full
    public void SelectBeast(BeastId beast)
    {
        if (!IsBeastUnlocked(beast))
            return;

        squad.Remove(beast);
        if (squad.Count >= MaxSquadSize)
            squad.RemoveAt(squad.Count - 1);
        squad.Insert(0, beast);
        SelectedBeast = beast;
        PlayerPrefs.SetInt(BeastKey, (int)beast);
        Save();
    }

    public bool IsInSquad(BeastId beast)
    {
        return squad.Contains(beast);
    }

    public SquadChange ToggleSquadMember(BeastId beast)
    {
        if (!IsBeastUnlocked(beast))
            return SquadChange.NotOwned;

        if (squad.Contains(beast))
        {
            if (squad.Count <= 1)
                return SquadChange.LastMember;
            squad.Remove(beast);
            if (SelectedBeast == beast)
            {
                SelectedBeast = squad[0];
                PlayerPrefs.SetInt(BeastKey, (int)SelectedBeast);
            }
            Save();
            return SquadChange.Removed;
        }

        if (squad.Count >= MaxSquadSize)
            return SquadChange.Full;
        squad.Add(beast);
        Save();
        return SquadChange.Added;
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
        SelectedBeast = BeastId.Choma;
        squad.Clear();
        squad.Add(BeastId.Choma);
        PlayerPrefs.DeleteKey(ThistleRescueSequence.SeenKey);
        foreach (BeastId beast in BeastRoster.AllBeasts)
            PlayerPrefs.DeleteKey(ThistleRescueSequence.SeenKeyFor(beast));
        PlayerPrefs.DeleteKey(GameHubController.SeenChapterKey);
        PlayerPrefs.DeleteKey(StarsKey);
        Save();
    }

    // reads everything back from PlayerPrefs (saved on the phone) when the game starts
    void Load()
    {
        Coins = PlayerPrefs.GetInt(CoinsKey, 0);
        HighestUnlockedStage = PlayerPrefs.GetInt(StageKey, 1);
        HighestClearedStage = PlayerPrefs.GetInt(ClearedStageKey, 0);
        LaunchPowerLevel = PlayerPrefs.GetInt(LaunchKey, 0);
        SkillCooldownLevel = PlayerPrefs.GetInt(SkillKey, 0);
        LaunchPowerLevel = Mathf.Clamp(LaunchPowerLevel, 0, MaxLaunchPowerLevel);
        SkillCooldownLevel = Mathf.Clamp(SkillCooldownLevel, 0, MaxSkillCooldownLevel);
        SelectedBeast = (BeastId)PlayerPrefs.GetInt(BeastKey, 0);

        if (!IsBeastUnlocked(SelectedBeast))
        {
            SelectedBeast = BeastId.Choma;
            PlayerPrefs.SetInt(BeastKey, (int)BeastId.Choma);
            PlayerPrefs.Save();
        }

        LoadSquad();
    }

    // saved like "0,1,2". older saves have no squad, so it starts like the old automatic one
    // (the equipped Beast, then the other owned Beasts in order)
    void LoadSquad()
    {
        squad.Clear();
        string saved = PlayerPrefs.GetString(SquadKey, string.Empty);
        foreach (string part in saved.Split(','))
        {
            if (squad.Count >= MaxSquadSize)
                break;
            if (int.TryParse(part, out int value) && System.Enum.IsDefined(typeof(BeastId), value))
            {
                BeastId beast = (BeastId)value;
                if (IsBeastUnlocked(beast) && !squad.Contains(beast))
                    squad.Add(beast);
            }
        }

        if (squad.Count == 0)
        {
            squad.Add(SelectedBeast);
            foreach (BeastId beast in BeastRoster.AllBeasts)
            {
                if (squad.Count >= MaxSquadSize)
                    break;
                if (IsBeastUnlocked(beast) && !squad.Contains(beast))
                    squad.Add(beast);
            }
        }

        // the equipped Beast always leads
        if (!squad.Contains(SelectedBeast))
        {
            if (squad.Count >= MaxSquadSize)
                squad.RemoveAt(squad.Count - 1);
            squad.Insert(0, SelectedBeast);
        }
        else if (squad[0] != SelectedBeast)
        {
            squad.Remove(SelectedBeast);
            squad.Insert(0, SelectedBeast);
        }
    }

    // writes coins, stages, upgrades and Beasts to PlayerPrefs so they're still there after closing the game
    void Save()
    {
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.SetInt(StageKey, HighestUnlockedStage);
        PlayerPrefs.SetInt(ClearedStageKey, HighestClearedStage);
        PlayerPrefs.SetInt(LaunchKey, LaunchPowerLevel);
        PlayerPrefs.SetInt(SkillKey, SkillCooldownLevel);
        PlayerPrefs.SetInt(BeastKey, (int)SelectedBeast);
        System.Text.StringBuilder saved = new System.Text.StringBuilder(8);
        for (int i = 0; i < squad.Count; i++)
        {
            if (i > 0)
                saved.Append(',');
            saved.Append((int)squad[i]);
        }
        PlayerPrefs.SetString(SquadKey, saved.ToString());
        PlayerPrefs.Save();
    }
}