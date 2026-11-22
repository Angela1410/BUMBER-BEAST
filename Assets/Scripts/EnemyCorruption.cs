using UnityEngine;

// Marks every enemy as corrupted: layered violet glow hugging the sprite's silhouette, a violet
// tint over the body, a dark aura and a bright violet haze behind it, a pool of corruption under
// its feet, purple smoke wisps and motes rising off it; bosses get a stronger, larger version.
// Added automatically by Enemy.Start, so wave spawns, reinforcements and bosses all get it.
// Visual only: child renderers, no colliders or physics, and the enemy's own renderer is never
// touched, so hit flashes, element tints, sprite swaps and movement behave exactly as before.
[DisallowMultipleComponent]
public class EnemyCorruption : MonoBehaviour
{
    private static readonly Color InnerRimColor = new Color(0.84f, 0.44f, 1f, 1f);
    private static readonly Color OuterRimColor = new Color(0.64f, 0.26f, 1f, 1f);
    private static readonly Color BodyTintColor = new Color(0.52f, 0.12f, 0.82f, 1f);
    private static readonly Color AuraColor = new Color(0.3f, 0.05f, 0.48f, 1f);
    private static readonly Color HazeColor = new Color(0.64f, 0.24f, 0.98f, 1f);
    private static readonly Color PoolColor = new Color(0.22f, 0.02f, 0.36f, 1f);
    private static readonly Color WispColor = new Color(0.42f, 0.1f, 0.68f, 1f);
    private static readonly Color ShadowRimColor = new Color(0.26f, 0.03f, 0.44f, 1f);
    private static readonly Color BossCloudColor = new Color(0.2f, 0.02f, 0.32f, 1f);
    private static readonly Color MoteColor = new Color(0.9f, 0.62f, 1f, 1f);

    private Enemy enemy;
    private SpriteRenderer source;
    private Transform layerRoot;
    private SpriteRenderer[] rims = new SpriteRenderer[0];
    private float[] rimScales = new float[0];
    private float[] rimAlphas = new float[0];
    private Color[] rimColors = new Color[0];
    private SpriteRenderer bodyTint;
    private SpriteRenderer aura;
    private SpriteRenderer haze;
    private SpriteRenderer pool;
    private SpriteRenderer bossCloud;
    private SpriteRenderer[] wisps = new SpriteRenderer[0];
    private float[] wispSeeds = new float[0];
    private SpriteRenderer[] motes = new SpriteRenderer[0];
    private float[] moteSeeds = new float[0];
    private bool built;
    private bool builtForBoss;
    private int behind = -2;
    private float phase;

    public static void Attach(Enemy target)
    {
        if (target != null && target.GetComponent<EnemyCorruption>() == null)
            target.gameObject.AddComponent<EnemyCorruption>();
    }

    void Start()
    {
        enemy = GetComponent<Enemy>();
        source = GetComponent<SpriteRenderer>();
        if (source == null)
        {
            enabled = false;
            return;
        }

        phase = Random.value * 10f;
        Build(enemy != null && enemy.isBoss);
    }

