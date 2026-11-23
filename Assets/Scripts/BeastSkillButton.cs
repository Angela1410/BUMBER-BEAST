using UnityEngine;
using UnityEngine.UI;
using TMPro;

// the big round skill button in the bottom right. it uses the current Beast's element color and
// symbol, shows the skill's name on a ribbon, shows the recharge as a sweep with a countdown,
// and glows when you can use it (while the Beast is flying)
public class BeastSkillButton : MonoBehaviour
{
    public BeastController beast;
    public Button button;
    public TMP_Text cooldownText;
    public Text legacyCooldownText;

    private const float HintSeconds = 1.3f;

    private HudRoundButton visual;
    private RectTransform rectTransform;

    private bool layoutApplied;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCanvasWidth;
    private float lastCanvasHeight;
    private Rect lastSafeArea;
    private float lastLaidOutSize = -1f;
    private float pressedUntil;
    private float activeUntil;
    private float hintUntil;
    private bool wasSkillActive;

    private bool visualStateInitialized;
    private BeastId shownBeast;
    private int shownRibbonState = -1;
    private int shownCooldown = -1;
    private bool shownDim;

    private static readonly Color32 ActiveRibbonColor = new Color32(255, 204, 66, 255);
    private static readonly Color32 HintRibbonColor = new Color32(120, 60, 40, 255);

