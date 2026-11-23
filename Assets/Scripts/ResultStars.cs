using UnityEngine;
using UnityEngine.UI;

// the row of stars on the stage complete card: three dim slots, then the stars you earned pop in one
// by one with a rising chime (unscaled time, the stage is frozen behind the card). stars you got for
// the first time get a small "NEW" tag
public class ResultStars : MonoBehaviour
{
    private const float StarSize = 96f;
    private const float Spacing = 118f;
    private const float FirstDelay = 0.35f;
    private const float StepDelay = 0.32f;

    private static readonly Color32 EmptyStar = new Color32(28, 33, 27, 255);

    private Image[] stars;
    private Image[] glows;
    private GameObject[] newTags;
    private int earned;
    private int previousBest;
    private float startedAt;
    private int shown;

    public static ResultStars Show(RectTransform card, Vector2 position, int earnedStars, int newStars)
    {
        Transform old = card.Find("ResultStars");
        if (old != null)
            Destroy(old.gameObject);

        GameObject root = new GameObject("ResultStars", typeof(RectTransform));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(card, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(Spacing * 3f, StarSize + 30f);

        ResultStars view = root.AddComponent<ResultStars>();
        view.earned = Mathf.Clamp(earnedStars, 0, 3);
        view.previousBest = Mathf.Clamp(earnedStars - Mathf.Max(0, newStars), 0, 3);
        view.Build(rect);
        view.startedAt = Time.unscaledTime;
        return view;
    }

    void Build(RectTransform root)
    {
        stars = new Image[3];
        glows = new Image[3];
        newTags = new GameObject[3];
        Sprite star = UiIcons.Get(UiIcon.Star);
        for (int index = 0; index < 3; index++)
        {
            float x = (index - 1) * Spacing;
            // the middle star is a bit higher and bigger, like a podium
            float y = index == 1 ? 14f : 0f;
            float size = index == 1 ? StarSize * 1.12f : StarSize;

            Image glow = CreateImage(root, "Glow" + index, new Color(1f, 0.82f, 0.3f, 0f), HudVisualUtility.GlowSprite());
            Place(glow.rectTransform, new Vector2(x, y), size * 1.9f);
            glows[index] = glow;

            Image back = CreateImage(root, "Slot" + index, EmptyStar, star);
            Place(back.rectTransform, new Vector2(x, y), size);
            Outline outline = back.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Ink;
            outline.effectDistance = new Vector2(3f, -3f);

            Image fill = CreateImage(root, "Star" + index, UiTheme.Gold, star);
            Place(fill.rectTransform, new Vector2(x, y), size);
            fill.rectTransform.localScale = Vector3.zero;
            Shadow shadow = fill.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(120, 60, 10, 200);
            shadow.effectDistance = new Vector2(0f, -4f);
            stars[index] = fill;

            GameObject tag = new GameObject("New" + index, typeof(RectTransform));
            tag.transform.SetParent(root, false);
            TMPro.TMP_Text label = tag.AddComponent<TMPro.TextMeshProUGUI>();
            UiTheme.StyleLabel(label, 24f, TMPro.TextAlignmentOptions.Center);
            label.text = "NEW";
            label.color = new Color32(150, 236, 110, 255);
            Place(label.rectTransform, new Vector2(x, y - size * 0.62f), 90f);
            label.rectTransform.sizeDelta = new Vector2(90f, 30f);
            tag.SetActive(false);
            newTags[index] = tag;
        }
    }

    void Update()
    {
        float t = Time.unscaledTime - startedAt;
        while (shown < earned && t >= FirstDelay + shown * StepDelay)
        {
            GameAudio.PlayPitched(Sfx.Star, 0.9f, 1f + shown * 0.12f);
            if (shown >= previousBest)
                newTags[shown].SetActive(true);
            shown++;
        }

        for (int index = 0; index < 3; index++)
        {
            float local = t - (FirstDelay + index * StepDelay);
            if (index >= earned || local < 0f)
                continue;
            // pop a bit too big, then settle, then a soft shimmer on the glow
            float pop = local < 0.18f ? Mathf.Lerp(0f, 1.3f, local / 0.18f)
                : local < 0.3f ? Mathf.Lerp(1.3f, 1f, (local - 0.18f) / 0.12f) : 1f;
            stars[index].rectTransform.localScale = Vector3.one * pop;
            Color glow = glows[index].color;
            glow.a = local < 0.3f ? local / 0.3f * 0.8f : 0.45f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f + index);
            glows[index].color = glow;
        }
    }

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    static void Place(RectTransform rect, Vector2 position, float size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
    }
}
