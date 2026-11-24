using System.Collections.Generic;
using UnityEngine;

// the flip book sprites for skills and boss moves, in Assets/Resources/Fx/<Name>/<Name>_00.png ...
// (fire, rocks, lightning, water, thorns, bubbles...). each folder is one effect, its pictures play
// in file name order. to change an effect, save new PNGs with the same names. if a folder is missing
// the game just skips that picture, the old rings and lines still show
public static class FxArt
{
    // drawn above enemies (5) and the Beast (10), below the HUD
    public const int SkillOrder = 34;
    public const int BossOrder = 42;

    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

    public static Sprite[] Frames(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (Cache.TryGetValue(name, out Sprite[] cached))
            return cached;

        Sprite[] loaded = Resources.LoadAll<Sprite>("Fx/" + name);
        System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
        Sprite[] frames = loaded.Length > 0 ? loaded : null;
        Cache[name] = frames;
        return frames;
    }

    public static bool Has(string name)
    {
        return Frames(name) != null;
    }

    // plays an effect once. size = its width in the world
    public static SpriteFlipbookFx Play(string name, Vector3 position, float size, float framesPerSecond = 18f,
        int sortingOrder = SkillOrder)
    {
        return SpriteFlipbookFx.Create(Frames(name), position, size, framesPerSecond, sortingOrder, Color.white, false);
    }

    public static SpriteFlipbookFx Play(string name, Vector3 position, float size, float framesPerSecond,
        int sortingOrder, Color color)
    {
        return SpriteFlipbookFx.Create(Frames(name), position, size, framesPerSecond, sortingOrder, color, false);
    }

    // a looping effect that sits on "target" (auras, shields). call Stop() on it, or give it a time
    public static SpriteFlipbookFx Loop(string name, Transform target, float size, float framesPerSecond,
        int sortingOrder, Color color, float seconds = -1f)
    {
        if (target == null)
            return null;
        SpriteFlipbookFx fx = SpriteFlipbookFx.Create(Frames(name), target.position, size, framesPerSecond,
            sortingOrder, color, true);
        if (fx == null)
            return null;
        fx.Follow(target, Vector3.zero);
        if (seconds > 0f)
            fx.StopAfter(seconds);
        return fx;
    }

    // a lightning bolt stretched from one point to another, flickering for a moment
    public static SpriteFlipbookFx Bolt(Vector3 from, Vector3 to, float thickness, float seconds,
        int sortingOrder = SkillOrder)
    {
        Sprite[] frames = Frames("ZapBolt");
        Vector3 delta = to - from;
        float length = ((Vector2)delta).magnitude;
        if (frames == null || length < 0.05f)
            return null;

        SpriteFlipbookFx fx = SpriteFlipbookFx.Create(frames, (from + to) * 0.5f, 1f, 24f, sortingOrder, Color.white, true);
        Vector2 spriteSize = frames[0].bounds.size;
        fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        fx.transform.localScale = new Vector3(
            length / Mathf.Max(0.01f, spriteSize.x),
            thickness / Mathf.Max(0.01f, spriteSize.y), 1f);
        fx.StopAfter(seconds, Mathf.Min(0.15f, seconds));
        return fx;
    }

    // Voltrex's lightning strike: the bolt comes down from above and hits "ground"
    public static SpriteFlipbookFx Strike(Vector3 ground, float width, int sortingOrder = BossOrder)
    {
        Sprite[] frames = Frames("LightningStrike");
        if (frames == null)
            return null;
        Vector2 spriteSize = frames[0].bounds.size;
        float height = width * spriteSize.y / Mathf.Max(0.01f, spriteSize.x);
        // the hit point is drawn 46 px above the bottom of the 512 px tall picture
        Vector3 center = ground + Vector3.up * (height * 0.5f - height * 46f / 512f);
        return SpriteFlipbookFx.Create(frames, center, height, 16f, sortingOrder, Color.white, false);
    }

    // a fire column that stands on "ground" (Pyrax's Ember Call and its rebirth)
    public static SpriteFlipbookFx Pillar(Vector3 ground, float width, int sortingOrder = BossOrder)
    {
        Sprite[] frames = Frames("FlamePillar");
        if (frames == null)
            return null;
        Vector2 spriteSize = frames[0].bounds.size;
        float height = width * spriteSize.y / Mathf.Max(0.01f, spriteSize.x);
        // its base is drawn 40 px above the bottom of the 384 px tall picture
        Vector3 center = ground + Vector3.up * (height * 0.5f - height * 40f / 384f);
        return SpriteFlipbookFx.Create(frames, center, height, 14f, sortingOrder, Color.white, false);
    }

    // how big a sprite looks in the world (its longer side), so effects can match a boss
    public static float SizeOf(SpriteRenderer renderer, float fallback)
    {
        if (renderer == null || renderer.sprite == null)
            return fallback;
        Vector3 size = renderer.bounds.size;
        return Mathf.Max(size.x, size.y);
    }
}
