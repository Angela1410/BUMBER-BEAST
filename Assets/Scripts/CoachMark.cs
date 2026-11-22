using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tutorial overlay: a speech bubble, a pointing hand and a pulsing ring that follow a target on
// screen (a UI element or something in the world), and a looping "drag back" gesture. It never
// blocks touches (the player plays through the tutorial) and runs on unscaled time, so it keeps
// moving while the tutorial freezes or slows the game.
public class CoachMark : MonoBehaviour
{
    private static readonly Vector2 FingerTip = new Vector2(0.475f, 0.91f);
    private const float HandSize = 150f;

    private RectTransform canvasRect;
    private Canvas ownCanvas;
    private RectTransform bubble;
    private CanvasGroup bubbleGroup;
    private TMP_Text bubbleText;
    private RectTransform hand;
    private Image handImage;
    private RectTransform ring;
    private Image ringImage;

    private RectTransform targetRect;
    private Transform targetTransform;
    private Vector3 targetWorld;
    private bool hasWorldPoint;
    private bool pointing;

    private bool dragging;
    private Vector3 dragFrom;
    private Vector3 dragTo;

    private float bubbleShownAt;
    private float pointShownAt;

    public static CoachMark Create()
    {
        GameObject root = new GameObject("TutorialCoach", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 280;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        CoachMark coach = root.AddComponent<CoachMark>();
        coach.canvasRect = root.GetComponent<RectTransform>();
        coach.ownCanvas = canvas;
        coach.Build();
        return coach;
    }

    void Build()
    {
        // Pulsing ring around the target.
        ringImage = CreateImage(transform, "Ring", UiTheme.Gold, HudVisualUtility.RingSprite());
        ring = ringImage.rectTransform;
        ring.sizeDelta = new Vector2(170f, 170f);
        ring.gameObject.SetActive(false);

        // The hand: cream with a dark outline so it reads on any background.
        handImage = CreateImage(transform, "Hand", UiTheme.Cream, UiIcons.Get(UiIcon.Hand));
        hand = handImage.rectTransform;
        hand.pivot = FingerTip;
        hand.sizeDelta = new Vector2(HandSize, HandSize);
        Outline outline = handImage.gameObject.AddComponent<Outline>();
        outline.effectColor = UiTheme.Ink;
        outline.effectDistance = new Vector2(4f, -4f);
        Shadow shadow = handImage.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(6f, -10f);
        hand.gameObject.SetActive(false);

        // Speech bubble.
        GameObject bubbleObject = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bubble = bubbleObject.GetComponent<RectTransform>();
        bubble.SetParent(transform, false);
        bubble.sizeDelta = new Vector2(940f, 220f);
        Image back = UiTheme.StylePanel(bubbleObject, new Color32(26, 32, 26, 228), UiTheme.Gold, 4f);
        back.raycastTarget = false;
        bubbleGroup = bubbleObject.AddComponent<CanvasGroup>();
        bubbleGroup.blocksRaycasts = false;

        Image badge = CreateImage(bubble, "Badge", UiTheme.Orange, HudVisualUtility.CircleSprite());
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
        badge.rectTransform.anchoredPosition = new Vector2(46f, -8f);
        badge.rectTransform.sizeDelta = new Vector2(76f, 76f);
        Outline badgeOutline = badge.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = UiTheme.Ink;
        badgeOutline.effectDistance = new Vector2(3f, -3f);
        Image paw = CreateImage(badge.rectTransform, "Paw", UiTheme.Cream, UiIcons.Get(UiIcon.Paw));
        paw.rectTransform.anchorMin = new Vector2(0.2f, 0.2f);
        paw.rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
        paw.rectTransform.offsetMin = paw.rectTransform.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(bubble, false);
        bubbleText = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.StyleLabel(bubbleText, 34f, TextAlignmentOptions.Center);
        bubbleText.fontSizeMin = 24f;
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        bubbleText.richText = true;
        RectTransform textRect = bubbleText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(70f, 18f);
        textRect.offsetMax = new Vector2(-34f, -18f);
        bubble.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ API

    // heightFraction: 0 = bottom of the screen, 1 = top.
    // heightFraction 0.8 or more = just under the wave bar at the top of the safe area (the field
    // stays clear); lower values place the bubble's centre at that fraction of the screen.
    public void Say(string text, float heightFraction)
    {
        bubble.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;
        if (heightFraction >= 0.8f && Screen.height > 0)
        {
            float safeTop = (Screen.height - Screen.safeArea.yMax) / Screen.height * canvasHeight;
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 1f);
            bubble.pivot = new Vector2(0.5f, 1f);
            bubble.anchoredPosition = new Vector2(0f, -(safeTop + 185f));
        }
        else
        {
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, Mathf.Clamp01(heightFraction));
            bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.anchoredPosition = Vector2.zero;
        }
        bubbleText.text = text;
        // Sized to the message, and never wider than the screen.
        int lines = 1;
        foreach (char c in text)
        {
            if (c == '\n')
                lines++;
        }
        float width = Mathf.Clamp(canvasWidth - 48f, 560f, 900f);
        bubble.sizeDelta = new Vector2(width, Mathf.Max(130f, 56f + lines * 50f));
        HudVisualUtility.ApplyTextStyle(bubbleText);
        bubbleText.color = UiTheme.Cream;
        bubbleShownAt = Time.unscaledTime;
        GameAudio.Play(Sfx.Tutorial);
    }

