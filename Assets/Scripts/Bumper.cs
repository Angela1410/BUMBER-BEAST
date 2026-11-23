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
    public float speedMultiplier = 1.6f;   
    public float minimumBoostSpeed = 12f;  
    public float maxSpeed = 18f;

    [Header("Blue Bumper (slows the beast)")]
    [Tooltip("The beast's speed is multiplied by this on contact with a blue bumper.")]
    public float blueSlowMultiplier = 0.55f;

    [Header("Red Bumper = POWER BUMPER (see PowerBurst)")]
    [Tooltip("Legacy blast settings, used only by Explode(). The Power Bumper now unleashes the " +
        "hitting Beast's power at the enemies instead.")]
    public float explosionRadius = 1.6f;
    public int explosionDamage = 2;
    public Color explosionColor = new Color(1f, 0.5f, 0.2f, 1f);

    [Header("Bramble (Thornmaw)")]
    public bool isBramble = false;
    public float brambleSlowMultiplier = 0.5f;
    public Sprite brambleSprite;

    [Header("Hit Feedback")]
    public float hitScale = 1.2f;          
    public float hitRotation = 10f;        
    public float hitDuration = 0.12f;      
    public ParticleSystem hitEffect;       

    private Vector3 originalScale;
    private SpriteRenderer sr;
    private static Sprite fallbackSprite;
    private SpriteRenderer powerGlow;
    private float powerGlowScale = 1f;

    // sent when a Power Bumper fires (the tutorial waits for this)
    public static event System.Action<Bumper> PowerBumperHit;

    // every bumper in the stage, so the tutorial can point at one
    public static readonly System.Collections.Generic.List<Bumper> All = new System.Collections.Generic.List<Bumper>();

    public bool IsPowerBumper => type == BumperType.Red && !isBramble;

    void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    // turns this bumper into a Power Bumper (the stage 2 tutorial makes sure there's one)
    public void MakePowerBumper()
    {
        if (isBramble)
            return;
        type = BumperType.Red;
        RefreshVisuals();
    }

    void Update()
    {
        if (powerGlow == null)
            return;
        // a slow warm pulse shows which one is the Power Bumper
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        Color color = powerGlow.color;
        color.a = 0.35f + 0.4f * pulse;
        powerGlow.color = color;
        powerGlow.transform.localScale = Vector3.one * powerGlowScale * (0.92f + 0.14f * pulse);
    }

    void UpdatePowerGlow()
    {
        bool wanted = IsPowerBumper && Application.isPlaying;
        if (!wanted)
        {
            if (powerGlow != null)
                powerGlow.gameObject.SetActive(false);
            return;
        }

        if (powerGlow == null)
        {
            GameObject glowObject = new GameObject("PowerGlow");
            glowObject.transform.SetParent(transform, false);
            powerGlow = glowObject.AddComponent<SpriteRenderer>();
            powerGlow.sprite = HudVisualUtility.GlowSprite();
            powerGlow.sharedMaterial = GameArt.AdditiveGlow(sr != null ? sr.sharedMaterial : null);
            powerGlow.color = new Color(1f, 0.45f, 0.2f, 0.6f);
        }
        powerGlow.gameObject.SetActive(true);
        if (sr != null)
        {
            powerGlow.sortingLayerID = sr.sortingLayerID;
            powerGlow.sortingOrder = sr.sortingOrder - 1;
            float spriteSize = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
            float glowSize = powerGlow.sprite != null ? Mathf.Max(0.01f, powerGlow.sprite.bounds.size.x) : 1f;
            powerGlowScale = spriteSize / glowSize * 1.6f;
            powerGlow.transform.localScale = Vector3.one * powerGlowScale;
        }
    }

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

        
        if (isBramble)
{
    if (brambleSprite != null)
    {
        sr.sprite = brambleSprite;
        sr.color = Color.white;   
    }
    else
    {
        sr.color = new Color(0.75f, 0.45f, 0.85f);
    }
}
        UpdatePowerGlow();
    }

    public void RefreshVisuals()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateVisuals();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        
        BeastController beast = collision.gameObject.GetComponent<BeastController>();

        if (beast != null)
        {
            // POWER BUMPER (red): uses the Beast's power move on the enemies, then breaks.
            // if there are no enemies yet it waits, and bounces the Beast like a green bumper
            bool power = type == BumperType.Red && !isBramble;
            if (power && PowerBurst.HasTargets())
            {
                if (hitEffect != null)
                {
                    ParticleSystem burstFx = Instantiate(hitEffect, transform.position, Quaternion.identity);
                    burstFx.Play();
                    Destroy(burstFx.gameObject, 1f);
                }
                PowerBurst.Trigger(beast.ActiveBeast, transform.position);
                if (PowerBumperHit != null)
                    PowerBumperHit(this);
                BreakBumper();
                return;
            }

            ApplyEffect(collision.rigidbody, collision);
            GameAudio.Play(Sfx.Bumper);

            if (hitEffect != null)
            {
                ParticleSystem fx = Instantiate(hitEffect, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, 1f);
            }

            if (power)
            {
                StartCoroutine(HitPunchEffect());
                return;
            }

            TakeDamage();
            StartCoroutine(HitPunchEffect());
        }
    }

    void Explode()
    {
        GameAudio.Play(Sfx.BumperExplode);
        float radiusSquared = explosionRadius * explosionRadius;
        System.Collections.Generic.List<Enemy> enemies = Enemy.ActiveEnemies;
        // TakeDamage might Destroy() an enemy, but that only happens later, so the list won't change while we loop
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null)
                continue;
            Vector2 offset = (Vector2)enemy.transform.position - (Vector2)transform.position;
            if (offset.sqrMagnitude <= radiusSquared)
                enemy.TakeDamage(explosionDamage);
        }

        BumperExplosionRing.Spawn(transform.position, explosionRadius, explosionColor, sr != null ? sr.sortingOrder + 1 : 10);
    }

    void ApplyEffect(Rigidbody2D beastRb, Collision2D collision)
    {
        if (beastRb == null) return;

        
        if (isBramble)
        {
            beastRb.linearVelocity *= brambleSlowMultiplier;
            return;
        }

        if (type == BumperType.Blue)
        {
            beastRb.linearVelocity *= blueSlowMultiplier;
            return;
        }

        // green bumpers, and a Power Bumper that's waiting for enemies, both push the Beast away
        if (type == BumperType.Green || type == BumperType.Red)
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

            BeastController beast = beastRb.GetComponent<BeastController>();
            if (beast != null)
                beast.SetBumperSpeedCap(newSpeed);
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
        
        BumperManager manager = FindAnyObjectByType<BumperManager>();
        if (manager != null && !isBramble)
        {
            manager.HandleBumperBroken();
        }

        Destroy(gameObject);
    }

    IEnumerator HitPunchEffect()
    {
        
        transform.localScale = originalScale * 1.3f;
        yield return new WaitForSeconds(0.05f);
        transform.localScale = originalScale;
    }
}
