using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PadSwapSystem : MonoBehaviour
{
    public BeastController beast;
    public Button swapButton;
    public TMP_Text swapLabel;
    public Text legacySwapLabel;
    public BeastId[] squad = new BeastId[0];

    [Header("Fast Swap")]
    [Tooltip("SWAP works at any time. While the Beast is flying it is called straight back to the pad " +
        "and arrives as the next Beast; while aiming or using a skill the swap waits for the pad.")]
    public bool recallOnSwap = true;

    [Header("Testing")]
    [Tooltip("Editor only: every Beast joins the squad without being unlocked. Ignored in built games.")]
    public bool unlockAllForTesting = false; // turn this OFF for the real game

    // the test switch never reaches players: a build always uses the real unlocks
    private bool TestUnlockAll => unlockAllForTesting && Application.isEditor;

    private readonly List<BeastId> activeSquad = new List<BeastId>(3);
    private int activeSquadIndex;
    // which squad member is waiting to swap in once the Beast is back on the pad, -1 = none
    private int pendingSquadIndex = -1;
    private int displayedPendingIndex = -2;
    private LaunchController launchController;

    private HudRoundButton swapVisual;
    private Image swapBadgeFace;
    private RectTransform swapBadgeArrows;
    private CanvasGroup swapGroup;
    private float badgeSpinUntil;
    private RectTransform swapRect;

    private bool layoutApplied;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCanvasWidth;
    private float lastCanvasHeight;
    private Rect lastSafeArea;
    private float pressedUntil;
    private int displayedNextIndex = -1;
    private bool displayedCanSwap;
    private bool canSwapInitialized;

    private static readonly Color32 SwapColor = new Color32(47, 145, 145, 255);
    private static readonly Color32 SwapQueuedColor = new Color32(196, 140, 40, 255);

    public BeastId ActiveSquadBeast => activeSquad.Count > 0 ? activeSquad[activeSquadIndex] : BeastId.Choma;
    public IReadOnlyList<BeastId> ActiveSquad => activeSquad;

    void Awake()
    {
        if (beast == null)
            beast = GetComponent<BeastController>();

        launchController = GetComponent<LaunchController>();

        if (beast == null)
        {
            Debug.LogError("PadSwapSystem requires a BeastController on the same GameObject.", this);
            enabled = false;
            return;
        }

        if (swapButton == null)
        {
            GameObject swapButtonObject = GameObject.Find("BeastSwapButton");
            if (swapButtonObject != null)
                swapButton = swapButtonObject.GetComponent<Button>();
        }

        if (swapLabel == null && legacySwapLabel == null)
        {
            GameObject labelObject = GameObject.Find("SwapLabel");
            if (labelObject != null)
            {
                swapLabel = labelObject.GetComponent<TMP_Text>();
                if (swapLabel == null)
                    legacySwapLabel = labelObject.GetComponent<Text>();
            }
        }

        if (swapButton != null)
        {
            swapRect = swapButton.transform as RectTransform;
            swapButton.onClick.AddListener(SwapToNextBeast);
        }
    }

    void Start()
    {
        CreateVisuals();
        RefreshLayout(true);
        BuildSquad();
        UpdateSwapButton();
    }

    void Update()
    {
        ApplyPendingSwap();
        RefreshLayout(false);
        UpdateSwapButton();
        UpdatePressedScale();
    }

    void OnDestroy()
    {
        if (swapButton != null)
        {
            swapButton.onClick.RemoveListener(SwapToNextBeast);
            swapButton.onClick.RemoveListener(PlayPressedState);
        }
    }

    public void SwapToNextBeast()
    {
        if (!CanSwap())
            return;

        if (IsReadyForImmediateSwap())
        {
            pendingSquadIndex = -1;
            SwapTo((activeSquadIndex + 1) % activeSquad.Count);
            return;
        }

        // not on the pad: wait to swap (each extra tap moves to the next squad member,
        // going back around to the current Beast cancels it)
        int from = pendingSquadIndex >= 0 ? pendingSquadIndex : activeSquadIndex;
        int next = (from + 1) % activeSquad.Count;
        pendingSquadIndex = next == activeSquadIndex ? -1 : next;
        GameAudio.Play(Sfx.Click);

        if (pendingSquadIndex >= 0 && recallOnSwap && beast.State == BeastState.Flying)
            beast.BeginReturn();

        UpdateSwapButton();
    }

    bool CanSwap()
    {
        return beast != null && activeSquad.Count > 1;
    }

    bool IsReadyForImmediateSwap()
    {
        return beast.State == BeastState.AtPad &&
            (launchController == null || !launchController.IsAiming);
    }

    void ApplyPendingSwap()
    {
        if (pendingSquadIndex < 0 || beast == null || !IsReadyForImmediateSwap())
            return;

        int target = pendingSquadIndex;
        pendingSquadIndex = -1;
        if (target < activeSquad.Count)
            SwapTo(target);
    }

    void SwapTo(int squadIndex)
    {
        activeSquadIndex = squadIndex;
        beast.SetActiveBeast(activeSquad[activeSquadIndex]);
        GameAudio.Play(Sfx.Swap);

        if (launchController != null)
            launchController.PlaySwapPop();

        UpdateSwapButton();
    }

    void BuildSquad()
    {
        activeSquad.Clear();
        pendingSquadIndex = -1;
        bool hasConfiguredSquad = squad != null && squad.Length > 0;

        if (hasConfiguredSquad)
        {
            foreach (BeastId candidate in squad)
            {
                if (activeSquad.Count == 3)
                    break;

                if (IsUnlocked(candidate) && !activeSquad.Contains(candidate))
                    activeSquad.Add(candidate);
                else if (!IsUnlocked(candidate))
                    Debug.LogWarning("Ignoring unavailable Beast in the Pad Swap squad: " + candidate, this);
            }
        }

        BeastId selected = GameProgress.Instance != null
            ? GameProgress.Instance.SelectedBeast
            : BeastId.Choma;
        // the squad picked in the shop (leader first)
        if (!hasConfiguredSquad && !TestUnlockAll && GameProgress.Instance != null)
        {
            foreach (BeastId candidate in GameProgress.Instance.Squad)
            {
                if (activeSquad.Count == 3)
                    break;
                if (IsUnlocked(candidate) && !activeSquad.Contains(candidate))
                    activeSquad.Add(candidate);
            }
        }

        if (!hasConfiguredSquad && activeSquad.Count == 0)
        {
            if (IsUnlocked(selected))
                activeSquad.Add(selected);

            foreach (BeastId candidate in BeastRoster.AllBeasts)
            {
                if (activeSquad.Count == 3)
                    break;

                if (IsUnlocked(candidate) && !activeSquad.Contains(candidate))
                    activeSquad.Add(candidate);
            }
        }

        if (activeSquad.Count == 0)
            activeSquad.Add(BeastId.Choma);

        activeSquadIndex = activeSquad.IndexOf(selected);
        if (activeSquadIndex < 0)
            activeSquadIndex = 0;

        beast.SetActiveBeast(activeSquad[activeSquadIndex]);
        squad = activeSquad.ToArray();
    }

    bool IsUnlocked(BeastId candidate)
    {
        if (System.Array.IndexOf(BeastRoster.AllBeasts, candidate) < 0)
            return false;

        if (TestUnlockAll)
            return true;

        return GameProgress.Instance == null
            ? candidate == BeastId.Choma
            : GameProgress.Instance.IsBeastUnlocked(candidate);
    }

    void UpdateSwapButton()
    {
        bool canSwap = CanSwap();
        if (swapButton != null && (!canSwapInitialized || displayedCanSwap != canSwap))
        {
            swapButton.interactable = canSwap;
            // with only one Beast there's nothing to swap to, so the button is hidden, not just greyed out
            if (swapGroup != null)
            {
                swapGroup.alpha = canSwap ? 1f : 0f;
                swapGroup.blocksRaycasts = canSwap;
                swapGroup.interactable = canSwap;
            }
            displayedCanSwap = canSwap;
            canSwapInitialized = true;
        }

        if (activeSquad.Count == 0 || swapVisual == null)
            return;

        bool pending = pendingSquadIndex >= 0;
        int nextIndex = pending ? pendingSquadIndex : (activeSquadIndex + 1) % activeSquad.Count;
        if (displayedNextIndex == nextIndex && displayedPendingIndex == pendingSquadIndex)
            return;

        BeastId next = activeSquad[nextIndex];
        ElementType element = BeastRoster.GetElement(next);
        swapVisual.SetElement(ElementVisuals.GetColor(element), false);

        Sprite portrait = launchController != null ? launchController.GetPortraitSprite(next) : null;
        swapVisual.Icon.sprite = portrait != null ? portrait : ElementVisuals.GetGlyphSprite(element);
        HudRoundButton.Stretch(swapVisual.Icon.rectTransform,
            portrait != null ? new Vector2(0.12f, 0.16f) : new Vector2(0.27f, 0.3f),
            portrait != null ? new Vector2(0.88f, 0.92f) : new Vector2(0.73f, 0.76f));

        // the ribbon says who comes in next, gold "NEXT UP" while a swap waits for the pad
        swapVisual.RibbonText.text = pending
            ? "<color=#2B1A0E>NEXT UP</color>"
            : BeastRoster.GetName(next).ToUpperInvariant();
        swapVisual.Ribbon.color = pending ? (Color)SwapQueuedColor : (Color)HudRoundButton.RibbonColor;
        if (swapBadgeFace != null)
            swapBadgeFace.color = pending ? (Color)SwapQueuedColor : (Color)SwapColor;

        displayedNextIndex = nextIndex;
        displayedPendingIndex = pendingSquadIndex;
    }

    // keeps trying to place it in the corner until it works, and again when the screen size changes
    private void RefreshLayout(bool force)
    {
        if (swapRect == null)
            return;

        RectTransform canvasRect = swapRect.parent as RectTransform;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 0f;
        float canvasHeight = canvasRect != null ? canvasRect.rect.height : 0f;
        bool screenChanged = Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            canvasWidth != lastCanvasWidth ||
            canvasHeight != lastCanvasHeight ||
            !Screen.safeArea.Equals(lastSafeArea);
        if (!force && layoutApplied && !screenChanged)
            return;

        layoutApplied = PortraitButtonLayout.Apply(swapRect, 1);
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastSafeArea = Screen.safeArea;

        if (swapVisual != null && layoutApplied)
        {
            float referenceScale = PortraitButtonLayout.ReferenceScale(swapRect);
            swapVisual.SetTextScale(swapRect.sizeDelta.x, 29f * referenceScale, 23f * referenceScale);
        }
    }

    private void CreateVisuals()
    {
        if (swapButton == null || swapRect == null)
            return;

        // only ONE label: reuse the old one if it's inside this button, otherwise make one
        TMP_Text label = null;
        if (swapLabel != null && swapLabel.transform.IsChildOf(swapButton.transform))
            label = swapLabel;
        else if (swapLabel != null)
            swapLabel.gameObject.SetActive(false); // a stray copy outside the button
        if (legacySwapLabel != null)
            legacySwapLabel.gameObject.SetActive(false);

        // hide every other text under this button (this fixes the doubled label), then build it
        PortraitButtonLayout.HideOtherLabels(swapButton.transform, label);
        swapVisual = HudRoundButton.Build(swapButton.gameObject, label, 1.5f);
        swapVisual.Gloss.transform.SetSiblingIndex(swapVisual.Icon.transform.GetSiblingIndex() + 1);
        swapVisual.Icon.color = Color.white; // portraits keep their own colors

        // little swap arrows badge on the top right of the circle
        Image badge = HudVisualUtility.AddImage(swapButton.gameObject, "SwapBadge", UiTheme.Ink);
        badge.sprite = HudVisualUtility.CircleSprite();
        badge.type = Image.Type.Simple;
        HudRoundButton.Stretch(badge.rectTransform, new Vector2(0.64f, 0.64f), new Vector2(1.06f, 1.06f));
        swapBadgeFace = HudVisualUtility.AddImage(badge.gameObject, "Face", SwapColor);
        swapBadgeFace.sprite = HudVisualUtility.CircleSprite();
        swapBadgeFace.type = Image.Type.Simple;
        HudRoundButton.Stretch(swapBadgeFace.rectTransform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
        Image arrows = HudVisualUtility.AddImage(badge.gameObject, "Arrows", new Color32(255, 248, 230, 255));
        arrows.sprite = ElementVisuals.GetSwapArrowsSprite();
        arrows.type = Image.Type.Simple;
        HudRoundButton.Stretch(arrows.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f));
        swapBadgeArrows = arrows.rectTransform;
        swapVisual.Ribbon.transform.SetAsLastSibling();

        swapGroup = swapButton.GetComponent<CanvasGroup>();
        if (swapGroup == null)
            swapGroup = swapButton.gameObject.AddComponent<CanvasGroup>();

        swapButton.targetGraphic = swapVisual.Bezel;
        swapButton.transition = Selectable.Transition.None;
        swapButton.onClick.AddListener(PlayPressedState);
    }

    private void PlayPressedState()
    {
        pressedUntil = Time.unscaledTime + 0.1f;
        badgeSpinUntil = Time.unscaledTime + 0.35f;
    }

    private void UpdatePressedScale()
    {
        if (swapRect == null)
            return;

        bool pending = pendingSquadIndex >= 0;
        float target = Time.unscaledTime < pressedUntil ? 0.9f
            : pending ? 1.04f + Mathf.Sin(Time.unscaledTime * 8f) * 0.03f
            : 1f;
        swapRect.localScale = Vector3.Lerp(
            swapRect.localScale,
            Vector3.one * target,
            Time.unscaledDeltaTime * 14f);

        // the arrows turn on each tap and keep turning while a swap is waiting
        if (swapBadgeArrows != null && (pending || Time.unscaledTime < badgeSpinUntil))
            swapBadgeArrows.Rotate(0f, 0f, 540f * Time.unscaledDeltaTime); // the arrows point counter-clockwise

        if (swapVisual != null)
        {
            Color glow = swapVisual.Glow.color;
            glow.a = pending ? 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            swapVisual.Glow.color = glow;
        }
    }
}
