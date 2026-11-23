using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the POWER meter (bottom left, above the barrier). every hit on a green bumper fills it a bit.
// when it's full, a green bumper turns into the red POWER BUMPER: bounce into it while enemies are
// on the field to unleash your Beast's power. then the meter starts again from empty.
// while a Power Bumper is waiting on the field the meter stays full and says READY.
// how many hits it needs is in GameTuning (Power meter)
public class PowerMeter : MonoBehaviour
{
    public static PowerMeter Current { get; private set; }

    private int hits;
    private float shownFill;
    private float bumpUntil;
    private float retryAt;
    private Bumper lastHit;

    private RectTransform canvasRect;
    private RectTransform barrierRect;
    private RectTransform panelRect;
    private RectTransform trackRect;
    private Image fill;
    private TMP_Text label;
    private TMP_Text value;
    private Outline panelOutline;
    private Vector2 trackSize;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Rect lastSafeArea;
    private float lastPanelTop = -1f;

    private static readonly Color32 FillColor = new Color32(255, 128, 48, 255);
    private static readonly Color32 ReadyColor = new Color32(255, 204, 66, 255);

    // StageManager calls this once per stage
    public static void Attach(GameObject host)
    {
        if (host != null && host.GetComponent<PowerMeter>() == null)
            host.AddComponent<PowerMeter>();
    }

    // Bumper calls this when the Beast bounces off a green bumper
    public static void GreenHit(Bumper bumper)
    {
        if (Current != null)
            Current.AddHit(bumper);
    }

    int Needed => Mathf.Max(1, GameTuning.Get.powerMeterHits);
    bool Ready => FindPowerBumper() != null;

    void OnEnable()
    {
        Current = this;
        Bumper.PowerBumperHit += OnPowerBumperUsed;
    }

    void OnDisable()
    {
        Bumper.PowerBumperHit -= OnPowerBumperUsed;
        if (Current == this)
            Current = null;
    }

    void Start()
    {
        BuildHud();
    }

    void AddHit(Bumper bumper)
    {
        lastHit = bumper;
        // a Power Bumper is already waiting: the meter stays full until you use it
        if (Ready)
            return;
        hits = Mathf.Min(hits + 1, Needed);
        bumpUntil = Time.unscaledTime + 0.18f;
        if (hits >= Needed)
            SpawnPowerBumper();
    }

