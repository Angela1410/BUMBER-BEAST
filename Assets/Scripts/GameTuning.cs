using UnityEngine;

// every number we tune for the gameplay lives here, in one place.
// the values are saved in Assets/Resources/GameTuning.asset: click it in the Project window and
// change them in the Inspector, no code needed. if that file is missing the game uses the numbers below
[CreateAssetMenu(fileName = "GameTuning", menuName = "Bumper Beast/Game Tuning")]
public class GameTuning : ScriptableObject
{
    [Header("Difficulty (one value each for EASY, MEDIUM, HARD, HELL)")]
    // MEDIUM was 1 and 1 before v1.4: we moved it halfway toward EASY because late stages were too much
    [Tooltip("Enemy walking speed. 1 = the speed the stages were made with.")]
    public float[] enemySpeed = { 0.75f, 0.875f, 1.2f, 1.45f };
    [Tooltip("Enemy health. 1 = the health the stages were made with.")]
    public float[] enemyHealth = { 0.7f, 0.85f, 1.3f, 1.7f };
    [Tooltip("Extra barrier health (can be below 0).")]
    public int[] barrierBonus = { 3, 0, -1, -2 };
    [Tooltip("Aim assist strength. 0 = off.")]
    public float[] aimAssist = { 1.4f, 1f, 0.6f, 0f };
    [Tooltip("Game speed while you pull back to aim. 1 = no slow motion.")]
    public float[] aimSlowMotion = { 0.35f, 0.65f, 1f, 1f };
    [Tooltip("Coin reward multiplier.")]
    public float[] coinMultiplier = { 1f, 1f, 1.5f, 2f };

    [Header("Screen fit")]
    [Tooltip("Free space kept between a bumper (or bramble, or boulder) and a side wall, in bumper widths.")]
    public float bumperWallGap = 1f;

    [Header("Bounce power")]
    [Tooltip("Extra damage for every bumper or wall the Beast bounces off in one shot.")]
    public int bouncePowerPerHit = 1;
    [Tooltip("The most extra damage bouncing can add in one shot.")]
    public int bouncePowerMax = 4;
    [Tooltip("How much higher the bumper sound gets with each bounce in a shot (0.08 = 8%).")]
    public float bouncePitchStep = 0.08f;

    [Header("Swap recall")]
    [Tooltip("When SWAP calls a flying Beast back, the next launch is locked for this many seconds.")]
    public float recallLaunchLock = 1.5f;

    [Header("Coming back to the pad")]
    [Tooltip("The Beast flies back to the pad after this many bounces in one shot...")]
    public int returnAfterBounces = 6;
    [Tooltip("...or after this many seconds in the air, whichever comes first.")]
    public float returnAfterSeconds = 3f;
    [Tooltip("How long the flight back to the pad takes, in seconds.")]
    public float returnFlightTime = 0.3f;
    [Tooltip("...or when it hasn't hit an enemy or a bumper for this many seconds (a shot that's just flying " +
        "around comes back sooner). 0 = off.")]
    public float returnWhenNothingHit = 1.5f;

    [Header("Help for new players")]
    [Tooltip("In stages up to this one, a hand shows how to launch when you wait on the pad during a wave.")]
    public int hintUpToStage = 4;
    [Tooltip("Seconds of waiting on the pad (with enemies coming) before the hand shows up.")]
    public float hintAfterIdleSeconds = 3f;
    [Tooltip("Game speed during the stage 1 skill lesson (1 = normal).")]
    public float skillLessonSlowMotion = 0.4f;
    [Tooltip("Longest the stage 1 skill lesson waits for the tap, in real seconds.")]
    public float skillLessonSeconds = 4f;

    [Header("Power meter")]
    [Tooltip("Green bumper hits needed to fill the POWER meter. Full = a green bumper turns into the Power Bumper.")]
    public int powerMeterHits = 6;

    [Header("Waves")]
    [Tooltip("Timed waves send their enemies in small groups of this many, with a break between groups. 0 = no breaks.")]
    public int enemiesPerGroup = 4;
    [Tooltip("Seconds of break between groups. Nothing spawns and the wave clock waits, so the wave still sends the same number of enemies.")]
    public float breakBetweenGroups = 2f;
    [Tooltip("Seconds an enemy stops walking after it gets hit.")]
    public float staggerSeconds = 0.3f;
    [Tooltip("Barrier health you get back when you clear a wave (never above full).")]
    public int barrierHealOnWaveClear = 1;

    [Header("Aiming (bumper magnet)")]
    [Tooltip("The most the aim line is turned toward a bumper, in degrees, at aim assist strength 1 " +
        "(it's multiplied by the difficulty's aim assist: EASY 1.4, MEDIUM 1, HARD 0.6, HELL 0 = off).")]
    public float magnetDegrees = 5f;
    [Tooltip("How close (world units) the aim line must pass a bumper before it's pulled onto it.")]
    public float magnetReach = 0.4f;

    [Header("Idle animation (launch pad and Beasts tab)")]
    [Tooltip("How high a resting Beast bobs, as a part of its height (0.04 = 4%).")]
    public float idleBobHeight = 0.04f;
    [Tooltip("How much it squashes and stretches while breathing (0.03 = 3%).")]
    public float idleBreath = 0.03f;
    [Tooltip("Bobs per second at speed 1.")]
    public float idleBobsPerSecond = 0.55f;
    [Tooltip("Speed for each Beast: Choma, Thistle, Mossback, Voltis, Ripple (different, so they never move together).")]
    public float[] idleSpeed = { 1f, 1.15f, 0.75f, 1.3f, 0.9f };

