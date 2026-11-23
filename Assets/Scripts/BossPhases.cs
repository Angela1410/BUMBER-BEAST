using UnityEngine;

// turns a boss fight into stages:
// when it arrives: "THORNMAW APPEARS!" plus the element that beats it
// at 60% health: ARMORED, it blocks the next three hits (bounce into it to break the armor)
// at 30% health: ENRAGED, it charges the barrier 70% faster and pulses red
// Enemy.SetBoss adds this, and it works together with each boss's own ability script
[DisallowMultipleComponent]
public class BossPhases : MonoBehaviour
{
    // armor turns on at 60% health and rage at 30%
    private const float ArmorAt = 0.6f;
    private const float EnrageAt = 0.3f;
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

    void Update()
    {
        if (boss == null)
            boss = GetComponent<Enemy>();
        if (boss == null || boss.IsDead || !boss.isBoss)
            return;

        if (!introduced)
        {
            introduced = true;
            displayName = string.IsNullOrEmpty(boss.bossName) ? "THE BOSS" : boss.bossName.ToUpperInvariant();
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

        // ENRAGED: at 30% health it moves 70% faster toward the barrier
        if (!enraged && health <= EnrageAt)
        {
            enraged = true;
            boss.Haste(EnrageSpeed, 100000f);
            StageToast.Show("ENRAGED!", displayName + " is charging your barrier!", AlarmRed, 1.8f, 0.62f);
            GameAudio.Play(Sfx.BossWave);
            HitStop.Pause(0.08f);
            CameraShake.Shake(true);
        }

        // an enraged boss keeps flashing red rings so you can see the danger
        if (enraged && Time.time >= nextPulse)
        {
            nextPulse = Time.time + PulseInterval;
            BumperExplosionRing.Spawn(transform.position, 1.6f, AlarmRed, 45);
        }
    }
}
