using UnityEngine;

// all the Beasts you can play as
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

    // each Beast's element
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

    // you have to beat this stage first to free the Beast
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

    // how many coins it costs to recruit the Beast
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

    // the name of each Beast's skill
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

    // placeholder color, only used while a Beast has no art yet.
    // once real sprites are set, LaunchController ignores this
    public static Color GetColor(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return new Color(0.6f, 1f, 0.6f);
            case BeastId.Mossback: return new Color(0.75f, 0.6f, 0.45f);
            case BeastId.Voltis: return new Color(1f, 0.95f, 0.5f);
            case BeastId.Ripple: return new Color(0.55f, 0.75f, 1f);
            default: return Color.white;
        }
    }

    // color of the charging sparks while aiming
    public static Color GetEffectColor(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return new Color(0.45f, 1f, 0.35f);
            case BeastId.Mossback: return new Color(0.7f, 0.5f, 0.3f);
            case BeastId.Voltis: return new Color(1f, 0.95f, 0.3f);
            case BeastId.Ripple: return new Color(0.4f, 0.75f, 1f);
            default: return new Color(1f, 0.55f, 0.15f);
        }
    }
}