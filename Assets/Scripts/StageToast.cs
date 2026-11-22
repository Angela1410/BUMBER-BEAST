using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Big centred callout for stage moments ("WAVE CLEAR!", "POWER BUMPER!"): pops in, holds, fades.
// One per scene, on its own overlay canvas under the pause menu; unscaled time, so it also plays
// while the game is slowed down.
public class StageToast : MonoBehaviour
{
    private static StageToast instance;

    private RectTransform labelRect;
    private TMP_Text label;
    private TMP_Text subLabel;
    private CanvasGroup group;
    private float shownAt = -10f;
    private float duration;

    public static void Show(string text, Color color, float seconds)
    {
        Show(text, null, color, seconds, 0.6f);
    }

    // heightFraction: where the callout sits, 0 = bottom of the screen, 1 = top.
    public static void Show(string text, string subtitle, Color color, float seconds, float heightFraction)
    {
        if (instance == null)
            instance = Create();

        instance.Play(text, subtitle, color, seconds, heightFraction);
    }

    static StageToast Create()
    {
        GameObject root = new GameObject("StageToast", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        StageToast toast = root.AddComponent<StageToast>();
        GameObject holder = new GameObject("Callout", typeof(RectTransform));
        holder.transform.SetParent(root.transform, false);
        toast.labelRect = holder.GetComponent<RectTransform>();
        toast.labelRect.sizeDelta = new Vector2(1000f, 240f);
        toast.group = holder.AddComponent<CanvasGroup>();
        toast.group.blocksRaycasts = false;
        toast.group.interactable = false;

        toast.label = CreateText(holder.transform, "Text", 104f);
        toast.label.rectTransform.anchoredPosition = new Vector2(0f, 30f);
        toast.subLabel = CreateText(holder.transform, "Subtitle", 40f);
        toast.subLabel.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        holder.SetActive(false);
        return toast;
    }

    static TMP_Text CreateText(Transform parent, string name, float size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.StyleLabel(text, size, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = true;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1000f, size * 1.3f);
        return text;
    }

    void Play(string text, string subtitle, Color color, float seconds, float heightFraction)
    {
        labelRect.gameObject.SetActive(true);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, Mathf.Clamp01(heightFraction));
        label.text = text;
        label.color = color;
        subLabel.text = subtitle ?? string.Empty;
        subLabel.color = UiTheme.Cream;
        // TMP outlines need an active object, so the style is applied after activation.
        HudVisualUtility.ApplyTextStyle(label);
        label.color = color;
        HudVisualUtility.ApplyTextStyle(subLabel);
        label.outlineWidth = 0.3f;
        shownAt = Time.unscaledTime;
        duration = Mathf.Max(0.5f, seconds);
        Animate();
    }

    void Update()
    {
        if (labelRect != null && labelRect.gameObject.activeSelf)
            Animate();
    }

    void Animate()
    {
        float t = Time.unscaledTime - shownAt;
        if (t >= duration)
        {
            labelRect.gameObject.SetActive(false);
            return;
        }

        // Pop in with a small overshoot, hold, then float up and fade out.
        float pop = t < 0.22f ? Mathf.SmoothStep(0.4f, 1.18f, t / 0.22f)
            : t < 0.34f ? Mathf.Lerp(1.18f, 1f, (t - 0.22f) / 0.12f) : 1f;
        float fadeStart = duration - 0.35f;
        float fade = t > fadeStart ? 1f - (t - fadeStart) / 0.35f : 1f;
        labelRect.localScale = new Vector3(pop, pop, 1f);
        labelRect.anchoredPosition = new Vector2(0f, t > fadeStart ? (t - fadeStart) * 120f : 0f);
        group.alpha = Mathf.Clamp01(Mathf.Min(t / 0.08f, fade));
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
