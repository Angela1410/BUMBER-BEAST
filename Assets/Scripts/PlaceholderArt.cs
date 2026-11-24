using System.Collections.Generic;
using UnityEngine;

// loads the generated PLACEHOLDER sprites in Assets/Resources/Placeholders by name, for the Beasts,
// bosses and enemies that don't have painted art yet. to swap one, replace its PNG with real art
// with the same name (imported as a Sprite), nothing else needs to change
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
        Cache[name] = sprite; // a miss gets saved too, so the warning only shows once
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

    // the idle animation in Placeholders/Frames: Beast_<name>_Idle_00 ... _07 bob the floating bits
    // (rocks, sparks, water drops), _08 to _10 are a blink. it plays 00-07 twice with a blink at
    // the end, like Thistle's idle. null if the frames aren't there (the single picture is used)
    private static readonly Dictionary<BeastId, Sprite[]> IdleFrames = new Dictionary<BeastId, Sprite[]>();

    public static Sprite[] BeastIdleFrames(BeastId beast)
    {
        if (IdleFrames.TryGetValue(beast, out Sprite[] cached))
            return cached;

        List<Sprite> found = new List<Sprite>();
        for (int index = 0; index < 32; index++)
        {
            Sprite frame = Resources.Load<Sprite>(Folder + "Frames/Beast_" + beast + "_Idle_" + index.ToString("00"));
            if (frame == null)
                break;
            found.Add(frame);
        }

        Sprite[] sequence = null;
        if (found.Count >= 11)
        {
            int[] order = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 8, 9, 10, 7 };
            sequence = new Sprite[order.Length];
            for (int i = 0; i < order.Length; i++)
                sequence[i] = found[order[i]];
        }
        else if (found.Count > 0)
        {
            sequence = found.ToArray();
        }
        IdleFrames[beast] = sequence;
        return sequence;
    }

    // "VOLTREX" / "Voltrex" -> Boss_Voltrex
    public static Sprite Boss(string bossName)
    {
        if (string.IsNullOrEmpty(bossName))
            return null;
        string trimmed = bossName.Trim();
        string title = trimmed.Substring(0, 1).ToUpperInvariant() + trimmed.Substring(1).ToLowerInvariant();
        return Get("Boss_" + title);
    }

    // Beasts that already have painted art (Choma, Thistle) never use a placeholder
    public static bool HasPaintedArt(BeastId beast)
    {
        return beast == BeastId.Choma || beast == BeastId.Thistle;
    }
}
