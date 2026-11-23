using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// the bosses of chapters 3-5. WaveManager adds this when one spawns (Thornmaw and Terrathorn have
// their own scripts). they use PLACEHOLDER sprites: Assets/Resources/Placeholders/Boss_Voltrex.png,
// Boss_Maelith.png and Boss_Pyrax.png. every move is warned first (the boss glows and a ring
// closes in) so the player has time to react:
// Voltrex (Electric) Overcharge: enemies near it move a lot faster for a few seconds
// Maelith (Water) Tide Shield: a bubble blocks the next two hits
// Pyrax (Fire) Ember Call: calls two of the wave's minions next to it
[RequireComponent(typeof(Enemy))]
public class ChapterBossAbility : MonoBehaviour
{
    public enum BossKind { Voltrex, Maelith, Pyrax }

    private const float BossScale = 2.7f;
    private const float OverchargeRadius = 4.5f;
    private const float OverchargeSpeed = 1.7f;
    private const float OverchargeSeconds = 3.2f;
    private const int ShieldHits = 2;
    private const float ShieldSeconds = 6f;
    private const int MaxSummoned = 4;

    private static readonly Color VoltColor = new Color(1f, 0.9f, 0.3f, 1f);
    private static readonly Color TideColor = new Color(0.55f, 0.88f, 1f, 1f);
    private static readonly Color EmberColor = new Color(1f, 0.55f, 0.2f, 1f);

    public BossKind Kind { get; private set; }

    private Enemy enemy;
    private SpriteRenderer sr;
    private Sprite bossArt;
    private WaveManager waveManager;
    private Enemy minionPrefab;
    private Sprite minionSprite;
    private ElementType minionElement;
    private readonly List<Enemy> summoned = new List<Enemy>(MaxSummoned);
    private readonly List<GameObject> effects = new List<GameObject>(8);
    private readonly List<Aura> auras = new List<Aura>(8);
    private SpriteRenderer shieldBubble;
    private bool lookApplied;

