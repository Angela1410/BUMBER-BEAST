using System.Collections;
using UnityEngine;

public enum BumperType { Green, Blue, Red }

public class Bumper : MonoBehaviour
{
    [Header("Bumper Sprites")]
    public Sprite greenSprite;
    public Sprite blueSprite;
    public Sprite redSprite;

    [Header("Bumper Settings")]
    public BumperType type = BumperType.Green;
    public int hp = 3;

    [Header("Green Bumper Physics")]
    public float speedMultiplier = 1.6f;   // 60% boost
    public float minimumBoostSpeed = 12f;  // Guarantees a fast exit even on a slow hit
    public float maxSpeed = 18f;

    [Header("Hit Feedback")]
    public float hitScale = 1.2f;          // not used yet
    public float hitRotation = 10f;        // not used yet
    public float hitDuration = 0.12f;      // not used yet
    public ParticleSystem hitEffect;       // drag the BumperHitEffect PREFAB here

    private Vector3 originalScale;
    private SpriteRenderer sr;
    private static Sprite fallbackSprite;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (sr == null)
            sr = gameObject.AddComponent<SpriteRenderer>();

        UpdateVisuals();
    }

    void Start()
    {
        originalScale = transform.localScale;
        UpdateVisuals();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            UpdateVisuals();
        };
#endif
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

        Sprite typeSprite = type switch
        {
            BumperType.Blue => blueSprite,
            BumperType.Red => redSprite,
            _ => greenSprite
        };

        if (typeSprite != null)
        {
            sr.sprite = typeSprite;
            sr.color = Color.white;
        }
        else if (sr.sprite == null)
        {
            sr.sprite = GetFallbackSprite();
            sr.color = type switch
            {
                BumperType.Blue => new Color(0.2f, 0.4f, 1f),
                BumperType.Red => new Color(0.9f, 0.2f, 0.2f),
                _ => new Color(0.2f, 0.8f, 0.2f)
            };
        }
    }

    public void RefreshVisuals()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisuals();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only react to the player's Beast
        BeastController beast = collision.gameObject.GetComponent<BeastController>();

        if (beast != null)
        {
            ApplyEffect(collision.rigidbody, collision);

            // Spark effect: spawn a copy at the bumper, play it, remove it after 1 second
            if (hitEffect != null)
            {
                ParticleSystem fx = Instantiate(hitEffect, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, 1f);
            }

            TakeDamage();
            StartCoroutine(HitPunchEffect());
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
        // Tell the manager to start the respawn timer before this object is destroyed
        BumperManager manager = FindAnyObjectByType<BumperManager>();
        if (manager != null)
        {
            manager.HandleBumperBroken();
        }

        Destroy(gameObject);
    }

    IEnumerator HitPunchEffect()
    {
        // The bumper bulges out and snaps back for game feel
        transform.localScale = originalScale * 1.3f;
        yield return new WaitForSeconds(0.05f);
        transform.localScale = originalScale;
    }
}