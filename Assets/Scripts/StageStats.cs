using UnityEngine;

// Combo chains and per-stage totals. Every enemy hit after a launch (bounces, skills, Power
// Bumpers) extends the chain; the next launch starts a new one. From the second hit on, the chain
// shows "COMBO xN" with a ding that climbs in pitch, and every hit past the second earns a bonus
// coin, paid on the result screen (capped per stage). Big moments get a hit-stop and a callout.
public static class StageStats
{
    public const int MaxComboCoinsPerStage = 40;

    private static readonly Color ComboLow = new Color(1f, 0.86f, 0.36f, 1f);
    private static readonly Color ComboMid = new Color(1f, 0.55f, 0.18f, 1f);
    private static readonly Color ComboHigh = new Color(0.86f, 0.42f, 1f, 1f);

    public static int Combo { get; private set; }
    public static int BestCombo { get; private set; }
    public static int ComboCoins { get; private set; }
    public static int TotalHits { get; private set; }
    public static int Defeated { get; private set; }

    // Raised on every hit (position, super effective, knocked out); the tutorial listens.
    public static event System.Action<Vector3, bool, bool> EnemyHit;

    public static void BeginStage()
    {
        Combo = 0;
        BestCombo = 0;
        ComboCoins = 0;
        TotalHits = 0;
        Defeated = 0;
    }

    public static void OnLaunch()
    {
        Combo = 0;
    }

    public static void RegisterHit(Vector3 position, bool superEffective, bool knockedOut, bool boss)
    {
        TotalHits++;
        if (knockedOut)
            Defeated++;
        Combo++;
        BestCombo = Mathf.Max(BestCombo, Combo);

        if (Combo >= 2)
        {
            Color color = Combo >= 8 ? ComboHigh : Combo >= 4 ? ComboMid : ComboLow;
            DamagePopup.ShowText((Vector2)position + new Vector2(0f, 0.75f), "COMBO x" + Combo, color);
            GameAudio.PlayPitched(Sfx.Combo, 0.8f, Mathf.Min(2.2f, 0.9f + 0.09f * Combo));
            if (Combo >= 3 && ComboCoins < MaxComboCoinsPerStage)
                ComboCoins++;
        }

        if (Combo == 5)
            Milestone("GREAT COMBO!", ComboMid);
        else if (Combo == 8)
            Milestone("AMAZING!", ComboHigh);
        else if (Combo == 12)
            Milestone("LEGENDARY!", ComboHigh);

        if (boss || (superEffective && knockedOut))
            HitStop.Pause(boss ? 0.05f : 0.04f);

        if (EnemyHit != null)
            EnemyHit(position, superEffective, knockedOut);
    }

    static void Milestone(string text, Color color)
    {
        StageToast.Show(text, "COMBO x" + Combo, color, 1.1f, 0.68f);
        HitStop.Pause(0.06f);
    }

    // 3 stars: barrier untouched. 2: at least 60% left. 1: cleared.
    public static int StarsFor(int barrierHealth, int barrierMaxHealth)
    {
        if (barrierMaxHealth <= 0)
            return 1;
        if (barrierHealth >= barrierMaxHealth)
            return 3;
        return barrierHealth >= Mathf.CeilToInt(barrierMaxHealth * 0.6f) ? 2 : 1;
    }
}
