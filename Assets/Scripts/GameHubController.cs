using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The world map and shop, built in code over the hub scene. Older scene buttons are hidden
// (their names stay, so nothing else breaks). A bottom tab bar switches between:
//  MAP       chapter banner, the chapter's four-stage path, boss and rescue tiles, PLAY
//  UPGRADES  launch power and skill cooldown, plus the element chart
//  BEASTS    the squad of three (tap a slot to make it lead) and every Beast's card
public class GameHubController : MonoBehaviour
{
    public const string SeenChapterKey = "BumperBeast.SeenChapter";

    public Text coinsText;
    public Text unlockedText;
    public Text shopText;
    public int stageCount = 20;

    [Header("Hub Art (optional; empty slots use generated art)")]
    [Tooltip("One per chapter: Wildroot, Earth, Electric, Water, Fire.")]
    public Sprite[] chapterArtwork;
    [Tooltip("Portraits in BeastRoster order: Choma, Thistle, Mossback, Voltis, Ripple.")]
    public Sprite[] beastPortraits;
    [Tooltip("One per chapter boss.")]
    public Sprite[] bossPortraits;

    private enum HubView
    {
        Chapters,
        Upgrades,
        Beasts
    }

    private sealed class StageNode
    {
        public Button button;
        public RectTransform rect;
        public Image glow;
        public Image face;
        public Image gloss;
        public Image ring;
        public TMP_Text number;
        public Image lockIcon;
        public Image check;
        public Image crown;
        public TMP_Text tag;
        public Image[] stars;
    }

    private sealed class UpgradeCard
    {
        public TMP_Text effect;
        public TMP_Text level;
        public Image[] dots;
        public Button buy;
    }

    private sealed class SquadSlot
    {
        public Button button;
        public Image ring;
        public Image portrait;
        public TMP_Text initial;
        public Image plus;
        public TMP_Text tag;
    }

    private sealed class BeastCard
    {
        public Button button;
        public Image back;
        public Outline outline;
        public Image portrait;
        public TMP_Text initial;
        public Image element;
        public TMP_Text name;
        public TMP_Text detail;
        public Image pill;
        public Image pillIcon;
        public TMP_Text pillLabel;
    }

    private sealed class NavTab
    {
        public Button button;
        public Image back;
        public Outline outline;
        public Image icon;
        public TMP_Text label;
        public Image badge;
    }

    private const int StagesPerChapter = ChapterData.StagesPerChapter;
    private const float ContentWidth = 1080f;
    private const float ContentHeight = 1840f;
    // Tall phones (20:9 and up) spread the layout out vertically by up to this much, so the
    // header reaches the top and the tab bar the bottom instead of floating mid-screen.
    private const float MaxLayoutStretch = 1.32f;
    private const float NodeSize = 150f;
    private const float SelectedNodeSize = 178f;
    private const float PathY = 6f;
    private static readonly float[] NodeX = { -345f, -115f, 115f, 345f };
    private static readonly float[] NodeY = { -14f, 26f, -14f, 26f };
    private static readonly Color32 TerrathornTint = new Color32(219, 173, 128, 255);
    private static readonly Color32 ClosedPath = new Color32(22, 26, 20, 255);
    private static readonly Color32 OpenPath = new Color32(255, 236, 170, 255);
    private static readonly Color32 TabIdle = new Color32(36, 44, 35, 255);
    private static readonly Color32 BossRed = new Color32(255, 150, 120, 255);
    private static readonly Color32 GoodGreen = new Color32(176, 236, 140, 255);
    private static readonly ElementType[] ChartOrder =
    {
        ElementType.Fire, ElementType.Grass, ElementType.Earth, ElementType.Electric, ElementType.Water
    };

    private string shopNotice;
    private float shopNoticeUntil;
    private int selectedChapter;
    private int selectedStage = 1;
    private HubView currentView = HubView.Chapters;
    private Canvas hubCanvas;
    private RectTransform safeRoot;
    private RectTransform contentRoot;
    private RectTransform chapterGroup;
    private RectTransform pathGroup;
    private RectTransform upgradeGroup;
    private RectTransform beastGroup;
    private float layoutStretch = 1f;
    // Laid-out elements and their design height (y), re-spread on tall screens.
    private readonly List<RectTransform> stretchRects = new List<RectTransform>();
    private readonly List<float> stretchBaseY = new List<float>();
    private Image screenBackdrop;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Vector2 lastCanvasSize;
    private int lastArtChapter = -1;
    private static Sprite fadeSprite;

    // Header
    private TMP_Text headerTitle;
    private TMP_Text progressLabel;
    private TMP_Text coinLabel;

    // Map
    private Outline bannerOutline;
    private Image bannerArt;
    private TMP_Text bannerChip;
    private TMP_Text bannerTitle;
    private TMP_Text bannerTagline;
    private TMP_Text bannerProgress;
    private Image bannerDisc;
    private Image bannerGlyph;
    private GameObject bannerLock;
    private TMP_Text bannerLockLabel;
    private Button previousChapterButton;
    private Button nextChapterButton;
    private Image[] chapterDots;
    private readonly StageNode[] nodes = new StageNode[StagesPerChapter];
    private readonly Image[] pathSegments = new Image[StagesPerChapter - 1];
    private TMP_Text stageTitle;
    private TMP_Text stageAdvice;
    private Image bossPortrait;
    private TMP_Text bossName;
    private TMP_Text bossStatus;
    private Image rescuePortrait;
    private TMP_Text rescueInitial;
    private Image rescueCrown;
    private TMP_Text rescueName;
    private TMP_Text rescueStatus;
    private Button playButton;
    private Image playGlow;

    // Shop
    private UpgradeCard launchCard;
    private UpgradeCard cooldownCard;
    private readonly SquadSlot[] squadSlots = new SquadSlot[GameProgress.MaxSquadSize];
    private BeastCard[] beastCards;
    private RectTransform noticePanel;
    private TMP_Text noticeLabel;
    private readonly NavTab[] tabs = new NavTab[3];

    private void Start()
    {
        EnsureProgress();
        hubCanvas = coinsText != null ? coinsText.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (hubCanvas == null)
        {
            Debug.LogError("GameHubController requires a Canvas in the hub scene.");
            return;
        }

        HideLegacySceneUi();
        BuildPresentation();

        int highest = Mathf.Clamp(GameProgress.Instance.HighestUnlockedStage, 1, stageCount);
        selectedChapter = Mathf.Clamp((highest - 1) / StagesPerChapter, 0, GetChapterCount() - 1);
        selectedStage = highest;

        RefreshPresentation();
        ShowNewChapterIntroIfNeeded();
        // Title art sits above everything (including a chapter intro) until PLAY is tapped.
        // The daily gift waits for the title to be dismissed (or shows now when it was already seen).
        if (TitleScreen.ShowIfNeeded())
            TitleScreen.Dismissed += OfferDailyGift;
        else
            OfferDailyGift();
    }

    private void OnDestroy()
    {
        TitleScreen.Dismissed -= OfferDailyGift;
    }

    private void OfferDailyGift()
    {
        TitleScreen.Dismissed -= OfferDailyGift;
        if (this != null && !GameMenus.IsOpen)
            GameMenus.OpenDailyGiftIfReady(RefreshPresentation);
    }

    private void Update()
    {
        RectTransform canvasRect = hubCanvas != null ? hubCanvas.transform as RectTransform : null;
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (safeRoot != null &&
            (Screen.width != lastScreenWidth ||
             Screen.height != lastScreenHeight ||
             Screen.safeArea != lastSafeArea ||
             canvasSize != lastCanvasSize))
            ApplySafeArea();

        if (!string.IsNullOrEmpty(shopNotice) && Time.unscaledTime >= shopNoticeUntil)
        {
            shopNotice = null;
            shopNoticeUntil = 0f;
            RefreshNotice();
        }

        AnimateHub();
    }