    void Build(bool boss)
    {
        if (layerRoot != null)
            Destroy(layerRoot.gameObject);

        GameObject root = new GameObject("Corruption");
        root.transform.SetParent(transform, false);
        layerRoot = root.transform;

        Material silhouette = GameArt.SilhouetteGlow();
        Material soft = GameArt.AlphaGlow(source.sharedMaterial);
        Material bright = GameArt.AdditiveGlow(source.sharedMaterial);
        Sprite glow = HudVisualUtility.GlowSprite();
        Sprite smoke = HudVisualUtility.SmokeSprite();

        // Silhouette glow: copies of the enemy's own sprite drawn slightly larger, behind it (bright
        // violet rims, then a dark violet shadow that keeps the shape readable on light ground),
        // plus one on top that tints the body violet.
        if (silhouette != null)
        {
            rimScales = boss ? new[] { 1.05f, 1.14f, 1.26f, 1.42f, 1.58f } : new[] { 1.06f, 1.15f, 1.3f, 1.44f };
            rimAlphas = boss ? new[] { 1f, 0.7f, 0.4f, 0.2f, 0.5f } : new[] { 1f, 0.6f, 0.3f, 0.42f };
            rimColors = boss
                ? new[] { InnerRimColor, OuterRimColor, OuterRimColor, OuterRimColor, ShadowRimColor }
                : new[] { InnerRimColor, OuterRimColor, OuterRimColor, ShadowRimColor };
            rims = new SpriteRenderer[rimScales.Length];
            for (int i = 0; i < rims.Length; i++)
                rims[i] = GameArt.AddLayer(layerRoot, "Rim" + i, source.sprite, silhouette, source, -(i + 1));
            bodyTint = GameArt.AddLayer(layerRoot, "BodyTint", source.sprite, silhouette, source, 1);
        }
        else
        {
            rims = new SpriteRenderer[0];
            bodyTint = null;
        }
        // Everything else sits behind the rims, each kind on its own order so the stacking is fixed.
        behind = -(rims.Length + 1);

        aura = GameArt.AddLayer(layerRoot, "Aura", glow, soft, source, behind - 1);
        haze = GameArt.AddLayer(layerRoot, "Haze", smoke, soft, source, behind - 2);
        pool = GameArt.AddLayer(layerRoot, "Pool", smoke, soft, source, behind - 2);
        bossCloud = boss ? GameArt.AddLayer(layerRoot, "BossCloud", glow, soft, source, behind - 3) : null;

        int wispCount = boss ? 6 : 3;
        wisps = new SpriteRenderer[wispCount];
        wispSeeds = new float[wispCount];
        for (int i = 0; i < wispCount; i++)
        {
            wisps[i] = GameArt.AddLayer(layerRoot, "Wisp" + i, smoke, soft, source, behind);
            wispSeeds[i] = (i + 0.5f) / wispCount;
        }

        int moteCount = boss ? 11 : 6;
        motes = new SpriteRenderer[moteCount];
        moteSeeds = new float[moteCount];
        for (int i = 0; i < moteCount; i++)
        {
            // Alternate in front of / behind the body so the motes wrap around it.
            motes[i] = GameArt.AddLayer(layerRoot, "Mote" + i, glow, bright, source, i % 2 == 0 ? 1 : behind);
            moteSeeds[i] = (i + 0.37f) / moteCount;
        }

        builtForBoss = boss;
        built = true;
    }

