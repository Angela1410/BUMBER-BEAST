using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// the Earth chapter boss. it's on the shared enemy prefab next to ThornmawAbility
// and only turns on for a boss whose name has "Terrathorn" in it.
// STONE WALL: Terrathorn stomps (dust where the rocks will come up), then three boulders rise in
// front of it and walk with it. they block straight shots from the launch pad, so you have to bank
// the Beast off a wall or a bumper to hit it from the side (that also gives bounce power), or break
// a rock (two hits). the wall crumbles by itself after a few seconds
// RAGE (AVALANCHE, at 30% health): no more walls. it charges at your barrier much faster, shaking
// and kicking up dust, but every hit pushes it back up its path
[RequireComponent(typeof(Enemy))]
public class TerrathornAbility : MonoBehaviour, IBossRage
{
    [Header("Boss Look")]
    public Sprite bossSprite;
    public float bossScale = 3f;
    public Color stoneTint = new Color(0.86f, 0.68f, 0.5f, 1f);

    [Header("Stone Wall")]
    public Sprite boulderSprite;
    public float firstWallDelay = 4f;
    public float warningTime = 0.9f;
    public float rockRadius = 0.5f;
    // the rocks stand on an arc below Terrathorn (toward the barrier), these angles in degrees
    public float[] wallAngles = { -132f, -90f, -48f };

    private static readonly Color EarthColor = new Color(0.86f, 0.62f, 0.32f, 1f);
    private static readonly Color AvalancheColor = new Color(1f, 0.55f, 0.2f, 1f);

    private readonly List<TerrathornBoulder> wall = new List<TerrathornBoulder>();
    private SpriteRenderer sr;
    private Enemy enemy;
    private EnemyHitFeedback hitFeedback;
    private CircleCollider2D bossCollider;
    private bool isTerrathorn;
    private bool raging;
    private Coroutine wallRoutine;
    private int lastHealth;
    private int lastShield;
    private float nextPush;
    private float nextDust;
    // the painted Terrathorn art (Assets/Resources/Art) keeps its own colors, the stone tint is only
    // for the borrowed sprite on the prefab
    private Color baseColor;

    public bool HasRage => isTerrathorn;
    public string RageName => "AVALANCHE!";
    public string RageHint => "Terrathorn charges! Every hit pushes it back.";
    public Color RageColor => AvalancheColor;