    // Gentle life: the PLAY glow breathes, the selected stage pulses, waiting badges bob.
    private void AnimateHub()
    {
        float time = Time.unscaledTime;
        if (playGlow != null)
        {
            Color glow = playGlow.color;
            glow.a = playButton != null && playButton.interactable ? 0.32f + Mathf.Sin(time * 3f) * 0.16f : 0f;
            playGlow.color = glow;
        }

        for (int slot = 0; slot < nodes.Length; slot++)
        {
            StageNode node = nodes[slot];
            if (node == null || !node.ring.enabled)
                continue;
            Color glow = node.glow.color;
            glow.a = 0.4f + Mathf.Sin(time * 4f) * 0.2f;
            node.glow.color = glow;
        }

        NavTab beasts = tabs[2];
        if (beasts != null && beasts.badge.enabled)
            beasts.badge.rectTransform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(time * 4f)) * 0.18f);
    }

    // =====================================================================
    // PUBLIC ACTIONS
    // =====================================================================

    public void LoadStage(int stageNumber)
    {
        if (GameProgress.Instance == null ||
            stageNumber < 1 ||
            stageNumber > stageCount ||
            stageNumber > GameProgress.Instance.HighestUnlockedStage)
            return;

        string sceneName = GetStageSceneName(stageNumber);
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            if (LoadingScreen.Load(sceneName))
                GameAudio.Play(Sfx.Play);
            return;
        }

        ShowShopNotice("STAGE SCENE UNAVAILABLE: " + sceneName.ToUpperInvariant());
    }

    public void ContinueToNextStage()
    {
        if (GameProgress.Instance == null)
            EnsureProgress();

        if (GameProgress.Instance == null ||
            GameProgress.Instance.HighestUnlockedStage > stageCount)
            return;

        selectedStage = Mathf.Clamp(GameProgress.Instance.HighestUnlockedStage, 1, stageCount);
        selectedChapter = (selectedStage - 1) / StagesPerChapter;
        LoadStage(selectedStage);
    }

    public void BuyLaunchPower()
    {
        if (GameProgress.Instance == null)
            ShowDenied("NOT ENOUGH COINS");
        else if (!GameProgress.Instance.CanUpgradeLaunchPower)
            ShowDenied("LAUNCH POWER IS MAXED");
        else if (!GameProgress.Instance.BuyLaunchPower())
            ShowDenied("NOT ENOUGH COINS - CLEAR STAGES TO EARN MORE");
        else
            ShowBought("LAUNCH POWER UPGRADED TO LEVEL " + GameProgress.Instance.LaunchPowerLevel + "!");

        RefreshPresentation();
    }

    public void BuySkillCooldown()
    {
        if (GameProgress.Instance == null)
            ShowDenied("NOT ENOUGH COINS");
        else if (!GameProgress.Instance.CanUpgradeSkillCooldown)
            ShowDenied("SKILL COOLDOWN IS MAXED");
        else if (!GameProgress.Instance.BuySkillCooldown())
            ShowDenied("NOT ENOUGH COINS - CLEAR STAGES TO EARN MORE");
        else
            ShowBought("SKILL COOLDOWN UPGRADED TO LEVEL " + GameProgress.Instance.SkillCooldownLevel + "!");

        RefreshPresentation();
    }

    // A Beast card: recruit a rescued Beast, or add / remove an owned Beast from the squad.
    public void BuyOrSelectBeast(BeastId beast)
    {
        if (GameProgress.Instance == null)
            EnsureProgress();
        if (GameProgress.Instance == null)
            return;

        GameProgress progress = GameProgress.Instance;
        string name = BeastRoster.GetName(beast).ToUpperInvariant();
        if (!progress.IsBeastUnlocked(beast))
        {
            bool reached = progress.HighestClearedStage >= BeastRoster.GetUnlockStage(beast);
            if (!reached)
                ShowDenied("BEAT STAGE " + BeastRoster.GetUnlockStage(beast) + " TO FREE " + name);
            else if (!progress.UnlockBeast(beast))
                ShowDenied("NOT ENOUGH COINS - " + name + " COSTS " + BeastRoster.GetPrice(beast));
            else
                ShowBought(progress.IsInSquad(beast)
                    ? name + " RECRUITED AND JOINED YOUR SQUAD!"
                    : name + " RECRUITED! TAP A SQUAD BEAST'S CARD TO MAKE ROOM");
        }
        else
        {
            switch (progress.ToggleSquadMember(beast))
            {
                case GameProgress.SquadChange.Added:
                    ShowSwapped(name + " JOINED THE SQUAD");
                    break;
                case GameProgress.SquadChange.Removed:
                    ShowSwapped(name + " LEFT THE SQUAD");
                    break;
                case GameProgress.SquadChange.Full:
                    ShowDenied("SQUAD IS FULL - TAP A SQUAD BEAST'S CARD TO REMOVE IT FIRST");
                    break;
                case GameProgress.SquadChange.LastMember:
                    ShowDenied("YOUR SQUAD NEEDS AT LEAST ONE BEAST");
                    break;
            }
        }

        RefreshPresentation();
    }

    private void MakeSquadLead(int slot)
    {
        GameProgress progress = GameProgress.Instance;
        if (progress == null || slot >= progress.Squad.Count)
        {
            ShowShopNotice("TAP A BEAST CARD BELOW TO ADD IT TO THE SQUAD");
            return;
        }

        BeastId beast = progress.Squad[slot];
        if (slot == 0)
        {
            ShowShopNotice(BeastRoster.GetName(beast).ToUpperInvariant() + " ALREADY LEADS THE SQUAD");
            return;
        }

        progress.SelectBeast(beast);
        ShowSwapped(BeastRoster.GetName(beast).ToUpperInvariant() + " NOW LEADS THE SQUAD");
        RefreshPresentation();
    }

    public static string GetStageSceneName(int stageNumber)
    {
        if (stageNumber <= 4)
            return "stage-" + stageNumber;

        if (stageNumber <= 8)
            return "stage-" + stageNumber + "_Earth";

        if (stageNumber <= 12)
            return "stage-" + stageNumber + "_Electric";

        if (stageNumber <= 16)
            return "stage-" + stageNumber + "_Water";

        return "stage-" + stageNumber + "_Fire";
    }

    // =====================================================================
    // BUILD
    // =====================================================================

    private void HideLegacySceneUi()
    {
        HideLegacyText(coinsText);
        HideLegacyText(unlockedText);
        HideLegacyText(shopText);
        HideSceneObject("ShopPanel");
        HideSceneObject("ContinueButton");
        HideSceneObject("BuyLaunchPower");
        HideSceneObject("BuySkillCooldown");
        for (int index = 1; index <= stageCount; index++)
            HideSceneObject("StageButton" + index);
        for (int index = 0; index < BeastRoster.AllBeasts.Length; index++)
            HideSceneObject("BeastButton" + index);
    }

    private void BuildPresentation()
    {
        // Full-screen themed backdrop (extends under notches), then a safe-area root,
        // then a fixed 1080x1840 content root that scales down on short screens.
        GameObject backdropObject = new GameObject("HubBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
        backdropRect.SetParent(hubCanvas.transform, false);
        Stretch(backdropRect);
        backdropRect.SetAsLastSibling();
        Image backdropBase = backdropObject.GetComponent<Image>();
        backdropBase.color = UiTheme.Background;
        backdropBase.raycastTarget = false;
        screenBackdrop = CreateImage(backdropRect, "Art", Color.white, null);
        Image vignette = CreateImage(backdropRect, "Fade", new Color(0.02f, 0.03f, 0.02f, 0.7f), FadeSprite());
        Stretch(vignette.rectTransform);

        GameObject rootObject = new GameObject("HubSafeArea", typeof(RectTransform));
        safeRoot = rootObject.GetComponent<RectTransform>();
        safeRoot.SetParent(hubCanvas.transform, false);
        safeRoot.SetAsLastSibling();

        GameObject contentObject = new GameObject("HubContent", typeof(RectTransform));
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.SetParent(safeRoot, false);
        contentRoot.anchorMin = contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.sizeDelta = new Vector2(ContentWidth, ContentHeight);
        ApplySafeArea();

        chapterGroup = CreateGroup(contentRoot, "MapView");
        upgradeGroup = CreateGroup(contentRoot, "UpgradesView");
        beastGroup = CreateGroup(contentRoot, "BeastsView");
        BuildHeader();
        BuildChapterView();
        BuildUpgradeView();
        BuildBeastView();
        BuildNotice();
        BuildNavBar();
        RegisterStretchRects();
        ApplySafeArea();
    }

    // Everything laid out on the 1080 x 1840 design grid moves with the vertical stretch, except
    // the notice (placed per view in RefreshNotice) and the stage path, which keeps its shape.
    private void RegisterStretchRects()
    {
        stretchRects.Clear();
        stretchBaseY.Clear();
        RectTransform[] parents = { contentRoot, chapterGroup, upgradeGroup, beastGroup };
        foreach (RectTransform parent in parents)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                RectTransform child = parent.GetChild(index) as RectTransform;
                if (child == null || child == noticePanel || child == chapterGroup || child == upgradeGroup ||
                    child == beastGroup || child == pathGroup)
                    continue;
                stretchRects.Add(child);
                stretchBaseY.Add(child.anchoredPosition.y);
            }
        }
    }

    private void ApplyLayoutStretch()
    {
        contentRoot.sizeDelta = new Vector2(ContentWidth, ContentHeight * layoutStretch);
        for (int index = 0; index < stretchRects.Count; index++)
        {
            RectTransform rect = stretchRects[index];
            if (rect != null)
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, stretchBaseY[index] * layoutStretch);
        }
        RefreshNotice();
    }

    // The profile card's avatar uses the same portraits as the Beasts screen.
    public Sprite GetBeastPortrait(BeastId beast)
    {
        return BeastPortrait(beast);
    }

    private void OpenSettingsMenu()
    {
        GameMenus.OpenSettings(true, GetBeastPortrait, RefreshPresentation);
    }

    private void BuildHeader()
    {
        headerTitle = CreateLabel(contentRoot, "PageTitle", "WORLD MAP", 58f, TextAlignmentOptions.Left);
        SetRect(headerTitle.rectTransform, new Vector2(-220f, 858f), new Vector2(600f, 86f));
        progressLabel = CreateLabel(contentRoot, "ProgressSummary", string.Empty, 26f, TextAlignmentOptions.Left);
        progressLabel.color = UiTheme.MutedText;
        SetRect(progressLabel.rectTransform, new Vector2(-220f, 800f), new Vector2(600f, 40f));

        RectTransform coinPill = CreatePanel(contentRoot, "CoinBalancePanel", new Vector2(345f, 840f), new Vector2(330f, 100f),
            new Color32(26, 32, 26, 245));
        HudVisualUtility.AddOutline(coinPill.gameObject, UiTheme.Gold, new Vector2(3f, -3f));
        CreateIcon(coinPill, "CoinIcon", UiIcon.Coin, Color.white, new Vector2(-112f, 0f), 76f);
        coinLabel = CreateLabel(coinPill, "CoinBalance", string.Empty, 44f, TextAlignmentOptions.Right);
        coinLabel.color = UiTheme.Gold;
        SetRect(coinLabel.rectTransform, new Vector2(34f, 0f), new Vector2(226f, 84f));

        // Settings (music, sound, screen shake, player profile).
        Button settings = CreateRoundIconButton(contentRoot, "SettingsButton", UiIcon.Gear, new Vector2(118f, 840f), 96f, OpenSettingsMenu);
        Image face = settings.transform.Find("Face").GetComponent<Image>();
        face.color = new Color32(52, 66, 50, 255);
    }

    private void BuildChapterView()
    {
        // ---- Chapter banner: art, chapter chip, element medallion, title and tagline.
        RectTransform banner = CreatePanel(chapterGroup, "ChapterBanner", new Vector2(0f, 470f), new Vector2(960f, 560f), UiTheme.PanelDark);
        bannerOutline = HudVisualUtility.AddOutline(banner.gameObject, UiTheme.Gold, new Vector2(5f, -5f));
        Image maskImage = CreateImage(banner, "Mask", Color.white, HudVisualUtility.RoundedSprite());
        maskImage.type = Image.Type.Sliced;
        Inset(maskImage.rectTransform, 6f);
        Mask mask = maskImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform maskRect = maskImage.rectTransform;

        bannerArt = CreateImage(maskRect, "Art", Color.white, null);
        Image shade = CreateImage(maskRect, "BottomShade", new Color(0f, 0f, 0f, 0.82f), FadeSprite());
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(1f, 0.62f);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

        RectTransform chip = CreatePanel(maskRect, "ChapterChip", new Vector2(-312f, 222f), new Vector2(280f, 66f), new Color32(18, 22, 18, 220));
        bannerChip = CreateLabel(chip, "ChipLabel", string.Empty, 30f, TextAlignmentOptions.Center);
        bannerChip.color = UiTheme.Gold;

        Image medallionBezel = CreateImage(maskRect, "MedallionBezel", UiTheme.Ink, HudVisualUtility.CircleSprite());
        SetRect(medallionBezel.rectTransform, new Vector2(382f, 196f), new Vector2(124f, 124f));
        bannerDisc = CreateImage(medallionBezel.transform, "Disc", Color.white, HudVisualUtility.CircleSprite());
        Fill(bannerDisc.rectTransform, 0.09f);
        Image medallionGloss = CreateImage(medallionBezel.transform, "Gloss", new Color(1f, 1f, 1f, 0.2f), HudVisualUtility.CircleSprite());
        medallionGloss.rectTransform.anchorMin = new Vector2(0.24f, 0.54f);
        medallionGloss.rectTransform.anchorMax = new Vector2(0.76f, 0.86f);
        medallionGloss.rectTransform.offsetMin = medallionGloss.rectTransform.offsetMax = Vector2.zero;
        bannerGlyph = CreateImage(medallionBezel.transform, "Glyph", new Color32(255, 248, 230, 255), null);
        Fill(bannerGlyph.rectTransform, 0.24f);

        bannerTitle = CreateLabel(maskRect, "ChapterTitle", string.Empty, 64f, TextAlignmentOptions.Left);
        SetRect(bannerTitle.rectTransform, new Vector2(-30f, -162f), new Vector2(860f, 88f));
        bannerTagline = CreateLabel(maskRect, "ChapterTagline", string.Empty, 30f, TextAlignmentOptions.Left);
        bannerTagline.color = UiTheme.MutedText;
        SetRect(bannerTagline.rectTransform, new Vector2(-30f, -222f), new Vector2(860f, 44f));
        bannerProgress = CreateLabel(maskRect, "ChapterProgress", string.Empty, 28f, TextAlignmentOptions.Right);
        bannerProgress.color = UiTheme.Gold;
        SetRect(bannerProgress.rectTransform, new Vector2(250f, -98f), new Vector2(400f, 44f));

        bannerLock = CreateImage(maskRect, "LockOverlay", new Color(0.03f, 0.03f, 0.04f, 0.76f), null).gameObject;
        Stretch(bannerLock.GetComponent<RectTransform>());
        CreateIcon(bannerLock.transform, "LockIcon", UiIcon.Lock, UiTheme.MutedText, new Vector2(0f, 70f), 120f);
        bannerLockLabel = CreateLabel(bannerLock.transform, "LockLabel", string.Empty, 36f, TextAlignmentOptions.Center);
        SetRect(bannerLockLabel.rectTransform, new Vector2(0f, -60f), new Vector2(820f, 120f));

        previousChapterButton = CreateRoundIconButton(chapterGroup, "PreviousChapter", UiIcon.ChevronLeft, new Vector2(-486f, 470f), 98f, ChangeChapterBackward);
        nextChapterButton = CreateRoundIconButton(chapterGroup, "NextChapter", UiIcon.ChevronRight, new Vector2(486f, 470f), 98f, ChangeChapterForward);

        chapterDots = new Image[GetChapterCount()];
        const float dotSpacing = 46f;
        float dotStart = -dotSpacing * (chapterDots.Length - 1) * 0.5f;
        for (int index = 0; index < chapterDots.Length; index++)
        {
            Image dot = CreateImage(chapterGroup, "ChapterDot" + index, UiTheme.Locked, HudVisualUtility.CircleSprite());
            SetRect(dot.rectTransform, new Vector2(dotStart + index * dotSpacing, 160f), new Vector2(22f, 22f));
            chapterDots[index] = dot;
        }

        // ---- The chapter's stage path (its own group, so it keeps its shape on tall screens).
        pathGroup = CreateGroup(chapterGroup, "StagePath");
        for (int slot = 0; slot < StagesPerChapter - 1; slot++)
        {
            Vector2 from = NodePosition(slot), to = NodePosition(slot + 1);
            Image segment = CreateImage(pathGroup, "Path" + slot, ClosedPath, HudVisualUtility.RoundedSprite());
            segment.type = Image.Type.Sliced;
            segment.pixelsPerUnitMultiplier = 3f;
            RectTransform rect = segment.rectTransform;
            SetRect(rect, (from + to) * 0.5f, new Vector2(Vector2.Distance(from, to), 18f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            HudVisualUtility.AddOutline(segment.gameObject, UiTheme.Ink, new Vector2(2f, -2f));
            pathSegments[slot] = segment;
        }

        for (int slot = 0; slot < StagesPerChapter; slot++)
            nodes[slot] = BuildStageNode(slot);

        stageTitle = CreateLabel(chapterGroup, "SelectedStage", string.Empty, 46f, TextAlignmentOptions.Center);
        SetRect(stageTitle.rectTransform, new Vector2(0f, -176f), new Vector2(980f, 64f));
        stageAdvice = CreateLabel(chapterGroup, "StageAdvice", string.Empty, 27f, TextAlignmentOptions.Center);
        stageAdvice.color = GoodGreen;
        SetRect(stageAdvice.rectTransform, new Vector2(0f, -250f), new Vector2(940f, 76f));

        // ---- Boss and rescue tiles.
        RectTransform bossTile = CreatePanel(chapterGroup, "BossTile", new Vector2(-243f, -408f), new Vector2(466f, 196f), UiTheme.PanelDark);
        CreateTileHeader(bossTile, "BOSS", BossRed);
        bossPortrait = CreatePortrait(bossTile, "BossPortrait", new Vector2(-148f, -12f), 132f, out _);
        bossName = CreateLabel(bossTile, "BossName", string.Empty, 36f, TextAlignmentOptions.Left);
        SetRect(bossName.rectTransform, new Vector2(70f, 20f), new Vector2(290f, 52f));
        bossStatus = CreateLabel(bossTile, "BossStatus", string.Empty, 24f, TextAlignmentOptions.TopLeft);
        bossStatus.color = UiTheme.MutedText;
        SetRect(bossStatus.rectTransform, new Vector2(70f, -46f), new Vector2(290f, 74f));

        RectTransform rescueTile = CreatePanel(chapterGroup, "RescueTile", new Vector2(243f, -408f), new Vector2(466f, 196f), UiTheme.PanelDark);
        CreateTileHeader(rescueTile, "RESCUE", GoodGreen);
        rescuePortrait = CreatePortrait(rescueTile, "RescuePortrait", new Vector2(-148f, -12f), 132f, out rescueInitial);
        rescueCrown = CreateIcon(rescuePortrait.transform.parent, "FinaleCrown", UiIcon.Crown, UiTheme.Gold, Vector2.zero, 74f);
        rescueName = CreateLabel(rescueTile, "RescueName", string.Empty, 36f, TextAlignmentOptions.Left);
        SetRect(rescueName.rectTransform, new Vector2(70f, 20f), new Vector2(290f, 52f));
        rescueStatus = CreateLabel(rescueTile, "RescueStatus", string.Empty, 24f, TextAlignmentOptions.TopLeft);
        rescueStatus.color = UiTheme.MutedText;
        SetRect(rescueStatus.rectTransform, new Vector2(70f, -46f), new Vector2(290f, 74f));

        // ---- PLAY.
        playGlow = CreateImage(chapterGroup, "PlayGlow", new Color(1f, 0.8f, 0.35f, 0f), HudVisualUtility.GlowSprite());
        SetRect(playGlow.rectTransform, new Vector2(0f, -612f), new Vector2(820f, 300f));
        playButton = CreateTextButton(chapterGroup, "PlayButton", "PLAY", new Vector2(0f, -612f), new Vector2(660f, 140f),
            UiButtonRole.Primary, PlaySelectedStage, 50f);
        Image playIcon = CreateIcon(playButton.transform, "PlayIcon", UiIcon.Play, UiTheme.Cream, Vector2.zero, 58f);
        RectTransform playIconRect = playIcon.rectTransform;
        playIconRect.anchorMin = playIconRect.anchorMax = new Vector2(0f, 0.5f);
        playIconRect.anchoredPosition = new Vector2(70f, 0f);
        TMP_Text playLabel = UiTheme.GetOrCreateLabel(playButton);
        playLabel.rectTransform.offsetMin = new Vector2(110f, 8f);
        playLabel.rectTransform.offsetMax = new Vector2(-40f, -8f);
    }

    private StageNode BuildStageNode(int slot)
    {
        StageNode node = new StageNode();
        GameObject nodeObject = new GameObject("StageNode" + slot, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        node.rect = nodeObject.GetComponent<RectTransform>();
        node.rect.SetParent(pathGroup, false);
        SetRect(node.rect, NodePosition(slot), new Vector2(NodeSize, NodeSize));
        Image bezel = nodeObject.GetComponent<Image>();
        bezel.sprite = HudVisualUtility.CircleSprite();
        bezel.color = UiTheme.Ink;
        Shadow shadow = UiTheme.GetOrAddEffect<Shadow>(nodeObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -7f);

        node.glow = CreateImage(node.rect, "Glow", new Color(1f, 0.86f, 0.4f, 0f), HudVisualUtility.GlowSprite());
        Fill(node.glow.rectTransform, -0.42f);
        node.glow.transform.SetAsFirstSibling();
        node.face = CreateImage(node.rect, "Face", UiTheme.Teal, HudVisualUtility.CircleSprite());
        Fill(node.face.rectTransform, 0.08f);
        // A small shine near the rim, clear of the stage number.
        node.gloss = CreateImage(node.rect, "Gloss", new Color(1f, 1f, 1f, 0.14f), HudVisualUtility.CircleSprite());
        node.gloss.rectTransform.anchorMin = new Vector2(0.3f, 0.7f);
        node.gloss.rectTransform.anchorMax = new Vector2(0.7f, 0.88f);
        node.gloss.rectTransform.offsetMin = node.gloss.rectTransform.offsetMax = Vector2.zero;
        node.number = CreateLabel(node.rect, "Number", string.Empty, 60f, TextAlignmentOptions.Center);
        node.lockIcon = CreateImage(node.rect, "Lock", new Color32(222, 214, 190, 235), UiIcons.Get(UiIcon.Lock));
        node.lockIcon.preserveAspect = true;
        Fill(node.lockIcon.rectTransform, 0.27f);
        Shadow lockShadow = UiTheme.GetOrAddEffect<Shadow>(node.lockIcon.gameObject);
        lockShadow.effectColor = new Color32(20, 24, 20, 200);
        lockShadow.effectDistance = new Vector2(0f, -3f);
        node.ring = CreateImage(node.rect, "SelectedRing", UiTheme.Gold, HudVisualUtility.RingSprite());
        Fill(node.ring.rectTransform, -0.06f);

        Image checkBack = CreateImage(node.rect, "CheckBadge", UiTheme.Green, HudVisualUtility.CircleSprite());
        RectTransform checkRect = checkBack.rectTransform;
        checkRect.anchorMin = checkRect.anchorMax = new Vector2(0.84f, 0.84f);
        checkRect.sizeDelta = new Vector2(54f, 54f);
        HudVisualUtility.AddOutline(checkBack.gameObject, UiTheme.Ink, new Vector2(3f, -3f));
        Image checkIcon = CreateImage(checkRect, "Check", UiTheme.Cream, UiIcons.Get(UiIcon.Check));
        Fill(checkIcon.rectTransform, 0.18f);
        node.check = checkBack;

        node.crown = CreateImage(node.rect, "BossCrown", UiTheme.Gold, UiIcons.Get(UiIcon.Crown));
        RectTransform crownRect = node.crown.rectTransform;
        crownRect.anchorMin = crownRect.anchorMax = new Vector2(0.5f, 1f);
        crownRect.sizeDelta = new Vector2(70f, 70f);
        crownRect.anchoredPosition = new Vector2(0f, 22f);
        Shadow crownShadow = UiTheme.GetOrAddEffect<Shadow>(node.crown.gameObject);
        crownShadow.effectColor = new Color32(43, 26, 14, 220);
        crownShadow.effectDistance = new Vector2(2f, -3f);

        // Best stars for the stage, on the node's lower rim (shown once it is cleared).
        node.stars = new Image[GameProgress.MaxStarsPerStage];
        for (int index = 0; index < node.stars.Length; index++)
        {
            Image star = CreateImage(node.rect, "Star" + index, UiTheme.Gold, UiIcons.Get(UiIcon.Star));
            star.preserveAspect = true;
            RectTransform starRect = star.rectTransform;
            starRect.anchorMin = starRect.anchorMax = new Vector2(0.5f, 0f);
            bool middle = index == 1;
            starRect.sizeDelta = middle ? new Vector2(44f, 44f) : new Vector2(36f, 36f);
            starRect.anchoredPosition = new Vector2((index - 1) * 36f, middle ? 0f : 8f);
            Outline outline = star.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Ink;
            outline.effectDistance = new Vector2(2f, -2f);
            node.stars[index] = star;
        }

        node.tag = CreateLabel(pathGroup, "StageTag" + slot, string.Empty, 24f, TextAlignmentOptions.Center);
        SetRect(node.tag.rectTransform, NodePosition(slot) + new Vector2(0f, -120f), new Vector2(210f, 38f));

        node.button = nodeObject.GetComponent<Button>();
        ConfigureButton(node.button, bezel);
        int captured = slot;
        node.button.onClick.AddListener(() => SelectStageSlot(captured));
        return node;
    }

    private void BuildUpgradeView()
    {
        launchCard = BuildUpgradeCard("LaunchPower", "LAUNCH POWER", "Launch speed +0.75 per level", UiIcon.ArrowUp,
            new Color32(47, 145, 145, 255), new Vector2(0f, 560f), BuyLaunchPower);
        cooldownCard = BuildUpgradeCard("SkillCooldown", "SKILL COOLDOWN", "Ability recharge -0.5 s per level", UiIcon.Clock,
            new Color32(122, 88, 196, 255), new Vector2(0f, 196f), BuySkillCooldown);
        BuildElementChart(upgradeGroup, new Vector2(0f, -176f));
    }

    private UpgradeCard BuildUpgradeCard(string id, string title, string description, UiIcon icon, Color iconColor,
        Vector2 center, UnityEngine.Events.UnityAction buy)
    {
        UpgradeCard card = new UpgradeCard();
        RectTransform panel = CreatePanel(upgradeGroup, id + "Card", center, new Vector2(960f, 330f), UiTheme.Panel);

        Image bezel = CreateImage(panel, id + "Medallion", UiTheme.Ink, HudVisualUtility.CircleSprite());
        SetRect(bezel.rectTransform, new Vector2(-345f, 10f), new Vector2(196f, 196f));
        Image disc = CreateImage(bezel.transform, "Disc", iconColor, HudVisualUtility.CircleSprite());
        Fill(disc.rectTransform, 0.08f);
        Image gloss = CreateImage(bezel.transform, "Gloss", new Color(1f, 1f, 1f, 0.18f), HudVisualUtility.CircleSprite());
        gloss.rectTransform.anchorMin = new Vector2(0.24f, 0.54f);
        gloss.rectTransform.anchorMax = new Vector2(0.76f, 0.86f);
        gloss.rectTransform.offsetMin = gloss.rectTransform.offsetMax = Vector2.zero;
        Image glyph = CreateImage(bezel.transform, "Glyph", UiTheme.Cream, UiIcons.Get(icon));
        Fill(glyph.rectTransform, 0.26f);

        TMP_Text titleLabel = CreateLabel(panel, id + "Title", title, 46f, TextAlignmentOptions.Left);
        SetRect(titleLabel.rectTransform, new Vector2(115f, 112f), new Vector2(660f, 62f));
        TMP_Text descriptionLabel = CreateLabel(panel, id + "Description", description, 27f, TextAlignmentOptions.Left);
        descriptionLabel.color = UiTheme.MutedText;
        SetRect(descriptionLabel.rectTransform, new Vector2(115f, 62f), new Vector2(660f, 40f));
        card.effect = CreateLabel(panel, id + "Effect", string.Empty, 28f, TextAlignmentOptions.Left);
        card.effect.color = GoodGreen;
        SetRect(card.effect.rectTransform, new Vector2(115f, 18f), new Vector2(660f, 40f));

        card.dots = new Image[GameProgress.MaxLaunchPowerLevel];
        for (int index = 0; index < card.dots.Length; index++)
        {
            Image dot = CreateImage(panel, id + "Pip" + index, UiTheme.Locked, HudVisualUtility.RoundedSprite());
            dot.type = Image.Type.Sliced;
            dot.pixelsPerUnitMultiplier = 3f;
            SetRect(dot.rectTransform, new Vector2(-196f + index * 62f, -42f), new Vector2(52f, 26f));
            card.dots[index] = dot;
        }

        card.level = CreateLabel(panel, id + "Level", string.Empty, 26f, TextAlignmentOptions.Left);
        card.level.color = UiTheme.MutedText;
        SetRect(card.level.rectTransform, new Vector2(-75f, -112f), new Vector2(440f, 44f));
        card.buy = CreateTextButton(panel, id + "Buy", string.Empty, new Vector2(250f, -110f), new Vector2(400f, 96f),
            UiButtonRole.Primary, buy, 32f);
        return card;
    }

    private void BuildBeastView()
    {
        RectTransform squadCard = CreatePanel(beastGroup, "SquadCard", new Vector2(0f, 625f), new Vector2(960f, 256f), UiTheme.Panel);
        HudVisualUtility.AddOutline(squadCard.gameObject, UiTheme.Gold, new Vector2(3f, -3f));
        TMP_Text squadTitle = CreateLabel(squadCard, "SquadTitle", "YOUR SQUAD", 38f, TextAlignmentOptions.Left);
        squadTitle.color = UiTheme.Gold;
        SetRect(squadTitle.rectTransform, new Vector2(-220f, 92f), new Vector2(480f, 52f));
        TMP_Text squadHint = CreateLabel(squadCard, "SquadHint", "Tap a slot to make it lead", 24f, TextAlignmentOptions.Right);
        squadHint.color = UiTheme.MutedText;
        SetRect(squadHint.rectTransform, new Vector2(225f, 92f), new Vector2(470f, 40f));

        for (int slot = 0; slot < squadSlots.Length; slot++)
            squadSlots[slot] = BuildSquadSlot(squadCard, slot);

        beastCards = new BeastCard[BeastRoster.AllBeasts.Length];
        for (int index = 0; index < beastCards.Length; index++)
            beastCards[index] = BuildBeastCard(index);
    }

    private SquadSlot BuildSquadSlot(RectTransform parent, int slot)
    {
        SquadSlot view = new SquadSlot();
        GameObject slotObject = new GameObject("SquadSlot" + slot, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, new Vector2(-300f + slot * 300f, -10f), new Vector2(142f, 142f));
        Image bezel = slotObject.GetComponent<Image>();
        bezel.sprite = HudVisualUtility.CircleSprite();
        bezel.color = UiTheme.Ink;
        Image face = CreateImage(rect, "Face", new Color32(30, 37, 30, 255), HudVisualUtility.CircleSprite());
        Fill(face.rectTransform, 0.06f);
        view.portrait = CreateImage(rect, "Portrait", Color.white, null);
        view.portrait.preserveAspect = true;
        Fill(view.portrait.rectTransform, 0.1f);
        view.initial = CreateLabel(rect, "Initial", string.Empty, 60f, TextAlignmentOptions.Center);
        view.plus = CreateImage(rect, "Empty", new Color(1f, 1f, 1f, 0.28f), UiIcons.Get(UiIcon.Plus));
        Fill(view.plus.rectTransform, 0.3f);
        view.ring = CreateImage(rect, "Ring", UiTheme.Gold, HudVisualUtility.RingSprite());
        Fill(view.ring.rectTransform, -0.04f);
        view.tag = CreateLabel(parent, "SlotTag" + slot, string.Empty, 24f, TextAlignmentOptions.Center);
        SetRect(view.tag.rectTransform, new Vector2(-300f + slot * 300f, -100f), new Vector2(250f, 36f));

        view.button = slotObject.GetComponent<Button>();
        ConfigureButton(view.button, bezel);
        int captured = slot;
        view.button.onClick.AddListener(() => MakeSquadLead(captured));
        return view;
    }

    private BeastCard BuildBeastCard(int index)
    {
        BeastCard card = new BeastCard();
        BeastId beast = BeastRoster.AllBeasts[index];
        GameObject cardObject = new GameObject("BeastCard" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = cardObject.GetComponent<RectTransform>();
        rect.SetParent(beastGroup, false);
        SetRect(rect, new Vector2(0f, 385f - index * 180f), new Vector2(960f, 166f));
        card.back = UiTheme.StylePanel(cardObject, UiTheme.Panel, UiTheme.PanelBorder, 3f);
        card.back.raycastTarget = true;
        card.outline = cardObject.GetComponent<Outline>();

        Image ring = CreateImage(rect, "PortraitRing", UiTheme.Ink, HudVisualUtility.CircleSprite());
        SetRect(ring.rectTransform, new Vector2(-392f, 0f), new Vector2(130f, 130f));
        Image ringFace = CreateImage(ring.transform, "Face", new Color32(30, 37, 30, 255), HudVisualUtility.CircleSprite());
        Fill(ringFace.rectTransform, 0.05f);
        card.portrait = CreateImage(ring.transform, "Portrait", Color.white, null);
        card.portrait.preserveAspect = true;
        Fill(card.portrait.rectTransform, 0.07f);
        card.initial = CreateLabel(ring.transform, "Initial", string.Empty, 56f, TextAlignmentOptions.Center);

        card.element = CreateImage(rect, "Element", Color.white, ElementVisuals.GetIcon(BeastRoster.GetElement(beast)));
        SetRect(card.element.rectTransform, new Vector2(-292f, 32f), new Vector2(48f, 48f));
        card.name = CreateLabel(rect, "Name", BeastRoster.GetName(beast).ToUpperInvariant(), 42f, TextAlignmentOptions.Left);
        SetRect(card.name.rectTransform, new Vector2(-38f, 32f), new Vector2(460f, 58f));
        card.detail = CreateLabel(rect, "Detail", string.Empty, 24f, TextAlignmentOptions.TopLeft);
        card.detail.color = UiTheme.MutedText;
        // Ends before the status pill (x 205) and wraps onto a second line instead of running under it.
        SetRect(card.detail.rectTransform, new Vector2(-62f, -34f), new Vector2(500f, 66f));

        card.pill = CreateImage(rect, "Status", UiTheme.Teal, HudVisualUtility.RoundedSprite());
        card.pill.type = Image.Type.Sliced;
        SetRect(card.pill.rectTransform, new Vector2(330f, 0f), new Vector2(250f, 84f));
        HudVisualUtility.AddOutline(card.pill.gameObject, UiTheme.Ink, new Vector2(3f, -3f));
        card.pillIcon = CreateIcon(card.pill.transform, "StatusIcon", UiIcon.Check, UiTheme.Cream, Vector2.zero, 40f);
        RectTransform iconRect = card.pillIcon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(36f, 0f);
        card.pillLabel = CreateLabel(card.pill.transform, "StatusLabel", string.Empty, 28f, TextAlignmentOptions.Center);
        card.pillLabel.rectTransform.offsetMin = new Vector2(56f, 4f);
        card.pillLabel.rectTransform.offsetMax = new Vector2(-12f, -4f);

        card.button = cardObject.GetComponent<Button>();
        ConfigureButton(card.button, card.back);
        card.button.onClick.AddListener(() => BuyOrSelectBeast(beast));
        return card;
    }

    private void BuildNotice()
    {
        noticePanel = CreatePanel(contentRoot, "NoticePanel", new Vector2(0f, -560f), new Vector2(960f, 100f), new Color32(26, 32, 26, 240));
        noticeLabel = CreateLabel(noticePanel, "Notice", string.Empty, 28f, TextAlignmentOptions.Center);
    }

    private void BuildNavBar()
    {
        RectTransform bar = CreatePanel(contentRoot, "NavBar", new Vector2(0f, -843f), new Vector2(1060f, 150f), new Color32(20, 25, 20, 245));
        string[] labels = { "MAP", "UPGRADES", "BEASTS" };
        UiIcon[] icons = { UiIcon.Flag, UiIcon.ArrowUp, UiIcon.Paw };
        UnityEngine.Events.UnityAction[] actions = { OpenChapters, OpenUpgrades, OpenBeasts };
        for (int index = 0; index < tabs.Length; index++)
        {
            NavTab tab = new NavTab();
            GameObject tabObject = new GameObject("Tab" + labels[index], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = tabObject.GetComponent<RectTransform>();
            rect.SetParent(bar, false);
            SetRect(rect, new Vector2(-346f + index * 346f, 0f), new Vector2(326f, 128f));
            tab.back = UiTheme.StylePanel(tabObject, TabIdle, UiTheme.Ink, 3f);
            tab.back.raycastTarget = true;
            tab.outline = tabObject.GetComponent<Outline>();
            tab.icon = CreateIcon(rect, "Icon", icons[index], UiTheme.MutedText, new Vector2(0f, 20f), 58f);
            tab.label = CreateLabel(rect, "Label", labels[index], 28f, TextAlignmentOptions.Center);
            SetRect(tab.label.rectTransform, new Vector2(0f, -38f), new Vector2(310f, 40f));
            tab.badge = CreateImage(rect, "Badge", UiTheme.Danger, HudVisualUtility.CircleSprite());
            RectTransform badgeRect = tab.badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.8f, 0.8f);
            badgeRect.sizeDelta = new Vector2(40f, 40f);
            HudVisualUtility.AddOutline(tab.badge.gameObject, UiTheme.Ink, new Vector2(2f, -2f));
            TMP_Text bang = CreateLabel(badgeRect, "Bang", "!", 28f, TextAlignmentOptions.Center);
            bang.margin = Vector4.zero;
            tab.badge.enabled = false;
            bang.enabled = false;
            tab.button = tabObject.GetComponent<Button>();
            ConfigureButton(tab.button, tab.back);
            tab.button.onClick.AddListener(actions[index]);
            tabs[index] = tab;
        }
    }

    // Fire > Grass > Earth > Electric > Water > (Fire).
    private static void BuildElementChart(RectTransform parent, Vector2 center)
    {
        RectTransform panel = CreatePanel(parent, "ElementChart", center, new Vector2(960f, 292f), UiTheme.PanelDark);
        TMP_Text heading = CreateLabel(panel, "Heading", "MATCHUP CHART", 34f, TextAlignmentOptions.Center);
        heading.color = UiTheme.Gold;
        SetRect(heading.rectTransform, new Vector2(0f, 108f), new Vector2(900f, 48f));
        TMP_Text caption = CreateLabel(panel, "Caption", "Each element deals DOUBLE damage to the next one", 24f, TextAlignmentOptions.Center);
        caption.color = UiTheme.MutedText;
        SetRect(caption.rectTransform, new Vector2(0f, -112f), new Vector2(900f, 38f));

        const float step = 176f;
        for (int index = 0; index < ChartOrder.Length; index++)
        {
            ElementType element = ChartOrder[index];
            float x = (index - 2) * step;
            Image icon = CreateImage(panel, "Icon" + element, Color.white, ElementVisuals.GetIcon(element));
            SetRect(icon.rectTransform, new Vector2(x, 14f), new Vector2(104f, 104f));
            TMP_Text name = CreateLabel(panel, "Name" + element, ElementVisuals.GetName(element), 23f, TextAlignmentOptions.Center);
            name.color = Color.Lerp(ElementVisuals.GetColor(element), Color.white, 0.35f);
            SetRect(name.rectTransform, new Vector2(x, -58f), new Vector2(170f, 34f));
            if (index < ChartOrder.Length - 1)
            {
                Image arrow = CreateImage(panel, "Arrow" + index, UiTheme.Gold, UiIcons.Get(UiIcon.ChevronRight));
                SetRect(arrow.rectTransform, new Vector2(x + step * 0.5f, 14f), new Vector2(40f, 40f));
            }
        }
    }

    // =====================================================================
    // REFRESH
    // =====================================================================

    private void RefreshPresentation()
    {
        if (GameProgress.Instance == null || safeRoot == null)
            return;

        GameProgress progress = GameProgress.Instance;
        chapterGroup.gameObject.SetActive(currentView == HubView.Chapters);
        upgradeGroup.gameObject.SetActive(currentView == HubView.Upgrades);
        beastGroup.gameObject.SetActive(currentView == HubView.Beasts);
        headerTitle.text = currentView == HubView.Chapters ? "WORLD MAP"
            : currentView == HubView.Upgrades ? "UPGRADES" : "BEASTS";
        coinLabel.text = progress.Coins.ToString("#,0", CultureInfo.InvariantCulture);
        int cleared = Mathf.Min(progress.HighestClearedStage, stageCount);
        string stars = "<color=#FFCC42>" + progress.TotalStars + " / " + stageCount * GameProgress.MaxStarsPerStage + " STARS</color>";
        progressLabel.text = cleared >= stageCount
            ? "<color=#FFCC42>CAMPAIGN COMPLETE!</color>  ·  " + stars
            : cleared + " / " + stageCount + " STAGES  ·  " + stars;

        RefreshChapterView();
        RefreshUpgradeView();
        RefreshBeastView();
        RefreshNotice();
        RefreshNav();
        RefreshBackdrop();
    }

    private void RefreshChapterView()
    {
        GameProgress progress = GameProgress.Instance;
        ChapterInfo info = ChapterData.Get(selectedChapter);
        int firstStage = ChapterData.FirstStage(selectedChapter);
        int lastStage = Mathf.Min(stageCount, ChapterData.LastStage(selectedChapter));
        int stagesInChapter = Mathf.Max(1, lastStage - firstStage + 1);
        int highestUnlocked = progress.HighestUnlockedStage;
        int highestCleared = progress.HighestClearedStage;
        bool chapterUnlocked = firstStage <= highestUnlocked;
        int clearedInChapter = Mathf.Clamp(highestCleared - firstStage + 1, 0, stagesInChapter);

        bannerChip.text = "CHAPTER " + (selectedChapter + 1);
        bannerTitle.text = info.title;
        bannerTagline.text = info.tagline;
        bannerProgress.text = clearedInChapter >= stagesInChapter ? "CHAPTER COMPLETE"
            : chapterUnlocked ? clearedInChapter + " / " + stagesInChapter + " CLEARED" : string.Empty;
        bannerOutline.effectColor = info.accent;
        bannerDisc.color = ElementVisuals.GetColor(info.element);
        bannerGlyph.sprite = ElementVisuals.GetGlyphSprite(info.element);
        bannerLock.SetActive(!chapterUnlocked);
        if (!chapterUnlocked)
        {
            ChapterInfo previous = ChapterData.Get(selectedChapter - 1);
            bannerLockLabel.text = "LOCKED\n<size=72%><color=#CDC4A8>Beat " + previous.boss + " in Stage " + (firstStage - 1) +
                " to enter</color></size>";
        }
        RefreshChapterArt();

        SetArrowState(previousChapterButton, selectedChapter > 0);
        SetArrowState(nextChapterButton, selectedChapter < GetChapterCount() - 1);
        for (int index = 0; index < chapterDots.Length; index++)
        {
            bool current = index == selectedChapter;
            chapterDots[index].color = current ? (Color)UiTheme.Orange
                : ChapterData.FirstStage(index) <= highestUnlocked ? (Color)UiTheme.MutedText : (Color)UiTheme.Locked;
            chapterDots[index].rectTransform.sizeDelta = current ? new Vector2(32f, 32f) : new Vector2(20f, 20f);
        }

        for (int slot = 0; slot < nodes.Length; slot++)
        {
            int stage = firstStage + slot;
            StageNode node = nodes[slot];
            bool exists = stage <= lastStage;
            node.rect.gameObject.SetActive(exists);
            node.tag.gameObject.SetActive(exists);
            if (!exists)
                continue;

            bool unlocked = stage <= highestUnlocked;
            bool cleared = stage <= highestCleared;
            bool selected = unlocked && stage == selectedStage;
            bool boss = ChapterData.IsBossStage(stage);
            float size = selected ? SelectedNodeSize : NodeSize;
            node.rect.sizeDelta = new Vector2(size, size);
            node.face.color = cleared ? (Color)UiTheme.Green
                : !unlocked ? (Color)UiTheme.Locked
                : boss ? (Color)UiTheme.Danger : (Color)UiTheme.Teal;
            node.number.text = unlocked ? stage.ToString(CultureInfo.InvariantCulture) : string.Empty;
            node.lockIcon.enabled = !unlocked;
            // The shine would sit over the padlock's shackle; locked stones stay matte.
            node.gloss.enabled = unlocked;
            int earned = cleared ? progress.GetStars(stage) : 0;
            for (int index = 0; index < node.stars.Length; index++)
            {
                node.stars[index].enabled = cleared;
                node.stars[index].color = index < earned ? (Color)UiTheme.Gold : new Color32(40, 46, 38, 255);
            }
            node.check.gameObject.SetActive(cleared);
            node.crown.enabled = boss;
            node.ring.enabled = selected;
            Color glow = node.glow.color;
            glow.a = 0f;
            node.glow.color = glow;
            node.button.interactable = unlocked;
            node.tag.text = boss ? "BOSS" : "STAGE " + stage;
            node.tag.color = boss ? (Color)BossRed : selected ? (Color)UiTheme.Gold : (Color)UiTheme.MutedText;
        }

        for (int slot = 0; slot < pathSegments.Length; slot++)
        {
            int nextStage = firstStage + slot + 1;
            pathSegments[slot].gameObject.SetActive(nextStage <= lastStage);
            pathSegments[slot].color = nextStage <= highestUnlocked ? (Color)OpenPath : (Color)ClosedPath;
        }

        bool stageAvailable = chapterUnlocked && selectedStage >= firstStage && selectedStage <= lastStage &&
            selectedStage <= highestUnlocked;
        bool bossSelected = stageAvailable && ChapterData.IsBossStage(selectedStage);
        bool selectedCleared = stageAvailable && selectedStage <= highestCleared;
        if (stageAvailable)
        {
            stageTitle.text = "STAGE " + selectedStage + "  ·  " + ChapterData.StageName(selectedStage).ToUpperInvariant();
            stageAdvice.text = bossSelected
                ? "<color=#FF967A>" + info.boss + "</color> is " + ElementVisuals.GetName(info.bossElement) +
                  ": weak to <color=#" + ElementVisuals.GetHex(ElementVisuals.GetCounter(info.bossElement)) + ">" +
                  ElementVisuals.GetName(ElementVisuals.GetCounter(info.bossElement)) +
                  "</color>. It wins at once if it reaches your barrier!"
                : info.advice;
        }
        else
        {
            stageTitle.text = chapterUnlocked ? "SELECT A STAGE" : "CHAPTER LOCKED";
            stageAdvice.text = chapterUnlocked ? info.advice : "Clear the previous chapter to open " + info.title + ".";
        }

        RefreshBossTile(info, lastStage, highestCleared, chapterUnlocked);
        RefreshRescueTile(info, highestCleared);

        playButton.interactable = stageAvailable;
        playButton.GetComponent<Image>().color = !stageAvailable ? (Color)UiTheme.Locked
            : selectedCleared ? (Color)UiTheme.Teal
            : bossSelected ? (Color)UiTheme.Danger : (Color)UiTheme.Orange;
        UiTheme.SetLabel(playButton, !stageAvailable ? (chapterUnlocked ? "SELECT A STAGE" : "LOCKED")
            : selectedCleared ? "REPLAY STAGE " + selectedStage
            : bossSelected ? "FIGHT " + info.boss
            : "PLAY STAGE " + selectedStage);
    }

    // A disabled arrow fades out entirely (its face and glyph are not the tinted graphic).
    private static void SetArrowState(Button arrow, bool enabled)
    {
        arrow.interactable = enabled;
        CanvasGroup group = arrow.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = enabled ? 1f : 0.3f;
    }

    private void RefreshBossTile(ChapterInfo info, int bossStage, int highestCleared, bool chapterUnlocked)
    {
        bool defeated = highestCleared >= bossStage;
        Sprite portrait = GetArray(bossPortraits, selectedChapter);
        // Painted Terrathorn art replaces the tinted stand-in portrait assigned for chapter 2.
        Sprite paintedTerrathorn = selectedChapter == 1 ? GameArt.Find("Boss_Terrathorn") : null;
        if (paintedTerrathorn != null)
            portrait = paintedTerrathorn;
        if (portrait == null && selectedChapter >= 2)
            portrait = PlaceholderArt.Boss(info.boss);
        bossPortrait.sprite = portrait != null ? portrait : HudVisualUtility.CircleSprite();
        Color tint = portrait == null ? (Color)UiTheme.Locked
            : selectedChapter == 1 && paintedTerrathorn == null && GetArray(bossPortraits, 1) != null
                ? (Color)TerrathornTint : Color.white;
        bossPortrait.color = chapterUnlocked ? tint : tint * new Color(0.3f, 0.3f, 0.32f, 1f);
        bossName.text = info.boss;
        ElementType counter = ElementVisuals.GetCounter(info.bossElement);
        bossStatus.text = defeated
            ? "<color=#8EE06A>DEFEATED</color>\n<size=90%>Replay Stage " + bossStage + " any time</size>"
            : "Stage " + bossStage + "\nWeak to <color=#" + ElementVisuals.GetHex(counter) + ">" + ElementVisuals.GetName(counter) + "</color>";
    }

    private void RefreshRescueTile(ChapterInfo info, int highestCleared)
    {
        if (!info.hasRescue)
        {
            rescuePortrait.sprite = HudVisualUtility.CircleSprite();
            rescuePortrait.color = new Color32(60, 52, 30, 255);
            rescueInitial.text = string.Empty;
            rescueCrown.enabled = true;
            rescueName.text = "FINALE";
            rescueStatus.text = highestCleared >= stageCount
                ? "<color=#FFCC42>The wilds are saved!</color>"
                : "Beat " + info.boss + " to save the wilds";
            return;
        }

        rescueCrown.enabled = false;
        BeastId beast = info.rescue;
        int unlockStage = BeastRoster.GetUnlockStage(beast);
        bool reached = highestCleared >= unlockStage;
        SetPortrait(rescuePortrait, rescueInitial, BeastPortrait(beast), BeastRoster.GetColor(beast),
            BeastRoster.GetName(beast).Substring(0, 1));
        if (!reached)
            rescuePortrait.color *= new Color(0.35f, 0.35f, 0.38f, 1f);
        rescueName.text = BeastRoster.GetName(beast).ToUpperInvariant();

        if (GameProgress.Instance.IsBeastUnlocked(beast))
            rescueStatus.text = "<color=#8EE06A>RECRUITED</color>\n<size=90%>" + ElementVisuals.GetName(BeastRoster.GetElement(beast)) + " Beast</size>";
        else if (reached)
            rescueStatus.text = "Rescued! Recruit in\nBEASTS: " + UiTheme.Coin(BeastRoster.GetPrice(beast));
        else
            rescueStatus.text = "Freed by beating\nStage " + unlockStage;
    }

    private void RefreshUpgradeView()
    {
        GameProgress progress = GameProgress.Instance;
        int coins = progress.Coins;
        RefreshUpgradeCard(launchCard, progress.LaunchPowerLevel, GameProgress.MaxLaunchPowerLevel,
            progress.CanUpgradeLaunchPower, progress.LaunchPowerPrice, coins,
            progress.LaunchPowerLevel > 0
                ? "Now: +" + (progress.LaunchPowerLevel * 0.75f).ToString("0.##", CultureInfo.InvariantCulture) + " launch speed"
                : "Not upgraded yet");
        RefreshUpgradeCard(cooldownCard, progress.SkillCooldownLevel, GameProgress.MaxSkillCooldownLevel,
            progress.CanUpgradeSkillCooldown, progress.SkillCooldownPrice, coins,
            progress.SkillCooldownLevel > 0
                ? "Now: abilities recharge " + (progress.SkillCooldownLevel * 0.5f).ToString("0.#", CultureInfo.InvariantCulture) + " s sooner"
                : "Not upgraded yet");
    }

    private static void RefreshUpgradeCard(UpgradeCard card, int level, int maxLevel, bool canUpgrade, int price, int coins,
        string effect)
    {
        card.effect.text = effect;
        card.level.text = "LEVEL " + level + " / " + maxLevel;
        for (int index = 0; index < card.dots.Length; index++)
            card.dots[index].color = index < level ? (Color)UiTheme.Green : (Color)UiTheme.Locked;

        bool affordable = coins >= price;
        // Unaffordable upgrades stay tappable so the shop can explain why the purchase failed.
        card.buy.interactable = canUpgrade;
        card.buy.GetComponent<Image>().color = !canUpgrade ? (Color)UiTheme.Green
            : affordable ? (Color)UiTheme.Orange : (Color)UiTheme.Panel;
        UiTheme.SetLabel(card.buy, !canUpgrade ? "MAX LEVEL"
            : affordable ? "UPGRADE  " + UiTheme.Coin(price)
            : "<color=#E6A07A>NEED</color>  " + UiTheme.Coin(price));
    }

    private void RefreshBeastView()
    {
        GameProgress progress = GameProgress.Instance;
        for (int slot = 0; slot < squadSlots.Length; slot++)
        {
            SquadSlot view = squadSlots[slot];
            bool filled = slot < progress.Squad.Count;
            view.plus.enabled = !filled;
            view.ring.enabled = filled && slot == 0;
            view.portrait.enabled = filled;
            if (filled)
            {
                BeastId beast = progress.Squad[slot];
                SetPortrait(view.portrait, view.initial, BeastPortrait(beast), BeastRoster.GetColor(beast),
                    BeastRoster.GetName(beast).Substring(0, 1));
                view.tag.text = slot == 0 ? "<color=#FFCC42>LEAD</color>  " + BeastRoster.GetName(beast).ToUpperInvariant()
                    : BeastRoster.GetName(beast).ToUpperInvariant();
            }
            else
            {
                view.initial.text = string.Empty;
                view.tag.text = "<color=#7F8A78>EMPTY</color>";
            }
        }

        int coins = progress.Coins;
        for (int index = 0; index < beastCards.Length; index++)
        {
            BeastId beast = BeastRoster.AllBeasts[index];
            BeastCard card = beastCards[index];
            bool owned = progress.IsBeastUnlocked(beast);
            bool reached = progress.HighestClearedStage >= BeastRoster.GetUnlockStage(beast);
            bool inSquad = owned && progress.IsInSquad(beast);
            bool affordable = coins >= BeastRoster.GetPrice(beast);

            SetPortrait(card.portrait, card.initial, BeastPortrait(beast), BeastRoster.GetColor(beast),
                BeastRoster.GetName(beast).Substring(0, 1));
            if (!owned && !reached)
                card.portrait.color *= new Color(0.25f, 0.25f, 0.28f, 1f);

            card.back.color = inSquad ? new Color32(50, 70, 42, 255) : owned || reached ? (Color)UiTheme.Panel : (Color)UiTheme.PanelDark;
            card.outline.effectColor = inSquad ? (Color)UiTheme.Gold : reached && !owned ? (Color)UiTheme.Orange : (Color)UiTheme.Ink;
            card.outline.effectDistance = inSquad ? new Vector2(5f, -5f) : new Vector2(3f, -3f);
            card.detail.text = "<color=#" + ElementVisuals.GetHex(BeastRoster.GetElement(beast)) + ">" +
                ElementVisuals.GetName(BeastRoster.GetElement(beast)) + "</color>  ·  " + AbilityBlurb(beast);

            if (inSquad)
                SetPill(card, UiTheme.Green, UiIcon.Check, "IN SQUAD");
            else if (owned)
                SetPill(card, UiTheme.Teal, UiIcon.Plus, "ADD");
            else if (reached)
                SetPill(card, affordable ? (Color)UiTheme.Orange : (Color)UiTheme.Panel, UiIcon.Coin,
                    (affordable ? "" : "<color=#E6A07A>") + BeastRoster.GetPrice(beast) + (affordable ? "" : "</color>"));
            else
                SetPill(card, UiTheme.Locked, UiIcon.Lock, "STAGE " + BeastRoster.GetUnlockStage(beast));
        }
    }

    private static void SetPill(BeastCard card, Color color, UiIcon icon, string label)
    {
        card.pill.color = color;
        card.pillIcon.sprite = UiIcons.Get(icon);
        card.pillIcon.color = icon == UiIcon.Coin ? Color.white : (Color)UiTheme.Cream;
        card.pillLabel.text = label;
    }

    private void RefreshNotice()
    {
        if (noticePanel == null)
            return;

        bool hasNotice = !string.IsNullOrEmpty(shopNotice) && Time.unscaledTime < shopNoticeUntil;
        string text = hasNotice ? shopNotice
            : currentView == HubView.Upgrades ? "Upgrades apply to every Beast in your squad."
            : currentView == HubView.Beasts ? "Tap a Beast to add or remove it. Up to 3 Beasts; swap them at the launch pad."
            : null;
        noticePanel.gameObject.SetActive(text != null);
        if (text != null)
        {
            noticeLabel.text = text;
            noticeLabel.color = hasNotice ? (Color)UiTheme.Gold : (Color)UiTheme.MutedText;
            // On the map the notice sits in the gap between PLAY and the tab bar.
            bool map = currentView == HubView.Chapters;
            SetRect(noticePanel, new Vector2(0f, (map ? -726f : -560f) * layoutStretch), new Vector2(960f, map ? 78f : 100f));
        }
    }

    private void RefreshNav()
    {
        for (int index = 0; index < tabs.Length; index++)
        {
            NavTab tab = tabs[index];
            bool active = (int)currentView == index;
            tab.back.color = active ? (Color)UiTheme.Orange : (Color)TabIdle;
            tab.outline.effectColor = active ? (Color)UiTheme.Gold : (Color)UiTheme.Ink;
            tab.icon.color = active ? (Color)UiTheme.Cream : (Color)UiTheme.MutedText;
            tab.label.color = active ? (Color)UiTheme.Cream : (Color)UiTheme.MutedText;
        }

        // A rescued Beast waiting to be recruited.
        bool beastWaiting = false;
        GameProgress progress = GameProgress.Instance;
        foreach (BeastId beast in BeastRoster.AllBeasts)
        {
            if (!progress.IsBeastUnlocked(beast) && progress.HighestClearedStage >= BeastRoster.GetUnlockStage(beast))
                beastWaiting = true;
        }
        NavTab beastsTab = tabs[2];
        beastsTab.badge.enabled = beastWaiting;
        TMP_Text bang = beastsTab.badge.GetComponentInChildren<TMP_Text>(true);
        if (bang != null)
            bang.enabled = beastWaiting;
        if (!beastWaiting)
            beastsTab.badge.rectTransform.localScale = Vector3.one;
    }

    private void RefreshChapterArt()
    {
        if (lastArtChapter == selectedChapter)
            return;

        lastArtChapter = selectedChapter;
        Sprite art = ChapterArt(selectedChapter, 384, 224);
        bannerArt.sprite = art;
        bannerArt.color = Color.white;
        AspectFill(bannerArt.rectTransform, art, new Vector2(948f, 548f));
    }

    private void RefreshBackdrop()
    {
        if (screenBackdrop == null)
            return;

        // The whole screen takes on the selected chapter's environment, dimmed so cards stay readable.
        screenBackdrop.sprite = ChapterArt(selectedChapter, 192, 340);
        float dim = currentView == HubView.Chapters ? 0.42f : 0.26f;
        screenBackdrop.color = new Color(dim, dim, dim, 1f);
        FitBackdrop();
    }

    private Sprite ChapterArt(int chapter, int width, int height)
    {
        Sprite art = GetArray(chapterArtwork, chapter);
        return art != null ? art : ProceduralBackdrop.GetSprite(ChapterData.Get(chapter).theme, 0, width, height, 100f);
    }

    private void FitBackdrop()
    {
        RectTransform canvasRect = hubCanvas != null ? hubCanvas.transform as RectTransform : null;
        if (screenBackdrop == null || canvasRect == null)
            return;

        AspectFill(screenBackdrop.rectTransform, screenBackdrop.sprite, canvasRect.rect.size);
    }

    // Painted portrait if assigned; otherwise the Beast's generated placeholder.
    private Sprite BeastPortrait(BeastId beast)
    {
        Sprite assigned = GetArray(beastPortraits, (int)beast);
        if (assigned != null)
            return assigned;
        return PlaceholderArt.HasPaintedArt(beast) ? null : PlaceholderArt.BeastIdle(beast);
    }

    private static string AbilityBlurb(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return "Bramble Snare roots nearby foes";
            case BeastId.Mossback: return "Heavy Knock smashes foes back";
            case BeastId.Voltis: return "Chain Zap leaps across 3 foes";
            case BeastId.Ripple: return "Splash Wave soaks every foe near";
            default: return "Flame Burst blasts and bounces";
        }
    }

    // =====================================================================
    // NEW CHAPTER INTRO
    // =====================================================================

    private void ShowNewChapterIntroIfNeeded()
    {
        int unlockedChapter = Mathf.Clamp(
            (Mathf.Min(GameProgress.Instance.HighestUnlockedStage, stageCount) - 1) / StagesPerChapter,
            0, GetChapterCount() - 1);
        int seenChapter = PlayerPrefs.GetInt(SeenChapterKey, 0);
        PlayerPrefs.SetInt(SeenChapterKey, Mathf.Max(seenChapter, unlockedChapter));
        PlayerPrefs.Save();
        if (unlockedChapter <= seenChapter || unlockedChapter == 0)
            return;

        selectedChapter = unlockedChapter;
        selectedStage = Mathf.Clamp(GameProgress.Instance.HighestUnlockedStage,
            ChapterData.FirstStage(unlockedChapter), Mathf.Min(stageCount, ChapterData.LastStage(unlockedChapter)));
        lastArtChapter = -1;
        RefreshPresentation();

        ChapterInfo info = ChapterData.Get(unlockedChapter);
        GameObject overlay = new GameObject("NewChapterIntro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(safeRoot.parent, false);
        Stretch(overlayRect);
        overlayRect.SetAsLastSibling();
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0.03f, 0.03f, 0.03f, 0.82f);
        dim.raycastTarget = true;

        // Same safe-area + scaled-content structure as the hub itself.
        GameObject safeObject = new GameObject("SafeArea", typeof(RectTransform));
        RectTransform safeFrame = safeObject.GetComponent<RectTransform>();
        safeFrame.SetParent(overlayRect, false);
        safeFrame.anchorMin = safeRoot.anchorMin;
        safeFrame.anchorMax = safeRoot.anchorMax;
        safeFrame.offsetMin = Vector2.zero;
        safeFrame.offsetMax = Vector2.zero;

        GameObject frameObject = new GameObject("Frame", typeof(RectTransform));
        RectTransform frame = frameObject.GetComponent<RectTransform>();
        frame.SetParent(safeFrame, false);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = new Vector2(ContentWidth, ContentHeight);
        frame.localScale = contentRoot.localScale;
        frame.anchoredPosition = Vector2.zero;

        RectTransform card = CreatePanel(frame, "IntroCard", new Vector2(0f, 30f), new Vector2(920f, 1180f), UiTheme.Panel);
        HudVisualUtility.AddOutline(card.gameObject, info.accent, new Vector2(5f, -5f));
        card.gameObject.AddComponent<UiPopIn>();

        TMP_Text eyebrow = CreateLabel(card, "Eyebrow", "NEW CHAPTER UNLOCKED", 36f, TextAlignmentOptions.Center);
        eyebrow.color = UiTheme.Gold;
        SetRect(eyebrow.rectTransform, new Vector2(0f, 525f), new Vector2(840f, 56f));

        RectTransform artFrame = CreatePanel(card, "IntroArt", new Vector2(0f, 250f), new Vector2(840f, 460f), UiTheme.PanelDark);
        artFrame.gameObject.AddComponent<RectMask2D>();
        Sprite art = ChapterArt(unlockedChapter, 384, 210);
        Image artImage = CreateImage(artFrame, "Art", Color.white, art);
        AspectFill(artImage.rectTransform, art, new Vector2(840f, 460f));
        Image medallion = CreateImage(artFrame, "Medallion", UiTheme.Ink, HudVisualUtility.CircleSprite());
        SetRect(medallion.rectTransform, new Vector2(330f, 150f), new Vector2(130f, 130f));
        Image disc = CreateImage(medallion.transform, "Disc", ElementVisuals.GetColor(info.element), HudVisualUtility.CircleSprite());
        Fill(disc.rectTransform, 0.09f);
        Image glyph = CreateImage(medallion.transform, "Glyph", new Color32(255, 248, 230, 255), ElementVisuals.GetGlyphSprite(info.element));
        Fill(glyph.rectTransform, 0.24f);

        TMP_Text chapterNumber = CreateLabel(card, "ChapterNumber", "CHAPTER " + (unlockedChapter + 1), 32f, TextAlignmentOptions.Center);
        chapterNumber.color = UiTheme.MutedText;
        SetRect(chapterNumber.rectTransform, new Vector2(0f, -22f), new Vector2(840f, 46f));
        TMP_Text name = CreateLabel(card, "ChapterName", info.title, 66f, TextAlignmentOptions.Center);
        SetRect(name.rectTransform, new Vector2(0f, -86f), new Vector2(860f, 90f));
        TMP_Text tagline = CreateLabel(card, "Tagline",
            info.tagline + "\n<size=85%><color=#CDC4A8>Stages " + ChapterData.FirstStage(unlockedChapter) + "-" +
            Mathf.Min(stageCount, ChapterData.LastStage(unlockedChapter)) + "   ·   Boss: " + info.boss + "</color></size>",
            32f, TextAlignmentOptions.Center);
        SetRect(tagline.rectTransform, new Vector2(0f, -186f), new Vector2(840f, 110f));

        BeastId waitingBeast = BeastRoster.AllBeasts[Mathf.Clamp(unlockedChapter, 0, BeastRoster.AllBeasts.Length - 1)];
        bool recruitWaiting = !GameProgress.Instance.IsBeastUnlocked(waitingBeast);
        string hint = recruitWaiting
            ? BeastRoster.GetName(waitingBeast) + " is waiting in BEASTS - recruit for " + UiTheme.Coin(BeastRoster.GetPrice(waitingBeast))
            : info.advice;
        TMP_Text hintLabel = CreateLabel(card, "Hint", hint, 28f, TextAlignmentOptions.Center);
        hintLabel.color = GoodGreen;
        SetRect(hintLabel.rectTransform, new Vector2(0f, -300f), new Vector2(840f, 100f));

        Button explore = CreateTextButton(card, "Explore", "EXPLORE", new Vector2(0f, -460f), new Vector2(560f, 128f),
            UiButtonRole.Primary, () => Destroy(overlay), 44f);
        if (recruitWaiting)
        {
            SetRect(explore.GetComponent<RectTransform>(), new Vector2(-160f, -460f), new Vector2(400f, 128f));
            CreateTextButton(card, "OpenBeastsFromIntro", "BEASTS", new Vector2(235f, -460f), new Vector2(300f, 128f),
                UiButtonRole.Secondary, () => { Destroy(overlay); OpenBeasts(); }, 40f);
        }
    }

    // =====================================================================
    // NAVIGATION
    // =====================================================================

    private void SelectStageSlot(int slot)
    {
        int stageNumber = ChapterData.FirstStage(selectedChapter) + slot;
        if (GameProgress.Instance == null || stageNumber > GameProgress.Instance.HighestUnlockedStage)
            return;

        selectedStage = stageNumber;
        RefreshPresentation();
    }

    private void PlaySelectedStage()
    {
        LoadStage(selectedStage);
    }

    private void ChangeChapterBackward()
    {
        ChangeChapter(-1);
    }

    private void ChangeChapterForward()
    {
        ChangeChapter(1);
    }

    private void ChangeChapter(int direction)
    {
        selectedChapter = Mathf.Clamp(selectedChapter + direction, 0, GetChapterCount() - 1);
        int firstStage = ChapterData.FirstStage(selectedChapter);
        int lastStage = Mathf.Min(stageCount, ChapterData.LastStage(selectedChapter));
        selectedStage = Mathf.Clamp(GameProgress.Instance.HighestUnlockedStage, firstStage, lastStage);
        RefreshPresentation();
    }

    private void OpenChapters()
    {
        currentView = HubView.Chapters;
        RefreshPresentation();
    }

    private void OpenUpgrades()
    {
        currentView = HubView.Upgrades;
        RefreshPresentation();
    }

    private void OpenBeasts()
    {
        currentView = HubView.Beasts;
        RefreshPresentation();
    }

    private void ShowDenied(string message)
    {
        GameAudio.Play(Sfx.Denied);
        ShowShopNotice(message);
    }

    private void ShowBought(string message)
    {
        GameAudio.Play(Sfx.Purchase);
        ShowShopNotice(message);
    }

    private void ShowSwapped(string message)
    {
        GameAudio.Play(Sfx.Swap);
        ShowShopNotice(message);
    }

    private void ShowShopNotice(string message)
    {
        shopNotice = message;
        shopNoticeUntil = Time.unscaledTime + 2.6f;
        RefreshNotice();
    }

    // =====================================================================
    // CHAPTER DATA
    // =====================================================================

    private int GetChapterCount()
    {
        return Mathf.Clamp((stageCount + StagesPerChapter - 1) / StagesPerChapter, 1, ChapterData.ChapterCount);
    }

    private static Vector2 NodePosition(int slot)
    {
        return new Vector2(NodeX[slot], PathY + NodeY[slot]);
    }

    private static Sprite GetArray(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    // =====================================================================
    // LAYOUT HELPERS
    // =====================================================================

    private void ApplySafeArea()
    {
        if (safeRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        lastSafeArea = Screen.safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        safeRoot.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;

        RectTransform canvasRect = hubCanvas.transform as RectTransform;
        lastCanvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (contentRoot == null || canvasRect == null)
            return;

        // Fit the fixed portrait layout inside the safe area on any aspect ratio.
        Vector2 safeSize = new Vector2(
            canvasRect.rect.width * (safeRoot.anchorMax.x - safeRoot.anchorMin.x),
            canvasRect.rect.height * (safeRoot.anchorMax.y - safeRoot.anchorMin.y));
        float scale = Mathf.Min(1f, safeSize.x / ContentWidth, safeSize.y / ContentHeight);
        if (scale <= 0f)
            scale = 1f;
        contentRoot.localScale = new Vector3(scale, scale, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        layoutStretch = Mathf.Clamp(safeSize.y / scale / ContentHeight, 1f, MaxLayoutStretch);
        ApplyLayoutStretch();
        FitBackdrop();
    }

    private static void AspectFill(RectTransform rect, Sprite sprite, Vector2 frameSize)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        if (sprite == null)
        {
            rect.sizeDelta = frameSize;
            return;
        }

        float spriteAspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        float frameAspect = frameSize.x / Mathf.Max(1f, frameSize.y);
        rect.sizeDelta = spriteAspect > frameAspect
            ? new Vector2(frameSize.y * spriteAspect, frameSize.y)
            : new Vector2(frameSize.x, frameSize.x / spriteAspect);
    }

    // Transparent at the top, opaque at the bottom (tint it with Image.color).
    private static Sprite FadeSprite()
    {
        if (fadeSprite != null)
            return fadeSprite;

        const int height = 64;
        Texture2D texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < height; y++)
        {
            float t = 1f - y / (float)(height - 1);
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
        }
        texture.Apply();
        texture.name = "Hub Fade";
        fadeSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), 100f);
        fadeSprite.name = "Hub Fade";
        return fadeSprite;
    }

    private static RectTransform CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name, typeof(RectTransform));
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);
        return rect;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        SetRect(rect, position, size);
        Image image = UiTheme.StylePanel(panel, color, UiTheme.PanelBorder, 3f);
        image.raycastTarget = false;
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateIcon(Transform parent, string name, UiIcon icon, Color color, Vector2 position, float size)
    {
        Image image = CreateImage(parent, name, color, UiIcons.Get(icon));
        image.preserveAspect = true;
        SetRect(image.rectTransform, position, new Vector2(size, size));
        return image;
    }

    private static void CreateTileHeader(RectTransform tile, string text, Color color)
    {
        TMP_Text header = CreateLabel(tile, "Header", text, 24f, TextAlignmentOptions.Left);
        header.color = color;
        SetRect(header.rectTransform, new Vector2(70f, 72f), new Vector2(290f, 36f));
    }

    private static Image CreatePortrait(Transform parent, string name, Vector2 position, float size, out TMP_Text initial)
    {
        Image ring = CreateImage(parent, name + "Ring", UiTheme.Ink, HudVisualUtility.CircleSprite());
        SetRect(ring.rectTransform, position, new Vector2(size, size));
        Image face = CreateImage(ring.transform, "Face", new Color32(30, 37, 30, 255), HudVisualUtility.CircleSprite());
        Fill(face.rectTransform, 0.05f);

        Image portrait = CreateImage(ring.transform, name, Color.white, HudVisualUtility.CircleSprite());
        portrait.preserveAspect = true;
        Fill(portrait.rectTransform, 0.07f);

        initial = CreateLabel(ring.transform, name + "Initial", string.Empty, size * 0.42f, TextAlignmentOptions.Center);
        return portrait;
    }

    private static void SetPortrait(Image portrait, TMP_Text initial, Sprite sprite, Color fallbackColor, string fallbackInitial)
    {
        if (portrait == null)
            return;

        portrait.sprite = sprite != null ? sprite : HudVisualUtility.CircleSprite();
        portrait.color = sprite != null ? Color.white : fallbackColor;
        if (initial != null)
            initial.text = sprite != null ? string.Empty : fallbackInitial;
    }

    private static TMP_Text CreateLabel(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        UiTheme.StyleLabel(label, fontSize, alignment);
        // Readable floor: auto-size never shrinks text below ~70% (and never under 20).
        label.fontSizeMin = Mathf.Min(fontSize, Mathf.Max(20f, fontSize * 0.7f));
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        label.margin = new Vector4(6f, 2f, 6f, 2f);
        Stretch(label.rectTransform);
        return label;
    }

    private static Button CreateTextButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
        UiButtonRole role, UnityEngine.Events.UnityAction onClick, float fontSize)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, size);
        Button button = buttonObject.GetComponent<Button>();
        UiTheme.StyleButton(button, role, false, fontSize);
        UiTheme.SetLabel(button, label);
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    private static Button CreateRoundIconButton(Transform parent, string name, UiIcon icon, Vector2 position, float size,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, new Vector2(size, size));
        Image bezel = buttonObject.GetComponent<Image>();
        bezel.sprite = HudVisualUtility.CircleSprite();
        bezel.color = UiTheme.Ink;
        Image face = CreateImage(buttonObject.transform, "Face", UiTheme.Teal, HudVisualUtility.CircleSprite());
        Fill(face.rectTransform, 0.08f);
        Image glyph = CreateImage(buttonObject.transform, "Glyph", UiTheme.Cream, UiIcons.Get(icon));
        Fill(glyph.rectTransform, 0.26f);
        Shadow shadow = UiTheme.GetOrAddEffect<Shadow>(buttonObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -6f);
        buttonObject.AddComponent<CanvasGroup>();
        Button button = buttonObject.GetComponent<Button>();
        ConfigureButton(button, bezel);
        button.onClick.AddListener(onClick);
        return button;
    }

    // Tint + press feedback for buttons whose look is built by hand (cards, nodes, tabs).
    private static void ConfigureButton(Button button, Graphic target)
    {
        button.targetGraphic = target;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 244, 222, 255);
        colors.pressedColor = new Color32(210, 204, 192, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(150, 150, 150, 255);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (button.GetComponent<UiButtonFeedback>() == null)
            button.gameObject.AddComponent<UiButtonFeedback>();
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Inset(RectTransform rect, float pixels)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(pixels, pixels);
        rect.offsetMax = new Vector2(-pixels, -pixels);
    }

    // Fills the parent with an inset given as a fraction of its size (negative = bigger).
    private static void Fill(RectTransform rect, float inset)
    {
        rect.anchorMin = new Vector2(inset, inset);
        rect.anchorMax = new Vector2(1f - inset, 1f - inset);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void HideSceneObject(string objectName)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        if (sceneObject != null)
            sceneObject.SetActive(false);
    }

    private static void HideLegacyText(Text text)
    {
        if (text != null)
            text.gameObject.SetActive(false);
    }

    private void EnsureProgress()
    {
        if (GameProgress.Instance == null)
            new GameObject("GameProgress").AddComponent<GameProgress>();
    }
}
