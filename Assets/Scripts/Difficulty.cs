using UnityEngine;

public enum GameDifficulty
{
    Easy,
    Medium,
    Hard,
    Hell
}

// what each difficulty changes (Settings > DIFFICULTY, MEDIUM is the normal game).
// the numbers live in GameTuning (Assets/Resources/GameTuning.asset), one value per difficulty:
// enemy speed, enemy health, extra barrier, aim assist, aim slow-mo and coins.
// enemy speed and health are set when enemies spawn, the barrier and coins once per stage
public static class Difficulty
{
    private static readonly string[] Names = { "EASY", "MEDIUM", "HARD", "HELL" };
    private static readonly Color32[] Colors =
    {
        new Color32(104, 173, 63, 255), new Color32(47, 145, 145, 255),
        new Color32(237, 125, 45, 255), new Color32(196, 52, 52, 255)
    };

    public static GameDifficulty Current => GameSettings.Difficulty;
    static int Index => Mathf.Clamp((int)Current, 0, Names.Length - 1);

    // other scripts read these to get the value for the difficulty we picked
    public static float EnemySpeed => GameTuning.Pick(GameTuning.Get.enemySpeed, Index, 1f);
    public static float EnemyHealth => GameTuning.Pick(GameTuning.Get.enemyHealth, Index, 1f);
    public static int BarrierBonus => GameTuning.Pick(GameTuning.Get.barrierBonus, Index, 0);
    public static float AssistStrength => GameTuning.Pick(GameTuning.Get.aimAssist, Index, 1f);
    public static float AimSlowMotion => GameTuning.Pick(GameTuning.Get.aimSlowMotion, Index, 1f);
    public static float RewardMultiplier => GameTuning.Pick(GameTuning.Get.coinMultiplier, Index, 1f);
    public static bool FullPowerShots => Current == GameDifficulty.Easy;

    public static string Name(GameDifficulty difficulty)
    {
        return Names[Mathf.Clamp((int)difficulty, 0, Names.Length - 1)];
    }

    public static Color ColorOf(GameDifficulty difficulty)
    {
        return Colors[Mathf.Clamp((int)difficulty, 0, Colors.Length - 1)];
    }

    public static string Hex(GameDifficulty difficulty)
    {
        return ColorUtility.ToHtmlStringRGB(ColorOf(difficulty));
    }

    // multiplies the coins we earn (x1.5 on HARD, x2 on HELL)
    public static int ScaleReward(int coins)
    {
        return Mathf.RoundToInt(coins * RewardMultiplier);
    }
}
