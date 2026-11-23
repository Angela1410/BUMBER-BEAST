using UnityEngine;

// player settings used by the title screen, the map and the pause menu: music and sound volume,
// screen shake, difficulty, aim sensitivity, aim assist, drag anywhere aiming and the profile name.
// saved in PlayerPrefs (only on this phone)
public static class GameSettings
{
    public const string DefaultPlayerName = "BEAST KEEPER";
    public const int MaxNameLength = 14;

    private const string MusicKey = "BumperBeast.Settings.MusicVolume";
    private const string SfxKey = "BumperBeast.Settings.SfxVolume";
    private const string ShakeKey = "BumperBeast.Settings.ScreenShake";
    private const string NameKey = "BumperBeast.PlayerName";
    private const string SensitivityKey = "BumperBeast.Settings.AimSensitivity";
    private const string DragAnywhereKey = "BumperBeast.Settings.DragAnywhere";
    private const string AimAssistKey = "BumperBeast.Settings.AimAssist";
    private const string DifficultyKey = "BumperBeast.Settings.Difficulty";

    private static bool loaded;
    private static float musicVolume = 0.8f;
    private static float sfxVolume = 1f;
    private static bool screenShake = true;
    private static string playerName = DefaultPlayerName;
    private static float sensitivitySlider = 0.5f;
    private static bool dragAnywhere = true;
    private static bool aimAssist = true;
    private static GameDifficulty difficulty = GameDifficulty.Medium;

    // aim sensitivity as a slider position (0..1): how fast the aim follows the finger.
    // low = steady (shaky fingers get smoothed out), high = instant. 0.5 is the normal feel
    public static float AimSensitivitySlider
    {
        get { Load(); return sensitivitySlider; }
        set { Load(); SetFloat(SensitivityKey, ref sensitivitySlider, value); }
    }

    // how fast the aim catches up to the finger, per second
    public static float AimResponse
    {
        get { return Mathf.Lerp(6f, 40f, AimSensitivitySlider); }
    }

    public static GameDifficulty Difficulty
    {
        get { Load(); return difficulty; }
        set
        {
            Load();
            if (difficulty == value)
                return;
            difficulty = value;
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.Save();
            NotifyChanged();
        }
    }

    // a flying Beast curves a little toward an enemy in front of it
    public static bool AimAssist
    {
        get { Load(); return aimAssist; }
        set
        {
            Load();
            if (aimAssist == value)
                return;
            aimAssist = value;
            PlayerPrefs.SetInt(AimAssistKey, value ? 1 : 0);
            PlayerPrefs.Save();
            NotifyChanged();
        }
    }

    // start aiming by touching anywhere on the field, not only on the Beast
    public static bool DragAnywhere
    {
        get { Load(); return dragAnywhere; }
        set
        {
            Load();
            if (dragAnywhere == value)
                return;
            dragAnywhere = value;
            PlayerPrefs.SetInt(DragAnywhereKey, value ? 1 : 0);
            PlayerPrefs.Save();
            NotifyChanged();
        }
    }

    // sent when any setting changes (the audio listens for volume changes)
    public static event System.Action Changed;

    public static float MusicVolume
    {
        get { Load(); return musicVolume; }
        set { Load(); SetFloat(MusicKey, ref musicVolume, value); }
    }

    public static float SfxVolume
    {
        get { Load(); return sfxVolume; }
        set { Load(); SetFloat(SfxKey, ref sfxVolume, value); }
    }

    public static bool ScreenShake
    {
        get { Load(); return screenShake; }
        set
        {
            Load();
            if (screenShake == value)
                return;
            screenShake = value;
            PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
            PlayerPrefs.Save();
            NotifyChanged();
        }
    }

    public static string PlayerName
    {
        get { Load(); return playerName; }
        set
        {
            Load();
            string cleaned = CleanName(value);
            if (cleaned == playerName)
                return;
            playerName = cleaned;
            PlayerPrefs.SetString(NameKey, cleaned);
            PlayerPrefs.Save();
            NotifyChanged();
        }
    }

    // trimmed, upper case, at most MaxNameLength letters. empty becomes the default name
    public static string CleanName(string value)
    {
        string cleaned = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        if (cleaned.Length > MaxNameLength)
            cleaned = cleaned.Substring(0, MaxNameLength).Trim();
        return cleaned.Length == 0 ? DefaultPlayerName : cleaned.ToUpperInvariant();
    }

    static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, musicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, sfxVolume));
        screenShake = PlayerPrefs.GetInt(ShakeKey, 1) != 0;
        playerName = CleanName(PlayerPrefs.GetString(NameKey, DefaultPlayerName));
        sensitivitySlider = Mathf.Clamp01(PlayerPrefs.GetFloat(SensitivityKey, sensitivitySlider));
        dragAnywhere = PlayerPrefs.GetInt(DragAnywhereKey, 1) != 0;
        aimAssist = PlayerPrefs.GetInt(AimAssistKey, 1) != 0;
        difficulty = (GameDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)GameDifficulty.Medium),
            (int)GameDifficulty.Easy, (int)GameDifficulty.Hell);
    }

    static void SetFloat(string key, ref float field, float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, value))
            return;
        field = value;
        PlayerPrefs.SetFloat(key, value);
        NotifyChanged();
    }

    // dragging a slider changes the volume many times a second, so we only write to disk
    // when the settings card closes (PlayerPrefs.Save), not on every step
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    static void NotifyChanged()
    {
        System.Action handler = Changed;
        if (handler != null)
            handler();
    }
}
