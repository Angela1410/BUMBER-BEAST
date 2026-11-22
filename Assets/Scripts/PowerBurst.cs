using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The POWER BUMPER (red bumper) unleashes the power of whichever Beast hits it, aimed at the
// enemies on the field so it never misses:
//   Choma    FIREBALL VOLLEY  homing fireballs into the three nearest enemies, each a wide blast
//   Thistle  WILD ROOTS       every enemy is rooted in place (and nicked)
//   Mossback EARTHQUAKE       every enemy is knocked back up the lane
//   Voltis   THUNDER CHAIN    lightning jumps through up to six enemies
//   Ripple   TIDAL WAVE       a wave rolls across the whole field
// Element advantage adds +1 damage, as with normal hits. The bumper only fires while there is an
// enemy to hit (Bumper checks HasTargets first).
public class PowerBurst : MonoBehaviour
{
    private const int SortingOrder = 40;
    private const float FireballBlastRadius = 1.9f;

    private static Material lineMaterial;
    private BeastId beast;
    private int running;

    public static bool HasTargets()
    {
        List<Enemy> enemies = Enemy.ActiveEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (IsTarget(enemy))
                return true;
        }
        return false;
    }

    public static string PowerName(BeastId beast)
    {
        switch (beast)
        {
            case BeastId.Thistle: return "WILD ROOTS!";
            case BeastId.Mossback: return "EARTHQUAKE!";
            case BeastId.Voltis: return "THUNDER CHAIN!";
            case BeastId.Ripple: return "TIDAL WAVE!";
            default: return "FIREBALL VOLLEY!";
        }
    }

    public static void Trigger(BeastId beast, Vector3 origin)
    {
        GameObject root = new GameObject("PowerBurst");
        root.transform.position = origin;
        PowerBurst burst = root.AddComponent<PowerBurst>();
        burst.beast = beast;

        Color color = ElementVisuals.GetColor(BeastRoster.GetElement(beast));
        StageToast.Show(PowerName(beast), "POWER BUMPER", Color.Lerp(color, Color.white, 0.25f), 1.3f, 0.72f);
        GameAudio.PlaySkill(BeastRoster.GetElement(beast));
        GameAudio.Play(Sfx.PowerBumper);
        BumperExplosionRing.Spawn(origin, 2.2f, color, SortingOrder);
        HitStop.Pause(0.06f);

        switch (beast)
        {
            case BeastId.Thistle: burst.StartCoroutine(burst.WildRoots(origin)); break;
            case BeastId.Mossback: burst.StartCoroutine(burst.Earthquake(origin)); break;
            case BeastId.Voltis: burst.StartCoroutine(burst.ThunderChain(origin)); break;
            case BeastId.Ripple: burst.StartCoroutine(burst.TidalWave(origin)); break;
            default: burst.StartCoroutine(burst.FireballVolley(origin)); break;
        }
    }

    static bool IsTarget(Enemy enemy)
    {
        return enemy != null && !enemy.IsDead && !enemy.HasReachedBarrier;
    }

    void Hit(Enemy enemy, int damage)
    {
        if (!IsTarget(enemy))
            return;
        bool super = ElementChart.IsSuperEffective(BeastRoster.GetElement(beast), enemy.element);
        enemy.TakeDamage(damage + (super ? 1 : 0), super);
    }

    // Live targets, nearest to "from" first.
    static List<Enemy> Targets(Vector3 from)
    {
        List<Enemy> found = new List<Enemy>();
        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (IsTarget(enemy))
                found.Add(enemy);
        }
        found.Sort((a, b) => ((Vector2)(a.transform.position - from)).sqrMagnitude
            .CompareTo(((Vector2)(b.transform.position - from)).sqrMagnitude));
        return found;
    }

    void Finish()
    {
        if (running <= 0)
            Destroy(gameObject);
    }

    // ------------------------------------------------------------------ Choma

    IEnumerator FireballVolley(Vector3 origin)
    {
        List<Enemy> targets = Targets(origin);
        int count = Mathf.Min(3, targets.Count);
        for (int index = 0; index < count; index++)
        {
            running++;
            StartCoroutine(Fireball(origin, targets[index]));
            yield return new WaitForSeconds(0.09f);
        }
        while (running > 0)
            yield return null;
        Finish();
    }

    IEnumerator Fireball(Vector3 origin, Enemy target)
    {
        Color fire = new Color(1f, 0.55f, 0.15f, 1f);
        SpriteRenderer glow = CreateSprite("Fireball", HudVisualUtility.GlowSprite(), fire, 0.95f, SortingOrder + 1);
        SpriteRenderer core = CreateSprite("FireballCore", HudVisualUtility.CircleSprite(), new Color(1f, 0.92f, 0.6f, 1f), 0.32f, SortingOrder + 2);
        Vector3 end = target != null ? target.transform.position : origin;
        const float flight = 0.34f;
        float elapsed = 0f;
        while (elapsed < flight)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flight);
            if (IsTarget(target))
                end = target.transform.position;
            Vector3 position = Vector3.Lerp(origin, end, t * t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.8f;
            glow.transform.position = position;
            core.transform.position = position;
            float pulse = 1f + 0.15f * Mathf.Sin(elapsed * 40f);
            glow.transform.localScale = Vector3.one * 0.95f * pulse;
            yield return null;
        }

        Destroy(glow.gameObject);
        Destroy(core.gameObject);
        BumperExplosionRing.Spawn(end, FireballBlastRadius, fire, SortingOrder);
        GameAudio.Play(Sfx.BumperExplode, 0.8f);
        CameraShake.Shake(false);
        float radiusSquared = FireballBlastRadius * FireballBlastRadius;
        foreach (Enemy enemy in Targets(end))
        {
            if (((Vector2)(enemy.transform.position - end)).sqrMagnitude <= radiusSquared)
                Hit(enemy, 2);
        }
        running--;
    }

    // ------------------------------------------------------------------ Thistle

    IEnumerator WildRoots(Vector3 origin)
    {
        LaunchController launcher = FindAnyObjectByType<LaunchController>();
        Sprite[] frames = launcher != null ? launcher.GetSkillEffectFrames(BeastId.Thistle) : null;
        Color green = ElementVisuals.GetColor(ElementType.Grass);
        foreach (Enemy enemy in Targets(origin))
        {
            enemy.Root(3.5f);
            Hit(enemy, 1);
            if (frames != null)
                SpriteFlipbookFx.Play(frames, enemy.transform.position, 1.1f, 14f);
            else
                BumperExplosionRing.Spawn(enemy.transform.position, 0.9f, green, SortingOrder);
            yield return new WaitForSeconds(0.05f);
        }
        Finish();
    }

    // ------------------------------------------------------------------ Mossback

    IEnumerator Earthquake(Vector3 origin)
    {
        Color earth = ElementVisuals.GetColor(ElementType.Earth);
        CameraShake.Shake(true);
        BumperExplosionRing.Spawn(origin, 4.5f, earth, SortingOrder);
        yield return new WaitForSeconds(0.08f);
        foreach (Enemy enemy in Targets(origin))
        {
            enemy.KnockBack(Vector2.up, 2.2f, 0.3f);
            Hit(enemy, 1);
            BumperExplosionRing.Spawn(enemy.transform.position, 0.8f, earth, SortingOrder);
        }
        Finish();
    }

    // ------------------------------------------------------------------ Voltis

    IEnumerator ThunderChain(Vector3 origin)
    {
        Color bolt = new Color(1f, 0.93f, 0.35f, 1f);
        List<Enemy> remaining = Targets(origin);
        List<Vector3> points = new List<Vector3> { origin };
        LineRenderer line = CreateLine(bolt);
        Vector3 current = origin;
        for (int hop = 0; hop < 6 && remaining.Count > 0; hop++)
        {
            Enemy next = remaining[0];
            float best = float.MaxValue;
            foreach (Enemy candidate in remaining)
            {
                float distance = ((Vector2)(candidate.transform.position - current)).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    next = candidate;
                }
            }
            remaining.Remove(next);
            Vector3 to = next.transform.position;
            Vector3 side = Vector3.Cross((to - current).normalized, Vector3.forward);
            for (int step = 1; step <= 3; step++)
                points.Add(Vector3.Lerp(current, to, step / 4f) + side * Random.Range(-0.3f, 0.3f));
            points.Add(to);
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            Hit(next, 2);
            BumperExplosionRing.Spawn(to, 0.7f, bolt, SortingOrder);
            current = to;
            yield return new WaitForSeconds(0.07f);
        }

        float fade = 0f;
        while (fade < 0.3f)
        {
            fade += Time.deltaTime;
            Color color = bolt;
            color.a = 1f - fade / 0.3f;
            line.startColor = color;
            line.endColor = color;
            yield return null;
        }
        Destroy(line.gameObject);
        Finish();
    }

    // ------------------------------------------------------------------ Ripple

    IEnumerator TidalWave(Vector3 origin)
    {
        Color water = ElementVisuals.GetColor(ElementType.Water);
        List<Enemy> targets = Targets(origin);
        const float speed = 10f;
        float radius = 0f;
        int next = 0;
        float ringTimer = 0f;
        while (next < targets.Count)
        {
            radius += speed * Time.deltaTime;
            ringTimer -= Time.deltaTime;
            if (ringTimer <= 0f)
            {
                BumperExplosionRing.Spawn(origin, Mathf.Max(1f, radius * 0.6f), water, SortingOrder);
                ringTimer = 0.12f;
            }
            while (next < targets.Count)
            {
                Enemy enemy = targets[next];
                if (enemy != null && ((Vector2)(enemy.transform.position - origin)).magnitude > radius)
                    break;
                if (IsTarget(enemy))
                {
                    enemy.KnockBack(Vector2.up, 1f, 0.2f);
                    Hit(enemy, 1);
                    BumperExplosionRing.Spawn(enemy.transform.position, 0.8f, water, SortingOrder);
                }
                next++;
            }
            yield return null;
        }
        Finish();
    }

    // ------------------------------------------------------------------ visuals

    SpriteRenderer CreateSprite(string name, Sprite sprite, Color color, float size, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        float spriteSize = sprite != null ? Mathf.Max(0.001f, sprite.bounds.size.x) : 1f;
        child.transform.localScale = Vector3.one * (size / spriteSize);
        return renderer;
    }

    LineRenderer CreateLine(Color color)
    {
        GameObject lineObject = new GameObject("ThunderChain");
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.startWidth = 0.12f;
        line.endWidth = 0.07f;
        line.numCornerVertices = 1;
        line.sortingOrder = SortingOrder + 1;
        line.startColor = color;
        line.endColor = color;
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                lineMaterial = new Material(shader);
        }
        if (lineMaterial != null)
            line.sharedMaterial = lineMaterial;
        line.positionCount = 0;
        return line;
    }
}
