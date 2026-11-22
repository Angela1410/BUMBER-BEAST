using UnityEngine;

// Player preferences shared by the title screen, the hub and the pause menu: music and sound
// volumes, screen shake, aim sensitivity, drag-anywhere aiming and the profile name. Saved in
// PlayerPrefs (on this device only).
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

    private static bool loaded;
    private static float musicVolume = 0.8f;
    private static float sfxVolume = 1f;
    private static bool screenShake = true;
    private static string playerName = DefaultPlayerName;
    private static float sensitivitySlider = 0.5f;
    private static bool dragAnywhere = true;

    // Aim sensitivity as a slider position (0..1); 0.5 is the normal feel.
    public static float AimSensitivitySlider
    {
        get { Load(); return sensitivitySlider; }
        set { Load(); SetFloat(SensitivityKey, ref sensitivitySlider, value); }
    }

    // How much launch power one unit of finger drag gives: 0.5x (precise) to 2x (short drags).
    public static float AimSensitivity
    {
        get { return 0.5f * Mathf.Pow(4f, AimSensitivitySlider); }
    }

    // Start aiming by touching anywhere on the field, not only on the Beast.
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

    // Raised after any value changes (the audio mixer listens for volume changes).
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

    // Trimmed, upper-case, at most MaxNameLength characters; empty becomes the default name.
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

    // Slider drags change volumes many times a second; they are written to disk once the
    // settings card closes (PlayerPrefs.Save), not on every step.
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
