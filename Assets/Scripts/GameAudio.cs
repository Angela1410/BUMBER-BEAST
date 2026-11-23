using UnityEngine;
using UnityEngine.SceneManagement;

// sound effects, in the same order as GameAudio's table. each one is Assets/Resources/Audio/Sfx_<Name>.wav,
// so to change a sound just replace the file with one that has the same name
public enum Sfx
{
    Click,
    Play,
    Denied,
    Stretch,
    Launch,
    Bounce,
    Bumper,
    BumperExplode,
    Hit,
    HitSuper,
    Block,
    EnemyDefeat,
    BossDefeat,
    BarrierHit,
    BarrierBreak,
    WaveStart,
    BossWave,
    Victory,
    Defeat,
    Coin,
    Purchase,
    Swap,
    SkillFire,
    SkillGrass,
    SkillEarth,
    SkillElectric,
    SkillWater,
    Unlock,
    WaveClear,
    PowerBumper,
    Combo,
    Star,
    Tutorial,
    Gift
}

// music loops: Assets/Resources/Audio/Music_<Name>.wav (swap them by name too)
public enum MusicTrack
{
    None,
    Hub,
    Chapter1,
    Chapter2,
    Chapter3,
    Chapter4,
    Chapter5,
    Boss
}

// the one audio player for the whole game. it starts itself before the first scene, stays when
// scenes change, picks the music for each scene (map theme, the chapter's theme on a stage, the
// boss theme on boss stages), fades between them, and plays sound effects on a few voices.
// volumes come from GameSettings. if a sound file is missing it reports it once and skips it
public class GameAudio : MonoBehaviour
{
    private const string Folder = "Audio/";
    private const int VoiceCount = 12;
    private const float MusicBaseVolume = 0.55f;
    private const float CrossfadeSeconds = 1.2f;

    // mix for each effect: volume, the shortest time between two plays (so a skill that hits eight
    // enemies doesn't play eight copies at once) and a bit of random pitch so repeats sound alive
    private static readonly float[] SfxVolume =
    {
        0.5f, 0.8f, 0.6f, 0.32f, 0.75f, 0.42f, 0.6f, 0.85f, 0.62f, 0.8f, 0.6f, 0.6f, 0.95f, 0.8f, 1f,
        0.7f, 0.85f, 0.9f, 0.85f, 0.6f, 0.75f, 0.55f, 0.85f, 0.85f, 0.9f, 0.8f, 0.85f, 0.9f,
        0.8f, 0.9f, 0.6f, 0.75f, 0.55f, 0.85f
    };
    private static readonly float[] SfxMinInterval =
    {
        0.05f, 0.3f, 0.15f, 0.2f, 0.08f, 0.07f, 0.05f, 0.1f, 0.045f, 0.05f, 0.06f, 0.05f, 0.5f, 0.08f, 0.5f,
        0.5f, 0.5f, 1f, 1f, 0.05f, 0.2f, 0.1f, 0.15f, 0.15f, 0.15f, 0.15f, 0.15f, 1f,
        0.5f, 0.2f, 0.06f, 0.1f, 0.2f, 0.5f
    };
    private static readonly float[] SfxPitchJitter =
    {
        0.03f, 0f, 0f, 0.04f, 0.05f, 0.08f, 0.06f, 0.05f, 0.08f, 0.05f, 0.05f, 0.08f, 0f, 0.05f, 0f,
        0f, 0f, 0f, 0f, 0.03f, 0f, 0.03f, 0.03f, 0.03f, 0.03f, 0.03f, 0.03f, 0f,
        0f, 0f, 0f, 0f, 0f, 0f
    };

    private static GameAudio instance;

    private AudioClip[] sfxClips;
    private bool[] sfxChecked;
    private float[] sfxLastPlayed;
    private AudioClip[] musicClips;
    private bool[] musicChecked;

    private AudioSource[] voices;
    private float[] voiceStarted;
    private int nextVoice;

    private AudioSource musicCurrent;
    private AudioSource musicPrevious;
    private MusicTrack currentTrack = MusicTrack.None;
    private float fade = 1f;
    private float duck = 1f;
    private float duckTarget = 1f;
    private float duckUntil;

