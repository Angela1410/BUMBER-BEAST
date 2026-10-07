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

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private float stoppedTime;
    private float flightTime;
    private Coroutine returnRoutine;

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

    void Start()
    {
        if (GameProgress.Instance != null)
            skillCooldown = Mathf.Max(1f,
                skillCooldown - GameProgress.Instance.SkillCooldownLevel * 0.5f);
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

        if (rb.linearVelocity.magnitude > maximumSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maximumSpeed;
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
        {
            int damage = enemy.element == ElementType.Grass ? 2 : 1;
            enemy.TakeDamage(damage);
        }

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

        IsDoubleBounceActive = false;
    }

    void RootNearbyEnemies()
    {
        foreach (Enemy enemy in FindEnemiesInRadius())
            enemy.Root(2.5f);
    }

    void KnockNearbyEnemies()
    {
        foreach (Enemy enemy in FindEnemiesInRadius())
        {
            Vector2 direction = (enemy.transform.position - transform.position).normalized;
            enemy.KnockBack(direction + Vector2.up * 0.75f, 2f, 0.25f);
            enemy.TakeDamage(1);
        }
    }

    void ChainZapEnemies()
    {
        List<Enemy> candidates = FindEnemiesInRadius();
        Vector3 currentPosition = transform.position;

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

            currentPosition = closest.transform.position;
            closest.TakeDamage(1);
            candidates.Remove(closest);
        }
    }

    void SplashNearbyEnemies()
    {
        foreach (Enemy enemy in FindEnemiesInRadius())
            enemy.TakeDamage(1);
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

    public void BeginReturn()
    {
        if (State == BeastState.Returning)
            return;

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