using UnityEngine;

// small grow + fade in animation for panels. uses unscaled time so it plays on paused result screens
[DisallowMultipleComponent]
public class UiPopIn : MonoBehaviour
{
    public float duration = 0.28f;
    public float startScale = 0.86f;

    private CanvasGroup group;
    private float elapsed;

    void OnEnable()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        elapsed = 0f;
        transform.localScale = Vector3.one * startScale;
        group.alpha = 0f;
    }

    void Update()
    {
        if (elapsed >= duration)
            return;

        elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
        float t = elapsed / duration;
        // ease-out-back for a little overshoot
        float c = 1.7f;
        float eased = 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, eased);
        group.alpha = Mathf.Clamp01(t * 2f);
    }
}
