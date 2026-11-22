using System.Collections;
using UnityEngine;

// The painted guardian wall for chapters 3-5 (stages 9-20). BarrierVisuals creates it on the
// barrier object ("Square") from that chapter's art in Resources/Art/Electric, /Water or /Fire
// (falling back to the shared Resources/Art set). Stages 1-8 keep their own animated barrier.
//
// Layout comes from the camera view and the barrier line (the wall's top edge sits where enemies
// count as reaching it), never from the barrier object's own scale, so every stage's tuned
// transform, collider and health rules stay exactly as they are. The wall shows the barrier's
// condition (healthy, damaged, critical), reacts to hits, and shatters when it falls.
public class BarrierArt : MonoBehaviour
{
    // One painted wall: its art folder under Resources/Art, where its glowing parts are, and its
    // energy colours. Anchors are measured on that folder's Barrier_Healthy.png, as fractions
    // from the bottom-left corner; new art with a different composition needs new numbers.
    private sealed class Style
    {
        public string folder;
        public float wallTopFromBottom;   // the wall's top edge: where enemies count as reaching it
        public Vector2[] lights;          // crystals / orbs / pearls on the pylons
        public float lightSize;           // their glow diameter, in sprite heights
        public Vector2 crest;
        public Vector2 crestSize;         // crest glow width and height, in sprite heights
        public Color energy;
        public Color weakEnergy;
        public Color chip;
    }

    // Stages 1-8: the crystal wall (Barrier_Healthy.png, 1540 x 310).
    private static readonly Style Shared = new Style
    {
        folder = "",
        wallTopFromBottom = 1f - 106f / 310f,
        lights = new[]
        {
            new Vector2(0.1360f, 0.839f), new Vector2(0.3421f, 0.839f),
            new Vector2(0.6559f, 0.839f), new Vector2(0.8628f, 0.839f)
        },
        lightSize = 0.42f,
        crest = new Vector2(0.4991f, 0.4876f),
        crestSize = new Vector2(0.75f, 1.05f),
        energy = new Color(0.36f, 1f, 0.9f, 1f),
        weakEnergy = new Color(0.45f, 0.75f, 0.85f, 1f),
        chip = new Color(0.9f, 0.8f, 0.6f, 1f)
    };

    // Chapter 3 (stages 9-12): slate and copper storm wall with tesla-coil orbs (1527 x 456).
    private static readonly Style Electric = new Style
    {
        folder = "Electric/",
        wallTopFromBottom = 1f - 182f / 456f,
        lights = new[]
        {
            new Vector2(0.1764f, 0.8592f), new Vector2(0.3565f, 0.8569f),
            new Vector2(0.6467f, 0.8582f), new Vector2(0.8224f, 0.8568f)
        },
        lightSize = 0.36f,
        crest = new Vector2(0.5007f, 0.4477f),
        crestSize = new Vector2(0.5f, 0.75f),
        energy = new Color(1f, 0.88f, 0.3f, 1f),
        weakEnergy = new Color(0.85f, 0.78f, 0.45f, 1f),
        chip = new Color(0.55f, 0.6f, 0.72f, 1f)
    };

    // Chapter 4 (stages 13-16): coral sea wall with pearls in clam shells (1537 x 472).
    private static readonly Style Water = new Style
    {
        folder = "Water/",
        wallTopFromBottom = 1f - 222f / 472f,
        lights = new[]
        {
            new Vector2(0.1693f, 0.7077f), new Vector2(0.3324f, 0.7077f),
            new Vector2(0.6679f, 0.7074f), new Vector2(0.8316f, 0.7076f)
        },
        lightSize = 0.3f,
        crest = new Vector2(0.5002f, 0.6808f),
        crestSize = new Vector2(0.5f, 0.5f),
        energy = new Color(0.35f, 0.9f, 1f, 1f),
        weakEnergy = new Color(0.45f, 0.7f, 0.8f, 1f),
        chip = new Color(0.75f, 0.82f, 0.88f, 1f)
    };

