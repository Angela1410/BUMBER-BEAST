using UnityEngine;

// every number we tune for the gameplay lives here, in one place.
// the values are saved in Assets/Resources/GameTuning.asset: click it in the Project window and
// change them in the Inspector, no code needed. if that file is missing the game uses the numbers below
[CreateAssetMenu(fileName = "GameTuning", menuName = "Bumper Beast/Game Tuning")]
public class GameTuning : ScriptableObject
{
    [Header("Difficulty (one value each for EASY, MEDIUM, HARD, HELL)")]
    [Tooltip("Enemy walking speed. 1 = normal.")]
    public float[] enemySpeed = { 0.75f, 1f, 1.2f, 1.45f };
    [Tooltip("Enemy health. 1 = normal.")]
    public float[] enemyHealth = { 0.7f, 1f, 1.3f, 1.7f };
    [Tooltip("Extra barrier health (can be below 0).")]
    public int[] barrierBonus = { 3, 0, -1, -2 };
    [Tooltip("Aim assist strength. 0 = off.")]
    public float[] aimAssist = { 1.4f, 1f, 0.6f, 0f };
    [Tooltip("Game speed while you pull back to aim. 1 = no slow motion.")]
    public float[] aimSlowMotion = { 0.35f, 0.65f, 1f, 1f };
    [Tooltip("Coin reward multiplier.")]
    public float[] coinMultiplier = { 1f, 1f, 1.5f, 2f };

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
