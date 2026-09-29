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
    public float CooldownRemaining { get; private set; }
    public bool IsSkillReady => CooldownRemaining <= 0f;
    public bool CanUseSkill => State == BeastState.Flying && IsSkillReady;

    private Rigidbody2D rb;
    private float stoppedTime;
    private float flightTime;
    private Coroutine returnRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

        StartCoroutine(ShowSkillFeedback());
        StartCoroutine(UseSelectedSkill());
    }

    IEnumerator ShowSkillFeedback()
    {
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite == null)
            yield break;

        Color originalColor = sprite.color;
        sprite.color = Color.white;
        yield return new WaitForSeconds(0.16f);

        if (sprite != null)
            sprite.color = originalColor;
    }

    IEnumerator UseSelectedSkill()
    {
        State = BeastState.UsingSkill;
        CooldownRemaining = skillCooldown;
        BeastId selectedBeast = GameProgress.Instance != null
            ? GameProgress.Instance.SelectedBeast
            : BeastId.Choma;

        switch (selectedBeast)
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
                DoubleBounce();
                break;
        }

        yield return new WaitForSeconds(0.2f);

        if (State == BeastState.UsingSkill)
            State = BeastState.Flying;
    }

    void DoubleBounce()
    {
        if (rb == null)
            return;

        Vector2 direction = rb.linearVelocity.sqrMagnitude > 0.01f
            ? rb.linearVelocity.normalized
            : Vector2.up;

        rb.linearVelocity = Vector2.ClampMagnitude(
            rb.linearVelocity + direction * skillBounceSpeed,
            maximumSpeed);
        StartCoroutine(SecondBounce(direction));
        stoppedTime = 0f;
    }

    IEnumerator SecondBounce(Vector2 direction)
    {
        yield return new WaitForSeconds(0.18f);

        if (State == BeastState.UsingSkill && rb != null)
            rb.linearVelocity = Vector2.ClampMagnitude(
                rb.linearVelocity + direction * skillBounceSpeed * 0.5f,
                maximumSpeed);
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

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (Vector2.Distance(transform.position, enemy.transform.position) <= skillRadius)
                found.Add(enemy);
        }

        return found;
    }

    public void BeginReturn()
    {
        if (State == BeastState.Returning || launchPad == null)
            return;

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnToPadRoutine());
    }

    IEnumerator ReturnToPadRoutine()
    {
        State = BeastState.Returning;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        while (Vector2.Distance(transform.position, launchPad.position) > 0.05f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                launchPad.position,
                returnSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = launchPad.position;
        State = BeastState.AtPad;
        stoppedTime = 0f;
        flightTime = 0f;
        returnRoutine = null;
    }
}