    // Chapter 5 (stages 17-20): basalt and obsidian wall with fire crystals (1533 x 434).
    private static readonly Style Fire = new Style
    {
        folder = "Fire/",
        wallTopFromBottom = 1f - 162f / 434f,
        lights = new[]
        {
            new Vector2(0.1561f, 0.8696f), new Vector2(0.3405f, 0.8715f),
            new Vector2(0.6630f, 0.8676f), new Vector2(0.8462f, 0.8692f)
        },
        lightSize = 0.3f,
        crest = new Vector2(0.4982f, 0.4956f),
        crestSize = new Vector2(0.45f, 0.75f),
        energy = new Color(1f, 0.55f, 0.18f, 1f),
        weakEnergy = new Color(0.85f, 0.45f, 0.25f, 1f),
        chip = new Color(0.4f, 0.34f, 0.32f, 1f)
    };

    private static readonly Color CorruptEnergyColor = new Color(0.74f, 0.36f, 1f, 1f);

    private const float MinWallWidth = 7.5f;
    private const float MaxWallWidth = 11f;
    private const int SparkCount = 18;
    private const int FragmentCount = 12;

    private enum Condition { Healthy, Damaged, Critical }

    private Style style = Shared;
    private Sprite healthySprite;
    private Sprite damagedSprite;
    private Sprite criticalSprite;
    private SpriteRenderer sortSource;
    private Transform root;
    private SpriteRenderer wall;
    private SpriteRenderer fadingWall;
    private SpriteRenderer[] crystalGlows;
    private SpriteRenderer crestGlow;
    private SpriteRenderer shield;
    private SpriteRenderer ripple;
    private SpriteRenderer[] sparks;
    private Vector3[] sparkVelocity;
    private float[] sparkAge;
    private float[] sparkLife;
    private Condition condition = Condition.Healthy;
    private float contactOffset = 0.35f;
    private float fittedAspect;
    private float fittedSize;
    private Camera fittedCamera;
    private float hitTime = -10f;
    private float rippleTime = -10f;
    private float stateChangeTime = -10f;
    private bool destroyed;

    // Bottom edge of the painted wall in world space (the launcher stays clear of it).
    public float WallBottomWorldY { get; private set; }
    public bool IsLaidOut { get; private set; }

    public static BarrierArt TryCreate(GameObject barrier)
    {
        // The chapter's own wall when its art is in the project, otherwise the shared crystal wall.
        // Chapters 1-2 (null folder) keep the scene's own animated barrier.
        string folder = GameArt.StageArtFolder();
        if (folder == null)
            return null;
        Style style = StyleFor(folder);
        Sprite healthy = GameArt.Find(style.folder + "Barrier_Healthy");
        if (healthy == null && style != Shared)
        {
            style = Shared;
            healthy = GameArt.Find("Barrier_Healthy");
        }
        if (healthy == null)
            return null;

        BarrierArt art = barrier.AddComponent<BarrierArt>();
        art.style = style;
        art.Build(healthy, GameArt.Find(style.folder + "Barrier_Damaged"), GameArt.Find(style.folder + "Barrier_Critical"));
        return art;
    }

    static Style StyleFor(string folder)
    {
        if (folder == Electric.folder) return Electric;
        if (folder == Water.folder) return Water;
        if (folder == Fire.folder) return Fire;
        return Shared;
    }

