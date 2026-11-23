using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    public GameObject winPanel;
    public GameObject losePanel;
    public LaunchController launchController;
    public WaveManager waveManager;
    public float loseY = -5.5f;
    public int stageReward = 25;
    public int stageNumber = 1;
    public int barrierMaxHealth = 5;
    public float barrierY = -3.5f;
    public float barrierContactOffset = 0.35f;
    public TMPro.TMP_Text barrierHealthText;
    public TMPro.TMP_Text winTitleText;
    public TMPro.TMP_Text loseTitleText;

    // ChapterBuilder writes this for stages it builds from the template (0 = made by hand)
    [HideInInspector] public int stageBuildVersion;

    // the break animation (0.5 s) plus a short pause on the rubble before the lose card shows
    private const float BarrierBreakRevealSeconds = 1.1f;
    private const float ResultCardHeight = 700f;
    private const float WinCardHeight = 880f;
    private const int StarCoinReward = 10;
    private int resultStars;
    private int resultNewStars;

    private PauseMenu pauseMenu;

    private bool stageFinished = false;

    // won, lost or failed to set up: the result card is showing (or about to)
    public bool IsFinished => stageFinished;
    private bool stageTimePaused;
    private int barrierHealth;
    
    
    private BarrierVisuals barrierVisuals;
    private Image barrierFill;
    private TMP_Text barrierValueText;
    private RectTransform barrierCanvasRect;
    private RectTransform barrierPanelRect;
    private RectTransform barrierTrackRect;
    private Image barrierPanelImage;
    private Outline barrierPanelOutline;
    private Vector2 barrierPanelPosition;
    private Vector2 barrierTrackSize;
    private float targetBarrierFill;
    private float currentBarrierFill;
    private float barrierFlashUntil;
    private float barrierShakeUntil;
    private TMP_Text loseMessageText;
    private TMP_Text resultRewardText;
    private GameObject winDimmer;
    private GameObject loseDimmer;
    private RectTransform loseCardRect;
    private string pendingRewardText;
    private bool winTextStyled;
    private bool loseTextStyled;
    private float lastCanvasWidth;
    private float lastCanvasHeight;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Rect lastSafeArea;
    private bool barrierLayoutApplied;
    private bool barrierFillInitialized;

    void Awake()
    {
        GameTime.ResetBase();
        if (GameProgress.Instance == null)
            new GameObject("GameProgress").AddComponent<GameProgress>();

        if (winPanel == null)
            winPanel = FindSceneObjectByName("WinPanel");
        if (losePanel == null)
            losePanel = FindSceneObjectByName("LosePanel");

        GameObject barrierObject = GameObject.Find("Square");
        if (barrierObject != null)
        {
            Collider2D barrierCollider = barrierObject.GetComponent<Collider2D>();
            if (barrierCollider != null)
                barrierCollider.isTrigger = true;

            barrierY = barrierObject.transform.position.y;
            
            
            barrierVisuals = barrierObject.GetComponent<BarrierVisuals>();
        }

        // Settings > DIFFICULTY: a stronger barrier on EASY, a weaker one on HARD and HELL
        barrierMaxHealth = Mathf.Max(2, barrierMaxHealth + Difficulty.BarrierBonus);
        barrierHealth = barrierMaxHealth;

        if (barrierHealthText == null)
        {
            GameObject barrierTextObject = FindSceneObjectByName("BarrierHealthText");

            if (barrierTextObject != null)
                barrierHealthText = barrierTextObject.GetComponent<TMP_Text>();
        }

        if (winTitleText == null)
            winTitleText = FindNamedTmpText(winPanel, "WinTitle");

        if (loseTitleText == null)
            loseTitleText = FindNamedTmpText(losePanel, "LoseTitle");
        loseMessageText = FindNamedTmpText(losePanel, "Message");

        UpdateBarrierText();
        CreateBarrierHud();

        // only for looks: a styling problem must never stop the stage from running
        try
        {
            StyleResultPanels();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    void Start()
    {
        StageStats.BeginStage();
        pauseMenu = PauseMenu.Create(this);
        TutorialDirector.StartIfNeeded(this);
        if (barrierVisuals != null)
            barrierVisuals.ShowHealth(barrierHealth, barrierMaxHealth);
    }

    // runs every frame and checks if we won or lost the stage
    void Update()
    {
        UpdateBarrierHud();
        if (stageFinished)
            return;

        // keep the clock on the stage speed (normal, or a tutorial lesson's slow motion), so an effect
        // that got cut off can never leave the game running slow
        if ((pauseMenu == null || !pauseMenu.IsPaused) && !HitStop.IsActive && !LoadingScreen.IsLoading &&
            !Mathf.Approximately(Time.timeScale, GameTime.BaseScale))
            Time.timeScale = GameTime.BaseScale;

        if (waveManager != null)
        {
            // LOSE: an enemy reached the barrier and it broke
            if (EnemyReachedBarrier())
            {
                StageFailed();
                return;
            }

            if (waveManager.HasSpawnFailed)
            {
                StageSetupFailed();
                return;
            }

            // WIN: every wave is done and there's no boss left
            if (waveManager.IsComplete && !HasActiveBoss())
            {
                StageComplete();
                return;
            }

            return;
        }

        // (only when there's no wave manager) WIN when no enemies are left
        if (Enemy.ActiveEnemies.Count == 0)
        {
            StageComplete();
            return;
        }

        
        if (launchController != null && !launchController.unlimitedLaunches && launchController.launchesLeft <= 0)
        {
            StageFailed();
        }
    }

    static bool HasActiveBoss()
    {
        System.Collections.Generic.List<Enemy> enemies = Enemy.ActiveEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            if (enemies[index] != null && enemies[index].isBoss)
                return true;
        }

        return false;
    }

    // checks every enemy: did it reach the barrier? returns true if the barrier broke
    bool EnemyReachedBarrier()
    {
        System.Collections.Generic.List<Enemy> enemies = Enemy.ActiveEnemies;
        bool barrierDestroyed = false;

        // Destroy() only happens at the end of the frame, so the list doesn't change while we loop
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null || enemy.HasReachedBarrier)
                continue;

            Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
            float enemyBottom = enemyCollider != null
                ? enemyCollider.bounds.min.y
                : enemy.transform.position.y;

            // the bottom of the enemy touched the barrier line
            if (enemyBottom <= barrierY + barrierContactOffset)
            {
                enemy.MarkBarrierReached();
                Vector3 impact = enemy.transform.position;
                // each enemy takes 1 barrier health (a boss takes all of it)
                barrierHealth -= enemy.destroysBarrierOnReach ? barrierHealth : 1;
                UpdateBarrierText();
                barrierFlashUntil = Time.unscaledTime + 0.15f;
                barrierShakeUntil = Time.unscaledTime + 0.2f;

                if (enemy != null)
                    Destroy(enemy.gameObject);

                if (barrierHealth <= 0)
                {
                    GameAudio.Play(Sfx.BarrierBreak);
                    if (barrierVisuals != null)
                        barrierVisuals.PlayDestroyed();

                    barrierDestroyed = true;
                    break;
                }
                else
                {
                    GameAudio.Play(Sfx.BarrierHit);
                    if (barrierVisuals != null)
                    {
                        barrierVisuals.ShowHealth(barrierHealth, barrierMaxHealth);
                        barrierVisuals.PlayHit(impact);
                    }
                }
            }
        }

        return barrierDestroyed;
    }

    void UpdateBarrierText()
    {
        if (barrierHealthText != null)
            barrierHealthText.text = barrierHealth + " / " + barrierMaxHealth;
        if (barrierValueText != null)
            barrierValueText.text = barrierHealth + " / " + barrierMaxHealth;

        if (barrierFill != null && barrierMaxHealth > 0)
        {
            targetBarrierFill = Mathf.Clamp01((float)barrierHealth / barrierMaxHealth);
            if (!barrierFillInitialized)
            {
                currentBarrierFill = targetBarrierFill;
                barrierFillInitialized = true;
            }
        }
    }

    private void CreateBarrierHud()
    {
        if (barrierHealthText == null)
            return;

        RectTransform textRect = barrierHealthText.rectTransform;
        RectTransform canvasRect = textRect.parent as RectTransform;
        if (canvasRect == null)
            return;
        barrierCanvasRect = canvasRect;

        Transform panelTransform = canvasRect.Find("BarrierPanel");
        GameObject panel = panelTransform != null ? panelTransform.gameObject : new GameObject("BarrierPanel");
        panel.transform.SetParent(canvasRect, false);
        barrierPanelRect = panel.GetComponent<RectTransform>();
        if (barrierPanelRect == null)
            barrierPanelRect = panel.AddComponent<RectTransform>();

        barrierPanelImage = panel.GetComponent<Image>();
        if (barrierPanelImage == null)
            barrierPanelImage = panel.AddComponent<Image>();
        barrierPanelImage.sprite = HudVisualUtility.RoundedSprite();
        barrierPanelImage.type = Image.Type.Sliced;
        barrierPanelImage.color = new Color32(42, 51, 38, 235);
        barrierPanelImage.raycastTarget = false;
        barrierPanelOutline = HudVisualUtility.AddOutline(
            panel, new Color32(138, 143, 115, 255), new Vector2(6f, -6f));

        TMP_Text label = HudVisualUtility.AddTmpText(panel, "BarrierLabel", "BARRIER", 30f, TextAlignmentOptions.Left);
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 32f;
        label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.56f, 1f);
        label.rectTransform.offsetMin = new Vector2(20f, 0f);
        label.rectTransform.offsetMax = new Vector2(0f, -8f);

        Image track = HudVisualUtility.AddImage(panel, "BarrierTrack", new Color32(25, 18, 13, 255));
        barrierTrackRect = track.rectTransform;
        barrierTrackRect.anchorMin = Vector2.zero;
        barrierTrackRect.anchorMax = Vector2.zero;
        barrierTrackRect.pivot = Vector2.zero;
        barrierTrackSize = new Vector2(286f, 28f);
        HudVisualUtility.AddOutline(track.gameObject, new Color32(13, 14, 11, 255), new Vector2(2f, -2f));

        barrierFill = HudVisualUtility.AddImage(track.gameObject, "BarrierFill", new Color32(111, 191, 58, 255));
        barrierFill.type = Image.Type.Sliced;
        barrierFill.rectTransform.anchorMin = Vector2.zero;
        barrierFill.rectTransform.anchorMax = Vector2.zero;
        barrierFill.rectTransform.pivot = Vector2.zero;
        barrierFill.rectTransform.anchoredPosition = Vector2.zero;

        barrierValueText = HudVisualUtility.AddTmpText(panel, "BarrierValue", barrierHealth + " / " + barrierMaxHealth,
            34f, TextAlignmentOptions.Right);
        barrierValueText.enableAutoSizing = true;
        barrierValueText.fontSizeMin = 26f;
        barrierValueText.fontSizeMax = 38f;
        barrierValueText.rectTransform.anchorMin = new Vector2(0.58f, 0.5f);
        barrierValueText.rectTransform.anchorMax = Vector2.one;
        barrierValueText.rectTransform.offsetMin = new Vector2(0f, 0f);
        barrierValueText.rectTransform.offsetMax = new Vector2(-20f, -8f);

        barrierHealthText.gameObject.SetActive(false);
        UpdateBarrierText();
        RefreshBarrierHudLayout(true);
    }

    private void UpdateBarrierHud()
    {
        RefreshBarrierHudLayout(false);
        if (barrierFill != null)
        {
            currentBarrierFill = Mathf.Lerp(
                currentBarrierFill,
                targetBarrierFill,
                1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (Mathf.Abs(currentBarrierFill - targetBarrierFill) < 0.001f)
                currentBarrierFill = targetBarrierFill;

            float width = barrierTrackSize.x * currentBarrierFill;
            barrierFill.rectTransform.sizeDelta = new Vector2(width, barrierTrackSize.y);
            float ratio = targetBarrierFill;
            barrierFill.color = Time.unscaledTime < barrierFlashUntil
                ? Color.white
                : ratio > 0.6f
                    ? new Color32(111, 191, 58, 255)
                    : ratio >= 0.3f
                        ? new Color32(230, 180, 34, 255)
                        : new Color32(217, 68, 42, 255);
        }

        if (barrierPanelRect != null)
        {
            if (Time.unscaledTime < barrierShakeUntil)
            {
                float remaining = Mathf.Clamp01((barrierShakeUntil - Time.unscaledTime) / 0.2f);
                float shake = 6f * remaining;
                barrierPanelRect.anchoredPosition = barrierPanelPosition +
                    new Vector2(Mathf.Sin(Time.unscaledTime * 90f) * shake, 0f);
            }
            else
            {
                barrierPanelRect.anchoredPosition = barrierPanelPosition;
            }
        }
    }

    private void RefreshBarrierHudLayout(bool force)
    {
        if (barrierCanvasRect == null || barrierPanelRect == null || barrierTrackRect == null)
            return;

        Rect safe = Screen.safeArea;
        float canvasWidth = barrierCanvasRect.rect.width;
        float canvasHeight = barrierCanvasRect.rect.height;
        bool changed = Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            canvasWidth != lastCanvasWidth ||
            canvasHeight != lastCanvasHeight ||
            !safe.Equals(lastSafeArea);
        if (!force && barrierLayoutApplied && !changed)
            return;

        float scale = Mathf.Min(canvasWidth / 1080f, canvasHeight / 1920f);
        float widthScale = canvasWidth / Mathf.Max(1f, Screen.width);
        float heightScale = canvasHeight / Mathf.Max(1f, Screen.height);
        float safeLeft = safe.x * widthScale;
        float safeRight = (Screen.width - safe.xMax) * widthScale;
        float safeBottom = safe.y * heightScale;
        float safeTop = (Screen.height - safe.yMax) * heightScale;
        float panelWidth = Mathf.Min(340f * scale, Mathf.Max(0f, canvasWidth - safeLeft - safeRight));
        float panelHeight = Mathf.Min(110f * scale, Mathf.Max(0f, canvasHeight - safeBottom - safeTop));
        float left = Mathf.Clamp(
            Mathf.Max(48f * scale, safeLeft),
            safeLeft,
            Mathf.Max(safeLeft, canvasWidth - panelWidth - safeRight));
        float bottom = Mathf.Clamp(
            Mathf.Max(150f * scale, safeBottom),
            safeBottom,
            Mathf.Max(safeBottom, canvasHeight - panelHeight - safeTop));

        barrierPanelRect.anchorMin = Vector2.zero;
        barrierPanelRect.anchorMax = Vector2.zero;
        barrierPanelRect.pivot = Vector2.zero;
        barrierPanelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        barrierPanelPosition = new Vector2(left, bottom);
        barrierPanelRect.anchoredPosition = barrierPanelPosition;
        if (barrierPanelOutline != null)
            barrierPanelOutline.effectDistance = new Vector2(6f * scale, -6f * scale);

        barrierTrackSize = new Vector2(286f * scale, 28f * scale);
        barrierTrackRect.sizeDelta = barrierTrackSize;
        barrierTrackRect.anchoredPosition = new Vector2(27f * scale, 20f * scale);
        if (barrierFill != null)
            barrierFill.rectTransform.sizeDelta = new Vector2(
                barrierTrackSize.x * currentBarrierFill, barrierTrackSize.y);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastSafeArea = safe;
        barrierLayoutApplied = true;
    }

    // called when you win: pays the coins, works out the stars and shows the win card
    void StageComplete()
    {
        stageFinished = true;
        PauseStageTime();
        if (pauseMenu != null)
            pauseMenu.SetAvailable(false);

        bool firstClear = GameProgress.Instance != null && !GameProgress.Instance.IsStageRewarded(stageNumber);
        // save the win: unlock the next stage and pay the first clear reward
        if (GameProgress.Instance != null)
            GameProgress.Instance.CompleteStage(stageNumber, Difficulty.ScaleReward(stageReward));

        // stars (how much barrier is left) and bonus coins: every NEW star pays, and combos pay too, even on replays
        resultStars = StageStats.StarsFor(barrierHealth, barrierMaxHealth);
        resultNewStars = GameProgress.Instance != null ? GameProgress.Instance.RecordStars(stageNumber, resultStars) : 0;
        int starCoins = Difficulty.ScaleReward(resultNewStars * StarCoinReward);
        int comboCoins = Difficulty.ScaleReward(StageStats.ComboCoins);
        if (GameProgress.Instance != null)
            GameProgress.Instance.AddCoins(starCoins + comboCoins);
        string bonus = string.Empty;
        if (starCoins > 0)
            bonus += "NEW STARS " + UiTheme.Coin(starCoins);
        if (comboCoins > 0)
            bonus += (bonus.Length > 0 ? "   ·   " : string.Empty) + "COMBO BONUS " + UiTheme.Coin(comboCoins);
        if (StageStats.BestCombo >= 2)
            bonus += (bonus.Length > 0 ? "\n" : string.Empty) + "<color=#CDC4A8>Best combo  x" + StageStats.BestCombo + "</color>";
        if (resultStars < 3)
            bonus += (bonus.Length > 0 ? "\n" : string.Empty) + "<color=#CDC4A8>" +
                (resultStars == 1 ? "Keep the barrier above 60% for 2 stars" : "Take no barrier damage for 3 stars") + "</color>";

        ChapterInfo chapter = ChapterData.Get(ChapterData.ChapterOfStage(stageNumber));
        bool bossStage = ChapterData.IsBossStage(stageNumber);
        if (winPanel != null)
        {
            if (winTitleText != null)
            {
                if (stageNumber >= ChapterData.StageCount)
                    winTitleText.text = "CAMPAIGN COMPLETE!";
                else if (bossStage)
                    winTitleText.text = chapter.title + " COMPLETE";
                else
                    winTitleText.text = "STAGE " + stageNumber + " COMPLETE";
            }

            pendingRewardText = firstClear
                ? "REWARD  " + UiTheme.Coin(Difficulty.ScaleReward(stageReward)) +
                  (Difficulty.RewardMultiplier > 1f ? "  <color=#" + Difficulty.Hex(Difficulty.Current) + ">" + Difficulty.Name(Difficulty.Current) + " x" + Difficulty.RewardMultiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "</color>" : string.Empty)
                : "<color=#CDC4A8>Reward already collected</color>";
            if (bonus.Length > 0)
                pendingRewardText += "\n<size=72%>" + bonus + "</size>";
            if (stageNumber >= ChapterData.StageCount && firstClear)
                pendingRewardText += "\n<size=80%><color=#FFCC42>The wilds are safe again. Thank you, " +
                    GameSettings.PlayerName + "!</color></size>";
        }

        // first win against a chapter boss: free that chapter's Beast before the result card
        if (bossStage && chapter.hasRescue && ThistleRescueSequence.ShouldPlayFor(chapter.rescue))
        {
            ThistleRescueSequence.PlayFor(chapter.rescue, FindBeastArt(chapter.rescue), ShowWinPanel);
            return;
        }

        ShowWinPanel();
    }

    void ShowWinPanel()
    {
        GameAudio.Play(Sfx.Victory);
        if (winDimmer != null)
            winDimmer.SetActive(true);
        if (winPanel == null)
            return;

        winPanel.SetActive(true);

        // TMP labels only start once their panel is active, so we style the text here instead of
        // in Awake (styling inactive TMP text throws an error)
        if (!winTextStyled)
        {
            winTextStyled = true;
            try
            {
                RectTransform winCard = winPanel.GetComponent<RectTransform>();
                StyleResultContents(winCard, winTitleText, UiTheme.Gold);
                LayoutWinCard(winCard);
                resultRewardText = CreateResultLabel(winCard, "RewardLine", new Vector2(0f, 2f), 34f);
                resultRewardText.rectTransform.sizeDelta = new Vector2(800f, 150f);
                resultRewardText.textWrappingMode = TextWrappingModes.Normal;
                AddMapButton(winCard);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        if (resultRewardText != null && pendingRewardText != null)
            resultRewardText.text = pendingRewardText;

        RectTransform card = winPanel.GetComponent<RectTransform>();
        if (card != null && resultStars > 0)
            ResultStars.Show(card, new Vector2(0f, card.sizeDelta.y * 0.5f - 215f), resultStars, resultNewStars);
    }

    // the taller win card, top to bottom: title, stars, the scene's message ("Great job!"),
    // the reward lines, then the buttons
    void LayoutWinCard(RectTransform card)
    {
        card.sizeDelta = new Vector2(card.sizeDelta.x, Mathf.Max(card.sizeDelta.y, WinCardHeight));
        float top = card.sizeDelta.y * 0.5f;
        foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.parent != card || text.GetComponentInParent<Button>() != null || text.name == "RewardLine")
                continue;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, text == winTitleText ? top - 100f : top - 318f);
            if (text != winTitleText)
                rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 760f), 56f);
        }
    }

    // the rescued Beast's art: its painted sprite, or its placeholder if it has none yet
    Sprite FindBeastArt(BeastId beast)
    {
        LaunchController launcher = launchController != null
            ? launchController
            : FindAnyObjectByType<LaunchController>();
        if (launcher != null && launcher.beastSprites != null)
        {
            foreach (BeastSpriteSet set in launcher.beastSprites)
            {
                if (set != null && set.beast == beast && set.idle != null)
                    return set.idle;
            }
        }

        Sprite portrait = launcher != null ? launcher.GetPortraitSprite(beast) : null;
        return portrait != null ? portrait : PlaceholderArt.BeastIdle(beast);
    }

    // restyles the scene's plain looking win/lose panels to match the HUD.
    // only the looks change: button listeners, names and texts stay the same
    void StyleResultPanels()
    {
        RectTransform winCard = winPanel != null ? winPanel.GetComponent<RectTransform>() : null;
        if (winCard != null)
        {
            winCard.sizeDelta = new Vector2(Mathf.Max(winCard.sizeDelta.x, 880f), Mathf.Max(winCard.sizeDelta.y, ResultCardHeight));
            winDimmer = AddResultDimmer(winCard);
            UiTheme.StylePanel(winPanel, UiTheme.Panel, UiTheme.Gold, 5f);
            if (winPanel.GetComponent<UiPopIn>() == null)
                winPanel.AddComponent<UiPopIn>();
        }

        if (losePanel != null)
        {
            RectTransform loseRect = losePanel.GetComponent<RectTransform>();
            Image loseBackground = losePanel.GetComponent<Image>();
            bool fullScreen = loseRect != null && loseRect.anchorMin == Vector2.zero && loseRect.anchorMax == Vector2.one;
            RectTransform loseCard = loseRect;

            if (fullScreen)
            {
                // the lose panel covers the whole screen: darken it and add a card behind its content
                if (loseBackground != null)
                {
                    loseBackground.sprite = null;
                    loseBackground.color = new Color(0.04f, 0.03f, 0.03f, 0.72f);
                    loseBackground.raycastTarget = true;
                }

                Transform existingCard = loseRect.Find("ResultCard");
                GameObject cardObject = existingCard != null
                    ? existingCard.gameObject
                    : new GameObject("ResultCard", typeof(RectTransform), typeof(CanvasRenderer));
                loseCard = cardObject.GetComponent<RectTransform>();
                loseCard.SetParent(loseRect, false);
                loseCard.anchorMin = loseCard.anchorMax = new Vector2(0.5f, 0.5f);
                loseCard.pivot = new Vector2(0.5f, 0.5f);
                loseCard.anchoredPosition = Vector2.zero;
                loseCard.sizeDelta = new Vector2(880f, ResultCardHeight);
                UiTheme.StylePanel(cardObject, UiTheme.Panel, UiTheme.Danger, 5f);

                for (int index = loseRect.childCount - 1; index >= 0; index--)
                {
                    Transform child = loseRect.GetChild(index);
                    // the card is centered where the panel is, so children anchored to the center keep
                    // their exact layout when we move them into it
                    if (child != loseCard)
                        child.SetParent(loseCard, false);
                }

                if (cardObject.GetComponent<UiPopIn>() == null)
                    cardObject.AddComponent<UiPopIn>();
            }
            else if (loseRect != null)
            {
                loseDimmer = AddResultDimmer(loseRect);
                UiTheme.StylePanel(losePanel, UiTheme.Panel, UiTheme.Danger, 5f);
                if (losePanel.GetComponent<UiPopIn>() == null)
                    losePanel.AddComponent<UiPopIn>();
            }

            loseCardRect = loseCard;
        }
    }

    // a full screen darkener right behind the card (as a sibling, so it never darkens the card itself).
    // shown and hidden together with the card
    static GameObject AddResultDimmer(RectTransform card)
    {
        Transform parent = card.parent;
        if (parent == null)
            return null;

        string dimmerName = card.name + "Dimmer";
        Transform existing = parent.Find(dimmerName);
        GameObject dimmer = existing != null
            ? existing.gameObject
            : new GameObject(dimmerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = dimmer.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(card.GetSiblingIndex());
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-200f, -200f);
        rect.offsetMax = new Vector2(200f, 200f);
        Image image = dimmer.GetComponent<Image>();
        image.color = new Color(0.04f, 0.03f, 0.03f, 0.72f);
        image.raycastTarget = true; // blocks the HUD while the result is showing
        dimmer.SetActive(card.gameObject.activeSelf);
        return dimmer;
    }

    void ShowLosePanel()
    {
        GameAudio.Play(Sfx.Defeat);
        if (loseDimmer != null)
            loseDimmer.SetActive(true);
        if (losePanel == null)
            return;

        losePanel.SetActive(true);

        if (!loseTextStyled)
        {
            loseTextStyled = true;
            try
            {
                StyleResultContents(loseCardRect, loseTitleText, new Color32(255, 150, 120, 255));
                AddMapButton(loseCardRect);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    static void StyleResultContents(RectTransform card, TMP_Text title, Color titleColor)
    {
        if (card == null)
            return;

        foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(false))
        {
            if (text.GetComponentInParent<Button>() != null)
                continue;

            bool isTitle = text == title;
            UiTheme.StyleLabel(text, isTitle ? 58f : 32f, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = isTitle ? titleColor : (Color)UiTheme.MutedText;
        }

        foreach (Button button in card.GetComponentsInChildren<Button>(false))
        {
            bool isRetry = button.name.IndexOf("Retry", System.StringComparison.OrdinalIgnoreCase) >= 0;
            UiTheme.StyleButton(button, isRetry ? UiButtonRole.Secondary : UiButtonRole.Primary, false, 34f);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 300f), Mathf.Max(rect.sizeDelta.y, 104f));
            Vector2 position = rect.anchoredPosition;
            if (position.y > -170f && position.y < 0f)
                rect.anchoredPosition = new Vector2(position.x, -175f);
            if (Mathf.Abs(position.x) > 0.01f && Mathf.Abs(position.x) < 175f)
                rect.anchoredPosition = new Vector2(Mathf.Sign(position.x) * 175f, rect.anchoredPosition.y);
        }
    }

    // a quiet "WORLD MAP" button under the result card's main buttons
    void AddMapButton(RectTransform card)
    {
        if (card == null || card.Find("MapButton") != null)
            return;

        GameObject buttonObject = new GameObject("MapButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(card, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -Mathf.Max(card.sizeDelta.y, ResultCardHeight) * 0.5f + 80f);
        rect.sizeDelta = new Vector2(320f, 84f);
        Button button = buttonObject.GetComponent<Button>();
        UiTheme.StyleButton(button, UiButtonRole.Neutral, false, 30f);
        UiTheme.SetLabel(button, "WORLD MAP");
        button.onClick.AddListener(ReturnToMap);
    }

    static TMP_Text CreateResultLabel(RectTransform parent, string name, Vector2 position, float size)
    {
        Transform existing = parent.Find(name);
        GameObject labelObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        if (label == null)
            label = labelObject.AddComponent<TextMeshProUGUI>();
        UiTheme.StyleLabel(label, size, TextAlignmentOptions.Center);
        label.richText = true;
        label.text = string.Empty;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(760f, 60f);
        return label;
    }

    // called when you lose: shows the lose card with a tip
    void StageFailed()
    {
        stageFinished = true;
        PauseStageTime();
        if (pauseMenu != null)
            pauseMenu.SetAvailable(false);

        if (losePanel != null)
        {
            if (loseTitleText != null)
                loseTitleText.text = "BARRIER DESTROYED";

            if (loseMessageText != null)
                loseMessageText.text = "The enemies broke through your barrier.\n<color=#B4EC8C>TIP: " + LoseTip() + "</color>";

            // let the barrier's break animation play (it uses unscaled time) before
            // the lose card covers the screen
            if (barrierVisuals != null && barrierHealth <= 0)
                StartCoroutine(ShowLosePanelAfterBreak());
            else
                ShowLosePanel();
        }
    }

    private static int lossesThisSession;
    private static readonly string[] GeneralTips =
    {
        "Bounce off the walls: one shot can hit several enemies for a COMBO.",
        "Tap your skill button while your Beast is flying.",
        "Hit the glowing POWER BUMPER while enemies are on the field.",
        "Aim feels touchy? Lower AIM SENSITIVITY in Settings.",
        "Spend coins on LAUNCH POWER in UPGRADES for faster shots.",
        "Pull back farther for a stronger, straighter shot."
    };

    // a different hint after each loss: the stage's matchup advice first, then general tips in turn
    string LoseTip()
    {
        int attempt = lossesThisSession++;
        ChapterInfo chapter = ChapterData.Get(ChapterData.ChapterOfStage(stageNumber));
        if (ChapterData.IsBossStage(stageNumber) && attempt % 2 == 0)
        {
            ElementType counter = ElementVisuals.GetCounter(chapter.bossElement);
            return chapter.boss + " wins at once if it reaches the barrier. Hit it with " +
                ElementVisuals.GetName(counter) + " for double damage!";
        }
        if (attempt % 2 == 0 && !string.IsNullOrEmpty(chapter.advice))
            return chapter.advice;
        return GeneralTips[(attempt / 2) % GeneralTips.Length];
    }

    private System.Collections.IEnumerator ShowLosePanelAfterBreak()
    {
        yield return new WaitForSecondsRealtime(BarrierBreakRevealSeconds);
        ShowLosePanel();
    }

    void StageSetupFailed()
    {
        stageFinished = true;
        PauseStageTime();
        if (pauseMenu != null)
            pauseMenu.SetAvailable(false);

        if (losePanel != null)
        {
            if (loseTitleText != null)
                loseTitleText.text = "WAVE SETUP ERROR";

            if (loseMessageText != null)
                loseMessageText.text = "An enemy wave could not start.\nCheck the wave settings and retry.";

            ShowLosePanel();
        }
    }

    private void PauseStageTime()
    {
        stageTimePaused = true;
        Time.timeScale = 0f;
    }

    void OnDestroy()
    {
        if (stageTimePaused)
            Time.timeScale = 1f;
    }

    // searches this stage's own scene. (while a stage is still loading, the "active" scene can be
    // the old one, so searching that one missed things like stage 2's BarrierHealthText)
    private GameObject FindSceneObjectByName(string objectName)
    {
        // every loaded object (inactive ones too), but only from this stage's scene. this works in
        // Awake, when the scene might not count as loaded yet for GetRootGameObjects
        Scene ownScene = gameObject.scene;
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (candidate.name == objectName && candidate.gameObject.scene == ownScene)
                return candidate.gameObject;
        }

        return null;
    }

    private static TMP_Text FindNamedTmpText(GameObject root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name == objectName)
                return candidate.GetComponent<TMP_Text>();
        }

        return null;
    }

    public void RetryStage()
    {
        LoadingScreen.Load(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMap()
    {
        if (!LoadingScreen.LoadHub())
            Debug.LogError("The GameHub scene is not in the Build Settings, so the map cannot open.", this);
    }

    // after a chapter's boss (and after the last stage) the map opens to show what got unlocked
    public void NextStage()
    {
        if (ChapterData.IsBossStage(stageNumber) && Application.CanStreamedLevelBeLoaded(LoadingScreen.HubScene))
        {
            LoadingScreen.LoadHub();
            return;
        }

        string nextSceneName = GameHubController.GetStageSceneName(stageNumber + 1);
        if (stageNumber < ChapterData.StageCount && Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            LoadingScreen.Load(nextSceneName);
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(LoadingScreen.HubScene))
        {
            LoadingScreen.LoadHub();
            return;
        }

        Debug.LogError("No valid next stage or hub scene is available in Build Settings.");
    }
}
