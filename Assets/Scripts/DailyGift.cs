using System;
using UnityEngine;

// A coin gift once per calendar day (device time). Coming back on consecutive days climbs a
// 7-day ladder of bigger gifts; missing a day starts again at day 1, and day 7 loops to day 1.
public static class DailyGift
{
    public static readonly int[] Rewards = { 20, 25, 30, 40, 50, 60, 100 };

    private const string LastKey = "BumperBeast.DailyGift.LastDay";
    private const string StreakKey = "BumperBeast.DailyGift.Streak";

    static int DayStamp(DateTime date)
    {
        return date.Year * 10000 + date.Month * 100 + date.Day;
    }

    static int Today => DayStamp(DateTime.Now.Date);
    static int Yesterday => DayStamp(DateTime.Now.Date.AddDays(-1));

    public static bool IsReady => PlayerPrefs.GetInt(LastKey, 0) != Today;

    // The ladder step (1-7) the next claim gives, or today's step once claimed.
    public static int CurrentDay
    {
        get
        {
            int last = PlayerPrefs.GetInt(LastKey, 0);
            int streak = Mathf.Clamp(PlayerPrefs.GetInt(StreakKey, 0), 0, Rewards.Length);
            if (last == Today)
                return Mathf.Max(1, streak);
            if (last == Yesterday)
                return streak >= Rewards.Length ? 1 : streak + 1;
            return 1;
        }
    }

    public static int RewardFor(int day)
    {
        return Rewards[Mathf.Clamp(day, 1, Rewards.Length) - 1];
    }

    // Pays today's gift; returns the coins (0 when it was already claimed today).
    public static int Claim()
    {
        if (!IsReady)
            return 0;
        int day = CurrentDay;
        int coins = RewardFor(day);
        PlayerPrefs.SetInt(LastKey, Today);
        PlayerPrefs.SetInt(StreakKey, day);
        PlayerPrefs.Save();
        if (GameProgress.Instance != null)
            GameProgress.Instance.AddCoins(coins);
        return coins;
    }
}
