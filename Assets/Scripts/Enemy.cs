using System.Collections;
using UnityEngine;
using TMPro;

public class Enemy : MonoBehaviour
{
    public int maxHealth = 1;
    public ElementType element = ElementType.Grass;

    public float moveSpeed = 0.3f;
    public TMP_Text bossHealthText;
    public int waveHealthBonus;
    public float waveSpeedMultiplier = 1f;
    public float hitFlashDuration = 0.08f;
    public float barrierAvoidanceDistance = 1.6f;
    public float barrierSideSpeed = 2f;
    public float barrierRouteMargin = 0.15f;

    private int currentHealth;
    private SpriteRenderer sr;
    private Color originalColor;
    private bool statsApplied;
    private Coroutine rootRoutine;
    private float baseMoveSpeed;
    private Rigidbody2D body;
    private Collider2D barrierObstacle;

    void Awake()
    {
        baseMoveSpeed = moveSpeed;
        body = GetComponent<Rigidbody2D>();
        FindBarrierObstacle();
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

        UpdateHealthText();
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
        float speed = direction.y == 0f ? Mathf.Max(moveSpeed, barrierSideSpeed) : moveSpeed;
        Vector2 nextPosition = (Vector2)transform.position + direction * speed * Time.fixedDeltaTime;

        if (body != null && body.bodyType == RigidbodyType2D.Kinematic)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    void FindBarrierObstacle()
    {
        GameObject barrier = GameObject.Find("Square");
        barrierObstacle = barrier != null ? barrier.GetComponent<Collider2D>() : null;
    }

    Vector2 GetMovementDirection()
    {
        if (barrierObstacle == null)
        {
            FindBarrierObstacle();
            return Vector2.down;
        }

        Bounds barrierBounds = barrierObstacle.bounds;
        Collider2D enemyCollider = GetComponent<Collider2D>();
        Bounds enemyBounds = enemyCollider != null
            ? enemyCollider.bounds
            : new Bounds(transform.position, Vector3.one);

        bool approachingBarrier = enemyBounds.min.y <= barrierBounds.max.y + barrierAvoidanceDistance &&
            enemyBounds.max.y > barrierBounds.max.y - 0.05f;
        bool overlapsBarrierX = enemyBounds.max.x > barrierBounds.min.x &&
            enemyBounds.min.x < barrierBounds.max.x;

        if (!approachingBarrier || !overlapsBarrierX)
            return Vector2.down;

        float leftRouteX = barrierBounds.min.x - enemyBounds.extents.x - barrierRouteMargin;
        float rightRouteX = barrierBounds.max.x + enemyBounds.extents.x + barrierRouteMargin;
        float direction = Mathf.Abs(transform.position.x - leftRouteX) <=
            Mathf.Abs(transform.position.x - rightRouteX) ? -1f : 1f;

        return Vector2.right * direction;
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
        if (bossHealthText != null)
            bossHealthText.text = "HP: " + currentHealth + " / " + (maxHealth + waveHealthBonus);
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