    void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnSkillPressed);
    }

    void Start()
    {
        CreateVisuals();
        RefreshLayout(true);
    }

    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnSkillPressed);
            button.onClick.RemoveListener(PlayPressedState);
        }
    }

    void Update()
    {
        RefreshLayout(false);

        if (beast == null || button == null)
            return;

        // always tappable, so tapping at the wrong time explains why instead of doing nothing
        if (!button.interactable)
            button.interactable = true;

        UpdateVisuals();
    }

    public void OnSkillPressed()
    {
        if (beast == null)
            return;

        if (beast.CanUseSkill)
            beast.ActivateSkill();
        else if (beast.State != BeastState.UsingSkill)
            hintUntil = Time.unscaledTime + HintSeconds;
    }

    // keeps trying to place it in the corner until it works, and again when the screen size changes
    private void RefreshLayout(bool force)
    {
        if (rectTransform == null)
            return;

        RectTransform canvasRect = rectTransform.parent as RectTransform;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 0f;
        float canvasHeight = canvasRect != null ? canvasRect.rect.height : 0f;
        bool screenChanged = Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            canvasWidth != lastCanvasWidth ||
            canvasHeight != lastCanvasHeight ||
            !Screen.safeArea.Equals(lastSafeArea);
        if (!force && layoutApplied && !screenChanged)
            return;

        layoutApplied = PortraitButtonLayout.Apply(rectTransform, 0);
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastSafeArea = Screen.safeArea;

        float size = rectTransform.sizeDelta.x;
        if (visual != null && layoutApplied && !Mathf.Approximately(size, lastLaidOutSize))
        {
            float referenceScale = PortraitButtonLayout.ReferenceScale(rectTransform);
            visual.SetTextScale(size, 31f * referenceScale, 24f * referenceScale);
            lastLaidOutSize = size;
        }
    }

    private void CreateVisuals()
    {
        if (rectTransform == null)
            return;

        // only ONE label: reuse the old one if it's inside this button, otherwise make one
        TMP_Text label = null;
        if (cooldownText != null && cooldownText.transform.IsChildOf(transform))
            label = cooldownText;
        else if (cooldownText != null)
            cooldownText.gameObject.SetActive(false); // a stray copy outside the button
        if (legacyCooldownText != null)
            legacyCooldownText.gameObject.SetActive(false);

        // hide every other text under this button (this fixes the doubled label), then build it
        PortraitButtonLayout.HideOtherLabels(transform, label);
        visual = HudRoundButton.Build(gameObject, label, 1.36f);

        if (button != null)
        {
            button.targetGraphic = visual.Bezel;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(PlayPressedState);
        }
    }

    private void UpdateVisuals()
    {
        if (visual == null)
            return;

        bool skillActive = beast.IsDoubleBounceActive || beast.State == BeastState.UsingSkill;
        if (skillActive && !wasSkillActive)
            activeUntil = Time.unscaledTime + 0.2f;
        wasSkillActive = skillActive;
        bool showActive = skillActive || Time.unscaledTime < activeUntil;
        bool cooling = beast.CooldownRemaining > 0f;
        bool usable = beast.CanUseSkill;
        bool showHint = !usable && !showActive && Time.unscaledTime < hintUntil;
        int cooldownSeconds = cooling ? Mathf.CeilToInt(beast.CooldownRemaining) : 0;

        // ribbon: 0 = skill name, 1 = ACTIVE, 2 = "launch first" hint, 3 = "recharging" hint
        int ribbonState = showActive ? 1 : showHint ? (cooling ? 3 : 2) : 0;
        bool dim = !usable && !showActive;

        if (!visualStateInitialized || shownBeast != beast.ActiveBeast || shownDim != dim)
        {
            ElementType element = BeastRoster.GetElement(beast.ActiveBeast);
            visual.SetElement(ElementVisuals.GetColor(element), dim);
            visual.Icon.sprite = ElementVisuals.GetGlyphSprite(element);
            shownDim = dim;
        }

        if (!visualStateInitialized || shownBeast != beast.ActiveBeast || shownRibbonState != ribbonState)
        {
            switch (ribbonState)
            {
                case 1:
                    visual.RibbonText.text = "<color=#2B1A0E>ACTIVE!</color>";
                    break;
                case 2:
                    visual.RibbonText.text = "LAUNCH FIRST";
                    break;
                case 3:
                    visual.RibbonText.text = "RECHARGING";
                    break;
                default:
                    visual.RibbonText.text = BeastRoster.GetAbilityName(beast.ActiveBeast).ToUpperInvariant();
                    break;
            }
            visual.Ribbon.color = ribbonState == 1 ? (Color)ActiveRibbonColor
                : ribbonState >= 2 ? (Color)HintRibbonColor
                : (Color)HudRoundButton.RibbonColor;
            shownRibbonState = ribbonState;
        }

        if (!visualStateInitialized || shownCooldown != cooldownSeconds)
        {
            visual.CenterText.text = cooling ? cooldownSeconds.ToString() : string.Empty;
            shownCooldown = cooldownSeconds;
        }
        shownBeast = beast.ActiveBeast;
        visualStateInitialized = true;

        float normalizedCooldown = cooling
            ? Mathf.Clamp01(beast.CooldownRemaining / Mathf.Max(1f, beast.skillCooldown))
            : 0f;
        visual.Cooldown.enabled = cooling;
        visual.Cooldown.fillAmount = normalizedCooldown;
        visual.Icon.enabled = !cooling;
        Color iconColor = visual.Icon.color;
        iconColor.a = dim ? 0.55f : 1f;
        visual.Icon.color = iconColor;

        // glow: steady while active, pulsing when you can use it right now
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        Color glow = visual.Glow.color;
        glow.a = showActive ? 0.95f : usable ? Mathf.Lerp(0.35f, 0.85f, pulse) : 0f;
        visual.Glow.color = glow;
        visual.RibbonOutline.effectColor = showActive ? (Color)UiTheme.Ink : usable
            ? Color.Lerp(UiTheme.Ink, UiTheme.Gold, pulse)
            : (Color)UiTheme.Ink;

        float target = 1f;
        if (Time.unscaledTime < pressedUntil)
            target = 0.9f;
        else if (usable)
            target = 1.02f + pulse * 0.04f;
        else if (showHint)
            target = 1f + Mathf.Sin(Time.unscaledTime * 40f) * 0.015f;

        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            Vector3.one * target,
            Time.unscaledDeltaTime * 14f);
    }

    private void PlayPressedState()
    {
        pressedUntil = Time.unscaledTime + 0.1f;
    }
}
