using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a short rescue scene (you can skip it) shown the first time you beat a chapter boss: the cage
// breaks, the chapter's Beast is freed, and the next chapter gets teased. Thistle (stage 4) was
// first, Mossback (8), Voltis (12) and Ripple (16) use the same scene with their own cage colors.
// uses unscaled time because the stage pauses when it ends.
// it doesn't change progress: the rule to recruit each Beast (clear its stage, then pay in the
// shop) is in GameProgress / BeastRoster
public class ThistleRescueSequence : MonoBehaviour, IPointerClickHandler
{
    public const string SeenKey = "BumperBeast.ThistleRescueSeen";

    public static string SeenKeyFor(BeastId beast)
    {
        return beast == BeastId.Thistle ? SeenKey : "BumperBeast.RescueSeen." + beast;
    }

    private const float CageBreakTime = 1.55f;
    private const float RevealTime = 2.05f;
    private const float TeaserTime = 2.7f;
    private const float EndTime = 3.3f;
    private const int BarCount = 7;

    private Action onFinished;
    private Sprite thistleSprite;
    private BeastId beast = BeastId.Thistle;
    private Color cageColor = new Color32(74, 44, 64, 255);
    private Color spikeColor = new Color32(122, 70, 96, 255);
    private Color glowColor = new Color(0.55f, 1f, 0.45f, 1f);
    private Color rescuedColor = new Color32(176, 255, 140, 255);
    private string breakingTitle = "THE THORNS ARE BREAKING!";
    private float elapsed;
    private bool finishing;
    private float finishElapsed;

    private CanvasGroup rootGroup;
    private Image dimmer;
    private RectTransform stage;
    private RectTransform glow;
    private RectTransform thistle;
    private Image thistleImage;
    private RectTransform[] bars;
    private Vector2[] barVelocities;
    private float[] barSpins;
    private RectTransform[] sparkles;
    private TMP_Text title;
    private TMP_Text subtitle;
    private TMP_Text info;
    private RectTransform teaser;
    private Button continueButton;

    public static bool ShouldPlay()
    {
        return ShouldPlayFor(BeastId.Thistle);
    }

    public static bool ShouldPlayFor(BeastId rescued)
    {
        return PlayerPrefs.GetInt(SeenKeyFor(rescued), 0) == 0;
    }

    public static ThistleRescueSequence Play(Sprite thistleArt, Action finished)
    {
        return PlayFor(BeastId.Thistle, thistleArt, finished);
    }

