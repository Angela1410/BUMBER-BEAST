using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

// Thornmaw, the chapter 1 boss (stage 4). every few seconds it drops a bramble on the field.
// brambles are bumpers, so the Beast bounces off them, and only maxBrambles stay at once.
// a thorn sprout grows where the next bramble will land, so you can see it coming.
// RAGE (OVERGROWTH, at 30% health): brambles drop faster, and every few seconds Thornmaw heals
// through vines from every bramble that's still standing. break the brambles to stop it
[RequireComponent(typeof(Enemy))]
public class ThornmawAbility : MonoBehaviour, IBossRage
{
    public GameObject bramblePrefab;

    [Header("Boss Look")]
    public Sprite bossSprite;
    public float bossScale = 3f;

    [Header("Bramble Settings")]
    public Sprite brambleSprite;
    public int maxBrambles = 4;
    public float firstDropDelay = 3f;
    public float dropInterval = 4.5f;
    public float warningTime = 0.6f;

    [Header("Random Bramble Area")]
    public Vector2 areaMin = new Vector2(-3.5f, -2.5f);
    public Vector2 areaMax = new Vector2(3.5f, 6f);
    public float minDistanceFromBoss = 1.5f;
    public float minDistanceFromOtherBrambles = 1.2f;

    private static readonly Color GrowGreen = new Color(0.45f, 1f, 0.35f, 1f);
    private static Material vineMaterial;

    private readonly Queue<GameObject> activeBrambles = new Queue<GameObject>();
    private readonly List<GameObject> vines = new List<GameObject>();
    private SpriteRenderer sr;
    private Enemy enemy;
    private bool isThornmaw;
    private bool raging;

    public bool HasRage => isThornmaw;
    public string RageName => "OVERGROWTH!";
    public string RageHint => "Thornmaw heals from its brambles. Break them!";
    public Color RageColor => GrowGreen;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        if (enemy.isBoss &&
            !string.IsNullOrEmpty(enemy.bossName) &&
            enemy.bossName.IndexOf("Thornmaw", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            isThornmaw = true;
            enemy.destroysBarrierOnReach = true;
            sr = GetComponent<SpriteRenderer>();
            StartCoroutine(ApplyBossLook());
            StartCoroutine(BrambleRoutine());
        }
    }

    IEnumerator ApplyBossLook()
    {
        yield return null;

        if (sr != null)
        {
            if (bossSprite != null) sr.sprite = bossSprite;
            GetComponent<Enemy>().SetHitFlashBaseColor(Color.white);
            CircleCollider2D bossCollider = GetComponent<CircleCollider2D>();
            if (bossCollider != null && sr.sprite != null)
            {
                Bounds spriteBounds = sr.sprite.bounds;
                bossCollider.radius = Mathf.Min(spriteBounds.extents.x, spriteBounds.extents.y) * 0.45f;
                bossCollider.offset = spriteBounds.center;
            }
        }
        transform.localScale = Vector3.one * bossScale;
        EnemyHitFeedback hitFeedback = GetComponent<EnemyHitFeedback>();
        if (hitFeedback != null)
            hitFeedback.SetBasePose(transform.localScale, transform.localRotation);
    }

    // drops a new bramble every dropInterval seconds (faster in rage)
    IEnumerator BrambleRoutine()
    {
        if (bramblePrefab == null)
        {
            Debug.LogError("ThornmawAbility requires a bramble prefab.", this);
            yield break;
        }

        yield return new WaitForSeconds(firstDropDelay);

        while (true)
        {
            Vector3 spot = PickBramblePosition();
            // a corrupted thorn sprout grows where it will land (Fx/ThornSprout)
            FxArt.Play("ThornSprout", spot, 1.4f, 6f / Mathf.Max(0.1f, warningTime), FxArt.BossOrder);
            yield return StartCoroutine(WarningFlash());

            GameObject bramble = Instantiate(
                bramblePrefab,
                spot,
                Quaternion.identity);

            Bumper bumper = bramble.GetComponent<Bumper>();
            if (bumper == null)
            {
                Debug.LogError("The Thornmaw bramble prefab must have a Bumper component.", bramble);
                Destroy(bramble);
                yield break;
            }

            bumper.isBramble = true;
            if (brambleSprite != null)
    bumper.brambleSprite = brambleSprite;
            bumper.hp = 1;
            bumper.RefreshVisuals();
            FxArt.Play("DustBurst", spot, 1.2f, 18f, FxArt.BossOrder);

            TrackBramble(bramble);

            float interval = raging ? dropInterval * Mathf.Clamp(GameTuning.Get.thornmawRageDropTime, 0.2f, 1f) : dropInterval;
            yield return new WaitForSeconds(Mathf.Max(0.1f, interval - warningTime));
        }
    }

