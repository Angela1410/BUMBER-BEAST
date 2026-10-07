using System.Collections;
using UnityEngine;
using TMPro;

public class Enemy : MonoBehaviour
{
    public int maxHealth = 3;
    public ElementType element = ElementType.Grass;
    public bool isBoss = false;
    public string bossName = "Boss";
    public bool HasReachedBarrier { get; private set; }

    public float moveSpeed = 0.5f;
    public TMP_Text bossHealthText;
    public int waveHealthBonus;
    public float waveSpeedMultiplier = 1f;
    public float hitFlashDuration = 0.08f;

    private int currentHealth;
    private SpriteRenderer sr;
    private Color originalColor;
    private bool statsApplied;
    private Coroutine rootRoutine;
    private float baseMoveSpeed;
    private Rigidbody2D body;
    private Transform[] routeWaypoints;
    private int currentRouteIndex;
    private float routeLaneOffset;

    void Awake()
    {
        baseMoveSpeed = moveSpeed;
        body = GetComponent<Rigidbody2D>();
    }

    void OnValidate()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
            renderer.color = GetElementColor();
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
            sr.color = GetElementColor();
            originalColor = sr.color;
        }

        if (isBoss && bossHealthText == null)
        {
            GameObject bossTextObject = GameObject.Find("BossHealthText");
            if (bossTextObject != null)
                bossHealthText = bossTextObject.GetComponent<TMP_Text>();
        }

        UpdateHealthText();
    }

    public void SetBoss(string bossNameText, int bossHealth = 0, float bossSpeedMultiplier = 1f)
    {
        isBoss = true;
        bossName = string.IsNullOrEmpty(bossNameText) ? bossName : bossNameText;
        waveHealthBonus = bossHealth;
        waveSpeedMultiplier = bossSpeedMultiplier;

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
        if (!isBoss || bossHealthText == null)
            return;

        bossHealthText.text = string.Empty;
        bossHealthText.enabled = false;
    }

    public void MarkBarrierReached()
    {
        HasReachedBarrier = true;
    }

    public void SetRoute(Transform[] waypoints, float laneOffset = 0f)
    {
        routeWaypoints = waypoints;
        currentRouteIndex = 0;
        routeLaneOffset = laneOffset;
    }

    public void ApplyWaveData(ElementType assignedElement, int healthBonus, float speedMultiplier)
    {
        SetElementForEditor(assignedElement);
        waveHealthBonus = healthBonus;
        waveSpeedMultiplier = speedMultiplier;

        ApplyWaveStats();

        if (sr != null)
        {
            sr.color = GetElementColor();
            originalColor = sr.color;
        }

        UpdateHealthText();
    }

    public void SetElementForEditor(ElementType assignedElement)
    {
        element = assignedElement;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
            renderer.color = GetElementColor();
    }

    void ApplyWaveStats()
    {
        currentHealth = maxHealth + waveHealthBonus;
        moveSpeed = baseMoveSpeed * Mathf.Max(0.1f, waveSpeedMultiplier);
        statsApplied = true;
    }

    void FixedUpdate()
    {
        Vector2 direction = GetMovementDirection();
        Vector2 nextPosition = (Vector2)transform.position + direction * moveSpeed * Time.fixedDeltaTime;

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

            // If the enemy is close enough to the waypoint, move to the next one
            if (toWaypoint.sqrMagnitude <= 0.04f)
            {
                currentRouteIndex++;
                continue;
            }

            return toWaypoint.normalized;
        }

        // STRICT ROUTING FALLBACK:
        // If the enemy has no route, or has reached the end of its route, it stops moving.
        // We log a warning so you immediately know if a stage layout is missing a route assignment.
        if (routeWaypoints == null || routeWaypoints.Length == 0)
        {
            Debug.LogWarning(gameObject.name + " was spawned without a route and cannot move! Check your WaveManager or Stage configuration.");
        }

        return Vector2.zero;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        BeastElement attacker =
            collision.gameObject.GetComponent<BeastElement>();

        if (attacker != null)
        {
            int damage = 1;

            if (DoesBeat(attacker.element, element))
            {
                damage = 2;
            }

            TakeDamage(damage);
        }
    }

    bool DoesBeat(ElementType attackerType, ElementType defenderType)
    {
        if (attackerType == ElementType.Fire &&
            defenderType == ElementType.Grass)
            return true;

        if (attackerType == ElementType.Grass &&
            defenderType == ElementType.Earth)
            return true;

        if (attackerType == ElementType.Earth &&
            defenderType == ElementType.Electric)
            return true;

        if (attackerType == ElementType.Electric &&
            defenderType == ElementType.Water)
            return true;

        if (attackerType == ElementType.Water &&
            defenderType == ElementType.Fire)
            return true;

        return false;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        UpdateHealthText();
        StartCoroutine(FlashOnHit());

        Debug.Log(
            gameObject.name +
            " took " +
            amount +
            " damage"
        );

        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }

    public void Root(float duration)
    {
        if (rootRoutine != null)
            StopCoroutine(rootRoutine);

        rootRoutine = StartCoroutine(RootRoutine(duration));
    }

    public void KnockBack(Vector2 direction, float distance, float duration)
    {
        StartCoroutine(KnockBackRoutine(direction.normalized, distance, duration));
    }

    IEnumerator RootRoutine(float duration)
    {
        float originalSpeed = moveSpeed;
        moveSpeed = 0f;
        yield return new WaitForSeconds(duration);
        moveSpeed = originalSpeed;
        rootRoutine = null;
    }

    IEnumerator KnockBackRoutine(Vector2 direction, float distance, float duration)
    {
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(direction * distance);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }
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
                bossHealthText.enabled = true;
                string label = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName.ToUpper();
                bossHealthText.text = label + " HP: " + currentHealth + " / " + (maxHealth + waveHealthBonus);
            }

            return;
        }
    }

    IEnumerator FlashOnHit()
    {
        if (sr == null)
            yield break;

        sr.color = Color.white;
        yield return new WaitForSeconds(hitFlashDuration);
        sr.color = originalColor;
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