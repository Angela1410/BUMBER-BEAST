using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ui = LoadingScreen.Ui;

// The SETTINGS and PLAYER PROFILE cards, opened from the title screen, the hub header and the
// pause menu. Each card sits on its own overlay canvas above everything else, scales down to fit
// any screen, and closes with its X, DONE, a tap outside it or the Android back button.
//  SETTINGS        music and sound volume, screen shake, the profile, the game version.
//  PLAYER PROFILE  the player's name and lead Beast, rank, campaign stats, reset progress.
// Everything is stored on the device (GameSettings, GameProgress); there is no online account.
public class GameMenus : MonoBehaviour
{
    private const float CardWidth = 900f;
    private static readonly Color32 SliderTrack = new Color32(20, 25, 20, 255);

    private static GameMenus open;

    private RectTransform frame;
    private Vector2 cardSize;
    private Action closed;
    private int lastWidth;
    private int lastHeight;
    private Rect lastSafeArea;

    // Settings card state.
    private TMP_Text musicValue;
    private TMP_Text sfxValue;
    private Button shakeToggle;
    private TMP_Text sensitivityValue;
    private float lastPreviewTime;

    // Profile card state.
    private Button resetButton;
    private float resetArmedUntil = -1f;

    public static bool IsOpen => open != null;

    // ------------------------------------------------------------------ entry points

    // showProfileButton: false in a stage (the profile's reset would end the run).
    public static void OpenSettings(bool showProfileButton, Func<BeastId, Sprite> portraits = null, Action onClosed = null)
    {
        GameMenus menu = Create("SettingsMenu", new Vector2(CardWidth, 1250f), onClosed);
        menu.BuildSettings(showProfileButton, portraits);
    }

    // The daily gift card, when today's gift has not been claimed yet. Returns false otherwise.
    public static bool OpenDailyGiftIfReady(Action onClosed)
    {
        if (!DailyGift.IsReady)
            return false;
        GameMenus menu = Create("DailyGiftMenu", new Vector2(CardWidth, 1000f), onClosed);
        menu.BuildDailyGift();
        return true;
    }

    public static void OpenProfile(Func<BeastId, Sprite> portraits = null, Action onClosed = null)
    {
        GameMenus menu = Create("ProfileMenu", new Vector2(CardWidth, 1300f), onClosed);
        menu.BuildProfile(portraits);
    }

