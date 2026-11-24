using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BeastState
{
    AtPad,
    Flying,
    UsingSkill,
    Returning
}

// when the Beast comes back to the pad (after enough bounces or seconds) and how fast it flies back
// are in GameTuning (Coming back to the pad), so is the bounce power
public class BeastController : MonoBehaviour
{
    public Transform launchPad;
    public float skillCooldown = 6f;
    public float stopDelay = 0.3f;
    public float autoReturnSpeed = 1.5f;
    public float maximumSpeed = 9f;
    public float skillBounceSpeed = 6.2f;
    public float skillRadius = 3f;

    public BeastState State { get; private set; } = BeastState.AtPad;
    public BeastId ActiveBeast { get; private set; } = BeastId.Choma;
    public float CooldownRemaining { get; private set; }
    public bool IsSkillReady => CooldownRemaining <= 0f;
    // the skill only works while the Beast is flying and the skill is recharged
    public bool CanUseSkill => State == BeastState.Flying && IsSkillReady;
    public bool IsDoubleBounceActive { get; private set; }

    // BOUNCE POWER: every bumper or wall the Beast bounces off in one shot adds damage to its hits
    // for the rest of that shot (GameTuning: Bounce power). it starts at 0 on every launch
    public int BouncePower { get; private set; }
    // everything the Beast bounced off in this shot (walls, bumpers, enemies)
    public int ShotBounces { get; private set; }

    // after SWAP calls a flying Beast back, the next launch waits a moment (the swap button shows it)
    public bool IsLaunchLocked => Time.time < launchLockUntil;
    // 1 right when the lock starts, 0 when you can launch again
    public float LaunchLockLeft => IsLaunchLocked ? Mathf.Clamp01((launchLockUntil - Time.time) / launchLockLength) : 0f;

