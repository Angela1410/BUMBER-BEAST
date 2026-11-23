using UnityEngine;
using UnityEngine.UI;

// shows our hand-made title screen (Assets/Resources/UI/TitleScreen.png) once each time the app
// opens, on top of the map, until the player taps PLAY. the art is never stretched: it's scaled to
// the screen width and stuck to the bottom of the safe area, and any space left is filled with the
// art's own sky and ground colors. there's nothing to load when PLAY is pressed (the map is
// already built underneath), so there's no progress bar. the painted player and gear buttons
// under PLAY open the PLAYER PROFILE and SETTINGS cards
public class TitleScreen : MonoBehaviour
{
    private const string ArtResourcePath = "UI/TitleScreen";
    private const float ArtAspect = 1080f / 1920f;
    // button areas in the art, measured from the bottom left as 0-1: the touch area (a bit bigger so
    // it's easier to hit) and the painted button itself (gets darker when pressed)
    private static readonly Rect PlayArea = new Rect(0.30f, 0.12f, 0.42f, 0.125f);
    private static readonly Rect PlayPainted = new Rect(0.317f, 0.133f, 0.39f, 0.104f);
    private static readonly Rect AccountArea = new Rect(0.30f, 0.02f, 0.2f, 0.11f);
    private static readonly Rect AccountPainted = new Rect(0.326f, 0.037f, 0.145f, 0.079f);
    private static readonly Rect SettingsArea = new Rect(0.52f, 0.02f, 0.2f, 0.11f);
    private static readonly Rect SettingsPainted = new Rect(0.546f, 0.037f, 0.146f, 0.079f);
    private static readonly Color32 SkyColor = new Color32(14, 120, 221, 255);
    private static readonly Color32 GroundColor = new Color32(73, 68, 44, 255);

    private static bool shownThisSession;

    // sent when PLAY is tapped (then the map offers the daily gift)
    public static event System.Action Dismissed;

    private CanvasGroup group;
    private RectTransform artRect;
    private RectTransform topFill;
    private RectTransform bottomFill;
    private float fadeElapsed = -1f;
    private int lastWidth;
    private int lastHeight;
    private Rect lastSafeArea;

    public static bool ShowIfNeeded()
    {
        if (shownThisSession)
            return false;

        Sprite art = LoadArt();
        if (art == null)
            return false;

        shownThisSession = true;
        GameObject root = new GameObject("TitleScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;
        TitleScreen screen = root.AddComponent<TitleScreen>();
        screen.Build(art);
        return true;
    }

    static Sprite LoadArt()
    {
        Sprite sprite = Resources.Load<Sprite>(ArtResourcePath);
        if (sprite != null)
            return sprite;

        // the texture is imported as a plain texture: wrap it without resampling
        Texture2D texture = Resources.Load<Texture2D>(ArtResourcePath);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    void Build(Sprite art)
    {
        group = gameObject.AddComponent<CanvasGroup>();

        Image sky = CreateImage(transform, "Sky", SkyColor);
        Stretch(sky.rectTransform);
        sky.raycastTarget = true; // blocks the map underneath while the title is showing

        bottomFill = CreateImage(transform, "GroundFill", GroundColor).rectTransform;
        topFill = CreateImage(transform, "SkyFill", SkyColor).rectTransform;

        Image artImage = CreateImage(transform, "Art", Color.white);
        artImage.sprite = art;
        artImage.preserveAspect = true;
        artRect = artImage.rectTransform;

        // invisible buttons over the painted PLAY, PLAYER and SETTINGS buttons
        AddPaintedButton("PlayButton", PlayArea, PlayPainted, Dismiss);
        AddPaintedButton("AccountButton", AccountArea, AccountPainted, OpenProfile);
        AddPaintedButton("SettingsButton", SettingsArea, SettingsPainted, OpenSettings);

        Layout();
    }

    // a touch area over a button painted in the title art. while you press it, a dark overlay in the
    // shape of the painted button shows it was hit (the art itself never changes)
    void AddPaintedButton(string name, Rect touchArea, Rect painted, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(artRect, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetAnchors(rect, touchArea);
        Image hitArea = buttonObject.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        Image press = CreateImage(rect, "Press", new Color(0f, 0f, 0f, 0.32f));
        press.sprite = HudVisualUtility.RoundedSprite();
        press.type = Image.Type.Sliced;
        // the painted rectangle, measured inside the touch area
        Rect local = new Rect((painted.x - touchArea.x) / touchArea.width, (painted.y - touchArea.y) / touchArea.height,
            painted.width / touchArea.width, painted.height / touchArea.height);
        SetAnchors(press.rectTransform, local);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = press;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.35f);
        colors.pressedColor = Color.white;
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        buttonObject.AddComponent<UiButtonFeedback>().pressedScale = 0.97f;
    }

    static void SetAnchors(RectTransform rect, Rect area)
    {
        rect.anchorMin = area.min;
        rect.anchorMax = area.max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void OpenSettings()
    {
        if (fadeElapsed < 0f)
            GameMenus.OpenSettings(true, HubPortraits());
    }

    void OpenProfile()
    {
        if (fadeElapsed < 0f)
            GameMenus.OpenProfile(HubPortraits());
    }

    // the map's Beast portraits (painted ones where we have them), for the profile picture
    static System.Func<BeastId, Sprite> HubPortraits()
    {
        GameHubController hub = FindAnyObjectByType<GameHubController>();
        if (hub == null)
            return null;
        return hub.GetBeastPortrait;
    }

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
            Layout();

        if (fadeElapsed < 0f)
            return;

        fadeElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(fadeElapsed / 0.3f);
        group.alpha = 1f - t;
        artRect.localScale = Vector3.one * (1f + t * 0.04f);
        if (t >= 1f)
            Destroy(gameObject);
    }

    void Layout()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        if (lastWidth <= 0 || lastHeight <= 0)
            return;

        // canvas units are the same as screen pixels (no scaler), so we lay it out in pixels
        float safeBottom = Mathf.Max(0f, lastSafeArea.yMin);
        float safeTop = Mathf.Max(0f, lastHeight - lastSafeArea.yMax);
        float availableHeight = lastHeight - safeBottom - safeTop;

        // fit the full width, but if that's too tall for the safe area (wide screens), fit the height instead
        float artWidth = lastWidth;
        float artHeight = artWidth / ArtAspect;
        if (artHeight > availableHeight)
        {
            artHeight = availableHeight;
            artWidth = artHeight * ArtAspect;
        }

        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0f);
        artRect.pivot = new Vector2(0.5f, 0f);
        artRect.sizeDelta = new Vector2(artWidth, artHeight);
        artRect.anchoredPosition = new Vector2(0f, safeBottom);

        // ground color below the art (safe area gap), sky color above it
        bottomFill.anchorMin = Vector2.zero;
        bottomFill.anchorMax = new Vector2(1f, 0f);
        bottomFill.pivot = new Vector2(0.5f, 0f);
        bottomFill.anchoredPosition = Vector2.zero;
        bottomFill.sizeDelta = new Vector2(0f, safeBottom + 1f);

        topFill.anchorMin = new Vector2(0f, 0f);
        topFill.anchorMax = new Vector2(1f, 1f);
        topFill.offsetMin = new Vector2(0f, safeBottom + artHeight - 1f);
        topFill.offsetMax = Vector2.zero;
    }

    void Dismiss()
    {
        if (fadeElapsed >= 0f)
            return;

        GameAudio.Play(Sfx.Play);
        fadeElapsed = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        if (Dismissed != null)
            Dismissed();
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
