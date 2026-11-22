using UnityEngine;

public enum GameDifficulty
{
    Easy,
    Medium,
    Hard,
    Hell
}

// What each difficulty changes (Settings > DIFFICULTY; Medium is the game as designed):
//                 enemy speed  enemy/boss HP  barrier  aim assist  slow-mo while aiming  coins
//   EASY             75%           70%          +3      strong       strong (35%)        x1   + every shot full power
//   MEDIUM          100%          100%           0      normal       light (65%)         x1
//   HARD            120%          130%          -1      weak         none                x1.5
//   HELL            145%          170%          -2      off          none                x2
// Enemy speed and health apply as enemies spawn; the barrier and rewards per stage.
public static class Difficulty
{
    private static readonly float[] Speed = { 0.75f, 1f, 1.2f, 1.45f };
    private static readonly float[] Health = { 0.7f, 1f, 1.3f, 1.7f };
    private static readonly int[] Barrier = { 3, 0, -1, -2 };
    private static readonly float[] Assist = { 1.4f, 1f, 0.6f, 0f };
    private static readonly float[] AimSlow = { 0.35f, 0.65f, 1f, 1f };
    private static readonly float[] Coins = { 1f, 1f, 1.5f, 2f };
    private static readonly string[] Names = { "EASY", "MEDIUM", "HARD", "HELL" };
    private static readonly Color32[] Colors =
    {
        new Color32(104, 173, 63, 255), new Color32(47, 145, 145, 255),
        new Color32(237, 125, 45, 255), new Color32(196, 52, 52, 255)
    };

    public static GameDifficulty Current => GameSettings.Difficulty;
    static int Index => Mathf.Clamp((int)Current, 0, Names.Length - 1);

    public static float EnemySpeed => Speed[Index];
    public static float EnemyHealth => Health[Index];
    public static int BarrierBonus => Barrier[Index];
    public static float AssistStrength => Assist[Index];
    public static float AimSlowMotion => AimSlow[Index];
    public static float RewardMultiplier => Coins[Index];
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

    public static int ScaleReward(int coins)
    {
        return Mathf.RoundToInt(coins * RewardMultiplier);
    }
}