    // the player's Beast in this stage (only one per scene). the enemy badges read it
    public static BeastController Active { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private float assistRadius = 1.6f;
    private float assistTurnDegrees = 190f;
    private float stoppedTime;
    private float flightTime;
    // seconds since the Beast last hit an enemy or a bumper (or used its skill) in this shot
    private float sinceUsefulHit;
    private float bumperSpeedCap;
    private Vector2 chargedBounceNormal;
    private bool chargedBouncePending;
    private Coroutine returnRoutine;
    private LineRenderer flameBurstRing;
    private Coroutine flameBurstRingRoutine;
    private Material flameBurstRingMaterial;
    private Color skillRingColor = new Color(1f, 0.48f, 0.08f, 1f);
    private LineRenderer zapLine;
    private Coroutine zapRoutine;
    private readonly Vector3[] zapPoints = new Vector3[16];
    private float launchLockUntil;
    private float launchLockLength = 1f;
    // where the Beast stands on the pad at the start (the pad's own middle is a bit lower)
    private Vector3 restSpot;
    private bool hasRestSpot;
    private SpriteRenderer powerGlow;
    private TrailRenderer powerTrail;
    private Material powerTrailMaterial;
    private float powerFlashUntil;

    private static readonly Color FlameRingColor = new Color(1f, 0.48f, 0.08f, 1f);
    private static readonly Color KnockRingColor = new Color(0.78f, 0.56f, 0.32f, 1f);
    private static readonly Color SplashRingColor = new Color(0.4f, 0.78f, 1f, 1f);
    private static readonly Color ZapColor = new Color(1f, 0.92f, 0.35f, 1f);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, skillRadius));
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (launchPad == null)
        {
            GameObject padObject = GameObject.Find("LaunchPad");
            if (padObject != null)
                launchPad = padObject.transform;
        }

        State = BeastState.AtPad;
        restSpot = transform.position;
        hasRestSpot = true;
    }

    void OnEnable()
    {
        Active = this;
    }

    void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    void Start()
    {
        if (GameProgress.Instance != null)
            skillCooldown = Mathf.Max(1f,
                skillCooldown - GameProgress.Instance.SkillCooldownLevel * 0.5f);

        // aim assist is strongest in chapter 1, when players are still learning to aim
        StageManager stage = FindAnyObjectByType<StageManager>();
        bool firstChapter = stage == null || ChapterData.ChapterOfStage(stage.stageNumber) == 0;
        assistRadius = firstChapter ? 2.1f : 1.6f;
        assistTurnDegrees = firstChapter ? 280f : 190f;
        // Settings > DIFFICULTY: strong on EASY, weak on HARD, off on HELL
        float strength = Difficulty.AssistStrength;
        assistRadius *= 0.5f + 0.5f * strength;
        assistTurnDegrees *= strength;
    }

    // Settings > AIM ASSIST: bends the flight a little toward the closest enemy ahead, only a few
    // degrees per physics step, and the Beast keeps its speed
    void ApplyAimAssist()
    {
        if (!GameSettings.AimAssist || State != BeastState.Flying || assistTurnDegrees <= 0f)
            return;
        Vector2 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < 1.5f)
            return;

        Vector2 position = rb.position;
        float bestDistance = assistRadius * assistRadius;
        Vector2 target = Vector2.zero;
        bool found = false;
        List<Enemy> enemies = Enemy.ActiveEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null || enemy.IsDead)
                continue;
            Vector2 toEnemy = (Vector2)enemy.transform.position - position;
            float distance = toEnemy.sqrMagnitude;
            if (distance > bestDistance || Vector2.Dot(toEnemy, velocity) <= 0f)
                continue;
            bestDistance = distance;
            target = toEnemy;
            found = true;
        }
        if (!found)
            return;

        float maxTurn = assistTurnDegrees * Mathf.Deg2Rad * Time.fixedDeltaTime;
        rb.linearVelocity = Vector3.RotateTowards(velocity, target.normalized * speed, maxTurn, 0f);
    }

    void Update()
    {
        if (CooldownRemaining > 0f)
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);

        if (State == BeastState.Flying)
        {
            flightTime += Time.deltaTime;
            sinceUsefulHit += Time.deltaTime;

            if (rb == null || rb.linearVelocity.magnitude <= autoReturnSpeed)
                stoppedTime += Time.deltaTime;
            else
                stoppedTime = 0f;

            // fly back to the pad when it stopped moving, after enough bounces or enough time, or when it
            // hasn't hit anything useful for a while (whichever comes first)
            GameTuning tuning = GameTuning.Get;
            bool enoughBounces = tuning.returnAfterBounces > 0 && ShotBounces >= tuning.returnAfterBounces;
            bool nothingHit = tuning.returnWhenNothingHit > 0f && sinceUsefulHit >= tuning.returnWhenNothingHit;
            if (stoppedTime >= stopDelay || flightTime >= tuning.returnAfterSeconds || enoughBounces || nothingHit)
                BeginReturn();
        }
    }

    // the glow and the trail grow with the bounce power (after LaunchController turned and sized the Beast)
    void LateUpdate()
    {
        UpdatePowerLook();
    }

    void FixedUpdate()
    {
        if (rb == null || State == BeastState.AtPad || State == BeastState.Returning)
            return;

        ApplyAimAssist();

        if (chargedBouncePending)
        {
            Vector2 velocity = rb.linearVelocity;
            float normalSpeed = Vector2.Dot(velocity, chargedBounceNormal);
            Vector2 tangentVelocity = velocity - chargedBounceNormal * normalSpeed;
            float reboundSpeed = Mathf.Max(skillBounceSpeed, Mathf.Abs(normalSpeed));
            float reboundSpeedLimit = Mathf.Max(maximumSpeed, bumperSpeedCap);
            rb.linearVelocity = Vector2.ClampMagnitude(
                tangentVelocity + chargedBounceNormal * reboundSpeed,
                reboundSpeedLimit);
            chargedBouncePending = false;
            stoppedTime = 0f;
        }

        float speedLimit = Mathf.Max(maximumSpeed, bumperSpeedCap);
        if (rb.linearVelocity.magnitude > speedLimit)
            rb.linearVelocity = rb.linearVelocity.normalized * speedLimit;

        if (rb.linearVelocity.magnitude <= maximumSpeed)
            bumperSpeedCap = 0f;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if ((State == BeastState.Flying || State == BeastState.UsingSkill) && collision.collider != null)
        {
            // every bounce counts toward flying back to the pad
            ShotBounces++;
            // a wall makes the Beast stronger and plays the wall sound (bumpers and enemies do their own)
            if (collision.collider.GetComponentInParent<Enemy>() == null && collision.collider.GetComponent<Bumper>() == null)
            {
                GainBouncePower();
                GameAudio.Play(Sfx.Bounce, Mathf.Clamp01(collision.relativeVelocity.magnitude / 12f) * 0.8f + 0.2f);
            }
            else
            {
                // an enemy or a bumper: the shot is still doing something, so it keeps flying
                sinceUsefulHit = 0f;
            }
        }

        if (!IsDoubleBounceActive || rb == null || collision.collider == null ||
            State == BeastState.AtPad || State == BeastState.Returning)
            return;

        Vector2 outward = rb.position - collision.collider.ClosestPoint(rb.position);
        if (outward.sqrMagnitude <= 0.0001f)
            outward = rb.position - (Vector2)collision.collider.bounds.center;
        if (outward.sqrMagnitude <= 0.0001f && collision.contactCount > 0)
            outward = collision.GetContact(0).normal;
        if (outward.sqrMagnitude <= 0.0001f)
            return;

        chargedBounceNormal = outward.normalized;
        chargedBouncePending = true;
        IsDoubleBounceActive = false;
        stoppedTime = 0f;
    }

    public void SetBumperSpeedCap(float speed)
    {
        bumperSpeedCap = Mathf.Max(bumperSpeedCap, speed);
    }

    public void NotifyLaunched()
    {
        if (State == BeastState.Returning)
            return;

        State = BeastState.Flying;
        stoppedTime = 0f;
        flightTime = 0f;
        sinceUsefulHit = 0f;
        // a new shot starts with no bounce power
        BouncePower = 0;
        ShotBounces = 0;
        if (powerTrail != null)
            powerTrail.Clear();
    }

    // a bumper or a wall hit: the Beast gets stronger for the rest of this shot (Bumper calls this too).
    // returns the power now, so the bumper sound can go up with it
    public int GainBouncePower()
    {
        if (State != BeastState.Flying && State != BeastState.UsingSkill)
            return BouncePower;
        GameTuning tuning = GameTuning.Get;
        BouncePower = Mathf.Min(Mathf.Max(0, tuning.bouncePowerMax), BouncePower + Mathf.Max(0, tuning.bouncePowerPerHit));
        powerFlashUntil = Time.time + 0.12f;
        return BouncePower;
    }

    // SWAP called the Beast back mid-flight: no launching for a moment
    public void LockLaunch(float seconds)
    {
        if (seconds <= 0f)
            return;
        launchLockLength = seconds;
        launchLockUntil = Time.time + seconds;
    }

    // a soft glow around the Beast and a short trail behind it, both brighter and bigger with more power
    void UpdatePowerLook()
    {
        bool show = BouncePower > 0 && (State == BeastState.Flying || State == BeastState.UsingSkill);
        if (!show)
        {
            if (powerGlow != null && powerGlow.enabled)
                powerGlow.enabled = false;
            if (powerTrail != null && powerTrail.emitting)
                powerTrail.emitting = false;
            return;
        }

        CreatePowerLook();
        float level = Mathf.Clamp01((float)BouncePower / Mathf.Max(1, GameTuning.Get.bouncePowerMax));
        bool flash = Time.time < powerFlashUntil;
        Color color = BeastRoster.GetEffectColor(ActiveBeast);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
        color.a = Mathf.Clamp01(0.3f + 0.45f * level + (flash ? 0.25f : 0f) + 0.08f * pulse);
        powerGlow.color = color;
        powerGlow.enabled = true;
        powerGlow.transform.localScale = Vector3.one * GlowScale() * (1.15f + 0.4f * level + (flash ? 0.2f : 0f));

        Color tail = color;
        tail.a = 0f;
        Color head = color;
        head.a = 0.35f + 0.45f * level;
        powerTrail.startColor = head;
        powerTrail.endColor = tail;
        powerTrail.widthMultiplier = BeastRadius() * 2f * (0.35f + 0.5f * level);
        powerTrail.time = 0.12f + 0.18f * level;
        powerTrail.emitting = true;
    }

    void CreatePowerLook()
    {
        if (powerGlow != null)
            return;

        int order = spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
        powerGlow = GameArt.AddLayer(transform, "BouncePowerGlow", HudVisualUtility.GlowSprite(),
            GameArt.AdditiveGlow(spriteRenderer != null ? spriteRenderer.sharedMaterial : null), spriteRenderer, -1);
        powerGlow.enabled = false;

        GameObject trailObject = new GameObject("BouncePowerTrail");
        trailObject.transform.SetParent(transform, false);
        powerTrail = trailObject.AddComponent<TrailRenderer>();
        powerTrail.emitting = false;
        powerTrail.numCapVertices = 4;
        powerTrail.minVertexDistance = 0.05f;
        powerTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        powerTrail.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        powerTrail.sortingOrder = order - 2;
        EnsureSkillRingMaterial();
        powerTrailMaterial = flameBurstRingMaterial;
        if (powerTrailMaterial != null)
            powerTrail.sharedMaterial = powerTrailMaterial;
    }

    // the glow is about 1.6 times the Beast's size, whatever its own scale is
    float GlowScale()
    {
        Sprite glow = powerGlow != null ? powerGlow.sprite : null;
        float glowSize = glow != null ? Mathf.Max(0.01f, glow.bounds.size.x) : 1f;
        float parentScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        return BeastRadius() * 2f * 1.6f / glowSize / parentScale;
    }

    float BeastRadius()
    {
        return CameraFit.WorldRadius(this, 0.5f);
    }

    Vector2 RestSpot(Transform pad)
    {
        return hasRestSpot ? (Vector2)restSpot : (Vector2)pad.position;
    }

    // the skill button calls this
    public void ActivateSkill()
    {
        if (!CanUseSkill)
            return;

        // using the skill counts as doing something, so the Beast doesn't fly back right after
        sinceUsefulHit = 0f;
        StartCoroutine(UseSelectedSkill());
    }

    IEnumerator UseSelectedSkill()
    {
        State = BeastState.UsingSkill;
        CooldownRemaining = skillCooldown;
        GameAudio.PlaySkill(BeastRoster.GetElement(ActiveBeast));
        switch (ActiveBeast)
        {
            case BeastId.Thistle:
                RootNearbyEnemies();
                break;
            case BeastId.Mossback:
                KnockNearbyEnemies();
                break;
            case BeastId.Voltis:
                ChainZapEnemies();
                break;
            case BeastId.Ripple:
                SplashNearbyEnemies();
                break;
            default:
                FlameBurst();
                break;
        }

        yield return new WaitForSeconds(0.2f);

        if (State == BeastState.UsingSkill)
            State = BeastState.Flying;
    }

    public void SetActiveBeast(BeastId beast)
    {
        if (System.Array.IndexOf(BeastRoster.AllBeasts, beast) < 0)
        {
            Debug.LogError("Cannot activate an unknown Beast: " + beast, this);
            return;
        }

        ActiveBeast = beast;
        gameObject.name = BeastRoster.GetName(beast);

        if (spriteRenderer != null)
            spriteRenderer.color = BeastRoster.GetColor(beast);

        BeastElement element = GetComponent<BeastElement>();
        if (element != null)
            element.element = BeastRoster.GetElement(beast);
    }

    void FlameBurst()
    {
        if (rb == null)
            return;

        IsDoubleBounceActive = true;
        StartCoroutine(ShowFlameBurstFeedback());

        Vector2 direction = rb.linearVelocity.sqrMagnitude > 0.01f
            ? rb.linearVelocity.normalized
            : Vector2.up;

        rb.linearVelocity = Vector2.ClampMagnitude(
            rb.linearVelocity + direction * skillBounceSpeed,
            maximumSpeed);

        foreach (Enemy enemy in FindEnemiesInRadius())
            ApplyElementalDamage(enemy, 2);

        // the fire blast picture (Assets/Resources/Fx/FlameBurst) plus the ring
        FxArt.Play("FlameBurst", transform.position, skillRadius * 1.6f, 20f);
        ShowSkillRing(FlameRingColor);
        stoppedTime = 0f;
    }

    IEnumerator ShowFlameBurstFeedback()
    {
        if (spriteRenderer == null)
            yield break;

        Color originalColor = spriteRenderer.color;
        Vector3 originalScale = transform.localScale;
        spriteRenderer.color = new Color(1f, 0.5f, 0.1f, 1f);
        transform.localScale = originalScale * 1.12f;
        yield return new WaitForSeconds(0.22f);

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (transform != null)
            transform.localScale = originalScale;

    }

    void RootNearbyEnemies()
    {
        LaunchController launcher = GetComponent<LaunchController>();
        Sprite[] effectFrames = launcher != null
            ? launcher.GetSkillEffectFrames(BeastId.Thistle)
            : null;
        SpriteFlipbookFx.Play(effectFrames, transform.position, skillRadius, 14f);

        foreach (Enemy enemy in FindEnemiesInRadius())
            enemy.Root(2.5f);
    }

    void KnockNearbyEnemies()
    {
        foreach (Enemy enemy in FindEnemiesInRadius())
        {
            Vector2 direction = (enemy.transform.position - transform.position).normalized;
            enemy.KnockBack(direction + Vector2.up * 0.75f, 2f, 0.25f);
            ApplyElementalDamage(enemy);
            FxArt.Play("DustBurst", enemy.transform.position, 1.2f, 18f);
        }

        // the ground cracks and rocks fly out (Fx/HeavyKnock)
        FxArt.Play("HeavyKnock", transform.position, skillRadius * 1.8f, 18f);
        ShowSkillRing(KnockRingColor);
        CameraShake.Shake(true);
    }

    void ChainZapEnemies()
    {
        List<Enemy> candidates = FindEnemiesInRadius();
        Vector3 currentPosition = transform.position;
        int zapCount = 0;
        zapPoints[zapCount++] = transform.position;

        for (int hit = 0; hit < 3 && candidates.Count > 0; hit++)
        {
            Enemy closest = null;
            float closestDistance = float.MaxValue;

            foreach (Enemy candidate in candidates)
            {
                float distance = Vector2.Distance(currentPosition, candidate.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            if (closest == null)
                break;

            // a zig-zag lightning bolt from the last point to this target
            Vector3 from = currentPosition;
            Vector3 to = closest.transform.position;
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.forward);
            for (int step = 1; step <= 3 && zapCount < zapPoints.Length - 1; step++)
                zapPoints[zapCount++] = Vector3.Lerp(from, to, step / 4f) + side * Random.Range(-0.28f, 0.28f);
            zapPoints[zapCount++] = to;

            // a lightning bolt picture jumps to the target and sparks on it (Fx/ZapBolt, Fx/ZapImpact)
            FxArt.Bolt(from, to, 0.5f, 0.3f);
            FxArt.Play("ZapImpact", to, 1.3f, 20f);
            currentPosition = closest.transform.position;
            ApplyElementalDamage(closest);
            candidates.Remove(closest);
        }

        ShowSkillRing(ZapColor);
        FxArt.Play("ZapImpact", transform.position, 1.6f, 20f);
        // the plain line is only for when the bolt pictures are missing
        if (zapCount > 1 && !FxArt.Has("ZapBolt"))
            ShowZap(zapCount);
    }

    void SplashNearbyEnemies()
    {
        foreach (Enemy enemy in FindEnemiesInRadius())
        {
            ApplyElementalDamage(enemy);
            FxArt.Play("SplashWave", enemy.transform.position, 1f, 22f);
        }

        // a ring of water rolls out from Ripple (Fx/SplashWave)
        FxArt.Play("SplashWave", transform.position, skillRadius * 2f, 18f);
        ShowSkillRing(SplashRingColor);
    }

    void ShowZap(int pointCount)
    {
        if (zapLine == null)
        {
            GameObject zapObject = new GameObject("ChainZap");
            zapLine = zapObject.AddComponent<LineRenderer>();
            zapLine.useWorldSpace = true;
            zapLine.numCornerVertices = 1;
            zapLine.startWidth = 0.09f;
            zapLine.endWidth = 0.05f;
            zapLine.sortingOrder = 31;
            EnsureSkillRingMaterial();
            if (flameBurstRingMaterial != null)
                zapLine.sharedMaterial = flameBurstRingMaterial;
        }

        zapLine.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
            zapLine.SetPosition(i, zapPoints[i]);

        if (zapRoutine != null)
            StopCoroutine(zapRoutine);
        zapRoutine = StartCoroutine(FadeZap());
    }

    IEnumerator FadeZap()
    {
        const float duration = 0.3f;
        zapLine.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Color color = ZapColor;
            color.a = 1f - Mathf.Clamp01(elapsed / duration);
            zapLine.startColor = color;
            zapLine.endColor = color;
            yield return null;
        }

        zapLine.enabled = false;
        zapRoutine = null;
    }

    void EnsureSkillRingMaterial()
    {
        if (flameBurstRingMaterial != null)
            return;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            flameBurstRingMaterial = new Material(shader);
        else
            Debug.LogError("Shader 'Sprites/Default' is missing; skill effects will render with the default material.", this);
    }

    void ApplyElementalDamage(Enemy enemy, int baseDamage = 1)
    {
        if (enemy == null)
            return;

        bool superEffective = IsSuperEffectiveAgainst(enemy);
        int damage = baseDamage + (superEffective ? 1 : 0);
        enemy.TakeDamage(damage, superEffective);
    }

    bool IsSuperEffectiveAgainst(Enemy enemy)
    {
        BeastElement attacker = GetComponent<BeastElement>();
        ElementType attackerElement = attacker != null ? attacker.element : ElementType.Fire;
        return enemy != null && ElementChart.IsSuperEffective(attackerElement, enemy.element);
    }

    // a ring that grows in the skill's color (every Beast's skill shows one)
    void ShowSkillRing(Color color)
    {
        skillRingColor = color;
        ShowFlameBurstRing();
    }

    void ShowFlameBurstRing()
    {
        if (flameBurstRing == null)
        {
            GameObject ringObject = new GameObject("FlameBurstRing");
            flameBurstRing = ringObject.AddComponent<LineRenderer>();
            flameBurstRing.useWorldSpace = false;
            flameBurstRing.loop = true;
            flameBurstRing.positionCount = 49;
            flameBurstRing.numCornerVertices = 2;
            flameBurstRing.startWidth = 0.08f;
            flameBurstRing.endWidth = 0.08f;
            flameBurstRing.sortingOrder = 30;
            flameBurstRing.startColor = new Color(1f, 0.48f, 0.08f, 1f);
            flameBurstRing.endColor = flameBurstRing.startColor;

            EnsureSkillRingMaterial();
            if (flameBurstRingMaterial != null)
                flameBurstRing.sharedMaterial = flameBurstRingMaterial;

            for (int i = 0; i < flameBurstRing.positionCount; i++)
            {
                float angle = i / 48f * Mathf.PI * 2f;
                flameBurstRing.SetPosition(i,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }

        if (flameBurstRingRoutine != null)
            StopCoroutine(flameBurstRingRoutine);

        flameBurstRingRoutine = StartCoroutine(AnimateFlameBurstRing());
    }

    IEnumerator AnimateFlameBurstRing()
    {
        const float duration = 0.25f;
        flameBurstRing.transform.position = transform.position;
        flameBurstRing.transform.localScale = Vector3.zero;
        flameBurstRing.enabled = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float radius = skillRadius * progress;
            flameBurstRing.transform.localScale = Vector3.one * radius;

            Color color = skillRingColor;
            color.a = 1f - progress;
            flameBurstRing.startColor = color;
            flameBurstRing.endColor = color;
            yield return null;
        }

        flameBurstRing.enabled = false;
        flameBurstRingRoutine = null;
    }

    void OnDestroy()
    {
        if (flameBurstRing != null)
            Destroy(flameBurstRing.gameObject);
        if (zapLine != null)
            Destroy(zapLine.gameObject);
        if (flameBurstRingMaterial != null)
            Destroy(flameBurstRingMaterial);
    }

    List<Enemy> FindEnemiesInRadius()
    {
        var found = new List<Enemy>();

        foreach (Enemy enemy in FindObjectsByType<Enemy>())
        {
            if (Vector2.Distance(transform.position, enemy.transform.position) <= skillRadius)
                found.Add(enemy);
        }

        return found;
    }

    // a wave was cleared: if the Beast is still flying it goes straight back to the pad
    public void RecallToPad()
    {
        if (State == BeastState.Flying || State == BeastState.UsingSkill)
            BeginReturn();
    }

    public void BeginReturn()
    {
        if (State == BeastState.Returning)
            return;

        IsDoubleBounceActive = false;
        chargedBouncePending = false;
        // the shot is over, so is its bounce power
        BouncePower = 0;
        ShotBounces = 0;

        if (launchPad == null)
        {
            Debug.LogError("BeastController cannot return to the launch pad because no LaunchPad is assigned or present in the scene.", this);
            return;
        }

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        State = BeastState.Returning;
        returnRoutine = StartCoroutine(ReturnToPadRoutine());
    }

    IEnumerator ReturnToPadRoutine()
    {
        Transform targetPad = launchPad;
        bool bodyWasSimulated = rb != null && rb.simulated;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // a quick flight back that always takes the same short time (GameTuning: Return Flight Time),
        // fast at first and gentle at the end. it lands where the Beast stands on the pad, so it
        // doesn't pop up from the pad's middle after landing
        float duration = Mathf.Max(0.05f, GameTuning.Get.returnFlightTime);
        Vector2 start = transform.position;
        float elapsed = 0f;
        while (targetPad != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            transform.position = Vector2.LerpUnclamped(start, RestSpot(targetPad), eased);
            yield return null;
        }

        if (targetPad != null)
        {
            transform.position = hasRestSpot ? restSpot : targetPad.position;
            State = BeastState.AtPad;
        }
        else
        {
            Debug.LogError("The LaunchPad was destroyed while the Beast was returning.", this);
            State = BeastState.Flying;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = bodyWasSimulated;
        }

        stoppedTime = 0f;
        flightTime = 0f;
        returnRoutine = null;
    }
}
