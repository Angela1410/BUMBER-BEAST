using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ui = LoadingScreen.Ui;

// In-stage pause: a round button in the top-left corner opens a card with the stage, the
// element chart (who beats whom) and RESUME / RESTART / MAP. Lives on its own overlay canvas
// so it never fights the stage HUD; hidden once the stage is won or lost.
public class PauseMenu : MonoBehaviour
{
    private const float ButtonSize = 112f;
    private static readonly ElementType[] ChartOrder =
    {
        ElementType.Fire, ElementType.Grass, ElementType.Earth, ElementType.Electric, ElementType.Water
    };

    private StageManager stage;
    private RectTransform safe;
    private GameObject pauseButton;
    private GameObject overlay;
    private bool available = true;
    private Rect lastSafeArea;
    private int lastWidth;
    private int lastHeight;

    public bool IsPaused { get; private set; }

    public static PauseMenu Create(StageManager owner)
    {
        GameObject root = new GameObject("PauseMenu", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        PauseMenu menu = root.AddComponent<PauseMenu>();
        menu.stage = owner;
        menu.BuildButton();
        return menu;
    }

    void BuildButton()
    {
        safe = Ui.SafeArea(transform);
        lastSafeArea = Screen.safeArea;
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        pauseButton = new GameObject("PauseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = pauseButton.GetComponent<RectTransform>();
        rect.SetParent(safe, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(34f, -34f);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        Image bezel = pauseButton.GetComponent<Image>();
        bezel.sprite = HudVisualUtility.CircleSprite();
        bezel.color = UiTheme.Ink;
        Shadow shadow = UiTheme.GetOrAddEffect<Shadow>(pauseButton);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -6f);
        Image rim = Ui.Image(rect, "Rim", new Color32(255, 244, 214, 220), HudVisualUtility.RingSprite());
        Ui.Fill(rim.rectTransform, 0.05f);
        Image face = Ui.Image(rect, "Face", new Color32(52, 66, 50, 255), HudVisualUtility.CircleSprite());
        Ui.Fill(face.rectTransform, 0.11f);
        Image gloss = Ui.Image(rect, "Gloss", new Color(1f, 1f, 1f, 0.14f), HudVisualUtility.CircleSprite());
        gloss.rectTransform.anchorMin = new Vector2(0.25f, 0.55f);
        gloss.rectTransform.anchorMax = new Vector2(0.75f, 0.85f);
        gloss.rectTransform.offsetMin = gloss.rectTransform.offsetMax = Vector2.zero;
        for (int side = -1; side <= 1; side += 2)
        {
            Image bar = Ui.Image(rect, "Bar", UiTheme.Cream, HudVisualUtility.RoundedSprite());
            bar.type = Image.Type.Sliced;
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.sizeDelta = new Vector2(ButtonSize * 0.14f, ButtonSize * 0.42f);
            barRect.anchoredPosition = new Vector2(side * ButtonSize * 0.12f, 0f);
            barRect.localScale = Vector3.one;
            // The rounded sprite's corner radius is larger than a thin bar; shrink it to fit.
            bar.pixelsPerUnitMultiplier = 4f;
        }

        Button button = pauseButton.GetComponent<Button>();
        button.targetGraphic = bezel;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(Pause);
        pauseButton.AddComponent<UiButtonFeedback>();
    }

    void Update()
    {
        if (safe != null && (Screen.safeArea != lastSafeArea || Screen.width != lastWidth || Screen.height != lastHeight))
        {
            Rect area = Screen.safeArea;
            float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            safe.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
            safe.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
            lastSafeArea = area;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }
    }

    // Called by StageManager when the stage ends: no pausing over the result card.
    public void SetAvailable(bool value)
    {
        available = value;
        if (pauseButton != null)
            pauseButton.SetActive(value);
        if (!value && IsPaused)
            CloseOverlay();
    }

    public void Pause()
    {
        if (!available || IsPaused || LoadingScreen.IsLoading)
            return;

        IsPaused = true;
        Time.timeScale = 0f;
        BuildOverlay();
    }

    public void Resume()
    {
        if (!IsPaused)
            return;
        CloseOverlay();
        // Back to the stage speed (normal, or a tutorial lesson's slow motion), never a leftover value.
        Time.timeScale = GameTime.BaseScale;
    }

    void CloseOverlay()
    {
        IsPaused = false;
        if (overlay != null)
            Destroy(overlay);
        overlay = null;
    }

    void Restart()
    {
        CloseOverlay();
        Time.timeScale = 1f;
        if (stage != null)
            stage.RetryStage();
    }

    void ReturnToMap()
    {
        CloseOverlay();
        Time.timeScale = 1f;
        if (stage != null)
            stage.ReturnToMap();
    }

    void BuildOverlay()
    {
        overlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(transform, false);
        Ui.Stretch(overlayRect);
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0.03f, 0.04f, 0.03f, 0.72f);
        dim.raycastTarget = true;

        int stageNumber = stage != null ? stage.stageNumber : 1;
        ChapterInfo chapter = ChapterData.Get(ChapterData.ChapterOfStage(stageNumber));

        RectTransform overlaySafe = Ui.SafeArea(overlayRect);
        RectTransform card = Ui.Rect(overlaySafe, "PauseCard", new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(880f, 1120f));
        UiTheme.StylePanel(card.gameObject, UiTheme.Panel, chapter.accent, 5f).raycastTarget = true;
        card.gameObject.AddComponent<UiPopIn>();

        TMP_Text title = Ui.Text(card, "Title", "PAUSED", 76f, UiTheme.Gold);
        Ui.Place(title.rectTransform, new Vector2(0f, 470f), new Vector2(800f, 100f));
        // Sound and screen-shake settings (no profile here: its reset would end the run).
        Button settings = GameMenus.RoundButton(card, "Settings", UiIcon.Gear, new Vector2(358f, 478f), 100f, new Color32(52, 66, 50, 255));
        settings.onClick.AddListener(() => GameMenus.OpenSettings(false));
        TMP_Text stageLine = Ui.Text(card, "Stage",
            "STAGE " + stageNumber + "  ·  " + ChapterData.StageName(stageNumber).ToUpperInvariant(), 40f, UiTheme.Cream);
        Ui.Place(stageLine.rectTransform, new Vector2(0f, 385f), new Vector2(800f, 60f));
        TMP_Text chapterLine = Ui.Text(card, "Chapter", chapter.title, 30f, UiTheme.MutedText);
        Ui.Place(chapterLine.rectTransform, new Vector2(0f, 332f), new Vector2(800f, 46f));

        BuildElementChart(card, new Vector2(0f, 175f));

        TMP_Text advice = Ui.Text(card, "Advice", chapter.advice, 30f, new Color32(214, 236, 190, 255));
        advice.textWrappingMode = TextWrappingModes.Normal;
        advice.fontSizeMin = 24f;
        Ui.Place(advice.rectTransform, new Vector2(0f, 20f), new Vector2(780f, 110f));

        MakeButton(card, "Resume", "RESUME", new Vector2(0f, -150f), new Vector2(600f, 128f), UiButtonRole.Primary, Resume, 46f);
        MakeButton(card, "Restart", "RESTART STAGE", new Vector2(0f, -292f), new Vector2(600f, 112f), UiButtonRole.Secondary, Restart, 38f);
        MakeButton(card, "Map", "WORLD MAP", new Vector2(0f, -422f), new Vector2(600f, 112f), UiButtonRole.Neutral, ReturnToMap, 38f);
    }

    // Fire > Grass > Earth > Electric > Water > (Fire): each icon beats the one after it.
    void BuildElementChart(RectTransform card, Vector2 center)
    {
        RectTransform panel = Ui.Rect(card, "ElementChart", new Vector2(0.5f, 0.5f), center, new Vector2(800f, 210f));
        Image panelImage = Ui.Image(panel, "Back", new Color32(30, 37, 30, 255), HudVisualUtility.RoundedSprite());
        panelImage.type = Image.Type.Sliced;
        Ui.Stretch(panelImage.rectTransform);

        TMP_Text heading = Ui.Text(panel, "Heading", "EACH ELEMENT BEATS THE NEXT", 26f, UiTheme.MutedText);
        Ui.Place(heading.rectTransform, new Vector2(0f, 72f), new Vector2(760f, 40f));

        const float iconSize = 96f;
        const float step = 150f;
        float start = -step * 2f;
        for (int index = 0; index < ChartOrder.Length; index++)
        {
            ElementType element = ChartOrder[index];
            float x = start + index * step;
            Image icon = Ui.Image(panel, "Icon" + element, Color.white, ElementVisuals.GetIcon(element));
            Ui.Place(icon.rectTransform, new Vector2(x, 0f), new Vector2(iconSize, iconSize));
            TMP_Text name = Ui.Text(panel, "Name" + element, ElementVisuals.GetName(element), 22f,
                Color.Lerp(ElementVisuals.GetColor(element), Color.white, 0.35f));
            Ui.Place(name.rectTransform, new Vector2(x, -70f), new Vector2(146f, 34f));
            if (index < ChartOrder.Length - 1)
            {
                TMP_Text arrow = Ui.Text(panel, "Arrow" + index, ">", 40f, UiTheme.Gold);
                Ui.Place(arrow.rectTransform, new Vector2(x + step * 0.5f, 2f), new Vector2(50f, 60f));
            }
        }
    }

    static void MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size,
        UiButtonRole role, UnityEngine.Events.UnityAction onClick, float fontSize)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Ui.Place(buttonObject.GetComponent<RectTransform>(), position, size);
        Button button = buttonObject.GetComponent<Button>();
        UiTheme.StyleButton(button, role, false, fontSize);
        UiTheme.SetLabel(button, label);
        button.onClick.AddListener(onClick);
    }

    void OnDestroy()
    {
        // Leaving the stage while paused must not freeze the next scene.
        if (IsPaused)
            Time.timeScale = 1f;
    }
}
