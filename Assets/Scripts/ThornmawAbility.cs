using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class ThornmawAbility : MonoBehaviour
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

    private readonly Queue<GameObject> activeBrambles = new Queue<GameObject>();
    private SpriteRenderer sr;

    void Start()
    {
        Enemy enemy = GetComponent<Enemy>();
        if (enemy.isBoss &&
            !string.IsNullOrEmpty(enemy.bossName) &&
            enemy.bossName.IndexOf("Thornmaw", StringComparison.OrdinalIgnoreCase) >= 0)
        {
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
            yield return StartCoroutine(WarningFlash());

            GameObject bramble = Instantiate(
                bramblePrefab,
                PickBramblePosition(),
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

            TrackBramble(bramble);

            yield return new WaitForSeconds(Mathf.Max(0.1f, dropInterval - warningTime));
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

    void OnDestroy()
    {
        foreach (GameObject bramble in activeBrambles)
        {
            if (bramble != null)
                Destroy(bramble);
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