    static GameMenus Create(string name, Vector2 size, Action onClosed)
    {
        if (open != null)
            open.Close();

        GameObject root = new GameObject(name, typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameMenus menu = root.AddComponent<GameMenus>();
        menu.cardSize = size;
        menu.closed = onClosed;
        open = menu;

        // A tap on the dimmed background closes the card.
        Image dim = Ui.Image(root.transform, "Dim", new Color(0.02f, 0.03f, 0.02f, 0.78f), null);
        Ui.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        Button dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(menu.Close);

        menu.frame = Ui.Rect(root.transform, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, size);
        menu.Fit();
        return menu;
    }

    RectTransform CreateCard(string title, UiIcon icon)
    {
        RectTransform card = Ui.Rect(frame, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);
        UiTheme.StylePanel(card.gameObject, UiTheme.Panel, UiTheme.Gold, 5f).raycastTarget = true;
        card.gameObject.AddComponent<UiPopIn>();

        float top = cardSize.y * 0.5f;
        Image badge = Ui.Image(card, "TitleIcon", UiTheme.Gold, UiIcons.Get(icon));
        badge.preserveAspect = true;
        Ui.Place(badge.rectTransform, new Vector2(-348f, top - 78f), new Vector2(60f, 60f));
        TMP_Text heading = Ui.Text(card, "Title", title, 60f, UiTheme.Gold);
        heading.alignment = TextAlignmentOptions.Left;
        Ui.Place(heading.rectTransform, new Vector2(10f, top - 78f), new Vector2(580f, 84f));

        Button close = RoundButton(card, "Close", UiIcon.Close, new Vector2(cardSize.x * 0.5f - 78f, top - 78f), 92f, UiTheme.Danger);
        close.onClick.AddListener(Close);
        return card;
    }

    // ------------------------------------------------------------------ settings

    void BuildSettings(bool showProfileButton, Func<BeastId, Sprite> portraits)
    {
        RectTransform card = CreateCard("SETTINGS", UiIcon.Gear);
        float y = cardSize.y * 0.5f - 210f;

        musicValue = VolumeRow(card, y, UiIcon.MusicNote, "MUSIC", GameSettings.MusicVolume, UiTheme.Teal, value =>
        {
            GameSettings.MusicVolume = value;
            musicValue.text = Percent(value);
        });
        y -= 190f;
        sfxValue = VolumeRow(card, y, UiIcon.Speaker, "SOUND EFFECTS", GameSettings.SfxVolume, UiTheme.Orange, value =>
        {
            GameSettings.SfxVolume = value;
            sfxValue.text = Percent(value);
            // Let the player hear the new level while dragging (a few times a second at most).
            if (Time.unscaledTime - lastPreviewTime > 0.18f)
            {
                lastPreviewTime = Time.unscaledTime;
                GameAudio.Play(Sfx.Coin);
            }
        });
        y -= 175f;
        // How much power a finger movement gives: low = long, precise pulls; high = short flicks.
        sensitivityValue = VolumeRow(card, y, UiIcon.ArrowUp, "AIM SENSITIVITY", GameSettings.AimSensitivitySlider, UiTheme.Green, value =>
        {
            GameSettings.AimSensitivitySlider = value;
            sensitivityValue.text = SensitivityText();
        });
        sensitivityValue.text = SensitivityText();
        y -= 160f;

        shakeToggle = ToggleRow(card, y, UiIcon.Shake, "SCREEN SHAKE", () => GameSettings.ScreenShake,
            value => GameSettings.ScreenShake = value);
        y -= 110f;
        ToggleRow(card, y, UiIcon.Hand, "DRAG ANYWHERE TO AIM", () => GameSettings.DragAnywhere,
            value => GameSettings.DragAnywhere = value);
        y -= 140f;

        Button replay = TextButton(card, "ReplayTutorial", "REPLAY TUTORIAL",
            new Vector2(showProfileButton ? -195f : 0f, y), new Vector2(showProfileButton ? 370f : 560f, 104f), UiButtonRole.Neutral, 30f);
        replay.onClick.AddListener(() =>
        {
            TutorialDirector.ResetAll();
            UiTheme.SetLabel(replay, "PLAYS IN STAGE 1 & 2");
            replay.GetComponent<Image>().color = UiTheme.Green;
            GameAudio.Play(Sfx.Tutorial);
        });
        if (showProfileButton)
        {
            Button profile = TextButton(card, "OpenProfile", "PROFILE", new Vector2(195f, y), new Vector2(370f, 104f),
                UiButtonRole.Secondary, 34f);
            AddButtonIcon(profile, UiIcon.Person);
            profile.onClick.AddListener(() =>
            {
                Action after = closed;
                closed = null;
                Close();
                OpenProfile(portraits, after);
            });
        }
        y -= 130f;

        Button done = TextButton(card, "Done", "DONE", new Vector2(0f, y), new Vector2(560f, 116f), UiButtonRole.Primary, 46f);
        done.onClick.AddListener(Close);

        TMP_Text footer = Ui.Text(card, "Version", "BUMPER BEASTS  v" + Application.version + "   ·   Saved on this device", 24f, UiTheme.MutedText);
        Ui.Place(footer.rectTransform, new Vector2(0f, -cardSize.y * 0.5f + 52f), new Vector2(820f, 40f));
    }

    TMP_Text VolumeRow(RectTransform card, float y, UiIcon icon, string label, float value, Color fill, Action<float> changed)
    {
        Image iconImage = Ui.Image(card, label + "Icon", UiTheme.MutedText, UiIcons.Get(icon));
        iconImage.preserveAspect = true;
        Ui.Place(iconImage.rectTransform, new Vector2(-350f, y + 42f), new Vector2(56f, 56f));
        TMP_Text title = Ui.Text(card, label + "Label", label, 38f, UiTheme.Cream);
        title.alignment = TextAlignmentOptions.Left;
        Ui.Place(title.rectTransform, new Vector2(-90f, y + 42f), new Vector2(460f, 56f));
        TMP_Text amount = Ui.Text(card, label + "Value", Percent(value), 36f, UiTheme.Gold);
        amount.alignment = TextAlignmentOptions.Right;
        Ui.Place(amount.rectTransform, new Vector2(300f, y + 42f), new Vector2(160f, 56f));

        Slider slider = CreateSlider(card, label + "Slider", new Vector2(0f, y - 34f), 760f, value, fill);
        slider.onValueChanged.AddListener(v => changed(v));
        return amount;
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 position, float width, float value, Color fillColor)
    {
        GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Ui.Place(rect, position, new Vector2(width, 76f));
        Image hitArea = sliderObject.GetComponent<Image>();
        hitArea.color = Color.clear;   // the whole row height reacts to touch, not only the thin track
        hitArea.raycastTarget = true;

        Image track = Ui.Image(rect, "Track", SliderTrack, HudVisualUtility.RoundedSprite());
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 3f;
        TrackRect(track.rectTransform, 28f);
        HudVisualUtility.AddOutline(track.gameObject, UiTheme.Ink, new Vector2(2f, -2f));

        RectTransform fillArea = Ui.Rect(rect, "FillArea", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TrackRect(fillArea, 28f);
        Image fill = Ui.Image(fillArea, "Fill", fillColor, HudVisualUtility.RoundedSprite());
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 3f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(value, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = Ui.Rect(rect, "HandleArea", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(30f, 0f);
        handleArea.offsetMax = new Vector2(-30f, 0f);
        Image handle = Ui.Image(handleArea, "Handle", UiTheme.Cream, HudVisualUtility.CircleSprite());
        handle.rectTransform.sizeDelta = new Vector2(68f, -8f);
        HudVisualUtility.AddOutline(handle.gameObject, UiTheme.Ink, new Vector2(3f, -3f));
        Shadow shadow = UiTheme.GetOrAddEffect<Shadow>(handle.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -4f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        slider.SetValueWithoutNotify(value);
        return slider;
    }

    static void TrackRect(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    // A label with an ON/OFF pill on the right.
    Button ToggleRow(RectTransform card, float y, UiIcon icon, string label, Func<bool> get, Action<bool> set)
    {
        Image iconImage = Ui.Image(card, label + "Icon", UiTheme.MutedText, UiIcons.Get(icon));
        iconImage.preserveAspect = true;
        Ui.Place(iconImage.rectTransform, new Vector2(-350f, y), new Vector2(58f, 58f));
        TMP_Text title = Ui.Text(card, label + "Label", label, 36f, UiTheme.Cream);
        title.alignment = TextAlignmentOptions.Left;
        Ui.Place(title.rectTransform, new Vector2(-95f, y), new Vector2(430f, 60f));
        Button toggle = TextButton(card, label + "Toggle", string.Empty, new Vector2(285f, y), new Vector2(190f, 86f), UiButtonRole.Success, 34f);
        toggle.onClick.AddListener(() =>
        {
            set(!get());
            RefreshToggle(toggle, get());
        });
        RefreshToggle(toggle, get());
        return toggle;
    }

    static void RefreshToggle(Button toggle, bool on)
    {
        toggle.GetComponent<Image>().color = on ? (Color)UiTheme.Green : (Color)UiTheme.Locked;
        UiTheme.SetLabel(toggle, on ? "ON" : "OFF");
    }

    static string SensitivityText()
    {
        return Mathf.RoundToInt(GameSettings.AimSensitivity * 100f).ToString(CultureInfo.InvariantCulture) + "%";
    }

    static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture) + "%";
    }

    // ------------------------------------------------------------------ profile

    void BuildProfile(Func<BeastId, Sprite> portraits)
    {
        RectTransform card = CreateCard("PLAYER PROFILE", UiIcon.Person);
        GameProgress progress = GameProgress.Instance;
        if (progress == null)
            progress = new GameObject("GameProgress").AddComponent<GameProgress>();

        float top = cardSize.y * 0.5f;

        // Avatar: the squad's lead Beast.
        BeastId lead = progress.Squad.Count > 0 ? progress.Squad[0] : BeastId.Choma;
        Image ring = Ui.Image(card, "AvatarRing", UiTheme.Gold, HudVisualUtility.CircleSprite());
        Ui.Place(ring.rectTransform, new Vector2(0f, top - 265f), new Vector2(230f, 230f));
        Image face = Ui.Image(ring.rectTransform, "Face", ElementVisuals.GetColor(BeastRoster.GetElement(lead)) * 0.55f, HudVisualUtility.CircleSprite());
        face.color = new Color(face.color.r, face.color.g, face.color.b, 1f);
        Ui.Fill(face.rectTransform, 0.05f);
        // The hub's portraits when it is there (painted ones for Choma and Thistle), otherwise the
        // Beast's own art in Resources/Placeholders, otherwise its initial.
        if (portraits == null)
        {
            GameHubController hub = FindAnyObjectByType<GameHubController>();
            if (hub != null)
                portraits = hub.GetBeastPortrait;
        }
        Sprite portrait = portraits != null ? portraits(lead)
            : PlaceholderArt.HasPaintedArt(lead) ? null : PlaceholderArt.BeastIdle(lead);
        if (portrait != null)
        {
            Image art = Ui.Image(ring.rectTransform, "Portrait", Color.white, portrait);
            art.preserveAspect = true;
            Ui.Fill(art.rectTransform, 0.08f);
        }
        else
        {
            TMP_Text initial = Ui.Text(ring.rectTransform, "Initial", BeastRoster.GetName(lead).Substring(0, 1), 110f, UiTheme.Cream);
            Ui.Stretch(initial.rectTransform);
        }
        Image element = Ui.Image(ring.rectTransform, "Element", Color.white, ElementVisuals.GetIcon(BeastRoster.GetElement(lead)));
        element.rectTransform.anchorMin = element.rectTransform.anchorMax = new Vector2(0.86f, 0.14f);
        element.rectTransform.sizeDelta = new Vector2(70f, 70f);

        // Name (tap to rename).
        TMP_InputField nameField = CreateNameField(card, new Vector2(0f, top - 450f), new Vector2(660f, 104f));
        nameField.SetTextWithoutNotify(GameSettings.PlayerName);
        TMP_Text hint = Ui.Text(card, "NameHint", "TAP YOUR NAME TO CHANGE IT", 22f, UiTheme.MutedText);
        Ui.Place(hint.rectTransform, new Vector2(0f, top - 522f), new Vector2(700f, 34f));

        int cleared = Mathf.Min(progress.HighestClearedStage, ChapterData.StageCount);
        int bosses = 0;
        for (int chapter = 0; chapter < ChapterData.ChapterCount; chapter++)
        {
            if (cleared >= ChapterData.LastStage(chapter))
                bosses++;
        }
        int beasts = 0;
        foreach (BeastId beast in BeastRoster.AllBeasts)
        {
            if (progress.IsBeastUnlocked(beast))
                beasts++;
        }

        TMP_Text rank = Ui.Text(card, "Rank", RankTitle(bosses, cleared) + "  ·  " + progress.TotalStars + " STARS", 36f, UiTheme.Gold);
        Ui.Place(rank.rectTransform, new Vector2(0f, top - 580f), new Vector2(760f, 52f));

        float gridTop = top - 680f;
        StatTile(card, new Vector2(-205f, gridTop), "STAGES CLEARED", cleared + " / " + ChapterData.StageCount);
        StatTile(card, new Vector2(205f, gridTop), "COINS", progress.Coins.ToString("#,0", CultureInfo.InvariantCulture));
        StatTile(card, new Vector2(-205f, gridTop - 122f), "BEASTS", beasts + " / " + BeastRoster.AllBeasts.Length);
        StatTile(card, new Vector2(205f, gridTop - 122f), "BOSSES DEFEATED", bosses + " / " + ChapterData.ChapterCount);
        StatTile(card, new Vector2(-205f, gridTop - 244f), "LAUNCH POWER", "LV " + progress.LaunchPowerLevel);
        StatTile(card, new Vector2(205f, gridTop - 244f), "SKILL COOLDOWN", "LV " + progress.SkillCooldownLevel);

        // Campaign bar.
        float barY = gridTop - 375f;
        float ratio = cleared / (float)ChapterData.StageCount;
        TMP_Text barLabel = Ui.Text(card, "CampaignLabel", "CAMPAIGN  " + Mathf.RoundToInt(ratio * 100f) + "%", 26f, UiTheme.MutedText);
        barLabel.alignment = TextAlignmentOptions.Left;
        Ui.Place(barLabel.rectTransform, new Vector2(-180f, barY + 30f), new Vector2(420f, 40f));
        Image barBack = Ui.Image(card, "CampaignBar", SliderTrack, HudVisualUtility.RoundedSprite());
        barBack.type = Image.Type.Sliced;
        barBack.pixelsPerUnitMultiplier = 3f;
        Ui.Place(barBack.rectTransform, new Vector2(0f, barY - 8f), new Vector2(780f, 26f));
        Image barFill = Ui.Image(barBack.rectTransform, "Fill", UiTheme.Green, HudVisualUtility.RoundedSprite());
        barFill.type = Image.Type.Sliced;
        barFill.pixelsPerUnitMultiplier = 3f;
        barFill.rectTransform.anchorMin = Vector2.zero;
        barFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, ratio), 1f);
        barFill.rectTransform.offsetMin = barFill.rectTransform.offsetMax = Vector2.zero;

        float buttonsY = -cardSize.y * 0.5f + 140f;
        resetButton = TextButton(card, "ResetProgress", "RESET PROGRESS", new Vector2(-205f, buttonsY), new Vector2(380f, 104f),
            UiButtonRole.Neutral, 30f);
        resetButton.onClick.AddListener(OnResetPressed);
        Button done = TextButton(card, "Done", "DONE", new Vector2(205f, buttonsY), new Vector2(380f, 104f), UiButtonRole.Primary, 42f);
        done.onClick.AddListener(Close);

        TMP_Text footer = Ui.Text(card, "Saved", "Your profile and progress are saved on this device.", 24f, UiTheme.MutedText);
        Ui.Place(footer.rectTransform, new Vector2(0f, -cardSize.y * 0.5f + 52f), new Vector2(820f, 40f));
    }

    static string RankTitle(int bosses, int cleared)
    {
        if (cleared >= ChapterData.StageCount) return "LEGENDARY BEAST KEEPER";
        switch (bosses)
        {
            case 0: return cleared > 0 ? "ROOKIE KEEPER" : "NEW KEEPER";
            case 1: return "WILDROOT RANGER";
            case 2: return "STONE WARDEN";
            case 3: return "STORM CHASER";
            default: return "TIDE GUARDIAN";
        }
    }

    static void StatTile(RectTransform card, Vector2 position, string title, string value)
    {
        RectTransform tile = Ui.Rect(card, title + "Tile", new Vector2(0.5f, 0.5f), position, new Vector2(390f, 108f));
        UiTheme.StylePanel(tile.gameObject, UiTheme.PanelDark, UiTheme.PanelBorder, 2f).raycastTarget = false;
        TMP_Text label = Ui.Text(tile, "Label", title, 22f, UiTheme.MutedText);
        Ui.Place(label.rectTransform, new Vector2(0f, 26f), new Vector2(370f, 32f));
        TMP_Text amount = Ui.Text(tile, "Value", value, 40f, UiTheme.Cream);
        Ui.Place(amount.rectTransform, new Vector2(0f, -16f), new Vector2(370f, 54f));
    }

    static TMP_InputField CreateNameField(RectTransform parent, Vector2 position, Vector2 size)
    {
        // Built inactive so the input field only wakes up once all of its parts are connected.
        GameObject fieldObject = new GameObject("NameField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fieldObject.SetActive(false);
        RectTransform rect = fieldObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Ui.Place(rect, position, size);
        Image back = UiTheme.StylePanel(fieldObject, UiTheme.PanelDark, UiTheme.Gold, 3f);
        back.raycastTarget = true;

        RectTransform viewport = Ui.Rect(rect, "TextArea", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(24f, 8f);
        viewport.offsetMax = new Vector2(-24f, -8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TMP_Text text = CreateFieldText(viewport, "Text", UiTheme.Cream);
        TMP_Text placeholder = CreateFieldText(viewport, "Placeholder", new Color32(205, 196, 168, 140));
        placeholder.text = "ENTER YOUR NAME";

        TMP_InputField field = fieldObject.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.targetGraphic = back;
        field.characterLimit = GameSettings.MaxNameLength;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.contentType = TMP_InputField.ContentType.Standard;
        field.caretColor = UiTheme.Gold;
        field.customCaretColor = true;
        field.caretWidth = 4;
        field.selectionColor = new Color(1f, 0.8f, 0.26f, 0.35f);
        // Letters, digits, spaces and dashes only (the game font has no other symbols), upper case.
        field.onValidateInput = (input, index, character) =>
        {
            if (character >= 'a' && character <= 'z')
                return char.ToUpperInvariant(character);
            if ((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9') ||
                character == ' ' || character == '-' || character == '_')
                return character;
            return '\0';
        };
        field.onEndEdit.AddListener(value =>
        {
            GameSettings.PlayerName = value;
            field.SetTextWithoutNotify(GameSettings.PlayerName);
        });
        fieldObject.SetActive(true);
        return field;
    }

    static TMP_Text CreateFieldText(RectTransform viewport, string name, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(viewport, false);
        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        HudVisualUtility.ApplyTextStyle(label);
        label.fontSize = 46f;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = false;
        label.color = color;
        Ui.Stretch(label.rectTransform);
        return label;
    }

    void OnResetPressed()
    {
        if (Time.unscaledTime > resetArmedUntil)
        {
            // First tap only arms the button; a second tap within a few seconds resets.
            resetArmedUntil = Time.unscaledTime + 3f;
            resetButton.GetComponent<Image>().color = UiTheme.Danger;
            UiTheme.SetLabel(resetButton, "TAP AGAIN TO RESET");
            GameAudio.Play(Sfx.Denied);
            return;
        }

        resetArmedUntil = -1f;
        if (GameProgress.Instance != null)
            GameProgress.Instance.ResetProgress();
        Close();
        // Rebuild the map from the fresh save.
        LoadingScreen.LoadHub();
    }

    // ------------------------------------------------------------------ daily gift

    private RectTransform giftTodayTile;
    private float giftClosesAt = -1f;

    void BuildDailyGift()
    {
        RectTransform card = CreateCard("DAILY GIFT", UiIcon.Star);
        float top = cardSize.y * 0.5f;
        int today = DailyGift.CurrentDay;

        TMP_Text intro = Ui.Text(card, "Intro", "Come back every day for bigger gifts!", 32f, UiTheme.MutedText);
        Ui.Place(intro.rectTransform, new Vector2(0f, top - 165f), new Vector2(800f, 48f));

        const float tileWidth = 188f, tileHeight = 196f;
        for (int day = 1; day <= DailyGift.Rewards.Length; day++)
        {
            int row = day <= 4 ? 0 : 1;
            int column = row == 0 ? day - 1 : day - 5;
            int inRow = row == 0 ? 4 : 3;
            float x = (column - (inRow - 1) * 0.5f) * (tileWidth + 18f);
            float y = top - 335f - row * (tileHeight + 22f);
            bool claimed = day < today;
            bool isToday = day == today;
            bool finalDay = day == DailyGift.Rewards.Length;

            RectTransform tile = Ui.Rect(card, "Day" + day, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(tileWidth, tileHeight));
            Color back = isToday ? new Color32(70, 58, 24, 255) : claimed ? new Color32(40, 62, 36, 255) : (Color)UiTheme.PanelDark;
            UiTheme.StylePanel(tile.gameObject, back, isToday ? UiTheme.Gold : UiTheme.PanelBorder, isToday ? 5f : 2f).raycastTarget = false;
            TMP_Text dayLabel = Ui.Text(tile, "Label", finalDay ? "DAY 7 BONUS" : "DAY " + day, 24f, isToday ? UiTheme.Gold : UiTheme.MutedText);
            Ui.Place(dayLabel.rectTransform, new Vector2(0f, 70f), new Vector2(tileWidth - 12f, 34f));
            Image coin = Ui.Image(tile, "Coin", Color.white, UiIcons.Get(UiIcon.Coin));
            coin.preserveAspect = true;
            Ui.Place(coin.rectTransform, new Vector2(0f, 6f), new Vector2(finalDay ? 86f : 70f, finalDay ? 86f : 70f));
            TMP_Text amount = Ui.Text(tile, "Amount", "+" + DailyGift.RewardFor(day), 36f, UiTheme.Cream);
            Ui.Place(amount.rectTransform, new Vector2(0f, -62f), new Vector2(tileWidth - 12f, 44f));
            if (claimed)
            {
                Image check = Ui.Image(tile, "Claimed", new Color32(150, 236, 110, 255), UiIcons.Get(UiIcon.Check));
                check.preserveAspect = true;
                Ui.Place(check.rectTransform, new Vector2(56f, 52f), new Vector2(54f, 54f));
            }
            if (isToday)
                giftTodayTile = tile;
        }

        int reward = DailyGift.RewardFor(today);
        Button claim = TextButton(card, "Claim", "CLAIM  " + UiTheme.Coin(reward), new Vector2(0f, -top + 190f), new Vector2(620f, 124f),
            UiButtonRole.Primary, 44f);
        claim.onClick.AddListener(() =>
        {
            if (giftClosesAt > 0f)
                return;
            int coins = DailyGift.Claim();
            GameAudio.Play(Sfx.Gift);
            UiTheme.SetLabel(claim, coins > 0 ? "+" + coins + " COINS!" : "COLLECTED");
            claim.GetComponent<Image>().color = UiTheme.Green;
            giftClosesAt = Time.unscaledTime + 1.1f;
        });

        TMP_Text footer = Ui.Text(card, "Rule", "Miss a day and the streak starts again from day 1.", 24f, UiTheme.MutedText);
        Ui.Place(footer.rectTransform, new Vector2(0f, -top + 70f), new Vector2(820f, 40f));
    }

    void AnimateDailyGift()
    {
        if (giftTodayTile != null)
        {
            float pulse = giftClosesAt > 0f ? 1.08f : 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f);
            giftTodayTile.localScale = new Vector3(pulse, pulse, 1f);
        }
        if (giftClosesAt > 0f && Time.unscaledTime >= giftClosesAt)
        {
            giftClosesAt = -1f;
            Close();
        }
    }

    // ------------------------------------------------------------------ shared widgets

    static Button TextButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size,
        UiButtonRole role, float fontSize)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Ui.Place(buttonObject.GetComponent<RectTransform>(), position, size);
        Button button = buttonObject.GetComponent<Button>();
        UiTheme.StyleButton(button, role, false, fontSize);
        UiTheme.SetLabel(button, label);
        return button;
    }

    static void AddButtonIcon(Button button, UiIcon icon)
    {
        Image image = Ui.Image(button.transform, "Icon", UiTheme.Cream, UiIcons.Get(icon));
        image.preserveAspect = true;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        image.rectTransform.sizeDelta = new Vector2(56f, 56f);
        image.rectTransform.anchoredPosition = new Vector2(64f, 0f);
    }

    public static Button RoundButton(Transform parent, string name, UiIcon icon, Vector2 position, float size, Color faceColor)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Ui.Place(buttonObject.GetComponent<RectTransform>(), position, new Vector2(size, size));
        Image bezel = buttonObject.GetComponent<Image>();
        bezel.sprite = HudVisualUtility.CircleSprite();
        bezel.color = UiTheme.Ink;
        bezel.raycastTarget = true;
        Image face = Ui.Image(buttonObject.transform, "Face", faceColor, HudVisualUtility.CircleSprite());
        Ui.Fill(face.rectTransform, 0.08f);
        Image gloss = Ui.Image(buttonObject.transform, "Gloss", new Color(1f, 1f, 1f, 0.16f), HudVisualUtility.CircleSprite());
        gloss.rectTransform.anchorMin = new Vector2(0.24f, 0.54f);
        gloss.rectTransform.anchorMax = new Vector2(0.76f, 0.86f);
        gloss.rectTransform.offsetMin = gloss.rectTransform.offsetMax = Vector2.zero;
        Image glyph = Ui.Image(buttonObject.transform, "Glyph", UiTheme.Cream, UiIcons.Get(icon));
        glyph.preserveAspect = true;
        Ui.Fill(glyph.rectTransform, 0.27f);
        Shadow shadow = UiTheme.GetOrAddEffect<Shadow>(buttonObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -5f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = bezel;
        button.transition = Selectable.Transition.ColorTint;
        buttonObject.AddComponent<UiButtonFeedback>();
        return button;
    }

    // ------------------------------------------------------------------ lifetime

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
            Fit();

        // Android back button.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        AnimateDailyGift();

        if (resetButton != null && resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil)
        {
            resetArmedUntil = -1f;
            resetButton.GetComponent<Image>().color = UiTheme.Panel;
            UiTheme.SetLabel(resetButton, "RESET PROGRESS");
        }
    }

    // Shrinks the card to fit inside the safe area of small or wide screens.
    void Fit()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        RectTransform canvasRect = transform as RectTransform;
        if (frame == null || canvasRect == null || lastWidth <= 0 || lastHeight <= 0)
            return;

        Canvas.ForceUpdateCanvases();
        Vector2 canvas = canvasRect.rect.size;
        float safeHeight = canvas.y * (lastSafeArea.height / lastHeight);
        float safeWidth = canvas.x * (lastSafeArea.width / lastWidth);
        float scale = Mathf.Min(1f, (safeWidth - 40f) / cardSize.x, (safeHeight - 60f) / cardSize.y);
        frame.localScale = Vector3.one * Mathf.Max(0.3f, scale);
        float safeCenter = (lastSafeArea.center.y / lastHeight - 0.5f) * canvas.y;
        frame.anchoredPosition = new Vector2(0f, safeCenter);
    }

    public void Close()
    {
        if (open == this)
            open = null;
        GameSettings.Save();
        Action callback = closed;
        closed = null;
        Destroy(gameObject);
        if (callback != null)
            callback();
    }

    void OnDestroy()
    {
        if (open == this)
            open = null;
    }
}
