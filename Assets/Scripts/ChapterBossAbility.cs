using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// the bosses of chapters 3-5. WaveManager adds this when one spawns (Thornmaw and Terrathorn have
// their own scripts). they use PLACEHOLDER sprites: Assets/Resources/Placeholders/Boss_Voltrex.png,
// Boss_Maelith.png and Boss_Pyrax.png, and the effect pictures in Assets/Resources/Fx. every move is
// warned first (the boss glows and a ring closes in) so the player has time to react.
// the numbers are in GameTuning (Voltrex / Maelith / Pyrax):
// Voltrex (Electric) OVERCHARGE: every enemy on the field (and Voltrex) runs a lot faster for a few
//   seconds, then THUNDER BLINK: Voltrex jumps sideways in a flash, so it's harder to hit.
//   rage THUNDERSTORM: lightning hits some bumpers and charges them. touching a sparking bumper
//   zaps the Beast straight back to the pad, so aim around them
// Maelith (Water) TIDE SHIELD: a bubble blocks the next two hits.
//   rage MIRROR MIST: two see-through water copies walk next to it. a copy pops after one hit,
//   so look for the solid one
// Pyrax (Fire) EMBER CALL: two of the wave's minions come out of fire columns next to it.
//   rage PHOENIX: calls more often, and when it's beaten it rises once more from the ashes
[RequireComponent(typeof(Enemy))]
public class ChapterBossAbility : MonoBehaviour, IBossRage
{
    public enum BossKind { Voltrex, Maelith, Pyrax }

    private const float BossScale = 2.7f;
    private const int MaxSummoned = 4;

    private static readonly Color VoltColor = new Color(1f, 0.9f, 0.3f, 1f);
    private static readonly Color TideColor = new Color(0.55f, 0.88f, 1f, 1f);
    private static readonly Color EmberColor = new Color(1f, 0.55f, 0.2f, 1f);
    private static bool quitting;

    public BossKind Kind { get; private set; }

    private Enemy enemy;
    private SpriteRenderer sr;
    private Sprite bossArt;
    private WaveManager waveManager;
    private Enemy minionPrefab;
    private Sprite minionSprite;
    private ElementType minionElement;
    private readonly List<Enemy> summoned = new List<Enemy>(MaxSummoned);
    private readonly List<Enemy> copies = new List<Enemy>(4);
    private readonly List<GameObject> effects = new List<GameObject>(8);
    private readonly Dictionary<Transform, SpriteFlipbookFx> auras = new Dictionary<Transform, SpriteFlipbookFx>();
    private readonly List<ChargedBumper> charged = new List<ChargedBumper>(4);
    private SpriteFlipbookFx shieldBubble;
    private bool lookApplied;
    private bool raging;
    private bool reborn;
    private bool canRise;