    [Header("Bosses: rage mode")]
    [Tooltip("A boss starts its own rage mode when its health drops to this part of full (0.3 = 30%).")]
    public float bossRageAt = 0.3f;

    [Header("Thornmaw (stage 4, Grass): Bramble Drop, rage OVERGROWTH")]
    [Tooltip("In rage, every this many seconds Thornmaw heals from the brambles that are still standing.")]
    public float thornmawRegrowSeconds = 3f;
    [Tooltip("Most health one regrow gives back (1 per standing bramble, up to this).")]
    public int thornmawRegrowMax = 2;
    [Tooltip("Regrowing never takes Thornmaw above this part of its health.")]
    public float thornmawRegrowCap = 0.45f;
    [Tooltip("In rage the brambles drop this much more often (0.6 = every 60% of the normal time).")]
    public float thornmawRageDropTime = 0.6f;

    [Header("Terrathorn (stage 8, Earth): Stone Wall, rage AVALANCHE")]
    [Tooltip("Seconds between Stone Walls (the wall goes up in front of Terrathorn and blocks straight shots).")]
    public float terrathornWallEvery = 8f;
    [Tooltip("Hits each wall rock takes before it breaks.")]
    public int terrathornWallHits = 2;
    [Tooltip("Seconds a wall stands before it crumbles by itself.")]
    public float terrathornWallSeconds = 5f;
    [Tooltip("In rage Terrathorn curls up and rolls this much faster.")]
    public float terrathornRollSpeed = 1.8f;
    [Tooltip("How far (world units) a hit pushes the rolling Terrathorn back up its path.")]
    public float terrathornRollPushBack = 0.9f;

    [Header("Voltrex (stage 12, Electric): Overcharge + Thunder Blink, rage THUNDERSTORM")]
    [Tooltip("Seconds between Overcharges.")]
    public float voltrexOverchargeEvery = 5.5f;
    [Tooltip("Overcharge speeds up every enemy on the field (and Voltrex) this much...")]
    public float voltrexOverchargeSpeed = 1.9f;
    [Tooltip("...for this many seconds.")]
    public float voltrexOverchargeSeconds = 3.5f;
    [Tooltip("How far Voltrex jumps sideways after an Overcharge (world units). 0 = no jump.")]
    public float voltrexBlinkDistance = 1.8f;
    [Tooltip("In rage, lightning strikes the bumpers every this many seconds.")]
    public float voltrexStormEvery = 6f;
    [Tooltip("Bumpers each storm charges (a charged bumper sparks; touching it zaps the Beast back to the pad).")]
    public int voltrexStormStrikes = 2;
    [Tooltip("Seconds the yellow mark shows on a bumper before the lightning hits it.")]
    public float voltrexStrikeWarning = 1f;
    [Tooltip("Seconds a bumper stays charged.")]
    public float voltrexChargedSeconds = 4f;

    [Header("Maelith (stage 16, Water): Tide Shield, rage MIRROR MIST")]
    [Tooltip("Hits the Tide Shield blocks.")]
    public int maelithShieldHits = 2;
    [Tooltip("Seconds between shields (in rage it comes back faster, 60% of this).")]
    public float maelithShieldEvery = 5.5f;
    [Tooltip("Water copies Maelith makes in rage (they pop after one hit and waste your shot).")]
    public int maelithCopies = 2;
    [Tooltip("Seconds before new copies come when the old ones are gone.")]
    public float maelithCopyEvery = 6f;

    [Header("Pyrax (stage 20, Fire): Ember Call, rage PHOENIX")]
    [Tooltip("Seconds between Ember Calls (two imps come out of fire next to Pyrax).")]
    public float pyraxCallEvery = 7f;
    [Tooltip("Seconds between Ember Calls in rage.")]
    public float pyraxRageCallEvery = 4f;
    [Tooltip("In rage, Pyrax comes back to life once with this part of its health (0 = no rebirth).")]
    public float pyraxRebornHealth = 0.3f;

    // the editor uses this to save the file again when new settings are added (GameTuningAsset)
    [HideInInspector] public int fileVersion;

    private const string ResourceName = "GameTuning";
    private static GameTuning loaded;
    private static bool warned;

    // the tuning the game is using right now
    public static GameTuning Get
    {
        get
        {
            if (loaded != null)
                return loaded;
            loaded = Resources.Load<GameTuning>(ResourceName);
            if (loaded == null)
            {
                // no file yet: use the numbers written in this script
                if (!warned)
                    Debug.LogWarning("Assets/Resources/GameTuning.asset is missing, using the default tuning. " +
                        "The Unity editor makes the file by itself after the next script reload.");
                warned = true;
                loaded = CreateInstance<GameTuning>();
            }
            return loaded;
        }
    }

    // one value from a per-difficulty list. a list that is too short uses its last value
    public static float Pick(float[] values, int index, float fallback)
    {
        if (values == null || values.Length == 0)
            return fallback;
        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }

    public static int Pick(int[] values, int index, int fallback)
    {
        if (values == null || values.Length == 0)
            return fallback;
        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }
}