    void Build(Sprite healthy, Sprite damaged, Sprite critical)
    {
        healthySprite = healthy;
        damagedSprite = damaged != null ? damaged : healthy;
        criticalSprite = critical != null ? critical : damagedSprite;

        sortSource = GetComponent<SpriteRenderer>();
        Material wallMaterial = sortSource != null ? sortSource.sharedMaterial : null;
        if (sortSource != null)
            sortSource.enabled = false;   // the old log art; its Animator is stopped by BarrierVisuals

        StageManager stage = Object.FindAnyObjectByType<StageManager>();
        if (stage != null)
            contactOffset = stage.barrierContactOffset;

        GameObject rootObject = new GameObject("BarrierArt");
        rootObject.transform.SetParent(transform, false);
        root = rootObject.transform;

        wall = GameArt.AddLayer(root, "Wall", healthySprite, wallMaterial, sortSource, 0);
        fadingWall = GameArt.AddLayer(root, "WallFade", healthySprite, wallMaterial, sortSource, 0);
        fadingWall.enabled = false;

        Material glow = GameArt.AdditiveGlow(wallMaterial);
        Sprite soft = HudVisualUtility.GlowSprite();
        Vector2 size = healthySprite.bounds.size;
        crystalGlows = new SpriteRenderer[style.lights.Length];
        for (int i = 0; i < crystalGlows.Length; i++)
        {
            crystalGlows[i] = GameArt.AddLayer(root, "CrystalGlow" + i, soft, glow, sortSource, 1);
            crystalGlows[i].transform.localPosition = AnchorToLocal(style.lights[i], size);
            crystalGlows[i].transform.localScale = Vector3.one * size.y * style.lightSize;
        }
        crestGlow = GameArt.AddLayer(root, "CrestGlow", soft, glow, sortSource, 1);
        crestGlow.transform.localPosition = AnchorToLocal(style.crest, size);
        crestGlow.transform.localScale = new Vector3(size.y * style.crestSize.x, size.y * style.crestSize.y, 1f);

        // A thin curtain of energy along the wall top: the actual "barrier" the enemies hit.
        shield = GameArt.AddLayer(root, "ShieldCurtain", soft, glow, sortSource, 2);
        shield.transform.localPosition = new Vector3(0f, size.y * (style.wallTopFromBottom - 0.5f) + size.y * 0.12f, 0f);
        shield.transform.localScale = new Vector3(size.x * 1.08f, size.y * 0.55f, 1f);

        ripple = GameArt.AddLayer(root, "HitRipple", HudVisualUtility.RingSprite(), glow, sortSource, 3);
        ripple.enabled = false;

        sparks = new SpriteRenderer[SparkCount];
        sparkVelocity = new Vector3[SparkCount];
        sparkAge = new float[SparkCount];
        sparkLife = new float[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            bool chip = i % 2 == 0;
            sparks[i] = GameArt.AddLayer(root, chip ? "Chip" + i : "Spark" + i,
                chip ? HudVisualUtility.CircleSprite() : soft, chip ? wallMaterial : glow, sortSource, 3);
            sparks[i].enabled = false;
            sparkLife[i] = 0f;
        }

        Layout();
    }

    static Vector3 AnchorToLocal(Vector2 anchor, Vector2 size)
    {
        return new Vector3((anchor.x - 0.5f) * size.x, (anchor.y - 0.5f) * size.y, 0f);
    }

    // Spans the camera's width (within limits) with the wall's top edge on the barrier line.
    void Layout()
    {
        Camera camera = Camera.main;
        float viewWidth = 10f;
        float centreX = 0f;
        if (camera != null && camera.orthographic)
        {
            viewWidth = camera.orthographicSize * 2f * camera.aspect;
            centreX = camera.transform.position.x;
            fittedCamera = camera;
            fittedAspect = camera.aspect;
            fittedSize = camera.orthographicSize;
        }

        Vector2 spriteSize = healthySprite.bounds.size;
        float width = Mathf.Clamp(viewWidth * 1.02f, MinWallWidth, MaxWallWidth);
        float scale = width / Mathf.Max(0.001f, spriteSize.x);
        float height = spriteSize.y * scale;
        float contactY = transform.position.y + contactOffset;
        float centreY = contactY - (style.wallTopFromBottom - 0.5f) * height;

        Vector3 parentScale = transform.lossyScale;
        root.localScale = new Vector3(
            scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        root.position = new Vector3(centreX, centreY, transform.position.z);
        WallBottomWorldY = centreY - height * 0.5f;
        IsLaidOut = true;
    }

    public void SetHealth(int current, int max)
    {
        if (destroyed || max <= 0)
            return;

        float fraction = Mathf.Clamp01((float)current / max);
        Condition next = fraction > 0.6f ? Condition.Healthy
            : fraction > 0.25f ? Condition.Damaged
            : Condition.Critical;
        if (next == condition)
            return;

        // Cross-fade from the old look and kick up dust, so the change reads as damage.
        fadingWall.sprite = wall.sprite;
        fadingWall.enabled = true;
        fadingWall.color = Color.white;
        condition = next;
        wall.sprite = condition == Condition.Healthy ? healthySprite
            : condition == Condition.Damaged ? damagedSprite : criticalSprite;
        stateChangeTime = Time.unscaledTime;
        if (condition != Condition.Healthy)
            BurstAt(root.position, 8, true);
    }

    public void PlayHit(Vector3? impact)
    {
        if (destroyed)
            return;

        hitTime = Time.unscaledTime;
        Vector3 point = impact.HasValue ? impact.Value : root.position;
        Vector2 size = healthySprite.bounds.size;
        Vector3 local = root.InverseTransformPoint(point);
        local.x = Mathf.Clamp(local.x, -size.x * 0.48f, size.x * 0.48f);
        local.y = size.y * (style.wallTopFromBottom - 0.5f);
        Vector3 contact = root.TransformPoint(local);

        ripple.transform.position = contact;
        rippleTime = Time.unscaledTime;
        ripple.enabled = true;
        BurstAt(contact, 10, false);
    }

    public void PlayDestroyed()
    {
        if (destroyed)
            return;
        destroyed = true;
        StartCoroutine(Shatter());
    }

    void LateUpdate()
    {
        if (root == null)
            return;
        if (fittedCamera == null || !Mathf.Approximately(fittedCamera.aspect, fittedAspect) ||
            !Mathf.Approximately(fittedCamera.orthographicSize, fittedSize))
            Layout();

        float now = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (!destroyed)
        {
            // A short shake on impact (unscaled: the stage may already be frozen).
            float sinceHit = now - hitTime;
            Vector3 shake = Vector3.zero;
            if (sinceHit < 0.22f)
            {
                float strength = (1f - sinceHit / 0.22f) * healthySprite.bounds.size.y * 0.025f;
                shake = new Vector3(Mathf.Sin(now * 90f) * strength, Mathf.Cos(now * 77f) * strength * 0.5f, 0f);
            }
            wall.transform.localPosition = shake;

            if (fadingWall.enabled)
            {
                float t = (now - stateChangeTime) / 0.3f;
                fadingWall.transform.localPosition = shake;
                fadingWall.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t));
                if (t >= 1f)
                    fadingWall.enabled = false;
            }

            UpdateEnergy(now, sinceHit);
        }