    public static ThistleRescueSequence PlayFor(BeastId rescued, Sprite art, Action finished)
    {
        PlayerPrefs.SetInt(SeenKeyFor(rescued), 1);
        PlayerPrefs.Save();
        GameAudio.Play(Sfx.Unlock);

        GameObject root = new GameObject(rescued + "Rescue", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        ThistleRescueSequence sequence = root.AddComponent<ThistleRescueSequence>();
        sequence.thistleSprite = art;
        sequence.onFinished = finished;
        sequence.ConfigureFor(rescued);
        sequence.Build();
        return sequence;
    }

    // cage, glow and text for each rescued Beast (Thistle keeps her original look)
    void ConfigureFor(BeastId rescued)
    {
        beast = rescued;
        Color element = ElementVisuals.GetColor(BeastRoster.GetElement(rescued));
        glowColor = element;
        rescuedColor = Color.Lerp(element, Color.white, 0.35f);
        switch (rescued)
        {
            case BeastId.Mossback:
                cageColor = new Color32(92, 78, 66, 255);
                spikeColor = new Color32(140, 120, 100, 255);
                breakingTitle = "THE ROCKS ARE CRACKING!";
                break;
            case BeastId.Voltis:
                cageColor = new Color32(58, 56, 92, 255);
                spikeColor = new Color32(240, 210, 80, 255);
                breakingTitle = "THE STORM CAGE IS FAILING!";
                break;
            case BeastId.Ripple:
                cageColor = new Color32(40, 70, 104, 255);
                spikeColor = new Color32(150, 220, 255, 255);
                breakingTitle = "THE TIDE CAGE IS BREAKING!";
                break;
            default:
                glowColor = new Color(0.55f, 1f, 0.45f, 1f);
                rescuedColor = new Color32(176, 255, 140, 255);
                break;
        }
    }

    void Build()
    {
        rootGroup = gameObject.AddComponent<CanvasGroup>();

        dimmer = CreateImage(transform, "Dimmer", new Color(0.05f, 0.04f, 0.06f, 0f), null);
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true; // blocks the paused stage and catches taps

        // everything else goes inside the safe area
        GameObject safeObject = new GameObject("SafeArea", typeof(RectTransform));
        stage = safeObject.GetComponent<RectTransform>();
        stage.SetParent(transform, false);
        Rect safe = Screen.safeArea;
        stage.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
        stage.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
        stage.offsetMin = Vector2.zero;
        stage.offsetMax = Vector2.zero;

        glow = CreateImage(stage, "Glow", new Color(glowColor.r, glowColor.g, glowColor.b, 0f), HudVisualUtility.CircleSprite()).rectTransform;
        Place(glow, new Vector2(0f, 180f), new Vector2(520f, 520f));

        thistleImage = CreateImage(stage, BeastRoster.GetName(beast), Color.white, thistleSprite);
        if (thistleSprite == null)
        {
            thistleImage.sprite = HudVisualUtility.CircleSprite();
            thistleImage.color = BeastRoster.GetColor(beast);
        }
        thistleImage.preserveAspect = true;
        thistle = thistleImage.rectTransform;
        Place(thistle, new Vector2(0f, 180f), new Vector2(360f, 360f));

        // thorn cage: dark bramble bars with little spikes, drawn in front of Thistle
        bars = new RectTransform[BarCount];
        barVelocities = new Vector2[BarCount];
        barSpins = new float[BarCount];
        for (int index = 0; index < BarCount; index++)
        {
            float x = Mathf.Lerp(-210f, 210f, index / (float)(BarCount - 1));
            Image bar = CreateImage(stage, "ThornBar" + index, cageColor, HudVisualUtility.RoundedSprite());
            bar.type = Image.Type.Sliced;
            HudVisualUtility.AddOutline(bar.gameObject, UiTheme.Ink, new Vector2(3f, -3f));
            bars[index] = bar.rectTransform;
            Place(bars[index], new Vector2(x, 180f), new Vector2(34f, 440f));
            for (int spike = 0; spike < 4; spike++)
            {
                Image thorn = CreateImage(bars[index], "Thorn", spikeColor, HudVisualUtility.RoundedSprite());
                RectTransform thornRect = thorn.rectTransform;
                Place(thornRect, new Vector2(spike % 2 == 0 ? -16f : 16f, -150f + spike * 100f), new Vector2(26f, 26f));
                thornRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            float direction = x < 0f ? -1f : x > 0f ? 1f : (index % 2 == 0 ? -1f : 1f);
            barVelocities[index] = new Vector2(direction * UnityEngine.Random.Range(500f, 900f), UnityEngine.Random.Range(300f, 700f));
            barSpins[index] = direction * UnityEngine.Random.Range(180f, 420f);
        }

        Image cageTop = CreateImage(stage, "CageTop", cageColor, HudVisualUtility.RoundedSprite());
        cageTop.type = Image.Type.Sliced;
        Place(cageTop.rectTransform, new Vector2(0f, 405f), new Vector2(480f, 40f));
        Image cageBottom = CreateImage(stage, "CageBottom", cageColor, HudVisualUtility.RoundedSprite());
        cageBottom.type = Image.Type.Sliced;
        Place(cageBottom.rectTransform, new Vector2(0f, -45f), new Vector2(480f, 40f));
        Array.Resize(ref bars, BarCount + 2);
        Array.Resize(ref barVelocities, BarCount + 2);
        Array.Resize(ref barSpins, BarCount + 2);
        bars[BarCount] = cageTop.rectTransform;
        barVelocities[BarCount] = new Vector2(120f, 900f);
        barSpins[BarCount] = 90f;
        bars[BarCount + 1] = cageBottom.rectTransform;
        barVelocities[BarCount + 1] = new Vector2(-150f, 200f);
        barSpins[BarCount + 1] = -70f;

        sparkles = new RectTransform[18];
        for (int index = 0; index < sparkles.Length; index++)
        {
            Image sparkle = CreateImage(stage, "Sparkle" + index,
                index % 3 == 0 ? (Color)UiTheme.Gold : Color.Lerp(glowColor, Color.white, 0.2f), HudVisualUtility.CircleSprite());
            sparkles[index] = sparkle.rectTransform;
            Place(sparkles[index], new Vector2(0f, 180f), new Vector2(22f, 22f));
            sparkle.enabled = false;
        }

        title = CreateText(stage, "Title", breakingTitle, 58f, new Vector2(0f, 620f), new Vector2(980f, 110f));
        subtitle = CreateText(stage, "Subtitle", string.Empty, 32f, new Vector2(0f, -125f), new Vector2(900f, 60f));
        subtitle.color = UiTheme.Gold;
        info = CreateText(stage, "Info", string.Empty, 28f, new Vector2(0f, -195f), new Vector2(900f, 90f));
        info.color = UiTheme.MutedText;
        info.textWrappingMode = TextWrappingModes.Normal;

        BuildTeaser();

        Button skip = CreateButton(stage, "SkipButton", "SKIP  ›", new Vector2(380f, 820f), new Vector2(220f, 86f), UiButtonRole.Neutral);
        skip.onClick.AddListener(Finish);
        RectTransform skipRect = skip.GetComponent<RectTransform>();
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
        skipRect.pivot = new Vector2(1f, 1f);
        skipRect.anchoredPosition = new Vector2(-30f, -30f);

        continueButton = CreateButton(stage, "ContinueButton", "CONTINUE", new Vector2(0f, -760f), new Vector2(520f, 120f), UiButtonRole.Primary);
        continueButton.onClick.AddListener(Finish);
        continueButton.gameObject.SetActive(false);
    }

    // the chapter that opens next (the one after the boss this Beast was freed from)
    void BuildTeaser()
    {
        int rescueStage = BeastRoster.GetUnlockStage(beast);
        int nextChapter = Mathf.Clamp(ChapterData.ChapterOfStage(rescueStage) + 1, 0, ChapterData.ChapterCount - 1);
        ChapterInfo next = ChapterData.Get(nextChapter);

        Image card = CreateImage(stage, "NextChapterTeaser", UiTheme.PanelDark, HudVisualUtility.RoundedSprite());
        card.type = Image.Type.Sliced;
        HudVisualUtility.AddOutline(card.gameObject, next.accent, new Vector2(4f, -4f));
        teaser = card.rectTransform;
        Place(teaser, new Vector2(0f, -470f), new Vector2(880f, 260f));

        Image art = CreateImage(teaser, "NextChapterArt", Color.white,
            ProceduralBackdrop.GetSprite(next.theme, 0, 384, 170, 100f));
        Place(art.rectTransform, new Vector2(-235f, 0f), new Vector2(370f, 220f));

        TMP_Text heading = CreateText(teaser, "TeaserHeading", "NEXT: " + next.title, 34f, new Vector2(185f, 60f), new Vector2(460f, 60f));
        heading.color = Color.Lerp(next.accent, Color.white, 0.35f);
        string advice = next.advice;
        int sentenceEnd = advice.IndexOf('.');
        if (sentenceEnd > 0)
            advice = advice.Substring(0, sentenceEnd) + "!";
        TMP_Text body = CreateText(teaser, "TeaserBody",
            "Stages " + ChapterData.FirstStage(nextChapter) + "-" + ChapterData.LastStage(nextChapter) +
            "  ·  Boss: " + next.boss + "\n" + advice,
            24f, new Vector2(185f, -30f), new Vector2(460f, 110f));
        body.textWrappingMode = TextWrappingModes.Normal;
        teaser.gameObject.SetActive(false);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (finishing)
        {
            finishElapsed += dt;
            rootGroup.alpha = 1f - Mathf.Clamp01(finishElapsed / 0.25f);
            if (finishElapsed >= 0.25f)
            {
                Action callback = onFinished;
                onFinished = null;
                Destroy(gameObject);
                callback?.Invoke();
            }
            return;
        }

        elapsed += dt;
        Animate(elapsed, dt);
    }

    void Animate(float time, float dt)
    {
        dimmer.color = new Color(0.05f, 0.04f, 0.06f, Mathf.Lerp(0f, 0.86f, Mathf.Clamp01(time / 0.35f)));

        // the cage shakes harder until it breaks
        bool broken = time >= CageBreakTime;
        for (int index = 0; index < bars.Length; index++)
        {
            RectTransform bar = bars[index];
            if (bar == null)
                continue;

            if (!broken)
            {
                float shake = Mathf.Lerp(0f, 10f, Mathf.Clamp01((time - 0.4f) / (CageBreakTime - 0.4f)));
                bar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 40f + index) * shake * 0.4f);
                continue;
            }

            barVelocities[index] += new Vector2(0f, -2400f) * dt;
            bar.anchoredPosition += barVelocities[index] * dt;
            bar.localRotation *= Quaternion.Euler(0f, 0f, barSpins[index] * dt);
            float fade = 1f - Mathf.Clamp01((time - CageBreakTime) / 0.55f);
            foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>())
            {
                Color color = graphic.color;
                color.a = fade;
                graphic.color = color;
            }
        }

