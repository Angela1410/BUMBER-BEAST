using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Full-screen loading card for every scene change (map <-> stages, retries): the destination
// chapter's landscape, its element medallion, the stage name, a progress bar and a strategy
// tip. The scene loads in the background; the card survives the switch and fades out once the
// new scene has drawn its first frame. Runs on unscaled time (stages pause on their result).
public class LoadingScreen : MonoBehaviour
{
    public const string HubScene = "GameHub";

    private const float FadeInSeconds = 0.2f;
    private const float MinimumSeconds = 0.9f;
    private const float FadeOutSeconds = 0.3f;

    private static LoadingScreen active;

    public static bool IsLoading => active != null;

    private CanvasGroup group;
    private RectTransform medallionRing;
    private RectTransform medallion;
    private RectTransform barFill;
    private TMP_Text percentLabel;
    private float shownProgress;

    // Loads a scene behind the loading card. Ignored while another load is running.
    public static bool Load(string sceneName)
    {
        if (active != null)
            return false;
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scene '" + sceneName + "' is not in the Build Settings, so it cannot be loaded.");
            return false;
        }

        GameObject root = new GameObject("LoadingScreen", typeof(RectTransform));
        DontDestroyOnLoad(root);
        active = root.AddComponent<LoadingScreen>();
        active.Build(sceneName);
        active.StartCoroutine(active.LoadRoutine(sceneName));
        return true;
    }

    public static bool LoadStage(int stageNumber)
    {
        return Load(GameHubController.GetStageSceneName(stageNumber));
    }

    public static bool LoadHub()
    {
        return Load(HubScene);
    }

    // "stage-12_Electric" -> 12; anything else -> 0 (the map).
    static int StageFromScene(string sceneName)
    {
        if (!sceneName.StartsWith("stage-"))
            return 0;
        int start = "stage-".Length;
        int end = start;
        while (end < sceneName.Length && char.IsDigit(sceneName[end]))
            end++;
        return end > start && int.TryParse(sceneName.Substring(start, end - start), out int stage) ? stage : 0;
    }

    void Build(string sceneName)
    {
        int stage = StageFromScene(sceneName);
        int progressStage = GameProgress.Instance != null
            ? Mathf.Clamp(GameProgress.Instance.HighestUnlockedStage, 1, ChapterData.StageCount)
            : 1;
        int chapter = ChapterData.ChapterOfStage(stage > 0 ? stage : progressStage);
        ChapterInfo info = ChapterData.Get(chapter);
        bool boss = stage > 0 && ChapterData.IsBossStage(stage);

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;

        // Landscape, darkened toward the bottom so text stays readable.
        Image backdrop = Ui.Image(transform, "Backdrop", UiTheme.Background, null);
        Ui.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        Image art = Ui.Image(backdrop.transform, "Art", new Color(0.62f, 0.62f, 0.62f, 1f),
            ProceduralBackdrop.GetSprite(info.theme, stage > 0 ? (stage - 1) % ChapterData.StagesPerChapter : 0, 216, 384, 100f));
        Ui.Stretch(art.rectTransform);
        Image shade = Ui.Image(backdrop.transform, "Shade", new Color(0.03f, 0.04f, 0.03f, 0.55f), null);
        Ui.Stretch(shade.rectTransform);

        RectTransform safe = Ui.SafeArea(transform);

        // Element medallion with a spinning rim.
        medallion = Ui.Rect(safe, "Medallion", new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(300f, 300f));
        Image glow = Ui.Image(medallion, "Glow", new Color(info.accent.r, info.accent.g, info.accent.b, 0.35f), HudVisualUtility.GlowSprite());
        Ui.Fill(glow.rectTransform, -0.35f);
        Image bezel = Ui.Image(medallion, "Bezel", UiTheme.Ink, HudVisualUtility.CircleSprite());
        Ui.Fill(bezel.rectTransform, 0f);
        Image disc = Ui.Image(medallion, "Disc", ElementVisuals.GetColor(info.element), HudVisualUtility.CircleSprite());
        Ui.Fill(disc.rectTransform, 0.1f);
        Image gloss = Ui.Image(medallion, "Gloss", new Color(1f, 1f, 1f, 0.18f), HudVisualUtility.CircleSprite());
        gloss.rectTransform.anchorMin = new Vector2(0.24f, 0.54f);
        gloss.rectTransform.anchorMax = new Vector2(0.76f, 0.86f);
        gloss.rectTransform.offsetMin = gloss.rectTransform.offsetMax = Vector2.zero;
        Image glyph = Ui.Image(medallion, "Glyph", new Color32(255, 248, 230, 255), ElementVisuals.GetGlyphSprite(info.element));
        Ui.Fill(glyph.rectTransform, 0.24f);
        Image ring = Ui.Image(medallion, "Ring", info.accent, HudVisualUtility.RingSprite());
        Ui.Fill(ring.rectTransform, -0.06f);
        medallionRing = ring.rectTransform;
        // Two bright sparks riding the ring make the spin readable.
        for (int index = 0; index < 2; index++)
        {
            Image spark = Ui.Image(medallionRing, "Spark" + index, Color.white, HudVisualUtility.CircleSprite());
            RectTransform sparkRect = spark.rectTransform;
            sparkRect.anchorMin = sparkRect.anchorMax = new Vector2(0.5f, index == 0 ? 1f : 0f);
            sparkRect.sizeDelta = new Vector2(26f, 26f);
            sparkRect.anchoredPosition = new Vector2(0f, index == 0 ? -9f : 9f);
        }

        string eyebrow = stage > 0
            ? (boss ? "BOSS STAGE  ·  STAGE " + stage : "STAGE " + stage)
            : "RETURNING TO";
        TMP_Text eyebrowLabel = Ui.Text(safe, "Eyebrow", eyebrow, 40f, boss ? (Color)new Color32(255, 150, 120, 255) : (Color)UiTheme.Gold);
        Ui.Place(eyebrowLabel.rectTransform, new Vector2(0f, 88f), new Vector2(940f, 64f));
        TMP_Text title = Ui.Text(safe, "Title", stage > 0 ? ChapterData.StageName(stage).ToUpperInvariant() : "WORLD MAP",
            78f, UiTheme.Cream);
        Ui.Place(title.rectTransform, new Vector2(0f, 10f), new Vector2(980f, 110f));
        TMP_Text chapterLabel = Ui.Text(safe, "Chapter",
            "CHAPTER " + (chapter + 1) + "  ·  " + info.title + "  ·  <color=#" + ElementVisuals.GetHex(info.element) + ">" +
            ElementVisuals.GetName(info.element) + "</color>", 30f, UiTheme.MutedText);
        Ui.Place(chapterLabel.rectTransform, new Vector2(0f, -62f), new Vector2(980f, 52f));

        // Progress bar.
        RectTransform track = Ui.Rect(safe, "ProgressTrack", new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(680f, 34f));
        Image trackImage = Ui.Image(track, "Track", new Color32(16, 20, 15, 235), HudVisualUtility.RoundedSprite());
        trackImage.type = Image.Type.Sliced;
        Ui.Fill(trackImage.rectTransform, 0f);
        HudVisualUtility.AddOutline(trackImage.gameObject, UiTheme.Ink, new Vector2(3f, -3f));
        Image fill = Ui.Image(track, "Fill", info.accent, HudVisualUtility.RoundedSprite());
        fill.type = Image.Type.Sliced;
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = new Vector2(4f, 4f);
        barFill.offsetMax = new Vector2(-4f, -4f);
        percentLabel = Ui.Text(safe, "Percent", "LOADING  0%", 28f, UiTheme.MutedText);
        Ui.Place(percentLabel.rectTransform, new Vector2(0f, -248f), new Vector2(600f, 44f));

        // Tip card.
        RectTransform tipCard = Ui.Rect(safe, "TipCard", new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(940f, 230f));
        Image tipImage = UiTheme.StylePanel(tipCard.gameObject, new Color32(28, 34, 27, 238), info.accent, 3f);
        tipImage.raycastTarget = false;
        TMP_Text tipHeading = Ui.Text(tipCard, "TipHeading", "TIP", 30f, UiTheme.Gold);
        Ui.Place(tipHeading.rectTransform, new Vector2(0f, 70f), new Vector2(860f, 44f));
        string tip = stage > 0 && (stage - 1) % ChapterData.StagesPerChapter == 0 ? info.advice : ChapterData.RandomTip();
        TMP_Text tipBody = Ui.Text(tipCard, "TipBody", tip, 32f, UiTheme.Cream);
        tipBody.textWrappingMode = TextWrappingModes.Normal;
        tipBody.fontSizeMin = 26f;
        Ui.Place(tipBody.rectTransform, new Vector2(0f, -18f), new Vector2(860f, 130f));
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        float elapsed = 0f;
        while (elapsed < FadeInSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / FadeInSeconds);
            yield return null;
        }
        group.alpha = 1f;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError("Unity refused to load scene '" + sceneName + "'.");
            Finish();
            yield break;
        }
        operation.allowSceneActivation = false;

        float shown = 0f;
        while (operation.progress < 0.9f || shown < MinimumSeconds)
        {
            shown += Time.unscaledDeltaTime;
            SetProgress(Mathf.Min(operation.progress / 0.9f, shown / MinimumSeconds));
            yield return null;
        }
        SetProgress(1f);

        // A paused stage (result card) must not carry its pause into the next scene.
        Time.timeScale = 1f;
        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;
        yield return null;   // let the new scene draw its first frame under the card

        elapsed = 0f;
        group.blocksRaycasts = false;
        while (elapsed < FadeOutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(elapsed / FadeOutSeconds);
            yield return null;
        }
        Finish();
    }

    void SetProgress(float progress)
    {
        shownProgress = Mathf.Max(shownProgress, Mathf.Clamp01(progress));
        if (barFill != null)
            barFill.anchorMax = new Vector2(shownProgress, 1f);
        if (percentLabel != null)
            percentLabel.text = shownProgress >= 1f ? "READY!" : "LOADING  " + Mathf.RoundToInt(shownProgress * 100f) + "%";
    }

    void Update()
    {
        if (medallionRing != null)
            medallionRing.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 160f);
        if (medallion != null)
            medallion.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.025f);
    }

    void Finish()
    {
        if (active == this)
            active = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (active == this)
            active = null;
    }

    // Small layout helpers shared with the pause menu.
    public static class Ui
    {
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject rectObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text Text(Transform parent, string name, string text, float size, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            UiTheme.StyleLabel(label, size, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.color = color;
            return label;
        }

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Fills the parent with an inset given as a fraction of its size (negative = bigger).
        public static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // A child that covers the screen's safe area (no notches or home bars).
        public static RectTransform SafeArea(Transform parent)
        {
            GameObject safeObject = new GameObject("SafeArea", typeof(RectTransform));
            RectTransform safe = safeObject.GetComponent<RectTransform>();
            safe.SetParent(parent, false);
            Rect area = Screen.safeArea;
            float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            safe.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
            safe.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
            safe.offsetMin = Vector2.zero;
            safe.offsetMax = Vector2.zero;
            return safe;
        }
    }
}
