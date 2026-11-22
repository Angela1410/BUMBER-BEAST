using UnityEngine;

// Temporary Rockfall obstacle spawned by TerrathornAbility. Drops in with a
// squash, cracks when a Beast hits it, and crumbles after enough hits or when
// its lifetime runs out.
public class TerrathornBoulder : MonoBehaviour
{
    private int hitsRemaining = 2;
    private float expiresAt;
    private float baseScale = 1f;
    private float spawnedAt;
    private float hitPunchUntil;
    private SpriteRenderer spriteRenderer;
    private bool crumbling;

    public void Initialise(int hits, float lifetime, float scale)
    {
        hitsRemaining = Mathf.Max(1, hits);
        baseScale = scale;
        spawnedAt = Time.time;
        expiresAt = Time.time + Mathf.Max(1f, lifetime);
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (crumbling)
            return;

        // Land with a short squash-and-settle.
        float landing = Mathf.Clamp01((Time.time - spawnedAt) / 0.22f);
        float squash = 1f - Mathf.Sin(landing * Mathf.PI) * 0.25f;
        float punch = Time.time < hitPunchUntil ? 0.9f : 1f;
        transform.localScale = new Vector3(
            baseScale * (2f - squash) * punch,
            baseScale * squash * punch,
            1f);

        // Blink during the last second so its disappearance is never a surprise.
        if (spriteRenderer != null)
        {
            float remaining = expiresAt - Time.time;
            Color color = spriteRenderer.color;
            color.a = remaining < 1f ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(remaining * 14f)) : 1f;
            spriteRenderer.color = color;
        }

        if (Time.time >= expiresAt)
            Crumble();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (crumbling || collision.gameObject.GetComponent<BeastElement>() == null)
            return;

        hitsRemaining--;
        hitPunchUntil = Time.time + 0.08f;
        CameraShake.Shake(false);
        if (spriteRenderer != null)
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, new Color(0.7f, 0.6f, 0.5f, 1f), 0.4f);

        if (hitsRemaining <= 0)
            Crumble();
    }

    void Crumble()
    {
        crumbling = true;
        Collider2D boulderCollider = GetComponent<Collider2D>();
        if (boulderCollider != null)
            boulderCollider.enabled = false;

        // A few rock chips burst outward.
        Sprite chip = HudVisualUtility.CircleSprite();
        for (int index = 0; index < 6; index++)
        {
            GameObject piece = new GameObject("BoulderChip");
            piece.transform.position = transform.position;
            piece.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);
            SpriteRenderer pieceRenderer = piece.AddComponent<SpriteRenderer>();
            pieceRenderer.sprite = chip;
            pieceRenderer.color = new Color(0.55f, 0.44f, 0.34f, 1f);
            pieceRenderer.sortingOrder = 3;
            Rigidbody2D body = piece.AddComponent<Rigidbody2D>();
            body.gravityScale = 1.5f;
            float angle = index / 6f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            body.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) + 0.4f) * Random.Range(2f, 3.5f);
            Destroy(piece, 0.6f);
        }

        Destroy(gameObject);
    }
}
