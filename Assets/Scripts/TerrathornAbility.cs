using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Earth chapter boss. Lives on the shared enemy prefab next to ThornmawAbility
// and only activates for a boss whose name contains "Terrathorn".
//
// Rockfall: a shadow telegraphs where a boulder will land, then a temporary
// boulder drops into the arena. Boulders block and deflect shots (the aim
// preview shows the bounce) and crumble after a few hits or a short time, so
// the player always has a clear lane.
[RequireComponent(typeof(Enemy))]
public class TerrathornAbility : MonoBehaviour
{
    [Header("Boss Look")]
    public Sprite bossSprite;
    public float bossScale = 3f;
    public Color stoneTint = new Color(0.86f, 0.68f, 0.5f, 1f);

    [Header("Rockfall")]
    public Sprite boulderSprite;
    public int maxBoulders = 3;
    public float firstDropDelay = 4f;
    public float dropInterval = 5.5f;
    public float warningTime = 0.9f;
    public float boulderLifetime = 10f;
    public int boulderHits = 2;
    public float boulderRadius = 0.55f;

    [Header("Rockfall Area")]
    public Vector2 areaMin = new Vector2(-3.2f, -1.4f);
    public Vector2 areaMax = new Vector2(3.2f, 3.4f);
    public float minDistanceFromBoss = 1.6f;
    public float clearanceRadius = 1.05f;

    private readonly List<GameObject> activeBoulders = new List<GameObject>();
    private SpriteRenderer sr;
    private Enemy enemy;
    // Painted Terrathorn art (Assets/Resources/Art) keeps its own colours; the stone tint only
    // dresses up the borrowed sprite assigned on the prefab.
    private Color baseColor;

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

        enemy.destroysBarrierOnReach = true;
        sr = GetComponent<SpriteRenderer>();
        baseColor = stoneTint;
        StartCoroutine(ApplyBossLook());
        StartCoroutine(RockfallRoutine());
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

            CircleCollider2D bossCollider = GetComponent<CircleCollider2D>();
            if (bossCollider != null && sr.sprite != null)
            {
                Bounds spriteBounds = sr.sprite.bounds;
                bossCollider.radius = Mathf.Min(spriteBounds.extents.x, spriteBounds.extents.y) * 0.5f;
                bossCollider.offset = spriteBounds.center;
            }
        }

        transform.localScale = Vector3.one * bossScale;
        EnemyHitFeedback hitFeedback = GetComponent<EnemyHitFeedback>();
        if (hitFeedback != null)
            hitFeedback.SetBasePose(transform.localScale, transform.localRotation);
    }

    IEnumerator RockfallRoutine()
    {
        yield return new WaitForSeconds(firstDropDelay);

        while (enemy != null)
        {
            activeBoulders.RemoveAll(item => item == null);
            if (activeBoulders.Count < Mathf.Max(1, maxBoulders) &&
                TryPickLandingSpot(out Vector2 landing))
            {
                yield return StartCoroutine(DropBoulder(landing));
            }

            yield return new WaitForSeconds(Mathf.Max(0.5f, dropInterval - warningTime));
        }
    }

    bool TryPickLandingSpot(out Vector2 landing)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(areaMin.x, areaMax.x),
                UnityEngine.Random.Range(areaMin.y, areaMax.y));

            if (Vector2.Distance(candidate, transform.position) < minDistanceFromBoss)
                continue;

            // Keep clear of bumpers, walls, other boulders and the Beast.
            bool blocked = false;
            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(candidate, clearanceRadius))
            {
                if (overlap == null || overlap.isTrigger || overlap.GetComponentInParent<Enemy>() != null)
                    continue;

                blocked = true;
                break;
            }

            if (blocked)
                continue;

            landing = candidate;
            return true;
        }

        landing = Vector2.zero;
        return false;
    }

    IEnumerator DropBoulder(Vector2 landing)
    {
        // Telegraph: the boss glows and a shadow grows where the rock will land.
        if (sr != null)
            sr.color = new Color(1f, 0.62f, 0.3f, 1f);

        GameObject shadow = new GameObject("RockfallWarning");
        shadow.transform.position = new Vector3(landing.x, landing.y, 0f);
        SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = HudVisualUtility.CircleSprite();
        shadowRenderer.sortingOrder = 1;

        float elapsed = 0f;
        while (elapsed < warningTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningTime);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 18f);
            float diameter = boulderRadius * 2f * Mathf.Lerp(0.4f, 1.1f, t);
            shadow.transform.localScale = new Vector3(diameter, diameter * 0.55f, 1f);
            shadowRenderer.color = new Color(0.12f, 0.06f, 0.02f, Mathf.Lerp(0.15f, 0.5f, t) + pulse * 0.1f);
            yield return null;
        }

        Destroy(shadow);
        if (sr != null && enemy != null)
            sr.color = baseColor;

        if (enemy == null)
            yield break;

        GameObject boulder = new GameObject("TerrathornBoulder");
        boulder.transform.position = new Vector3(landing.x, landing.y, 0f);
        SpriteRenderer boulderRenderer = boulder.AddComponent<SpriteRenderer>();
        Sprite paintedBoulder = GameArt.Find("Terrathorn_Boulder");
        Sprite rock = paintedBoulder != null ? paintedBoulder : boulderSprite;
        boulderRenderer.sprite = rock != null ? rock : HudVisualUtility.CircleSprite();
        boulderRenderer.color = rock != null ? Color.white : new Color(0.55f, 0.45f, 0.36f, 1f);
        boulderRenderer.sortingOrder = 2;

        // Scale the art so the visible rock matches the collider.
        Vector2 spriteSize = boulderRenderer.sprite.bounds.size;
        float spriteDiameter = Mathf.Max(0.01f, Mathf.Max(spriteSize.x, spriteSize.y));
        float scale = boulderRadius * 2.2f / spriteDiameter;
        boulder.transform.localScale = Vector3.one * scale;

        CircleCollider2D boulderCollider = boulder.AddComponent<CircleCollider2D>();
        boulderCollider.radius = boulderRadius / scale;

        TerrathornBoulder boulderComponent = boulder.AddComponent<TerrathornBoulder>();
        boulderComponent.Initialise(boulderHits, boulderLifetime, scale);
        activeBoulders.Add(boulder);
        CameraShake.Shake(true);
    }

    void OnDestroy()
    {
        foreach (GameObject boulder in activeBoulders)
        {
            if (boulder != null)
                Destroy(boulder);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.6f, 0.3f, 0.9f);
        Vector3 center = (Vector3)((areaMin + areaMax) * 0.5f);
        Vector3 size = (Vector3)(areaMax - areaMin);
        Gizmos.DrawWireCube(center, size);
    }
}
