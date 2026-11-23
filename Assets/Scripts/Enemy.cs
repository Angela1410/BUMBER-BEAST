using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Enemy : MonoBehaviour
{
    private const int MinimumRegularEnemyHealth = 4;
    private const float MinimumRegularEnemySpeed = 0.55f;
    private const int MinimumBossHealth = 8;
    private const int MaximumBossBonusHealth = 2;
    private const float BossMoveSpeed = 0.15f;

    public int maxHealth = 4;
    public ElementType element = ElementType.Grass;
    [Range(0f, 1f)]
    [Tooltip("How strongly the element colour tints the sprite. 1 = full tint (Chapter 1 critters); " +
        "painted enemies such as the Stone Hollow set use a low value so their own colours survive.")]
    public float elementTintStrength = 1f;
    public bool isBoss = false;
    public bool destroysBarrierOnReach;
    public string bossName = "Boss";
    public bool HasReachedBarrier { get; private set; }
    public bool IsRooted => isRooted;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    // full health after the wave bonus and the difficulty (Settings > DIFFICULTY)
    public int MaxHealthTotal => Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, maxHealth + waveHealthBonus) * Difficulty.EnemyHealth));
    public float HealthRatio => Mathf.Clamp01(
        (float)currentHealth / MaxHealthTotal);

    // list of the enemies that are alive right now, updated in OnEnable/OnDisable so the checks every
    // frame (barrier contact, is a boss alive) don't have to search the whole scene
    public static readonly System.Collections.Generic.List<Enemy> ActiveEnemies =
        new System.Collections.Generic.List<Enemy>();

    public float moveSpeed = 0.55f;
    public TMP_Text bossHealthText;
    public int waveHealthBonus;
    public float waveSpeedMultiplier = 1f;
    public float hitFlashDuration = 0.08f;

    private int currentHealth;
    private SpriteRenderer sr;
    private Color originalColor;
    private bool statsApplied;
    private bool isRooted;
    private bool isDead;
    private bool isKnockedBack;
    private Coroutine rootRoutine;
    private Coroutine knockBackRoutine;
    private Coroutine hitFlashRoutine;
    private float baseMoveSpeed;
    private Rigidbody2D body;
    private EnemyHitFeedback hitFeedback;
    private EnemyRootVisual rootVisual;
    private EnemyDamagedSprite damagedSprite;
    private RectTransform bossHealthRect;
    private RectTransform bossPanelRect;
    private RectTransform bossHealthTrackRect;
    private RectTransform bossHealthFillRect;
    private Image bossPanelImage;
    private Image bossHealthTrack;
    private Image bossHealthFill;
    private Rect lastBossHudSafeArea;
    private int lastBossHudScreenWidth;
    private int lastBossHudScreenHeight;
    private float lastBossHudScaleFactor;
    private Transform[] routeWaypoints;
    private int currentRouteIndex;
    private float routeLaneOffset;
    private float hasteMultiplier = 1f;
    private float hasteUntil;
    // a hit stops the enemy for a moment (GameTuning: Stagger Seconds)
    private float staggerUntil;

    // how many hits a boss shield (Maelith's Tide Shield) can still block
    public int ShieldHits { get; private set; }
    public bool IsHasted => Time.time < hasteUntil;
    public int RouteIndex => currentRouteIndex;

    void Awake()
    {
        if (!isBoss)
        {
            maxHealth = Mathf.Max(maxHealth, MinimumRegularEnemyHealth);
            moveSpeed = Mathf.Max(moveSpeed, MinimumRegularEnemySpeed);
        }

        baseMoveSpeed = moveSpeed;
        body = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        hitFeedback = GetComponent<EnemyHitFeedback>();
        rootVisual = GetComponent<EnemyRootVisual>();
        if (rootVisual == null)
            rootVisual = gameObject.AddComponent<EnemyRootVisual>();
        damagedSprite = GetComponent<EnemyDamagedSprite>();
    }

    void OnValidate()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
            renderer.color = ElementTint();
    }

    void Start()
    {
        if (!statsApplied)
        {
            ApplyWaveStats();
        }

        sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            originalColor = sr.color;
            sr.color = ElementTint();
            originalColor = sr.color;
        }

        if (rootVisual != null)
            rootVisual.SetBaseColor(originalColor);

        if (damagedSprite != null)
            damagedSprite.Initialize();

        // element icon above the enemy, so you can plan which Beast to use
        if (GetComponent<EnemyElementBadge>() == null)
            gameObject.AddComponent<EnemyElementBadge>();

        // every enemy gets the purple corruption glow, so it never looks like one of your Beasts
        EnemyCorruption.Attach(this);

        if (isBoss && bossHealthText == null)
        {
            GameObject bossTextObject = GameObject.Find("BossHealthText");
            if (bossTextObject != null)
                bossHealthText = bossTextObject.GetComponent<TMP_Text>();
        }

        UpdateHealthText();
    }

    // health of each chapter's boss (0 = not a chapter boss, keeps the old minimum)
    public static int ChapterBossHealth(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;
        string key = name.ToUpperInvariant();
        if (key.Contains("THORNMAW")) return 20;
        if (key.Contains("TERRATHORN")) return 28;
        if (key.Contains("VOLTREX")) return 34;
        if (key.Contains("MAELITH")) return 40;
        if (key.Contains("PYRAX")) return 48;
        return 0;
    }

    public void SetBoss(string bossNameText, int bossHealth = 0, float bossSpeedMultiplier = 1f)
    {
        isBoss = true;
        maxHealth = Mathf.Max(maxHealth, MinimumBossHealth);
        bossName = string.IsNullOrEmpty(bossNameText) ? bossName : bossNameText;
        // chapter bosses get a real health bar (more every chapter), and the wave's small bonus still adds on top
        int chapterHealth = ChapterBossHealth(bossName);
        waveHealthBonus = Mathf.Clamp(bossHealth, 0, MaximumBossBonusHealth) +
            (chapterHealth > maxHealth ? chapterHealth - maxHealth : 0);
        waveSpeedMultiplier = bossSpeedMultiplier;
        BossPhases.Attach(this);

        if (bossHealthText == null)
        {
            GameObject bossTextObject = GameObject.Find("BossHealthText");
            if (bossTextObject != null)
                bossHealthText = bossTextObject.GetComponent<TMP_Text>();
        }

        if (bossHealthText != null)
            bossHealthText.enabled = true;

        ApplyWaveStats();
        UpdateHealthText();
    }

    void OnDestroy()
    {
        if (bossHealthText != null)
        {
            bossHealthText.text = string.Empty;
            bossHealthText.enabled = false;
        }
        if (bossPanelImage != null)
            bossPanelImage.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
            ActiveEnemies.Add(this);
    }

    void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    public void MarkBarrierReached()
    {
        HasReachedBarrier = true;
    }

    public void SetRoute(Transform[] waypoints, float laneOffset = 0f, int startIndex = 0)
    {
        routeWaypoints = waypoints;
        currentRouteIndex = waypoints != null ? Mathf.Clamp(startIndex, 0, waypoints.Length) : 0;
        routeLaneOffset = laneOffset;
    }

    // moves faster for a while (Voltrex's Overcharge). it never slows an enemy down
    public void Haste(float multiplier, float seconds)
    {
        if (isDead || seconds <= 0f)
            return;
        hasteMultiplier = Mathf.Max(1f, multiplier);
        hasteUntil = Mathf.Max(hasteUntil, Time.time + seconds);
    }

    public void AddShield(int hits)
    {
        if (!isDead)
            ShieldHits = Mathf.Max(ShieldHits, hits);
    }

    public void ClearShield()
    {
        ShieldHits = 0;
    }

    public void ApplyWaveData(ElementType assignedElement, int healthBonus, float speedMultiplier)
    {
        SetElementForEditor(assignedElement);
        waveHealthBonus = healthBonus;
        waveSpeedMultiplier = speedMultiplier;

        ApplyWaveStats();

        if (sr != null)
        {
            sr.color = ElementTint();
            originalColor = sr.color;
        }

        if (rootVisual != null)
            rootVisual.SetBaseColor(originalColor);

        UpdateHealthText();
    }

    public void SetElementForEditor(ElementType assignedElement)
    {
        element = assignedElement;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
            renderer.color = ElementTint();
    }

    public void SetHitFlashBaseColor(Color color)
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        originalColor = color;
        if (sr != null)
            sr.color = color;
        if (rootVisual != null)
            rootVisual.SetBaseColor(color);
    }

    void ApplyWaveStats()
    {
        // easy waves can lower health (negative bonus) but never below one hit. the difficulty
        // changes health and speed for every enemy and boss
        currentHealth = MaxHealthTotal;
        float speed = isBoss ? BossMoveSpeed : baseMoveSpeed;
        // walking speed, changed by the difficulty (faster on HARD and HELL)
        moveSpeed = speed * Mathf.Max(0.1f, waveSpeedMultiplier) * Difficulty.EnemySpeed;
        statsApplied = true;
    }

    void FixedUpdate()
    {
        if (isDead)
            return;

        if (isKnockedBack)
        {
            if (hitFeedback != null)
                hitFeedback.SetWalking(false);
            return;
        }

        Vector2 direction = GetMovementDirection();
        // rooted or just hit (staggered): it stands still
        bool standing = isRooted || Time.time < staggerUntil;
        float effectiveMoveSpeed = standing ? 0f : moveSpeed * (Time.time < hasteUntil ? hasteMultiplier : 1f);
        if (hitFeedback != null)
            hitFeedback.SetWalking(effectiveMoveSpeed > 0.01f && direction.sqrMagnitude > 0.001f);
        if (sr != null && Mathf.Abs(direction.x) > 0.01f)
            sr.flipX = direction.x < 0f;
        Vector2 nextPosition = (Vector2)transform.position + direction * effectiveMoveSpeed * Time.fixedDeltaTime;

        if (body != null && body.bodyType == RigidbodyType2D.Kinematic)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    Vector2 GetMovementDirection()
    {
        while (routeWaypoints != null && currentRouteIndex < routeWaypoints.Length)
        {
            Transform waypoint = routeWaypoints[currentRouteIndex];
            if (waypoint == null)
            {
                currentRouteIndex++;
                continue;
            }

            Vector2 laneWaypoint = (Vector2)waypoint.position + Vector2.right * routeLaneOffset;
            Vector2 toWaypoint = laneWaypoint - (Vector2)transform.position;

            
            if (toWaypoint.sqrMagnitude <= 0.04f)
            {
                currentRouteIndex++;
                continue;
            }

            return toWaypoint.normalized;
        }

        
        
        
        if (routeWaypoints == null || routeWaypoints.Length == 0)
        {
            Debug.LogWarning(gameObject.name + " was spawned without a route and cannot move! Check your WaveManager or Stage configuration.");
        }

        return Vector2.zero;
    }

    // Unity calls this when something bumps into the enemy. if it's a Beast we deal damage
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead)
            return;

        BeastElement attacker =
            collision.gameObject.GetComponent<BeastElement>();

        if (attacker != null)
        {
            // check the element chart: is the Beast strong against this enemy?
            // if yes it does 2 damage, if not just 1
            bool superEffective = ElementChart.IsSuperEffective(attacker.element, element);
            // bounce power: every bumper or wall hit earlier in this shot adds damage
            BeastController beast = collision.gameObject.GetComponent<BeastController>();
            int bonus = beast != null ? beast.BouncePower : 0;
            TakeDamage((superEffective ? 2 : 1) + bonus, superEffective);
        }
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, false);
    }

    // lowers the enemy's health and shows the hit (damage number, shake, sound)
    public void TakeDamage(int amount, bool superEffective)
    {
        if (isDead)
            return;

        // every hit (even one a shield blocks) stops the enemy for a moment
        staggerUntil = Mathf.Max(staggerUntil, Time.time + GameTuning.Get.staggerSeconds);

        // a boss shield blocks the hit and uses up one charge instead
        if (ShieldHits > 0)
        {
            ShieldHits--;
            GameAudio.Play(Sfx.Block);
            StageStats.RegisterHit(transform.position, false, false, isBoss);
            DamagePopup.ShowText(transform.position, ShieldHits > 0 ? "BLOCKED" : "SHIELD BROKEN!",
                new Color(0.62f, 0.9f, 1f, 1f));
            if (hitFeedback != null)
                hitFeedback.PlayHit(transform.position);
            return;
        }

        DamagePopup.Show(transform.position, amount, superEffective);
        CameraShake.Shake(isBoss || superEffective);

        if (hitFeedback != null)
            hitFeedback.PlayHit(transform.position);

        // take the damage away from the enemy's health
        currentHealth -= amount;
        if (damagedSprite != null)
            damagedSprite.Refresh();
        UpdateHealthText();

        GameAudio.Play(superEffective ? Sfx.HitSuper : Sfx.Hit);
        if (currentHealth <= 0)
            GameAudio.Play(isBoss ? Sfx.BossDefeat : Sfx.EnemyDefeat);
        // count this hit for the combo and the stats
        StageStats.RegisterHit(transform.position, superEffective, currentHealth <= 0, isBoss);

        // no health left: the enemy is defeated
        if (currentHealth <= 0)
        {
            isDead = true;
            isRooted = false;
            if (rootVisual != null)
                rootVisual.Refresh();
            moveSpeed = 0f;

            if (rootRoutine != null)
            {
                StopCoroutine(rootRoutine);
                rootRoutine = null;
            }

            StopAllCoroutines();
            hitFlashRoutine = null;

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (Collider2D enemyCollider in colliders)
                enemyCollider.enabled = false;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }

            Destroy(gameObject);
            return;
        }

        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);
        hitFlashRoutine = StartCoroutine(FlashOnHit());
    }

    public void Root(float duration)
    {
        if (isDead)
            return;

        if (rootRoutine != null)
            StopCoroutine(rootRoutine);

        isRooted = true;
        if (rootVisual != null)
            rootVisual.Refresh();
        rootRoutine = StartCoroutine(RootRoutine(duration));
    }

    public void KnockBack(Vector2 direction, float distance, float duration)
    {
        if (isDead)
            return;

        if (knockBackRoutine != null)
            StopCoroutine(knockBackRoutine);

        knockBackRoutine = StartCoroutine(
            KnockBackRoutine(direction.normalized, distance, duration));
    }

    IEnumerator RootRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (isDead)
            yield break;

        isRooted = false;
        if (rootVisual != null)
            rootVisual.Refresh();
        rootRoutine = null;
    }

    IEnumerator KnockBackRoutine(Vector2 direction, float distance, float duration)
    {
        isKnockedBack = true;
        Vector2 start = body != null ? body.position : (Vector2)transform.position;
        Vector2 end = start + direction * distance;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.fixedDeltaTime);
            Vector2 nextPosition = Vector2.Lerp(start, end, elapsed / duration);

            if (body != null && body.bodyType == RigidbodyType2D.Kinematic)
                body.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            yield return new WaitForFixedUpdate();
        }

        if (isDead)
            yield break;

        isKnockedBack = false;
        knockBackRoutine = null;
    }

    void UpdateHealthText()
    {
        if (isBoss)
        {
            if (bossHealthText == null)
            {
                GameObject bossTextObject = GameObject.Find("BossHealthText");
                if (bossTextObject != null)
                    bossHealthText = bossTextObject.GetComponent<TMP_Text>();
            }

            if (bossHealthText != null)
            {
                EnsureBossHealthHud();
                bossHealthText.enabled = true;
                string label = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName.ToUpperInvariant();
                bossHealthText.text = "<size=75%><color=#F3C887>" + label +
                    "  |  BOSS</color></size>\n<size=95%><color=#FFF4D6>" +
                    currentHealth + " / " + MaxHealthTotal +
                    "  HP</color></size>";
                UpdateBossHealthFill();
            }

            return;
        }
    }

    void LateUpdate()
    {
        if (isBoss && bossPanelImage != null)
            ApplyBossHealthHudLayout(false);
    }

    void EnsureBossHealthHud()
    {
        if (bossHealthText == null || bossHealthRect != null)
            return;

        bossHealthRect = bossHealthText.rectTransform;
        Transform canvasParent = bossHealthRect.parent;
        if (canvasParent == null)
            return;

        GameObject panelObject = new GameObject(
            "BossHealthPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(Shadow));
        panelObject.transform.SetParent(canvasParent, false);
        bossPanelRect = panelObject.GetComponent<RectTransform>();
        bossPanelImage = panelObject.GetComponent<Image>();
        bossPanelImage.sprite = HudVisualUtility.RoundedSprite();
        bossPanelImage.type = Image.Type.Sliced;
        bossPanelImage.color = new Color32(35, 29, 25, 238);
        bossPanelImage.raycastTarget = false;

        Outline outline = panelObject.GetComponent<Outline>();
        outline.effectColor = new Color32(196, 90, 70, 255);
        outline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = panelObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -5f);

        GameObject trackObject = new GameObject(
            "BossHealthTrack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        trackObject.transform.SetParent(panelObject.transform, false);
        bossHealthTrackRect = trackObject.GetComponent<RectTransform>();
        bossHealthTrack = trackObject.GetComponent<Image>();
        bossHealthTrack.sprite = HudVisualUtility.RoundedSprite();
        bossHealthTrack.type = Image.Type.Sliced;
        bossHealthTrack.color = new Color32(22, 18, 16, 255);
        bossHealthTrack.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "BossHealthFill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        fillObject.transform.SetParent(trackObject.transform, false);
        bossHealthFillRect = fillObject.GetComponent<RectTransform>();
        bossHealthFill = fillObject.GetComponent<Image>();
        bossHealthFill.sprite = HudVisualUtility.RoundedSprite();
        bossHealthFill.type = Image.Type.Sliced;
        bossHealthFill.color = new Color32(111, 191, 58, 255);
        bossHealthFill.raycastTarget = false;

        // keep the label in the same space as the panel. before, the label was placed against the whole
        // Canvas and stretched, which moved it away from the bar on some phone screens
        bossHealthRect.SetParent(panelObject.transform, false);
        bossHealthRect.SetAsLastSibling();

        bossHealthText.enableAutoSizing = true;
        bossHealthText.fontSize = 30f;
        bossHealthText.fontSizeMin = 18f;
        bossHealthText.fontSizeMax = 30f;
        bossHealthText.alignment = TextAlignmentOptions.Center;
        bossHealthText.textWrappingMode = TextWrappingModes.NoWrap;
        bossHealthText.raycastTarget = false;
        HudVisualUtility.ApplyTextStyle(bossHealthText);

        ApplyBossHealthHudLayout(true);
    }

    void ApplyBossHealthHudLayout(bool force)
    {
        if (bossHealthRect == null || bossPanelRect == null)
            return;

        Canvas canvas = bossHealthText.canvas;
        float scaleFactor = canvas != null && canvas.scaleFactor > 0f
            ? canvas.scaleFactor
            : 1f;
        Rect safeArea = Screen.safeArea;
        if (!force &&
            lastBossHudScreenWidth == Screen.width &&
            lastBossHudScreenHeight == Screen.height &&
            lastBossHudSafeArea.Equals(safeArea) &&
            Mathf.Approximately(lastBossHudScaleFactor, scaleFactor))
            return;

        float safeTopInset = Mathf.Max(0f, Screen.height - safeArea.yMax) / scaleFactor;
        Vector2 panelPosition = new Vector2(0f, -(safeTopInset + 188f));
        Vector2 panelSize = new Vector2(460f, 96f);

        ConfigureBossHudRect(bossPanelRect, panelPosition, panelSize);

        bossHealthRect.anchorMin = Vector2.zero;
        bossHealthRect.anchorMax = Vector2.one;
        bossHealthRect.sizeDelta = Vector2.zero;
        bossHealthRect.anchoredPosition = Vector2.zero;
        bossHealthRect.offsetMin = new Vector2(14f, 24f);
        bossHealthRect.offsetMax = new Vector2(-14f, -6f);

        bossHealthTrackRect.anchorMin = new Vector2(0f, 0f);
        bossHealthTrackRect.anchorMax = new Vector2(1f, 0f);
        bossHealthTrackRect.pivot = new Vector2(0.5f, 0f);
        bossHealthTrackRect.sizeDelta = new Vector2(-32f, 9f);
        bossHealthTrackRect.anchoredPosition = new Vector2(0f, 10f);
        bossHealthFillRect.anchorMin = Vector2.zero;
        bossHealthFillRect.anchorMax = Vector2.one;
        bossHealthFillRect.offsetMin = Vector2.zero;
        bossHealthFillRect.offsetMax = Vector2.zero;

        lastBossHudScreenWidth = Screen.width;
        lastBossHudScreenHeight = Screen.height;
        lastBossHudSafeArea = safeArea;
        lastBossHudScaleFactor = scaleFactor;
    }

    static void ConfigureBossHudRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    void UpdateBossHealthFill()
    {
        if (bossHealthFillRect == null)
            return;

        int maximumHealth = MaxHealthTotal;
        float fill = Mathf.Clamp01((float)currentHealth / maximumHealth);
        bossHealthFillRect.anchorMax = new Vector2(fill, 1f);
        bossHealthFillRect.offsetMax = Vector2.zero;

        if (fill > 0.6f)
            bossHealthFill.color = new Color32(111, 191, 58, 255);
        else if (fill > 0.3f)
            bossHealthFill.color = new Color32(230, 180, 34, 255);
        else
            bossHealthFill.color = new Color32(217, 68, 42, 255);
    }

    IEnumerator FlashOnHit()
    {
        if (sr == null)
        {
            hitFlashRoutine = null;
            yield break;
        }

        sr.color = Color.white;
        yield return new WaitForSeconds(hitFlashDuration);

        if (!isDead && sr != null)
        {
            if (rootVisual != null)
                rootVisual.Refresh();
            else
                sr.color = originalColor;
        }

        hitFlashRoutine = null;
    }

    // the sprite color: the element color mixed toward white by elementTintStrength
    Color ElementTint()
    {
        return Color.Lerp(Color.white, GetElementColor(), Mathf.Clamp01(elementTintStrength));
    }

    Color GetElementColor()
    {
        switch (element)
        {
            case ElementType.Fire:
                return new Color(1f, 0.2f, 0.1f);
            case ElementType.Water:
                return new Color(0.15f, 0.45f, 1f);
            case ElementType.Earth:
                return new Color(0.65f, 0.3f, 0.08f);
            case ElementType.Electric:
                return new Color(1f, 0.85f, 0.05f);
            default:
                return new Color(0.34f, 0.4f, 0.31f);
        }
    }
}