    public bool HasRage => true;
    public string RageName
    {
        get
        {
            switch (Kind)
            {
                case BossKind.Voltrex: return "THUNDERSTORM!";
                case BossKind.Maelith: return "MIRROR MIST!";
                default: return "PHOENIX!";
            }
        }
    }
    public string RageHint
    {
        get
        {
            switch (Kind)
            {
                case BossKind.Voltrex: return "Lightning charges bumpers. Don't touch the sparking ones!";
                case BossKind.Maelith: return "Water copies! Only the solid Maelith is real.";
                default: return "Pyrax burns hotter... and it can rise again!";
            }
        }
    }
    public Color RageColor
    {
        get
        {
            switch (Kind)
            {
                case BossKind.Voltrex: return VoltColor;
                case BossKind.Maelith: return TideColor;
                default: return EmberColor;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetQuitting()
    {
        quitting = false;
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }

    static void OnQuit()
    {
        quitting = true;
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
                if (reborn)
                    StartCoroutine(RebornIntro());
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
    }

    public void BeginRage()
    {
        if (raging || enemy == null)
            return;
        raging = true;
        switch (Kind)
        {
            case BossKind.Voltrex:
                StartCoroutine(ThunderstormRoutine());
                break;
            case BossKind.Maelith:
                StartCoroutine(MirrorMistRoutine());
                break;
            default:
                // a reborn Pyrax can't rise again
                canRise = !reborn && GameTuning.Get.pyraxRebornHealth > 0f;
                enemy.Haste(1.2f, 100000f);
                break;
        }
    }

    // --- Voltrex ---

    IEnumerator OverchargeRoutine()
    {
        GameTuning tuning = GameTuning.Get;
        yield return new WaitForSeconds(3.5f);
        while (enemy != null)
        {
            yield return StartCoroutine(Telegraph(VoltColor, 0.7f));
            if (enemy == null)
                yield break;
            Overcharge(tuning);
            if (tuning.voltrexBlinkDistance > 0.05f)
            {
                yield return new WaitForSeconds(0.45f);
                yield return StartCoroutine(ThunderBlink(tuning.voltrexBlinkDistance));
            }
            float wait = Mathf.Max(1.5f, tuning.voltrexOverchargeEvery) * (raging ? 0.75f : 1f);
            yield return new WaitForSeconds(wait);
        }
    }

    // every enemy on the field gets faster, with a lightning bolt from Voltrex to it
    void Overcharge(GameTuning tuning)
    {
        float speed = Mathf.Max(1f, tuning.voltrexOverchargeSpeed);
        float seconds = Mathf.Max(0.5f, tuning.voltrexOverchargeSeconds);
        List<Enemy> enemies = Enemy.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy other = enemies[i];
            if (other == null || other == enemy || other.IsDead || other.isBoss)
                continue;
            Supercharge(other, speed, seconds, 1.3f);
            FxArt.Bolt(transform.position, other.transform.position, 0.45f, 0.3f, FxArt.BossOrder);
        }
        // Voltrex too, a little less
        enemy.Haste(1f + (speed - 1f) * 0.6f, seconds);
        Supercharge(null, 0f, seconds, FxArt.SizeOf(sr, 2f) * 0.95f);

        DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "OVERCHARGE!", VoltColor);
        FxArt.Play("ZapImpact", transform.position, 2.4f, 18f, FxArt.BossOrder);
        StartCoroutine(RingPulse(transform.position, VoltColor, 0.6f, 9f, 0.45f));
        GameAudio.Play(Sfx.SkillElectric, 0.8f);
        CameraShake.Shake(false);
    }

    // a crackling electric ring on a sped-up enemy (null = on Voltrex itself)
    void Supercharge(Enemy target, float speed, float seconds, float size)
    {
        Transform holder = target != null ? target.transform : transform;
        if (target != null)
            target.Haste(speed, seconds);
        if (auras.TryGetValue(holder, out SpriteFlipbookFx existing) && existing != null)
        {
            existing.StopAfter(seconds);
            return;
        }
        SpriteFlipbookFx aura = FxArt.Loop("ElectricAura", holder, size, 12f, FxArt.BossOrder - 2, Color.white, seconds);
        if (aura != null)
            auras[holder] = aura;
    }

    // Voltrex jumps to the side in a flash of lightning
    IEnumerator ThunderBlink(float distance)
    {
        if (enemy == null || enemy.IsDead)
            yield break;

        float radius = Mathf.Max(0.5f, CameraFit.WorldRadius(GetComponent<CircleCollider2D>(), 1f) * 0.6f);
        Vector2 here = transform.position;
        float left = here.x - CameraFit.ClampToField(here + Vector2.left * distance, radius).x;
        float right = CameraFit.ClampToField(here + Vector2.right * distance, radius).x - here.x;
        // the side with more room, and a little random so it can't be learned
        float step = (right > left || (Mathf.Approximately(right, left) && Random.value > 0.5f)) ? right : -left;
        if (Mathf.Abs(step) < 0.4f)
            yield break;

        Vector3 target = transform.position + Vector3.right * step;
        FxArt.Play("ZapImpact", target, 1.6f, 14f, FxArt.BossOrder);
        if (sr != null)
            sr.color = Color.Lerp(Color.white, VoltColor, 0.6f);
        yield return new WaitForSeconds(0.25f);
        if (enemy == null || enemy.IsDead)
            yield break;

        Vector3 from = transform.position;
        enemy.ShiftLane(step);
        FxArt.Bolt(from, transform.position, 0.8f, 0.3f, FxArt.BossOrder);
        FxArt.Play("ZapImpact", from, 2f, 20f, FxArt.BossOrder);
        FxArt.Play("ZapImpact", transform.position, 2.2f, 20f, FxArt.BossOrder);
        if (sr != null)
            sr.color = Color.white;
        GameAudio.Play(Sfx.SkillElectric, 0.6f);
        CameraShake.Shake(false);
    }

    // rage: lightning charges a few bumpers. a charged bumper sparks and zaps a Beast that touches it
    IEnumerator ThunderstormRoutine()
    {
        GameTuning tuning = GameTuning.Get;
        yield return new WaitForSeconds(1.2f);
        while (enemy != null && !enemy.IsDead)
        {
            List<Bumper> targets = PickStormBumpers(Mathf.Max(1, tuning.voltrexStormStrikes));
            List<SpriteRenderer> marks = new List<SpriteRenderer>(targets.Count);
            Sprite[] markSprite = FxArt.Frames("StrikeMark");
            foreach (Bumper bumper in targets)
            {
                GameObject mark = CreateEffect("StrikeMark", markSprite != null ? markSprite[0] : HudVisualUtility.RingSprite(),
                    VoltColor, FxArt.BossOrder - 3);
                mark.transform.position = bumper.transform.position;
                marks.Add(mark.GetComponent<SpriteRenderer>());
            }

            // the marks pulse while the storm builds up
            float warning = Mathf.Max(0.3f, tuning.voltrexStrikeWarning);
            float elapsed = 0f;
            while (elapsed < warning && enemy != null)
            {
                elapsed += Time.deltaTime;
                float pulse = 1f + 0.12f * Mathf.Sin(elapsed * 16f);
                for (int i = 0; i < marks.Count; i++)
                {
                    if (marks[i] == null || targets[i] == null)
                        continue;
                    marks[i].transform.position = targets[i].transform.position;
                    float size = BumperSize(targets[i]) * 1.9f * pulse;
                    float spriteSize = Mathf.Max(0.01f, marks[i].sprite.bounds.size.x);
                    marks[i].transform.localScale = Vector3.one * (size / spriteSize);
                    Color color = Color.white;
                    color.a = Mathf.Lerp(0.35f, 1f, elapsed / warning);
                    marks[i].color = color;
                }
                yield return null;
            }
            foreach (SpriteRenderer mark in marks)
            {
                if (mark != null)
                    RemoveEffect(mark.gameObject);
            }
            if (enemy == null || enemy.IsDead)
                yield break;

            foreach (Bumper bumper in targets)
            {
                if (bumper == null || !bumper.gameObject.activeInHierarchy)
                    continue;
                FxArt.Strike(bumper.transform.position, 1.5f);
                ChargedBumper charge = ChargedBumper.Charge(bumper, Mathf.Max(0.5f, tuning.voltrexChargedSeconds));
                if (charge != null)
                    charged.Add(charge);
            }
            // one more strike on a minion: it gets supercharged
            Enemy minion = RandomMinion();
            if (minion != null)
            {
                FxArt.Strike(minion.transform.position, 1.4f);
                Supercharge(minion, Mathf.Max(1f, tuning.voltrexOverchargeSpeed), Mathf.Max(0.5f, tuning.voltrexOverchargeSeconds), 1.3f);
            }
            GameAudio.Play(Sfx.SkillElectric);
            CameraShake.Shake(true);

            yield return new WaitForSeconds(Mathf.Max(1.5f, tuning.voltrexStormEvery));
        }
    }

    // bumpers that can be charged: normal ones on the field, not the Power Bumper or brambles,
    // and not ones that are still charged
    static List<Bumper> PickStormBumpers(int count)
    {
        List<Bumper> pool = new List<Bumper>();
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper == null || !bumper.gameObject.activeInHierarchy || bumper.isBramble || bumper.IsPowerBumper)
                continue;
            if (bumper.GetComponent<ChargedBumper>() != null)
                continue;
            pool.Add(bumper);
        }
        List<Bumper> picked = new List<Bumper>(count);
        while (picked.Count < count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return picked;
    }

