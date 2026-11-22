using System.Collections.Generic;
using UnityEngine;

// Loads the generated PLACEHOLDER sprites in Assets/Resources/Placeholders by name, for the
// Beasts, bosses and enemies that have no painted art yet. To swap one out, replace its PNG
// with real art under the same name (imported as a Sprite); nothing else needs rewiring.
public static class PlaceholderArt
{
    private const string Folder = "Placeholders/";
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (Cache.TryGetValue(name, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(Folder + name);
        if (sprite == null)
            Debug.LogWarning("Placeholder art Assets/Resources/" + Folder + name + ".png is missing; a plain " +
                "shape is used instead. The Unity editor generates it after the next script reload.");
        Cache[name] = sprite;   // a miss is cached too, so the warning appears once
        return sprite;
    }

    public static Sprite BeastIdle(BeastId beast)
    {
        return Get("Beast_" + beast + "_Idle");
    }

    public static Sprite BeastBall(BeastId beast)
    {
        return Get("Beast_" + beast + "_Ball");
    }

    // "VOLTREX" / "Voltrex" -> Boss_Voltrex.
    public static Sprite Boss(string bossName)
    {
        if (string.IsNullOrEmpty(bossName))
            return null;
        string trimmed = bossName.Trim();
        string title = trimmed.Substring(0, 1).ToUpperInvariant() + trimmed.Substring(1).ToLowerInvariant();
        return Get("Boss_" + title);
    }

    // Beasts that ship with painted art (Choma, Thistle) never fall back to a placeholder.
    public static bool HasPaintedArt(BeastId beast)
    {
        return beast == BeastId.Choma || beast == BeastId.Thistle;
    }
}