        if (ripple.enabled)
        {
            float t = (now - rippleTime) / 0.45f;
            float diameter = healthySprite.bounds.size.y * Mathf.Lerp(0.25f, 1f, t);
            ripple.transform.localScale = new Vector3(diameter * 1.6f, diameter * 0.6f, 1f);
            Color color = condition == Condition.Critical ? CorruptEnergyColor : style.energy;
            color.a = (1f - t) * 0.9f;
            ripple.color = color;
            if (t >= 1f)
                ripple.enabled = false;
        }

        UpdateSparks(dt);
    }

    void UpdateEnergy(float now, float sinceHit)
    {
        float flash = sinceHit < 0.3f ? 1f - sinceHit / 0.3f : 0f;
        Color energy;
        float steadiness;
        switch (condition)
        {
            case Condition.Healthy:
                energy = style.energy;
                steadiness = 1f;
                break;
            case Condition.Damaged:
                energy = style.weakEnergy;
                // Flickers: most of the time steady, sometimes it sputters.
                steadiness = Mathf.PerlinNoise(now * 5f, 0.3f) > 0.32f ? 0.75f : 0.25f;
                break;
            default:
                energy = Color.Lerp(style.weakEnergy, CorruptEnergyColor, 0.5f + 0.5f * Mathf.Sin(now * 9f));
                steadiness = Mathf.PerlinNoise(now * 11f, 0.7f) > 0.5f ? 0.55f : 0.1f;
                break;
        }

        float pulse = 0.85f + 0.15f * Mathf.Sin(now * 2.4f);
        for (int i = 0; i < crystalGlows.Length; i++)
        {
            float own = condition == Condition.Healthy ? 1f
                : Mathf.PerlinNoise(now * 7f + i * 3.1f, i) > 0.35f ? steadiness : steadiness * 0.3f;
            Color color = energy;
            color.a = Mathf.Clamp01((0.55f * pulse * own) + flash * 0.6f);
            crystalGlows[i].color = color;
        }

        Color crest = energy;
        crest.a = Mathf.Clamp01(0.5f * pulse * steadiness + flash * 0.7f);
        crestGlow.color = crest;

        Color curtain = energy;
        curtain.a = Mathf.Clamp01((0.16f + 0.05f * Mathf.Sin(now * 3.3f)) * steadiness + flash * 0.45f);
        shield.color = curtain;
    }

    // Stone chips and energy sparks thrown from a point (pooled; nothing is allocated).
    void BurstAt(Vector3 world, int count, bool dust)
    {
        float unit = healthySprite.bounds.size.y;
        int started = 0;
        for (int i = 0; i < sparks.Length && started < count; i++)
        {
            if (sparks[i].enabled)
                continue;
            bool chip = i % 2 == 0;
            SpriteRenderer spark = sparks[i];
            Vector3 local = root.InverseTransformPoint(world);
            spark.transform.localPosition = local + new Vector3(Random.Range(-0.4f, 0.4f) * unit, 0f, 0f);
            float speed = unit * Random.Range(1.4f, 3.2f);
            float angle = Random.Range(25f, 155f) * Mathf.Deg2Rad;
            sparkVelocity[i] = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            sparkAge[i] = 0f;
            sparkLife[i] = Random.Range(0.35f, 0.65f);
            float sparkSize = unit * (chip ? Random.Range(0.05f, 0.09f) : Random.Range(0.08f, 0.15f));
            spark.transform.localScale = new Vector3(sparkSize, sparkSize, 1f);
            Color color = chip || dust ? style.chip
                : condition == Condition.Critical ? CorruptEnergyColor : style.energy;
            spark.color = color;
            spark.enabled = true;
            started++;
        }
    }

    void UpdateSparks(float dt)
    {
        float gravity = healthySprite.bounds.size.y * 7f;
        for (int i = 0; i < sparks.Length; i++)
        {
            SpriteRenderer spark = sparks[i];
            if (!spark.enabled)
                continue;
            sparkAge[i] += dt;
            if (sparkAge[i] >= sparkLife[i])
            {
                spark.enabled = false;
                continue;
            }
            sparkVelocity[i].y -= gravity * dt;
            spark.transform.localPosition += sparkVelocity[i] * dt;
            Color color = spark.color;
            color.a = 1f - sparkAge[i] / sparkLife[i];
            spark.color = color;
        }
    }

    // The wall breaks apart: a flash, then vertical slices of the (critical) art fly off and
    // fade while dust and corruption spill out. Runs on unscaled time (the stage is frozen).
    IEnumerator Shatter()
    {
        Sprite art = criticalSprite;
        Material wallMaterial = wall.sharedMaterial;
        Vector2 size = art.bounds.size;
        Rect rect = art.rect;
        float pixelsPerUnit = art.pixelsPerUnit;

        // Flash.
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < 0.08f)
        {
            Color white = new Color(1f, 1f, 1f, 0.9f);
            crestGlow.color = white;
            shield.color = white;
            for (int i = 0; i < crystalGlows.Length; i++)
                crystalGlows[i].color = white;
            yield return null;
        }

        wall.enabled = false;
        fadingWall.enabled = false;
        shield.enabled = false;
        crestGlow.enabled = false;
        for (int i = 0; i < crystalGlows.Length; i++)
            crystalGlows[i].enabled = false;

        SpriteRenderer[] pieces = new SpriteRenderer[FragmentCount];
        Vector3[] velocity = new Vector3[FragmentCount];
        float[] spin = new float[FragmentCount];
        float sliceWidth = rect.width / FragmentCount;
        for (int i = 0; i < FragmentCount; i++)
        {
            Rect slice = new Rect(rect.x + sliceWidth * i, rect.y, sliceWidth, rect.height);
            // FullRect: a tight mesh would need a readable texture.
            Sprite piece = Sprite.Create(art.texture, slice, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            pieces[i] = GameArt.AddLayer(root, "Fragment" + i, piece, wallMaterial, sortSource, 1);
            float x = (i + 0.5f) / FragmentCount - 0.5f;
            pieces[i].transform.localPosition = new Vector3(x * size.x, 0f, 0f);
            velocity[i] = new Vector3(x * size.y * 4f + Random.Range(-0.6f, 0.6f) * size.y,
                Random.Range(1.2f, 2.6f) * size.y, 0f);
            spin[i] = Random.Range(-160f, 160f);
        }

        ripple.transform.position = root.position;
        rippleTime = Time.unscaledTime;
        ripple.enabled = true;
        for (int burst = 0; burst < 3; burst++)
            BurstAt(root.TransformPoint(new Vector3((burst - 1) * size.x * 0.3f, 0f, 0f)), 6, burst != 1);

        float gravity = size.y * 9f;
        float elapsed = 0f;
        const float Duration = 1f;
        while (elapsed < Duration)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            elapsed += dt;
            for (int i = 0; i < FragmentCount; i++)
            {
                velocity[i].y -= gravity * dt;
                Transform piece = pieces[i].transform;
                piece.localPosition += velocity[i] * dt;
                piece.localRotation = Quaternion.Euler(0f, 0f, spin[i] * elapsed);
                pieces[i].color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((elapsed - 0.45f) / 0.55f));
            }
            yield return null;
        }

        for (int i = 0; i < FragmentCount; i++)
            pieces[i].enabled = false;
    }
}