    public void HideBubble()
    {
        bubble.gameObject.SetActive(false);
    }

    public void PointAt(RectTransform target)
    {
        ClearTarget();
        targetRect = target;
        StartPointing();
    }

    public void PointAtWorld(Transform target)
    {
        ClearTarget();
        targetTransform = target;
        StartPointing();
    }

    public void PointAtWorld(Vector3 position)
    {
        ClearTarget();
        targetWorld = position;
        hasWorldPoint = true;
        StartPointing();
    }

    // The hand pulls back from one world point to another, over and over.
    public void DragGesture(Vector3 worldFrom, Vector3 worldTo)
    {
        ClearTarget();
        dragging = true;
        dragFrom = worldFrom;
        dragTo = worldTo;
        hand.gameObject.SetActive(true);
        ring.gameObject.SetActive(true);
        pointShownAt = Time.unscaledTime;
    }

    public void HidePointer()
    {
        ClearTarget();
        hand.gameObject.SetActive(false);
        ring.gameObject.SetActive(false);
    }

    public void HideAll()
    {
        HideBubble();
        HidePointer();
    }

    void ClearTarget()
    {
        targetRect = null;
        targetTransform = null;
        hasWorldPoint = false;
        dragging = false;
        pointing = false;
    }

    void StartPointing()
    {
        pointing = true;
        hand.gameObject.SetActive(true);
        ring.gameObject.SetActive(true);
        pointShownAt = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ animation

    void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (bubble.gameObject.activeSelf)
        {
            float t = now - bubbleShownAt;
            float pop = t < 0.18f ? Mathf.Lerp(0.85f, 1.04f, t / 0.18f) : t < 0.28f ? Mathf.Lerp(1.04f, 1f, (t - 0.18f) / 0.1f) : 1f;
            bubble.localScale = new Vector3(pop, pop, 1f);
            bubbleGroup.alpha = Mathf.Clamp01(t / 0.12f);
        }

        if (dragging)
        {
            // 0-0.25 press, 0.25-0.85 pull back, 0.85-1.15 hold, then release and repeat.
            float cycle = (now - pointShownAt) % 1.6f;
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.85f, cycle));
            Vector2 from, to;
            if (ToCanvas(dragFrom, out from) && ToCanvas(dragTo, out to))
            {
                Vector2 position = Vector2.Lerp(from, to, pull);
                hand.anchoredPosition = position;
                float press = cycle < 0.25f ? Mathf.Lerp(1.12f, 0.95f, cycle / 0.25f) : cycle > 1.15f ? 1.1f : 0.95f;
                hand.localScale = new Vector3(press, press, 1f);
                ring.anchoredPosition = from;
                float ringPulse = 0.8f + 0.25f * Mathf.Sin(now * 6f);
                ring.localScale = new Vector3(ringPulse, ringPulse, 1f);
                SetAlpha(ringImage, 0.55f);
                SetAlpha(handImage, cycle > 1.3f ? Mathf.Lerp(1f, 0f, (cycle - 1.3f) / 0.3f) : 1f);
            }
            return;
        }

        if (!pointing)
            return;

        Vector2 point;
        if (!TargetPoint(out point))
        {
            hand.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
            return;
        }
        hand.gameObject.SetActive(true);
        ring.gameObject.SetActive(true);
        float bob = Mathf.Abs(Mathf.Sin((now - pointShownAt) * 4.5f)) * 26f;
        hand.anchoredPosition = point + new Vector2(10f, -18f - bob);
        hand.localScale = Vector3.one;
        SetAlpha(handImage, 1f);
        ring.anchoredPosition = point;
        float pulse = 0.85f + 0.2f * Mathf.Sin(now * 6f);
        ring.localScale = new Vector3(pulse, pulse, 1f);
        SetAlpha(ringImage, 0.75f);
    }

    bool TargetPoint(out Vector2 point)
    {
        point = Vector2.zero;
        if (targetRect != null)
        {
            if (!targetRect.gameObject.activeInHierarchy)
                return false;
            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, targetRect.TransformPoint(targetRect.rect.center));
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, OwnCamera(), out point);
        }
        if (targetTransform != null)
            return ToCanvas(targetTransform.position, out point);
        if (hasWorldPoint)
            return ToCanvas(targetWorld, out point);
        return false;
    }

    bool ToCanvas(Vector3 world, out Vector2 point)
    {
        point = Vector2.zero;
        Camera camera = Camera.main;
        if (camera == null)
            return false;
        Vector3 screen = camera.WorldToScreenPoint(world);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, OwnCamera(), out point);
    }

    // The coach canvas's camera: none for an overlay canvas, its world camera otherwise.
    Camera OwnCamera()
    {
        return ownCanvas != null && ownCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? ownCanvas.worldCamera : null;
    }

    static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
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
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        return image;
    }
}
