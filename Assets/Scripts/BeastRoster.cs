using UnityEngine;
using UnityEngine.UI;

public enum BeastId
{
    Choma,
    Thistle,
    Mossback,
    Voltis,
    Ripple
}

public class BeastRoster : MonoBehaviour
{
    public static readonly BeastId[] AllBeasts =
    {
        BeastId.Choma,
        BeastId.Thistle,
        BeastId.Mossback,
        BeastId.Voltis,
        BeastId.Ripple
    };

    public static string GetName(BeastId beast)
    {
        return beast.ToString();
    }

    public static ElementType GetElement(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return ElementType.Grass;
            case BeastId.Mossback: return ElementType.Earth;
            case BeastId.Voltis: return ElementType.Electric;
            case BeastId.Ripple: return ElementType.Water;
            default: return ElementType.Fire;
        }
    }

    public static int GetUnlockStage(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return 4;
            case BeastId.Mossback: return 8;
            case BeastId.Voltis: return 12;
            case BeastId.Ripple: return 16;
            default: return 1;
        }
    }

    public static int GetPrice(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return 75;
            case BeastId.Mossback: return 150;
            case BeastId.Voltis: return 250;
            case BeastId.Ripple: return 350;
            default: return 0;
        }
    }

    public static string GetAbilityName(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return "Bramble Snare";
            case BeastId.Mossback: return "Heavy Knock";
            case BeastId.Voltis: return "Chain Zap";
            case BeastId.Ripple: return "Splash Wave";
            default: return "Flame Burst";
        }
    }

   public static Color GetColor(BeastId beast)
    {
        // Returning pure white ensures the original 2D sprite artwork 
        // is displayed exactly as it was drawn, without any color tinting.
        return Color.white; 
    }
}