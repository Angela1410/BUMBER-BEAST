using UnityEngine;

// each boss's own rage mode. the boss's ability script has it (ThornmawAbility, TerrathornAbility,
// ChapterBossAbility) and BossPhases starts it at 30% health
public interface IBossRage
{
    // true on the boss this script is driving (the shared enemy prefab has every script on it)
    bool HasRage { get; }
    string RageName { get; }
    // one short line under the name that tells you what to do about it
    string RageHint { get; }
    Color RageColor { get; }
    void BeginRage();
}

// turns a boss fight into stages:
// when it arrives: "THORNMAW APPEARS!" plus the element that beats it
// at 60% health: ARMORED, it blocks the next three hits (bounce into it to break the armor)
// at 30% health: its own RAGE MODE (Thornmaw regrows, Terrathorn rolls, Voltrex calls a storm,
// Maelith makes water copies, Pyrax becomes a phoenix), with an aura in its color
// Enemy.SetBoss adds this, and it works together with each boss's own ability script
[DisallowMultipleComponent]
public class BossPhases : MonoBehaviour
{
    // armor turns on at 60% health, rage at GameTuning's Boss Rage At (30%)
    private const float ArmorAt = 0.6f;
    private const int ArmorHits = 3;
    private const float EnrageSpeed = 1.7f;
    private const float PulseInterval = 0.75f;

    private static readonly Color AlarmRed = new Color(1f, 0.32f, 0.22f, 1f);
    private static readonly Color ArmorBlue = new Color(0.62f, 0.9f, 1f, 1f);

    private Enemy boss;
    private bool introduced;
    private bool armored;
    private bool enraged;
    private float nextPulse;
    private string displayName;
    private Color rageColor = AlarmRed;
    private SpriteFlipbookFx rageAura;
    private bool quietRage;

    public bool IsEnraged => enraged;

    public static BossPhases Attach(Enemy enemy)
    {
        if (enemy == null)
            return null;
        BossPhases phases = enemy.GetComponent<BossPhases>();
        if (phases == null)
            phases = enemy.gameObject.AddComponent<BossPhases>();
        phases.boss = enemy;
        return phases;
    }

    // a boss that comes back to life (Pyrax) is already angry: no intro, no armor, rage right away
    public void StartEnraged()
    {
        introduced = true;
        armored = true;
        quietRage = true;
    }

    void Update()
    {
        if (boss == null)
            boss = GetComponent<Enemy>();
        if (boss == null || boss.IsDead || !boss.isBoss)
            return;

        if (displayName == null)
            displayName = string.IsNullOrEmpty(boss.bossName) ? "THE BOSS" : boss.bossName.ToUpperInvariant();

        if (!introduced)
        {
            introduced = true;
            ElementType counter = ElementVisuals.GetCounter(boss.element);
            StageToast.Show(displayName + " APPEARS!",
                "Weak to <color=#" + ElementVisuals.GetHex(counter) + ">" + ElementVisuals.GetName(counter) + "</color>",
                AlarmRed, 2.4f, 0.62f);
        }

        // how much health the boss has left, from 0 to 1
        float health = boss.CurrentHealth / (float)boss.MaxHealthTotal;

        // ARMORED: at 60% health it blocks the next 3 hits
        if (!armored && health <= ArmorAt)
        {
            armored = true;
            boss.AddShield(ArmorHits);
            StageToast.Show("ARMORED!", displayName + " blocks the next " + ArmorHits + " hits", ArmorBlue, 1.8f, 0.62f);
            GameAudio.Play(Sfx.Block);
            BumperExplosionRing.Spawn(transform.position, 2.4f, ArmorBlue, 45);
            CameraShake.Shake(true);
        }

        // RAGE: at 30% health the boss starts its own rage mode
        if (!enraged && health <= GameTuning.Get.bossRageAt)
            BeginRage();

        // an angry boss keeps flashing rings in its rage color so you can see the danger
        if (enraged && Time.time >= nextPulse)
        {
            nextPulse = Time.time + PulseInterval;
            BumperExplosionRing.Spawn(transform.position, 1.6f, rageColor, 45);
        }
    }

    void BeginRage()
    {
        enraged = true;
        IBossRage rage = FindRage();
        rageColor = rage != null ? rage.RageColor : AlarmRed;
        // (a boss that came back to life already said why it's angry)
        if (!quietRage)
            StageToast.Show(rage != null ? rage.RageName : "ENRAGED!",
                rage != null ? rage.RageHint : displayName + " is charging your barrier!",
                rageColor, 2.2f, 0.62f);
        GameAudio.Play(Sfx.BossWave);
        HitStop.Pause(0.08f);
        CameraShake.Shake(true);

        // a flickering aura in the rage color behind the boss (Fx/RageAura)
        SpriteRenderer body = GetComponent<SpriteRenderer>();
        Color aura = rageColor;
        aura.a = 0.75f;
        rageAura = FxArt.Loop("RageAura", transform, FxArt.SizeOf(body, 2f) * 1.45f, 10f,
            body != null ? body.sortingOrder - 1 : 4, aura);

        if (rage != null)
            rage.BeginRage();
        else
            boss.Haste(EnrageSpeed, 100000f);
    }

    IBossRage FindRage()
    {
        foreach (IBossRage rage in GetComponents<IBossRage>())
        {
            if (rage != null && rage.HasRage)
                return rage;
        }
        return null;
    }

    void OnDestroy()
    {
        if (rageAura != null)
            rageAura.Stop();
    }
}