        // sparkle burst when the Beast is freed
        for (int index = 0; index < sparkles.Length; index++)
        {
            Image sparkle = sparkles[index].GetComponent<Image>();
            float local = time - CageBreakTime;
            sparkle.enabled = local >= 0f && local < 0.8f;
            if (!sparkle.enabled)
                continue;

            float angle = index / (float)sparkles.Length * Mathf.PI * 2f;
            float distance = Mathf.Lerp(40f, 330f + (index % 3) * 40f, 1f - Mathf.Pow(1f - local / 0.8f, 2f));
            sparkles[index].anchoredPosition = new Vector2(0f, 180f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            float size = Mathf.Lerp(26f, 6f, local / 0.8f);
            sparkles[index].sizeDelta = new Vector2(size, size);
        }

        // Thistle: dim and small while in the cage, then pops out and bobs happily
        float reveal = Mathf.Clamp01((time - CageBreakTime) / 0.4f);
        float pop = reveal < 1f ? 1f + Mathf.Sin(reveal * Mathf.PI) * 0.22f : 1f;
        float baseScale = Mathf.Lerp(0.82f, 1f, reveal) * pop;
        float bob = broken ? Mathf.Abs(Mathf.Sin((time - CageBreakTime) * 4f)) * 26f : 0f;
        thistle.localScale = new Vector3(baseScale * (1f + bob * 0.002f), baseScale * (1f - bob * 0.002f), 1f);
        thistle.anchoredPosition = new Vector2(0f, 180f + bob);
        Color tint = thistleSprite != null ? Color.white : (Color)BeastRoster.GetColor(beast);
        thistleImage.color = Color.Lerp(tint * new Color(0.45f, 0.45f, 0.5f, 1f), tint, reveal);

        glow.localScale = Vector3.one * (1f + Mathf.Sin(time * 3f) * 0.05f);
        glow.GetComponent<Image>().color = new Color(glowColor.r, glowColor.g, glowColor.b, Mathf.Lerp(0f, 0.28f, reveal));

        if (time >= RevealTime && subtitle.text.Length == 0)
        {
            string name = BeastRoster.GetName(beast);
            title.text = name.ToUpperInvariant() + " RESCUED!";
            title.color = rescuedColor;
            subtitle.text = ElementVisuals.GetName(BeastRoster.GetElement(beast)) + " BEAST  ·  " +
                BeastRoster.GetAbilityName(beast).ToUpperInvariant();
            bool unlocked = GameProgress.Instance != null && GameProgress.Instance.IsBeastUnlocked(beast);
            info.text = unlocked
                ? name + " is ready to join your squad. Swap Beasts at the launch pad."
                : "Recruit " + (beast == BeastId.Thistle ? "her" : name) + " in the Shop for " +
                  UiTheme.Coin(BeastRoster.GetPrice(beast)) + ", then swap Beasts at the launch pad.";
        }

        float titlePop = time >= RevealTime ? Mathf.Clamp01((time - RevealTime) / 0.25f) : 1f;
        title.rectTransform.localScale = Vector3.one * (titlePop < 1f ? 1f + Mathf.Sin(titlePop * Mathf.PI) * 0.15f : 1f);

        if (time >= TeaserTime)
        {
            if (!teaser.gameObject.activeSelf)
                teaser.gameObject.SetActive(true);
            float slide = 1f - Mathf.Pow(1f - Mathf.Clamp01((time - TeaserTime) / 0.35f), 3f);
            teaser.anchoredPosition = new Vector2(0f, Mathf.Lerp(-700f, -470f, slide));
        }

        if (time >= EndTime && !continueButton.gameObject.activeSelf)
            continueButton.gameObject.SetActive(true);
    }

    // tapping during the animation jumps to the end, the buttons finish it
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!finishing && elapsed < EndTime)
        {
            float target = EndTime + 0.4f;
            while (elapsed < target)
            {
                elapsed = Mathf.Min(target, elapsed + 0.05f);
                Animate(elapsed, 0.05f);
            }
        }
    }

    void Finish()
    {
        if (finishing)
            return;

        finishing = true;
        finishElapsed = 0f;
        if (continueButton != null)
            continueButton.interactable = false;
    }

    // --- small UI helpers ---

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text CreateText(Transform parent, string name, string text, float size, Vector2 position, Vector2 boxSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        UiTheme.StyleLabel(label, size, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = true;
        Place(label.rectTransform, position, boxSize);
        return label;
    }

    static Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UiButtonRole role)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Place(buttonObject.GetComponent<RectTransform>(), position, size);
        Button button = buttonObject.GetComponent<Button>();
        UiTheme.StyleButton(button, role, false, 36f);
        UiTheme.SetLabel(button, text);
        return button;
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