    static float BumperSize(Bumper bumper)
    {
        return CameraFit.WorldRadius(bumper, 0.55f) * 2f;
    }

    Enemy RandomMinion()
    {
        List<Enemy> options = new List<Enemy>();
        foreach (Enemy other in Enemy.ActiveEnemies)
        {
            if (other != null && other != enemy && !other.IsDead && !other.isBoss)
                options.Add(other);
        }
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }

    // --- Maelith ---

    IEnumerator TideShieldRoutine()
    {
        GameTuning tuning = GameTuning.Get;
        yield return new WaitForSeconds(3f);
        while (enemy != null)
        {
            yield return StartCoroutine(Telegraph(TideColor, 0.6f));
            if (enemy == null)
                yield break;
            enemy.AddShield(Mathf.Max(1, tuning.maelithShieldHits));
            DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "TIDE SHIELD!", TideColor);
            FxArt.Play("SplashWave", transform.position, FxArt.SizeOf(sr, 2f) * 1.1f, 18f, FxArt.BossOrder);

            float until = Time.time + 6f;
            while (enemy != null && enemy.ShieldHits > 0 && Time.time < until)
                yield return null;
            if (enemy == null)
                yield break;
            if (enemy.ShieldHits > 0)
                enemy.ClearShield();
            // the bubble pops in a splash
            FxArt.Play("SplashWave", transform.position, FxArt.SizeOf(sr, 2f) * 1.3f, 20f, FxArt.BossOrder);
            StartCoroutine(RingPulse(transform.position, TideColor, 1.4f, 3.2f, 0.35f));
            float wait = Mathf.Max(1f, tuning.maelithShieldEvery) * (raging ? 0.6f : 1f);
            yield return new WaitForSeconds(wait);
        }
    }

    void UpdateShieldBubble()
    {
        bool shielded = enemy != null && enemy.ShieldHits > 0;
        if (!shielded)
        {
            if (shieldBubble != null)
            {
                shieldBubble.Stop();
                shieldBubble = null;
            }
            return;
        }

        // a wobbly water bubble around the boss (Fx/Bubble). it fades as it gets weaker
        if (shieldBubble == null)
        {
            float size = FxArt.SizeOf(sr, 2f) * 1.15f;
            shieldBubble = FxArt.Loop("Bubble", transform, size, 6f, (sr != null ? sr.sortingOrder : 5) + 5, Color.white);
            if (shieldBubble == null)
                return;
        }
        Color color = Color.white;
        color.a = enemy.ShieldHits > 1 ? 0.95f : 0.6f;
        shieldBubble.SetColor(color);
    }

    // rage: see-through water copies walk next to Maelith. they pop after one hit
    IEnumerator MirrorMistRoutine()
    {
        GameTuning tuning = GameTuning.Get;
        yield return new WaitForSeconds(0.8f);
        while (enemy != null && !enemy.IsDead)
        {
            copies.RemoveAll(copy => copy == null);
            int wanted = Mathf.Max(0, tuning.maelithCopies);
            if (copies.Count == 0 && wanted > 0 && minionPrefab != null && waveManager != null)
            {
                yield return StartCoroutine(Telegraph(TideColor, 0.5f));
                if (enemy == null || enemy.IsDead)
                    yield break;
                for (int i = 0; i < wanted; i++)
                {
                    // one on each side, then further out
                    float side = (i % 2 == 0 ? -1f : 1f) * (1.4f + (i / 2) * 1.2f);
                    Enemy copy = MakeCopy(side);
                    if (copy != null)
                        copies.Add(copy);
                }
                DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "MIRROR MIST!", TideColor);
            }
            yield return new WaitForSeconds(Mathf.Max(1f, tuning.maelithCopyEvery));
        }
    }

    Enemy MakeCopy(float side)
    {
        Vector3 position = transform.position;
        Enemy copy = waveManager.SpawnReinforcement(minionPrefab, enemy.element, bossArt != null ? bossArt : sr.sprite,
            1f, position, enemy.RouteIndex);
        if (copy == null)
            return null;
        copy.MakeDecoy(1);
        copy.transform.localScale = Vector3.one * BossScale * 0.85f;
        EnemyHitFeedback copyFeedback = copy.GetComponent<EnemyHitFeedback>();
        if (copyFeedback != null)
            copyFeedback.SetBasePose(copy.transform.localScale, copy.transform.localRotation);
        // keep it inside the field: if there's no room on this side it goes to the other
        float radius = 0.8f;
        float x = CameraFit.ClampToField((Vector2)position + Vector2.right * side, radius).x;
        if (Mathf.Abs(x - position.x) < Mathf.Abs(side) * 0.5f)
            x = CameraFit.ClampToField((Vector2)position - Vector2.right * side, radius).x;
        copy.ShiftLane(x - position.x);
        copy.gameObject.AddComponent<MaelithCopy>();
        FxArt.Play("SplashWave", copy.transform.position, 2.2f, 20f, FxArt.BossOrder);
        return copy;
    }

    // --- Pyrax ---

    IEnumerator EmberCallRoutine()
    {
        GameTuning tuning = GameTuning.Get;
        yield return new WaitForSeconds(reborn ? 2f : 4f);
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
                    // a fire column where the imp comes out (Fx/FlamePillar)
                    FxArt.Pillar(position + Vector3.down * 0.4f, 1.3f);
                    Enemy imp = waveManager.SpawnReinforcement(minionPrefab, minionElement, minionSprite, 1f,
                        position, enemy.RouteIndex);
                    if (imp != null)
                        summoned.Add(imp);
                    StartCoroutine(RingPulse(position, EmberColor, 0.2f, 1.4f, 0.3f));
                }
                DamagePopup.ShowText(transform.position + Vector3.up * 1.2f, "EMBER CALL!", EmberColor);
            }
            float wait = raging ? tuning.pyraxRageCallEvery : tuning.pyraxCallEvery;
            yield return new WaitForSeconds(Mathf.Max(1.5f, wait));
        }
    }

    // rage: beaten for the first time, Pyrax rises again from the ashes with some health
    void RiseFromTheAshes()
    {
        canRise = false;
        if (waveManager == null || minionPrefab == null || enemy == null)
            return;

        Vector3 position = transform.position;
        Enemy phoenix = waveManager.SpawnReinforcement(minionPrefab, enemy.element, null, 1f, position, enemy.RouteIndex);
        if (phoenix == null)
            return;
        // the boss health label is hidden with the old Pyrax's bar by now, so it can't be found by
        // name: hand it over, and the new Pyrax builds its own bar around it
        phoenix.bossHealthText = enemy.bossHealthText;
        phoenix.SetBoss(enemy.bossName);
        phoenix.transform.localScale = Vector3.one * 1.5f;

        ChapterBossAbility ability = phoenix.gameObject.AddComponent<ChapterBossAbility>();
        ability.Kind = BossKind.Pyrax;
        ability.waveManager = waveManager;
        ability.minionPrefab = minionPrefab;
        ability.minionSprite = minionSprite;
        ability.minionElement = minionElement;
        ability.reborn = true;

        BossPhases phases = phoenix.GetComponent<BossPhases>();
        if (phases != null)
            phases.StartEnraged();
        FxArt.Pillar(position + Vector3.down * 1.2f, 3.6f);
    }

    IEnumerator RebornIntro()
    {
        // the old Pyrax hid the boss health text when it went away, so turn it back on
        yield return null;
        if (enemy == null)
            yield break;
        if (enemy.bossHealthText != null)
            enemy.bossHealthText.enabled = true;
        enemy.SetHealth(Mathf.CeilToInt(enemy.MaxHealthTotal * Mathf.Clamp01(GameTuning.Get.pyraxRebornHealth)));
        StageToast.Show("PYRAX IS REBORN!", "It rose from the ashes. Finish it!", EmberColor, 2.2f, 0.62f);
        GameAudio.Play(Sfx.BossWave);
        CameraShake.Shake(true);
        FxArt.Play("FlameBurst", transform.position, FxArt.SizeOf(sr, 2f) * 1.6f, 16f, FxArt.BossOrder);
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
        // beaten in rage: Pyrax comes back once (not when the stage is closing)
        if (canRise && enemy != null && enemy.IsDead && !quitting && gameObject.scene.isLoaded)
            RiseFromTheAshes();

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null)
                Destroy(effects[i]);
        }
        effects.Clear();

        foreach (SpriteFlipbookFx aura in auras.Values)
        {
            if (aura != null)
                aura.Stop();
        }
        auras.Clear();

        foreach (ChargedBumper charge in charged)
        {
            if (charge != null)
                charge.Discharge();
        }
        charged.Clear();

        if (shieldBubble != null)
            shieldBubble.Stop();
    }
}