    // the meter is full: the green bumper nearest the middle of the field turns red
    // (not the one you just hit, if there's another one)
    void SpawnPowerBumper()
    {
        Bumper best = null;
        float bestDistance = float.MaxValue;
        Vector2 middle = new Vector2(CameraFit.FieldCenterX, 1f);
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper == null || bumper.isBramble || bumper.type != BumperType.Green)
                continue;
            float distance = ((Vector2)bumper.transform.position - middle).sqrMagnitude;
            if (bumper == lastHit)
                distance += 1000f;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = bumper;
            }
        }

        if (best == null)
        {
            retryAt = Time.time + 1f; // no green bumper right now (all broken), try again soon
            return;
        }

        best.MakePowerBumper();
        hits = 0;
        GameAudio.Play(Sfx.Unlock);
        DamagePopup.ShowText(best.transform.position + Vector3.up * 0.8f, "POWER BUMPER!", ReadyColor);
    }

    void OnPowerBumperUsed(Bumper bumper)
    {
        hits = 0;
    }

    static Bumper FindPowerBumper()
    {
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper != null && bumper.IsPowerBumper)
                return bumper;
        }
        return null;
    }

    void Update()
    {
        if (hits >= Needed && !Ready && Time.time >= retryAt)
            SpawnPowerBumper();
        RefreshLayout();
        RefreshLook();
    }

    // --- HUD ---

    void BuildHud()
    {
        GameObject barrier = GameObject.Find("BarrierPanel");
        barrierRect = barrier != null ? barrier.transform as RectTransform : null;
        canvasRect = barrier != null ? barrier.transform.parent as RectTransform : null;
        if (canvasRect == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        }
        if (canvasRect == null)
            return;

        GameObject panel = new GameObject("PowerPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasRect, false);
        panelRect = panel.GetComponent<RectTransform>();
        Image back = panel.AddComponent<Image>();
        back.sprite = HudVisualUtility.RoundedSprite();
        back.type = Image.Type.Sliced;
        back.color = new Color32(42, 51, 38, 235);
        back.raycastTarget = false;
        panelOutline = HudVisualUtility.AddOutline(panel, new Color32(138, 143, 115, 255), new Vector2(5f, -5f));

        label = HudVisualUtility.AddTmpText(panel, "PowerLabel", "POWER", 28f, TextAlignmentOptions.Left);
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 30f;
        label.rectTransform.anchorMin = new Vector2(0f, 0.48f);
        label.rectTransform.anchorMax = new Vector2(0.6f, 1f);
        label.rectTransform.offsetMin = new Vector2(20f, 0f);
        label.rectTransform.offsetMax = new Vector2(0f, -6f);

        value = HudVisualUtility.AddTmpText(panel, "PowerValue", string.Empty, 30f, TextAlignmentOptions.Right);
        value.enableAutoSizing = true;
        value.fontSizeMin = 20f;
        value.fontSizeMax = 32f;
        value.rectTransform.anchorMin = new Vector2(0.5f, 0.48f);
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.offsetMin = Vector2.zero;
        value.rectTransform.offsetMax = new Vector2(-20f, -6f);

        Image track = HudVisualUtility.AddImage(panel, "PowerTrack", new Color32(25, 18, 13, 255));
        trackRect = track.rectTransform;
        trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = Vector2.zero;
        HudVisualUtility.AddOutline(track.gameObject, new Color32(13, 14, 11, 255), new Vector2(2f, -2f));

        fill = HudVisualUtility.AddImage(track.gameObject, "PowerFill", FillColor);
        fill.type = Image.Type.Sliced;
        fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = fill.rectTransform.pivot = Vector2.zero;
        fill.rectTransform.anchoredPosition = Vector2.zero;

        RefreshLayout(true);
    }

    // sits right above the barrier panel, the same width, inside the safe area
    void RefreshLayout(bool force = false)
    {
        if (panelRect == null || canvasRect == null)
            return;
        RectTransform barrier = barrierRect;
        // the barrier panel only shakes sideways, so its height on the screen is steady
        float barrierTop = barrier != null ? barrier.anchoredPosition.y + barrier.sizeDelta.y : -1f;
        Rect safe = Screen.safeArea;
        if (!force && Screen.width == lastScreenWidth && Screen.height == lastScreenHeight && safe.Equals(lastSafeArea) &&
            Mathf.Approximately(barrierTop, lastPanelTop))
            return;

        float canvasWidth = canvasRect.rect.width, canvasHeight = canvasRect.rect.height;
        if (canvasWidth <= 0f || canvasHeight <= 0f)
            return;
        float scale = Mathf.Min(canvasWidth / 1080f, canvasHeight / 1920f);
        float safeLeft = safe.x * canvasWidth / Mathf.Max(1f, Screen.width);
        float safeBottom = safe.y * canvasHeight / Mathf.Max(1f, Screen.height);
        float width = barrier != null ? barrier.sizeDelta.x : 340f * scale;
        float height = 84f * scale;
        float left = Mathf.Max(48f * scale, safeLeft);
        float bottom = (barrierTop > 0f ? barrierTop : Mathf.Max(150f * scale, safeBottom) + 110f * scale) + 14f * scale;

        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.zero;
        panelRect.sizeDelta = new Vector2(width, height);
        panelRect.anchoredPosition = new Vector2(left, bottom);
        panelOutline.effectDistance = new Vector2(5f * scale, -5f * scale);
        trackSize = new Vector2(width - 54f * scale, 22f * scale);
        trackRect.sizeDelta = trackSize;
        trackRect.anchoredPosition = new Vector2(27f * scale, 16f * scale);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = safe;
        lastPanelTop = barrierTop;
    }

    void RefreshLook()
    {
        if (fill == null)
            return;
        bool ready = Ready;
        float target = ready ? 1f : (float)hits / Needed;
        shownFill = Mathf.MoveTowards(shownFill, target, Time.unscaledDeltaTime * 3f);
        fill.rectTransform.sizeDelta = new Vector2(trackSize.x * shownFill, trackSize.y);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
        fill.color = ready ? Color.Lerp(FillColor, ReadyColor, pulse) : (Color)FillColor;
        value.text = ready ? "<color=#FFCC42>READY!</color>" : hits + " / " + Needed;
        float bump = Time.unscaledTime < bumpUntil ? 1.06f : 1f;
        panelRect.localScale = Vector3.Lerp(panelRect.localScale, Vector3.one * (ready ? 1f + 0.03f * pulse : bump),
            1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
    }
}