    public static bool IsTerrathorn(Enemy candidate)
    {
        return candidate != null && candidate.isBoss &&
            !string.IsNullOrEmpty(candidate.bossName) &&
            candidate.bossName.IndexOf("Terrathorn", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void Start()
    {
        enemy = GetComponent<Enemy>();
        if (!IsTerrathorn(enemy))
            return;

        isTerrathorn = true;
        enemy.destroysBarrierOnReach = true;
        sr = GetComponent<SpriteRenderer>();
        hitFeedback = GetComponent<EnemyHitFeedback>();
        bossCollider = GetComponent<CircleCollider2D>();
        baseColor = stoneTint;
        StartCoroutine(ApplyBossLook());
        wallRoutine = StartCoroutine(StoneWallRoutine());
    }

    IEnumerator ApplyBossLook()
    {
        yield return null;

        if (sr != null)
        {
            Sprite painted = GameArt.Find("Boss_Terrathorn");
            Sprite look = painted != null ? painted : bossSprite;
            if (look != null)
                sr.sprite = look;
            baseColor = painted != null ? Color.white : stoneTint;
            enemy.SetHitFlashBaseColor(baseColor);

            if (bossCollider != null && sr.sprite != null)
            {
                Bounds spriteBounds = sr.sprite.bounds;
                bossCollider.radius = Mathf.Min(spriteBounds.extents.x, spriteBounds.extents.y) * 0.5f;
                bossCollider.offset = spriteBounds.center;
            }
        }

        transform.localScale = Vector3.one * bossScale;
        if (hitFeedback != null)
            hitFeedback.SetBasePose(transform.localScale, transform.localRotation);
    }

    // --- STONE WALL ---

    IEnumerator StoneWallRoutine()
    {
        yield return new WaitForSeconds(firstWallDelay);
        GameTuning tuning = GameTuning.Get;

        while (enemy != null && !raging)
        {
            wall.RemoveAll(rock => rock == null);
            if (wall.Count == 0)
                yield return StartCoroutine(RaiseWall(tuning));
            yield return new WaitForSeconds(Mathf.Max(1f, tuning.terrathornWallEvery));
        }
    }

    IEnumerator RaiseWall(GameTuning tuning)
    {
        // warning: Terrathorn glows and dust puffs where each rock will come up
        if (sr != null)
            sr.color = new Color(1f, 0.62f, 0.3f, 1f);
        float elapsed = 0f;
        float nextPuff = 0f;
        while (elapsed < warningTime && enemy != null && !raging)
        {
            if (elapsed >= nextPuff)
            {
                nextPuff += 0.3f;
                foreach (Vector2 spot in WallSpots())
                    FxArt.Play("DustBurst", spot, rockRadius * 2.2f, 16f, FxArt.BossOrder);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (sr != null && enemy != null)
            sr.color = baseColor;
        if (enemy == null || raging)
            yield break;

        Sprite paintedBoulder = GameArt.Find("Terrathorn_Boulder");
        Sprite rock = paintedBoulder != null ? paintedBoulder : boulderSprite;
        foreach (Vector2 spot in WallSpots())
            wall.Add(MakeRock(spot, rock, tuning));

        DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "STONE WALL!", EarthColor);
        GameAudio.Play(Sfx.Block, 0.8f);
        CameraShake.Shake(true);
    }

    List<Vector2> WallSpots()
    {
        List<Vector2> spots = new List<Vector2>(wallAngles.Length);
        Vector2 center = BodyCenter();
        float arc = BodyRadius() + rockRadius + 0.2f;
        foreach (float angle in wallAngles)
        {
            float radians = angle * Mathf.Deg2Rad;
            spots.Add(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * arc);
        }
        return spots;
    }

    Vector2 BodyCenter()
    {
        if (bossCollider == null)
            return transform.position;
        return transform.TransformPoint(bossCollider.offset);
    }

    float BodyRadius()
    {
        return CameraFit.WorldRadius(bossCollider, 1f);
    }

    TerrathornBoulder MakeRock(Vector2 spot, Sprite rock, GameTuning tuning)
    {
        GameObject boulder = new GameObject("TerrathornWallRock");
        boulder.transform.position = new Vector3(spot.x, spot.y, 0f);
        SpriteRenderer boulderRenderer = boulder.AddComponent<SpriteRenderer>();
        boulderRenderer.sprite = rock != null ? rock : HudVisualUtility.CircleSprite();
        boulderRenderer.color = rock != null ? Color.white : new Color(0.55f, 0.45f, 0.36f, 1f);
        boulderRenderer.sortingOrder = (sr != null ? sr.sortingOrder : 5) + 6;

        // scale the art so the rock you see matches the collider
        Vector2 spriteSize = boulderRenderer.sprite.bounds.size;
        float spriteDiameter = Mathf.Max(0.01f, Mathf.Max(spriteSize.x, spriteSize.y));
        float scale = rockRadius * 2.2f / spriteDiameter;
        boulder.transform.localScale = Vector3.one * scale;

        CircleCollider2D boulderCollider = boulder.AddComponent<CircleCollider2D>();
        boulderCollider.radius = rockRadius / scale;
        // it moves with Terrathorn, so it gets a kinematic body (it never pushes the enemies)
        Rigidbody2D body = boulder.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        TerrathornBoulder boulderComponent = boulder.AddComponent<TerrathornBoulder>();
        boulderComponent.Initialise(Mathf.Max(1, tuning.terrathornWallHits), Mathf.Max(1f, tuning.terrathornWallSeconds), scale);
        boulderComponent.Follow(transform, spot - (Vector2)transform.position);
        FxArt.Play("DustBurst", spot, rockRadius * 2.6f, 18f, FxArt.BossOrder);
        return boulderComponent;
    }

    void CrumbleWall()
    {
        foreach (TerrathornBoulder rock in wall)
        {
            if (rock != null)
                rock.CrumbleNow();
        }
        wall.Clear();
    }

    // --- RAGE: AVALANCHE ---

    public void BeginRage()
    {
        if (raging || enemy == null)
            return;
        raging = true;
        if (wallRoutine != null)
            StopCoroutine(wallRoutine);
        CrumbleWall();
        if (sr != null)
            sr.color = baseColor;
        enemy.Haste(Mathf.Max(1f, GameTuning.Get.terrathornRollSpeed), 100000f);
        lastHealth = enemy.CurrentHealth;
        lastShield = enemy.ShieldHits;
    }

    void Update()
    {
        if (!raging || enemy == null || enemy.IsDead)
            return;

        // it shakes from side to side as it charges
        if (hitFeedback != null)
            hitFeedback.SetBasePose(Vector3.one * bossScale, Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 15f) * 7f));

        // dust behind its feet
        if (Time.time >= nextDust)
        {
            nextDust = Time.time + 0.22f;
            Vector2 feet = BodyCenter() + Vector2.down * BodyRadius() * 0.8f +
                Vector2.right * UnityEngine.Random.Range(-0.5f, 0.5f);
            FxArt.Play("DustBurst", feet, 1.1f, 18f, (sr != null ? sr.sortingOrder : 5) - 1);
        }

        // every hit (or blocked hit) pushes it back up its path
        bool hit = enemy.CurrentHealth < lastHealth || enemy.ShieldHits < lastShield;
        lastHealth = enemy.CurrentHealth;
        lastShield = enemy.ShieldHits;
        if (hit && Time.time >= nextPush)
        {
            nextPush = Time.time + 0.15f;
            enemy.KnockBack(Vector2.up, Mathf.Max(0f, GameTuning.Get.terrathornRollPushBack), 0.18f);
            FxArt.Play("DustBurst", BodyCenter(), 2f, 18f, FxArt.BossOrder);
            CameraShake.Shake(false);
        }
    }

    void OnDestroy()
    {
        foreach (TerrathornBoulder rock in wall)
        {
            if (rock != null)
                Destroy(rock.gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.6f, 0.3f, 0.9f);
        if (bossCollider == null)
            bossCollider = GetComponent<CircleCollider2D>();
        foreach (Vector2 spot in WallSpots())
            Gizmos.DrawWireSphere(spot, rockRadius);
    }
}
