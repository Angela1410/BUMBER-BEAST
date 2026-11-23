using UnityEngine;

// a gentle idle animation for a Beast that is just waiting: a slow bob up and down plus a little
// "breathing" squash and stretch. every Beast has its own speed so they never move together.
// the launch pad uses Sample() (LaunchController), and in the Beasts tab this component sits on each
// portrait picture. how big and how fast is in GameTuning (Idle animation)
public class BeastIdleMotion : MonoBehaviour
{
    public BeastId beast;

    private RectTransform rect;
    private Vector2 basePosition;
    private Vector3 baseScale;
    private bool hasBase;

    // where the Beast is in its bob right now: an offset up (as a part of its height, 0 = resting)
    // and its scale (x gets a bit wider when y gets shorter, like breathing)
    public static void Sample(BeastId beast, float time, out float lift, out Vector2 scale)
    {
        GameTuning tuning = GameTuning.Get;
        float speed = GameTuning.Pick(tuning.idleSpeed, (int)beast, 1f);
        // each Beast starts at a different point too, so two Beasts side by side never match
        float phase = (int)beast * 1.37f;
        float wave = Mathf.Sin((time * Mathf.Max(0f, tuning.idleBobsPerSecond) * speed) * Mathf.PI * 2f + phase);
        lift = Mathf.Max(0f, tuning.idleBobHeight) * (0.5f + 0.5f * wave);
        float breath = Mathf.Max(0f, tuning.idleBreath) * wave;
        scale = new Vector2(1f - breath * 0.6f, 1f + breath);
    }

    void OnEnable()
    {
        rect = transform as RectTransform;
        if (rect != null)
        {
            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;
            hasBase = true;
        }
    }

    void OnDisable()
    {
        if (hasBase && rect != null)
        {
            rect.anchoredPosition = basePosition;
            rect.localScale = baseScale;
        }
        hasBase = false;
    }

    // menus run while the game is paused too, so this uses real time
    void LateUpdate()
    {
        if (!hasBase || rect == null)
            return;
        Sample(beast, Time.unscaledTime, out float lift, out Vector2 scale);
        float height = rect.rect.height;
        rect.anchoredPosition = basePosition + Vector2.up * (lift * height);
        rect.localScale = new Vector3(baseScale.x * scale.x, baseScale.y * scale.y, baseScale.z);
    }
}