    private AudioListener fallbackListener;
    private float listenerCheckAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        EnsureInstance();
    }

    static GameAudio EnsureInstance()
    {
        if (instance == null)
        {
            GameObject root = new GameObject("GameAudio");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameAudio>();
        }
        return instance;
    }

    // --- what other scripts call ---

    public static void Play(Sfx sfx)
    {
        Play(sfx, 1f);
    }

    public static void Play(Sfx sfx, float volumeScale)
    {
        GameAudio audio = EnsureInstance();
        if (audio != null)
            audio.PlayEffect(sfx, volumeScale, 0f);
    }

    // a fixed pitch (1 = normal), like the combo sound that gets higher with each hit
    public static void PlayPitched(Sfx sfx, float volumeScale, float pitch)
    {
        GameAudio audio = EnsureInstance();
        if (audio != null)
            audio.PlayEffect(sfx, volumeScale, Mathf.Clamp(pitch, 0.25f, 3f));
    }

    // the skill sound for a Beast's element
    public static void PlaySkill(ElementType element)
    {
        switch (element)
        {
            case ElementType.Grass: Play(Sfx.SkillGrass); break;
            case ElementType.Earth: Play(Sfx.SkillEarth); break;
            case ElementType.Electric: Play(Sfx.SkillElectric); break;
            case ElementType.Water: Play(Sfx.SkillWater); break;
            default: Play(Sfx.SkillFire); break;
        }
    }

    public static void PlayMusic(MusicTrack track)
    {
        GameAudio audio = EnsureInstance();
        if (audio != null)
            audio.SwitchMusic(track);
    }

    // boss stages get the boss theme, every other stage gets its chapter's theme
    public static MusicTrack TrackForStage(int stageNumber)
    {
        if (ChapterData.IsBossStage(stageNumber))
            return MusicTrack.Boss;
        switch (ChapterData.ChapterOfStage(stageNumber))
        {
            case 1: return MusicTrack.Chapter2;
            case 2: return MusicTrack.Chapter3;
            case 3: return MusicTrack.Chapter4;
            case 4: return MusicTrack.Chapter5;
            default: return MusicTrack.Chapter1;
        }
    }

    // --- start up ---

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        int sfxCount = System.Enum.GetValues(typeof(Sfx)).Length;
        if (SfxVolume.Length != sfxCount || SfxMinInterval.Length != sfxCount || SfxPitchJitter.Length != sfxCount)
            Debug.LogError("GameAudio: the sound mix tables need one entry per Sfx value (" + sfxCount + ").");
        sfxClips = new AudioClip[sfxCount];
        sfxChecked = new bool[sfxCount];
        sfxLastPlayed = new float[sfxCount];
        for (int index = 0; index < sfxCount; index++)
            sfxLastPlayed[index] = -10f;
        int musicCount = System.Enum.GetValues(typeof(MusicTrack)).Length;
        musicClips = new AudioClip[musicCount];
        musicChecked = new bool[musicCount];

        voices = new AudioSource[VoiceCount];
        voiceStarted = new float[VoiceCount];
        for (int index = 0; index < VoiceCount; index++)
            voices[index] = CreateSource(false, 64);
        musicCurrent = CreateSource(true, 0);
        musicPrevious = CreateSource(true, 0);

        // the map scene doesn't have its own AudioListener, so this one fills in wherever there's none
        fallbackListener = gameObject.AddComponent<AudioListener>();
        fallbackListener.enabled = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
        GameSettings.Changed += ApplyVolumes;
    }

    void OnDestroy()
    {
        if (instance != this)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameSettings.Changed -= ApplyVolumes;
        instance = null;
    }

    AudioSource CreateSource(bool loop, int priority)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.priority = priority;
        source.volume = 0f;
        return source;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckListener();
        // objects from the old scene can stay for one more frame, so check again soon
        listenerCheckAt = Time.unscaledTime + 0.3f;

        if (scene.name == LoadingScreen.HubScene)
        {
            SwitchMusic(MusicTrack.Hub);
            return;
        }

        StageManager stage = FindAnyObjectByType<StageManager>();
        if (stage != null)
            SwitchMusic(TrackForStage(stage.stageNumber));
    }

    void CheckListener()
    {
        if (fallbackListener == null)
            return;

        bool sceneHasListener = false;
        AudioListener[] listeners = FindObjectsByType<AudioListener>();
        for (int index = 0; index < listeners.Length; index++)
        {
            if (listeners[index] != fallbackListener && listeners[index].isActiveAndEnabled)
            {
                sceneHasListener = true;
                break;
            }
        }
        fallbackListener.enabled = !sceneHasListener;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (listenerCheckAt >= 0f && now >= listenerCheckAt)
        {
            listenerCheckAt = -1f;
            CheckListener();
        }

        float delta = Time.unscaledDeltaTime;
        bool changed = false;
        if (fade < 1f)
        {
            fade = Mathf.Min(1f, fade + delta / CrossfadeSeconds);
            changed = true;
            if (fade >= 1f && musicPrevious.isPlaying)
                musicPrevious.Stop();
        }

        float target = now < duckUntil ? duckTarget : 1f;
        if (!Mathf.Approximately(duck, target))
        {
            // goes quiet fast, comes back slowly
            duck = Mathf.MoveTowards(duck, target, delta * (target < duck ? 4f : 0.8f));
            changed = true;
        }

        if (changed)
            ApplyVolumes();
    }

    void ApplyVolumes()
    {
        float music = MusicBaseVolume * GameSettings.MusicVolume * duck;
        if (musicCurrent != null)
            musicCurrent.volume = music * fade;
        if (musicPrevious != null)
            musicPrevious.volume = music * (1f - fade);
    }

    // --- sound effects ---

    // fixedPitch 0 = use the effect's own random pitch
    void PlayEffect(Sfx sfx, float volumeScale, float fixedPitch)
    {
        int index = (int)sfx;
        if (index < 0 || index >= sfxClips.Length)
            return;

        float volume = SfxVolume[index] * volumeScale * GameSettings.SfxVolume;
        float now = Time.unscaledTime;
        if (volume <= 0.001f || now - sfxLastPlayed[index] < SfxMinInterval[index])
            return;

        AudioClip clip = LoadSfx(index);
        if (clip == null)
            return;

        sfxLastPlayed[index] = now;
        int voiceIndex = PickVoice();
        AudioSource voice = voices[voiceIndex];
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(volume);
        float jitter = SfxPitchJitter[index];
        voice.pitch = fixedPitch > 0f ? fixedPitch : jitter > 0f ? 1f + Random.Range(-jitter, jitter) : 1f;
        voice.Play();
        voiceStarted[voiceIndex] = now;

        // jingles and big moments make the music quieter while they play
        if (sfx == Sfx.Victory || sfx == Sfx.Defeat || sfx == Sfx.Unlock || sfx == Sfx.BossDefeat || sfx == Sfx.BarrierBreak)
            Duck(clip.length + 0.3f, 0.25f);
    }

    int PickVoice()
    {
        for (int step = 0; step < voices.Length; step++)
        {
            int index = (nextVoice + step) % voices.Length;
            if (!voices[index].isPlaying)
            {
                nextVoice = (index + 1) % voices.Length;
                return index;
            }
        }

        // every voice is busy, so reuse the one that started the longest ago
        int oldest = 0;
        for (int index = 1; index < voices.Length; index++)
        {
            if (voiceStarted[index] < voiceStarted[oldest])
                oldest = index;
        }
        return oldest;
    }

    void Duck(float seconds, float level)
    {
        duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);
        duckTarget = level;
    }

    AudioClip LoadSfx(int index)
    {
        if (!sfxChecked[index])
        {
            sfxChecked[index] = true;
            string path = Folder + "Sfx_" + ((Sfx)index);
            sfxClips[index] = Resources.Load<AudioClip>(path);
            if (sfxClips[index] == null)
                Debug.LogWarning("Sound effect Assets/Resources/" + path + ".wav is missing; that sound is skipped.");
        }
        return sfxClips[index];
    }

    // --- music ---

    void SwitchMusic(MusicTrack track)
    {
        if (track == currentTrack)
            return;

        currentTrack = track;
        AudioClip clip = track == MusicTrack.None ? null : LoadMusic((int)track);

        // the old song fades out on the spare source while the new one fades in
        AudioSource old = musicCurrent;
        musicCurrent = musicPrevious;
        musicPrevious = old;
        musicCurrent.Stop();
        musicCurrent.clip = clip;
        fade = 0f;
        if (clip != null)
            musicCurrent.Play();
        ApplyVolumes();
    }

    AudioClip LoadMusic(int index)
    {
        if (!musicChecked[index])
        {
            musicChecked[index] = true;
            string path = Folder + "Music_" + ((MusicTrack)index);
            musicClips[index] = Resources.Load<AudioClip>(path);
            if (musicClips[index] == null)
                Debug.LogWarning("Music Assets/Resources/" + path + ".wav is missing; that scene plays without music.");
        }
        return musicClips[index];
    }
}
