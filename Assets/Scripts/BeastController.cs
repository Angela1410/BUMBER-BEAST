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

public class BeastController : MonoBehaviour
{
    public Transform launchPad;
    public float skillCooldown = 6f;
    public float returnSpeed = 12f;
    public float stopDelay = 0.3f;
    public float autoReturnSpeed = 1.5f;
    public float maximumFlightTime = 3.2f;
    public float maximumSpeed = 9f;
    public float skillBounceSpeed = 6.2f;
    public float skillRadius = 3f;

    public BeastState State { get; private set; } = BeastState.AtPad;
    public BeastId ActiveBeast { get; private set; } = BeastId.Choma;
    public float CooldownRemaining { get; private set; }
    public bool IsSkillReady => CooldownRemaining <= 0f;
    public bool CanUseSkill => State == BeastState.Flying && IsSkillReady;
    public bool IsDoubleBounceActive { get; private set; }

    // The player's Beast in the running stage (one per scene); read by enemy element badges.
    public static BeastController Active { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private float assistRadius = 1.6f;
    private float assistTurnDegrees = 190f;
    private float stoppedTime;
    private float flightTime;
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

        // Aim assist is strongest in chapter 1, where players are still learning to aim.
        StageManager stage = FindAnyObjectByType<StageManager>();
        bool firstChapter = stage == null || ChapterData.ChapterOfStage(stage.stageNumber) == 0;
        assistRadius = firstChapter ? 2.1f : 1.6f;
        assistTurnDegrees = firstChapter ? 280f : 190f;
        // Settings > DIFFICULTY: strong on EASY, weak on HARD, off on HELL.
        float strength = Difficulty.AssistStrength;
        assistRadius *= 0.5f + 0.5f * strength;
        assistTurnDegrees *= strength;
    }

    // Settings > AIM ASSIST: bends the flight toward the nearest enemy just ahead, never more than
    // a few degrees per physics step, keeping the Beast's speed.
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

            if (rb == null || rb.linearVelocity.magnitude <= autoReturnSpeed)
                stoppedTime += Time.deltaTime;
            else
                stoppedTime = 0f;

            if (stoppedTime >= stopDelay || flightTime >= maximumFlightTime)
                BeginReturn();
        }
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
        // Wall thuds; enemies and bumpers play their own hit sounds.
        if ((State == BeastState.Flying || State == BeastState.UsingSkill) && collision.collider != null &&
            collision.collider.GetComponentInParent<Enemy>() == null && collision.collider.GetComponent<Bumper>() == null)
            GameAudio.Play(Sfx.Bounce, Mathf.Clamp01(collision.relativeVelocity.magnitude / 12f) * 0.8f + 0.2f);

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
    }

    public void ActivateSkill()
    {
        if (!CanUseSkill)
            return;

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
        }

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

            // A jagged bolt from the last point to this target.
            Vector3 from = currentPosition;
            Vector3 to = closest.transform.position;
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.forward);
            for (int step = 1; step <= 3 && zapCount < zapPoints.Length - 1; step++)
                zapPoints[zapCount++] = Vector3.Lerp(from, to, step / 4f) + side * Random.Range(-0.28f, 0.28f);
            zapPoints[zapCount++] = to;

            currentPosition = closest.transform.position;
            ApplyElementalDamage(closest);
            candidates.Remove(closest);
        }

        ShowSkillRing(ZapColor);
        if (zapCount > 1)
            ShowZap(zapCount);
    }

    void SplashNearbyEnemies()
    {
        foreach (Enemy enemy in FindEnemiesInRadius())
            ApplyElementalDamage(enemy);

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

    // An expanding ring in the ability's colour (every Beast's skill shows one).
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

    // A wave was cleared: a Beast still in the air flies straight home to the pad.
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

        while (targetPad != null &&
            Vector2.Distance(transform.position, targetPad.position) > 0.05f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPad.position,
                Mathf.Max(0.01f, returnSpeed) * Time.deltaTime);
            yield return null;
        }

        if (targetPad != null)
        {
            transform.position = targetPad.position;
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
