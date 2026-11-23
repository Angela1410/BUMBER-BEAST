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