    void LateUpdate()
    {
        if (!built || source == null)
            return;

        bool boss = enemy != null && enemy.isBoss;
        if (boss != builtForBoss)
            Build(boss);

        Sprite sprite = source.sprite;
        bool visible = source.enabled && sprite != null;
        if (layerRoot.gameObject.activeSelf != visible)
            layerRoot.gameObject.SetActive(visible);
        if (!visible)
            return;

        Bounds bounds = sprite.bounds;
        Vector3 center = bounds.center;
        if (source.flipX) center.x = -center.x;
        if (source.flipY) center.y = -center.y;
        float size = Mathf.Max(bounds.size.x, bounds.size.y);
        float time = Time.time + phase;
        float pulse = 0.8f + 0.2f * Mathf.Sin(time * (boss ? 3.4f : 2.6f));
        float fade = source.color.a;
        int layer = source.sortingLayerID;
        int order = source.sortingOrder;

        for (int i = 0; i < rims.Length; i++)
        {
            SpriteRenderer rim = rims[i];
            if (rim.sprite != sprite)
                rim.sprite = sprite;
            rim.flipX = source.flipX;
            rim.flipY = source.flipY;
            float scale = rimScales[i] * (1f + 0.015f * Mathf.Sin(time * 2.1f + i * 1.7f));
            rim.transform.localScale = new Vector3(scale, scale, 1f);
            // Scale about the sprite's visual centre rather than its pivot.
            rim.transform.localPosition = center * (1f - scale);
            Color color = rimColors[i];
            color.a = rimAlphas[i] * pulse * fade;
            rim.color = color;
            SetSorting(rim, layer, order - (i + 1));
        }

        if (bodyTint != null)
        {
            if (bodyTint.sprite != sprite)
                bodyTint.sprite = sprite;
            bodyTint.flipX = source.flipX;
            bodyTint.flipY = source.flipY;
            Color tint = BodyTintColor;
            tint.a = (boss ? 0.24f : 0.2f) * (0.75f + 0.25f * pulse) * fade;
            bodyTint.color = tint;
            SetSorting(bodyTint, layer, order + 1);
        }

        float auraScale = boss ? 1.85f : 1.6f;
        aura.transform.localPosition = center;
        aura.transform.localScale = new Vector3(bounds.size.x * auraScale, bounds.size.y * auraScale * 0.95f, 1f);
        Color auraColor = AuraColor;
        auraColor.a = (boss ? 0.7f : 0.55f) * (0.85f + 0.15f * pulse) * fade;
        aura.color = auraColor;
        SetSorting(aura, layer, order + behind - 1);

        // A brighter violet haze around the dark aura, so the corruption reads on dark ground too.
        float hazeBreathe = 1f + 0.05f * Mathf.Sin(time * 1.7f);
        haze.transform.localPosition = center;
        haze.transform.localScale = new Vector3(bounds.size.x * auraScale * 1.3f * hazeBreathe,
            bounds.size.y * auraScale * 1.2f * hazeBreathe, 1f);
        Color hazeColor = HazeColor;
        hazeColor.a = (boss ? 0.34f : 0.28f) * pulse * fade;
        haze.color = hazeColor;
        SetSorting(haze, layer, order + behind - 2);

        // Corrupted ground under its feet.
        float poolBreathe = 1f + 0.08f * Mathf.Sin(time * 1.9f);
        pool.transform.localPosition = new Vector3(center.x, center.y - bounds.extents.y * 0.86f, 0f);
        pool.transform.localScale = new Vector3(bounds.size.x * (boss ? 1.5f : 1.3f) * poolBreathe,
            bounds.size.y * 0.34f * poolBreathe, 1f);
        Color poolColor = PoolColor;
        poolColor.a = (boss ? 0.72f : 0.6f) * fade;
        pool.color = poolColor;
        SetSorting(pool, layer, order + behind - 2);

        if (bossCloud != null)
        {
            float breathe = 1f + 0.06f * Mathf.Sin(time * 1.3f);
            bossCloud.transform.localPosition = center + new Vector3(0f, -bounds.extents.y * 0.15f, 0f);
            bossCloud.transform.localScale = new Vector3(size * 2.6f * breathe, size * 2.1f * breathe, 1f);
            Color cloud = BossCloudColor;
            cloud.a = 0.5f * fade;
            bossCloud.color = cloud;
            SetSorting(bossCloud, layer, order + behind - 3);
        }

        // Purple smoke rising off the body like dark flames.
        float wispLife = boss ? 1.4f : 1.6f;
        for (int i = 0; i < wisps.Length; i++)
        {
            float cycleTime = time / wispLife + wispSeeds[i];
            float progress = cycleTime - Mathf.Floor(cycleTime);
            float lane = Hash(i + 31, Mathf.FloorToInt(cycleTime)) * 2f - 1f;
            float x = center.x + lane * bounds.extents.x * 0.7f +
                Mathf.Sin(progress * 4.2f + i * 2.3f) * bounds.extents.x * 0.12f;
            float y = center.y - bounds.extents.y * 0.4f + progress * bounds.size.y * 1.1f;
            float wispSize = size * (boss ? 0.48f : 0.42f) * (0.55f + progress * 0.6f);

            SpriteRenderer wisp = wisps[i];
            wisp.transform.localPosition = new Vector3(x, y, 0f);
            wisp.transform.localScale = new Vector3(wispSize, wispSize * 1.25f, 1f);
            Color color = WispColor;
            color.a = Mathf.Sin(progress * Mathf.PI) * (boss ? 0.58f : 0.52f) * fade;
            wisp.color = color;
            SetSorting(wisp, layer, order + behind);
        }

        float life = boss ? 1.7f : 1.35f;
        for (int i = 0; i < motes.Length; i++)
        {
            float cycleTime = time / life + moteSeeds[i];
            float progress = cycleTime - Mathf.Floor(cycleTime);
            float lane = Hash(i, Mathf.FloorToInt(cycleTime)) * 2f - 1f;
            float x = center.x + lane * bounds.extents.x * 0.85f +
                Mathf.Sin(progress * 6.283f + i) * bounds.extents.x * 0.08f;
            float y = center.y - bounds.extents.y * 0.55f + progress * bounds.size.y * 1.1f;
            float moteSize = size * (boss ? 0.11f : 0.1f) * (1f - progress * 0.45f);

            SpriteRenderer mote = motes[i];
            mote.transform.localPosition = new Vector3(x, y, 0f);
            mote.transform.localScale = new Vector3(moteSize, moteSize, 1f);
            Color color = MoteColor;
            color.a = Mathf.Sin(progress * Mathf.PI) * (boss ? 0.95f : 0.85f) * fade;
            mote.color = color;
            SetSorting(mote, layer, order + (i % 2 == 0 ? 1 : behind));
        }
    }

    static void SetSorting(SpriteRenderer renderer, int layer, int order)
    {
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
    }

    // Stable pseudo-random value in [0, 1) per mote and cycle (no allocation, no Random state).
    static float Hash(int index, int cycle)
    {
        float value = Mathf.Sin(index * 12.9898f + cycle * 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }
}
