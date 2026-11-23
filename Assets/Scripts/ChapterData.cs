using UnityEngine;

public struct ChapterInfo
{
    public int index;
    public string title;
    public ElementType element;
    public BackdropTheme theme;
    public string tagline;
    public string boss;
    public ElementType bossElement;
    public bool hasRescue;
    public BeastId rescue;
    public Color accent;
    // one line of matchup advice ("Earth beats Electric - bring Mossback")
    public string advice;
}

// all the chapters and stages in one place, so the map, loading screen, pause menu,
// result cards and rescue scenes all describe the same world
public static class ChapterData
{
    public const int StagesPerChapter = 4;
    public const int ChapterCount = 5;
    public const int StageCount = ChapterCount * StagesPerChapter;

    // the names of all 20 stages, in order
    private static readonly string[] StageNames =
    {
        "Mossy Trail", "Briar Hollow", "Tangled Grove", "Thornmaw's Den",
        "Dustway Trail", "Rubble Weave", "Fault Line", "Terrathorn's Quarry",
        "Static Ridge", "Thunder Steps", "Coil Canyon", "Voltrex's Spire",
        "Shallow Fords", "Drowned Arches", "Riptide Run", "Maelith's Grotto",
        "Cinder Road", "Ashfall Basin", "Magma Gate", "Pyrax's Caldera"
    };

    // tips shown on the loading screen
    private static readonly string[] Tips =
    {
        "Every enemy wears its element badge. Swap to the Beast that beats it.",
        "A gold glow on an enemy's badge means the Beast on the pad deals double damage.",
        "Tap SWAP while your Beast is flying to call it straight back as the next Beast.",
        "Fire beats Grass, Grass beats Earth, Earth beats Electric, Electric beats Water, Water beats Fire.",
        "Your squad holds 3 Beasts. Choose them in the Shop's Beasts tab.",
        "Boss stages end at once if the boss reaches your barrier. Keep it busy!",
        "Bank shots off the bumpers to reach enemies hiding behind others.",
        "Skill Cooldown upgrades let every Beast use its ability more often.",
        "Cleared stages can be replayed from the map at any time.",
        "Plan first: the wave card shows what is coming and who beats it."
    };

    public static int ChapterOfStage(int stage)
    {
        return Mathf.Clamp((stage - 1) / StagesPerChapter, 0, ChapterCount - 1);
    }

    public static int FirstStage(int chapter)
    {
        return chapter * StagesPerChapter + 1;
    }

    public static int LastStage(int chapter)
    {
        return Mathf.Min(StageCount, (chapter + 1) * StagesPerChapter);
    }

    // every 4th stage (4, 8, 12, 16, 20) is a boss stage
    public static bool IsBossStage(int stage)
    {
        return stage >= 1 && stage <= StageCount && stage % StagesPerChapter == 0;
    }

    public static string StageName(int stage)
    {
        return stage >= 1 && stage <= StageNames.Length ? StageNames[stage - 1] : "Stage " + stage;
    }

    public static string RandomTip()
    {
        return Tips[Random.Range(0, Tips.Length)];
    }

    public static ChapterInfo Get(int chapter)
    {
        switch (Mathf.Clamp(chapter, 0, ChapterCount - 1))
        {
            case 0:
                return new ChapterInfo
                {
                    index = 0, title = "WILDROOT THICKET", element = ElementType.Grass, theme = BackdropTheme.Grass,
                    tagline = "Thornmaw's corrupted forest", boss = "THORNMAW", bossElement = ElementType.Grass,
                    hasRescue = true, rescue = BeastId.Thistle, accent = new Color32(140, 200, 96, 255),
                    advice = "Fire beats Grass - Choma's Flame Burst clears the thorns."
                };
            case 1:
                return new ChapterInfo
                {
                    index = 1, title = "STONE HOLLOW", element = ElementType.Earth, theme = BackdropTheme.Earth,
                    tagline = "Sun-baked canyons and rumbling quarries", boss = "TERRATHORN", bossElement = ElementType.Earth,
                    hasRescue = true, rescue = BeastId.Mossback, accent = new Color32(214, 160, 96, 255),
                    advice = "Grass beats Earth - bring Thistle. Choma still burns the Thorn Pups."
                };
            case 2:
                return new ChapterInfo
                {
                    index = 2, title = "STORMCREST PEAKS", element = ElementType.Electric, theme = BackdropTheme.Electric,
                    tagline = "Storm-charged highlands", boss = "VOLTREX", bossElement = ElementType.Electric,
                    hasRescue = true, rescue = BeastId.Voltis, accent = new Color32(240, 220, 90, 255),
                    advice = "Earth beats Electric - Mossback grounds the storm. Thistle handles the rock crawlers."
                };
            case 3:
                return new ChapterInfo
                {
                    index = 3, title = "TIDEWATER RUINS", element = ElementType.Water, theme = BackdropTheme.Water,
                    tagline = "Flooded ruins and tide pools", boss = "MAELITH", bossElement = ElementType.Water,
                    hasRescue = true, rescue = BeastId.Ripple, accent = new Color32(110, 190, 230, 255),
                    advice = "Electric beats Water - Voltis cuts through the tide. Mossback still breaks the sparks."
                };
            default:
                return new ChapterInfo
                {
                    index = 4, title = "EMBERFALL CALDERA", element = ElementType.Fire, theme = BackdropTheme.Fire,
                    tagline = "Volcanic badlands", boss = "PYRAX", bossElement = ElementType.Fire,
                    hasRescue = false, rescue = BeastId.Choma, accent = new Color32(240, 120, 70, 255),
                    advice = "Water beats Fire - Ripple douses the flames. Voltis zaps the water foes."
                };
        }
    }
}
