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
        switch (beast)
        {
            case BeastId.Thistle: return new Color(0.55f, 1f, 0.12f);
            case BeastId.Mossback: return new Color(0.65f, 0.3f, 0.08f);
            case BeastId.Voltis: return new Color(1f, 0.85f, 0.05f);
            case BeastId.Ripple: return new Color(0.15f, 0.45f, 1f);
            default: return new Color(1f, 0.2f, 0.1f);
        }
    }
}