    private struct Aura
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float until;
    }

    public static ChapterBossAbility AttachIfChapterBoss(Enemy boss, WaveManager manager, EnemyWave wave)
    {
        if (boss == null || !boss.isBoss || string.IsNullOrEmpty(boss.bossName))
            return null;

        BossKind kind;
        if (Named(boss, "Voltrex"))
            kind = BossKind.Voltrex;
        else if (Named(boss, "Maelith"))
            kind = BossKind.Maelith;
        else if (Named(boss, "Pyrax"))
            kind = BossKind.Pyrax;
        else
            return null;

        ChapterBossAbility ability = boss.gameObject.AddComponent<ChapterBossAbility>();
        ability.Kind = kind;
        ability.waveManager = manager;
        if (wave != null && wave.enemyPrefabs != null && wave.enemyPrefabs.Length > 0)
            ability.minionPrefab = wave.enemyPrefabs[0];
        ability.minionSprite = wave != null ? wave.enemySprite : null;
        ability.minionElement = wave != null && wave.elementPattern != null && wave.elementPattern.Length > 0
            ? wave.elementPattern[0]
            : boss.element;
        return ability;
    }

    static bool Named(Enemy boss, string name)
    {
        return boss.bossName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void Start()
    {
        enemy = GetComponent<Enemy>();
        sr = GetComponent<SpriteRenderer>();
        enemy.destroysBarrierOnReach = true;
        StartCoroutine(ApplyBossLook());

        switch (Kind)
        {
            case BossKind.Voltrex:
                StartCoroutine(OverchargeRoutine());
                break;
            case BossKind.Maelith:
                StartCoroutine(TideShieldRoutine());
                break;
            default:
                if (minionPrefab == null || waveManager == null)
                    Debug.LogError("Pyrax needs its boss wave's minion prefab to summon Ember Imps.", this);
                else
                    StartCoroutine(EmberCallRoutine());
                break;
        }
    }

    IEnumerator ApplyBossLook()
    {
        yield return null;

        // the shared body's "damaged" sprite would change the boss back to minion art on every hit
        EnemyDamagedSprite damaged = GetComponent<EnemyDamagedSprite>();
        if (damaged != null)
        {
            damaged.damagedSprite = null;
            Destroy(damaged);
        }

        bossArt = PlaceholderArt.Boss(enemy.bossName);
        if (sr != null && bossArt != null)
            sr.sprite = bossArt;
        enemy.SetHitFlashBaseColor(Color.white);

        CircleCollider2D bossCollider = GetComponent<CircleCollider2D>();
        if (bossCollider != null && sr != null && sr.sprite != null)
        {
            Bounds bounds = sr.sprite.bounds;
            bossCollider.radius = Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.55f;
            bossCollider.offset = bounds.center;
        }

        transform.localScale = Vector3.one * BossScale;
        EnemyHitFeedback hitFeedback = GetComponent<EnemyHitFeedback>();
        if (hitFeedback != null)
            hitFeedback.SetBasePose(transform.localScale, transform.localRotation);
        lookApplied = true;
    }

    void Update()
    {
        // nothing else is allowed to put the minion art back on the boss
        if (lookApplied && bossArt != null && sr != null && sr.sprite != bossArt)
            sr.sprite = bossArt;

        UpdateShieldBubble();
        UpdateAuras();
    }

    // --- Voltrex ---

    IEnumerator OverchargeRoutine()
    {
        yield return new WaitForSeconds(4f);
        while (enemy != null)
        {
            yield return StartCoroutine(Telegraph(VoltColor, 0.8f));
            List<Enemy> enemies = Enemy.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy other = enemies[i];
                if (other == null || other == enemy || other.isBoss)
                    continue;
                if (Vector2.Distance(other.transform.position, transform.position) > OverchargeRadius)
                    continue;
                other.Haste(OverchargeSpeed, OverchargeSeconds);
                AddAura(other.transform, OverchargeSeconds);
            }

            DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "OVERCHARGE!", VoltColor);
            StartCoroutine(RingPulse(transform.position, VoltColor, 0.6f, OverchargeRadius * 2f, 0.45f));
            CameraShake.Shake(false);
            yield return new WaitForSeconds(6.5f);
        }
    }

    void AddAura(Transform target, float seconds)
    {
        for (int i = 0; i < auras.Count; i++)
        {
            if (auras[i].transform == target)
            {
                Aura refreshed = auras[i];
                refreshed.until = Time.time + seconds;
                auras[i] = refreshed;
                return;
            }
        }

        GameObject ring = new GameObject("OverchargeAura");
        ring.transform.SetParent(target, false);
        SpriteRenderer renderer = ring.AddComponent<SpriteRenderer>();
        renderer.sprite = HudVisualUtility.RingSprite();
        renderer.color = VoltColor;
        renderer.sortingOrder = 40;
        auras.Add(new Aura { transform = target, renderer = renderer, until = Time.time + seconds });
    }

    void UpdateAuras()
    {
        for (int i = auras.Count - 1; i >= 0; i--)
        {
            Aura aura = auras[i];
            if (aura.transform == null || aura.renderer == null || Time.time >= aura.until)
            {
                if (aura.renderer != null)
                    Destroy(aura.renderer.gameObject);
                auras.RemoveAt(i);
                continue;
            }

            // about one world unit wide, whatever the enemy's own scale is
            Vector3 lossy = aura.transform.lossyScale;
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.08f;
            aura.renderer.transform.localScale = new Vector3(
                1.1f * pulse / Mathf.Max(0.01f, Mathf.Abs(lossy.x)),
                1.1f * pulse / Mathf.Max(0.01f, Mathf.Abs(lossy.y)), 1f);
            Color color = VoltColor;
            color.a = 0.55f + Mathf.Sin(Time.time * 24f) * 0.3f;
            aura.renderer.color = color;
        }
    }

    // --- Maelith ---

    IEnumerator TideShieldRoutine()
    {
        yield return new WaitForSeconds(3f);
        while (enemy != null)
        {
            yield return StartCoroutine(Telegraph(TideColor, 0.6f));
            enemy.AddShield(ShieldHits);
            DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "TIDE SHIELD!", TideColor);

            float until = Time.time + ShieldSeconds;
            while (enemy != null && enemy.ShieldHits > 0 && Time.time < until)
                yield return null;
            if (enemy == null)
                yield break;
            if (enemy.ShieldHits > 0)
                enemy.ClearShield();
            StartCoroutine(RingPulse(transform.position, TideColor, 1.4f, 3.2f, 0.35f));
            yield return new WaitForSeconds(5.5f);
        }
    }

    void UpdateShieldBubble()
    {
        bool shielded = enemy != null && enemy.ShieldHits > 0;
        if (shieldBubble == null)
        {
            if (!shielded)
                return;
            GameObject bubble = new GameObject("TideShield");
            bubble.transform.SetParent(transform, false);
            shieldBubble = bubble.AddComponent<SpriteRenderer>();
            shieldBubble.sprite = HudVisualUtility.CircleSprite();
            shieldBubble.sortingOrder = 39;
        }

        shieldBubble.enabled = shielded;
        if (!shielded)
            return;

        // the bubble wraps around the boss sprite and wobbles a bit
        float size = sr != null && sr.sprite != null
            ? Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) * 1.2f
            : 1.2f;
        float wobble = Mathf.Sin(Time.time * 5f) * 0.04f;
        shieldBubble.transform.localScale = new Vector3(size * (1f + wobble), size * (1f - wobble), 1f);
        Color color = TideColor;
        color.a = enemy.ShieldHits > 1 ? 0.34f : 0.2f;
        shieldBubble.color = color;
    }

    // --- Pyrax ---

    IEnumerator EmberCallRoutine()
    {
        yield return new WaitForSeconds(4f);
        while (enemy != null)
        {
            for (int i = summoned.Count - 1; i >= 0; i--)
            {
                if (summoned[i] == null)
                    summoned.RemoveAt(i);
            }

            if (summoned.Count < MaxSummoned)
            {
                yield return StartCoroutine(Telegraph(EmberColor, 0.7f));
                if (enemy == null)
                    yield break;
                for (int side = -1; side <= 1 && summoned.Count < MaxSummoned; side += 2)
                {
                    Vector3 position = transform.position + new Vector3(side * 0.95f, -0.25f, 0f);
                    Enemy imp = waveManager.SpawnReinforcement(minionPrefab, minionElement, minionSprite, 1f,
                        position, enemy.RouteIndex);
                    if (imp != null)
                        summoned.Add(imp);
                    StartCoroutine(RingPulse(position, EmberColor, 0.2f, 1.4f, 0.3f));
                }
                DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "EMBER CALL!", EmberColor);
            }
            yield return new WaitForSeconds(7f);
        }
    }

    // --- effects all bosses use ---

    // the boss glows in its color while a ring closes in on it
    IEnumerator Telegraph(Color color, float seconds)
    {
        GameObject ring = CreateEffect("BossTelegraph", HudVisualUtility.RingSprite(), color, 41);
        SpriteRenderer renderer = ring.GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        while (elapsed < seconds && enemy != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float diameter = Mathf.Lerp(4.2f, 1.6f, t);
            ring.transform.position = transform.position;
            ring.transform.localScale = new Vector3(diameter, diameter, 1f);
            Color ringColor = color;
            ringColor.a = Mathf.Lerp(0.2f, 0.95f, t);
            renderer.color = ringColor;
            if (sr != null)
                sr.color = Color.Lerp(Color.white, color, 0.45f + 0.35f * Mathf.Sin(elapsed * 30f));
            yield return null;
        }

        RemoveEffect(ring);
        if (sr != null && enemy != null)
            sr.color = Color.white;
    }

    IEnumerator RingPulse(Vector3 position, Color color, float fromDiameter, float toDiameter, float seconds)
    {
        GameObject ring = CreateEffect("BossPulse", HudVisualUtility.RingSprite(), color, 41);
        SpriteRenderer renderer = ring.GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float diameter = Mathf.Lerp(fromDiameter, toDiameter, 1f - (1f - t) * (1f - t));
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(diameter, diameter, 1f);
            Color ringColor = color;
            ringColor.a = 1f - t;
            renderer.color = ringColor;
            yield return null;
        }
        RemoveEffect(ring);
    }

    GameObject CreateEffect(string name, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject effect = new GameObject(name);
        effect.transform.position = transform.position;
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        effects.Add(effect);
        return effect;
    }

    void RemoveEffect(GameObject effect)
    {
        effects.Remove(effect);
        if (effect != null)
            Destroy(effect);
    }

    void OnDestroy()
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null)
                Destroy(effects[i]);
        }
        effects.Clear();

        for (int i = 0; i < auras.Count; i++)
        {
            if (auras[i].renderer != null)
                Destroy(auras[i].renderer.gameObject);
        }
        auras.Clear();
    }
}