    Vector3 PickBramblePosition()
    {
        Vector2 candidate = Vector2.zero;

        for (int i = 0; i < 15; i++)
        {
            candidate = new Vector2(
                UnityEngine.Random.Range(areaMin.x, areaMax.x),
                UnityEngine.Random.Range(areaMin.y, areaMax.y));
            // never outside the screen or too close to a wall
            candidate = CameraFit.ClampToField(candidate, CameraFit.WorldRadius(bramblePrefab != null ? bramblePrefab.transform : null, 0.55f));

            if (Vector2.Distance(candidate, transform.position) < minDistanceFromBoss)
                continue;

            bool tooClose = false;
            foreach (GameObject other in activeBrambles)
            {
                if (other != null &&
                    Vector2.Distance(candidate, other.transform.position) < minDistanceFromOtherBrambles)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                break;
        }

        return new Vector3(candidate.x, candidate.y, 0f);
    }

    IEnumerator WarningFlash()
    {
        if (sr != null)
            sr.color = new Color(0.8f, 0.45f, 1f);

        yield return new WaitForSeconds(warningTime);

        if (sr != null)
            sr.color = Color.white;
    }

    // keeps track of the brambles and removes the oldest one when there are too many
    void TrackBramble(GameObject bramble)
    {
        activeBrambles.Enqueue(bramble);

        while (activeBrambles.Count > 0 && activeBrambles.Peek() == null)
            activeBrambles.Dequeue();

        while (activeBrambles.Count > maxBrambles)
        {
            GameObject oldest = activeBrambles.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    // --- RAGE: OVERGROWTH ---

    public void BeginRage()
    {
        if (raging || enemy == null)
            return;
        raging = true;
        enemy.Haste(1.15f, 100000f);
        StartCoroutine(RegrowRoutine());
    }

    // every few seconds: +1 health for each bramble still standing (up to Thornmaw Regrow Max),
    // with green vines from the brambles to the boss so you can see where it comes from
    IEnumerator RegrowRoutine()
    {
        GameTuning tuning = GameTuning.Get;
        while (enemy != null && !enemy.IsDead)
        {
            yield return new WaitForSeconds(Mathf.Max(0.5f, tuning.thornmawRegrowSeconds));
            if (enemy == null || enemy.IsDead)
                yield break;

            List<GameObject> standing = new List<GameObject>();
            foreach (GameObject bramble in activeBrambles)
            {
                if (bramble != null && bramble.activeInHierarchy)
                    standing.Add(bramble);
            }
            if (standing.Count == 0)
                continue;

            foreach (GameObject bramble in standing)
            {
                StartCoroutine(VineLink(bramble.transform.position));
                FxArt.Play("HealLeaf", bramble.transform.position, 0.9f, 12f, FxArt.BossOrder);
            }

            int cap = Mathf.Max(1, Mathf.RoundToInt(enemy.MaxHealthTotal * Mathf.Clamp01(tuning.thornmawRegrowCap)));
            int healed = enemy.Heal(Mathf.Min(standing.Count, Mathf.Max(1, tuning.thornmawRegrowMax)), cap);
            if (healed > 0)
            {
                FxArt.Play("HealLeaf", transform.position + Vector3.up * 0.9f, 1.8f, 12f, FxArt.BossOrder);
                DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "+" + healed, GrowGreen);
            }
        }
    }

    // a green vine that flashes from a bramble to Thornmaw and fades
    IEnumerator VineLink(Vector3 from)
    {
        GameObject vine = new GameObject("OvergrowthVine");
        vines.Add(vine);
        LineRenderer line = vine.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 6;
        line.numCapVertices = 2;
        line.sortingOrder = FxArt.BossOrder - 1;
        if (vineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                vineMaterial = new Material(shader);
        }
        if (vineMaterial != null)
            line.sharedMaterial = vineMaterial;

        const float seconds = 0.7f;
        float elapsed = 0f;
        while (elapsed < seconds && this != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            Vector3 to = transform.position;
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.forward);
            for (int i = 0; i < line.positionCount; i++)
            {
                float u = i / (line.positionCount - 1f);
                // a wavy vine that grows from the bramble toward the boss
                float reach = Mathf.Min(1f, t * 2.5f);
                line.SetPosition(i, Vector3.Lerp(from, to, u * reach) + side * Mathf.Sin(u * 9f + elapsed * 6f) * 0.12f);
            }
            float width = 0.14f * (1f - t * 0.6f);
            line.startWidth = width;
            line.endWidth = width * 0.5f;
            Color color = GrowGreen;
            color.a = 1f - t * t;
            line.startColor = color;
            line.endColor = color;
            yield return null;
        }
        vines.Remove(vine);
        Destroy(vine);
    }

    void OnDestroy()
    {
        foreach (GameObject bramble in activeBrambles)
        {
            if (bramble != null)
                Destroy(bramble);
        }
        foreach (GameObject vine in vines)
        {
            if (vine != null)
                Destroy(vine);
        }
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.9f);
        Vector3 center = (Vector3)((areaMin + areaMax) * 0.5f);
        Vector3 size = (Vector3)(areaMax - areaMin);
        Gizmos.DrawWireCube(center, size);
    }
}
