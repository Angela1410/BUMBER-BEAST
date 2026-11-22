using UnityEngine;

// "Hit-stop": the game nearly freezes for a few hundredths of a second on a big hit (a
// super-effective knockout, a boss hit, a Power Bumper), which makes impacts feel heavy. It only
// runs while the game is playing normally and gives way to anything else that changes the time
// scale (the pause menu, the stage result, the tutorial's slow motion).
public class HitStop : MonoBehaviour
{
    private const float FrozenScale = 0.05f;

    private static HitStop instance;

    private float resumeAt;
    private float savedScale = 1f;
    private bool active;

    public static void Pause(float seconds)
    {
        if (Time.timeScale <= 0.001f && (instance == null || !instance.active))
            return;   // paused or finished: no hit-stop
        if (instance == null)
            instance = new GameObject("HitStop").AddComponent<HitStop>();
        instance.Begin(seconds);
    }

    void Begin(float seconds)
    {
        if (!active)
        {
            savedScale = Time.timeScale;
            active = true;
        }
        resumeAt = Mathf.Max(resumeAt, Time.unscaledTime + Mathf.Clamp(seconds, 0.01f, 0.2f));
        Time.timeScale = FrozenScale;
    }

    void Update()
    {
        if (!active)
            return;

        // Something else set the time scale (pause, result screen): leave it alone.
        if (!Mathf.Approximately(Time.timeScale, FrozenScale))
        {
            active = false;
            return;
        }

        if (Time.unscaledTime >= resumeAt)
        {
            Time.timeScale = savedScale;
            active = false;
        }
    }

    void OnDestroy()
    {
        if (active && Mathf.Approximately(Time.timeScale, FrozenScale))
            Time.timeScale = savedScale;
        if (instance == this)
            instance = null;
    }
}
