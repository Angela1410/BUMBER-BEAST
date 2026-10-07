using System.Collections;
using UnityEngine;

public enum BumperType { Green, Blue, Red }

public class Bumper : MonoBehaviour
{
    [Header("Bumper Settings")]
    public BumperType type = BumperType.Green;
    public int hp = 3;
    
    [Header("Green Bumper Physics")]
    public float speedMultiplier = 1.6f; // Cranked up to 60% boost
    public float minimumBoostSpeed = 12f; // Guarantees it shoots out fast even if hit slowly
    public float maxSpeed = 18f;

    private SpriteRenderer sr;
    private Vector3 originalScale;
    private static Sprite fallbackSprite;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (sr == null)
            sr = gameObject.AddComponent<SpriteRenderer>();

        sr.sprite = GetFallbackSprite();
        UpdateVisuals();
    }

    void Start()
    {
        originalScale = transform.localScale;
        UpdateVisuals();
    }

    void OnValidate()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            UpdateVisuals();
    }

    static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        const int textureSize = 64;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                Color pixel = distance > 30f
                    ? Color.clear
                    : distance > 24f
                        ? new Color(0.2f, 0.2f, 0.2f, 1f)
                        : Color.white;
                pixels[y * textureSize + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(false, true);
        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        fallbackSprite.name = "BumperFallback";
        return fallbackSprite;
    }

    void UpdateVisuals()
    {
        if (sr == null)
            return;

        switch (type)
        {
            case BumperType.Blue:
                sr.color = new Color(0.2f, 0.4f, 1f);
                break;
            case BumperType.Red:
                sr.color = new Color(0.9f, 0.2f, 0.2f);
                break;
            default:
                sr.color = new Color(0.2f, 0.8f, 0.2f);
                break;
        }
    }

    public void RefreshVisuals()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisuals();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // STRICT CHECK: Only take damage and apply effect if it's the player's Beast
        BeastController beast = collision.gameObject.GetComponent<BeastController>();
        
        if (beast != null)
        {
            ApplyEffect(collision.rigidbody, collision);
            TakeDamage();
            StartCoroutine(HitPunchEffect()); // Visual juice
        }
    }

    void ApplyEffect(Rigidbody2D beastRb, Collision2D collision)
    {
        if (beastRb == null) return;

        if (type == BumperType.Green)
        {
            Vector2 awayFromBumper = beastRb.position - (Vector2)transform.position;
            if (awayFromBumper.sqrMagnitude <= 0.0001f && collision.contactCount > 0)
                awayFromBumper = collision.GetContact(0).normal;
            if (awayFromBumper.sqrMagnitude <= 0.0001f)
                awayFromBumper = Vector2.up;
            awayFromBumper.Normalize();

            Vector2 velocity = beastRb.linearVelocity;
            float currentSpeed = velocity.magnitude;
            if (currentSpeed <= 0.01f)
            {
                velocity = awayFromBumper;
                currentSpeed = 0f;
            }
            else if (Vector2.Dot(velocity, awayFromBumper) <= 0f)
            {
                velocity = Vector2.Reflect(velocity, awayFromBumper);
            }

            if (Vector2.Dot(velocity, awayFromBumper) <= 0.01f)
                velocity += awayFromBumper * Mathf.Max(0.5f, velocity.magnitude * 0.25f);

            float newSpeed = Mathf.Min(
                Mathf.Max(currentSpeed * speedMultiplier, minimumBoostSpeed),
                maxSpeed);
            beastRb.linearVelocity = velocity.normalized * newSpeed;
        }
    }

    void TakeDamage()
    {
        hp--;
        UpdateVisuals();

        if (hp <= 0) BreakBumper();
    }

    void BreakBumper()
    {
        // Tell the manager to start the respawn timer before we destroy this object
        BumperManager manager = FindAnyObjectByType<BumperManager>();
        if (manager != null)
        {
            manager.HandleBumperBroken();
        }
        
        Destroy(gameObject);
    }

    IEnumerator HitPunchEffect()
    {
        // Makes the bumper quickly bulge out and snap back for game feel
        transform.localScale = originalScale * 1.3f;
        yield return new WaitForSeconds(0.05f);
        transform.localScale = originalScale;
    }
}