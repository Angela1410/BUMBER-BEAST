using UnityEngine;

// a bumper Voltrex's storm charged: it sparks, and a Beast that touches it gets zapped back to the pad
public class ChargedBumper : MonoBehaviour
{
    private float until;
    private SpriteFlipbookFx sparks;

    public static ChargedBumper Charge(Bumper bumper, float seconds)
    {
        if (bumper == null)
            return null;
        ChargedBumper charge = bumper.GetComponent<ChargedBumper>();
        if (charge == null)
            charge = bumper.gameObject.AddComponent<ChargedBumper>();
        charge.until = Time.time + seconds;
        if (charge.sparks == null)
        {
            float size = CameraFit.WorldRadius(bumper, 0.55f) * 2f * 1.7f;
            charge.sparks = FxArt.Loop("ElectricAura", bumper.transform, size, 14f, FxArt.BossOrder - 2, Color.white);
        }
        if (charge.sparks != null)
            charge.sparks.StopAfter(seconds);
        return charge;
    }

    public void Discharge()
    {
        if (sparks != null)
            sparks.Stop();
        Destroy(this);
    }

    void Update()
    {
        if (Time.time >= until)
            Discharge();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        BeastController beast = collision.gameObject.GetComponent<BeastController>();
        if (beast == null || (beast.State != BeastState.Flying && beast.State != BeastState.UsingSkill))
            return;

        FxArt.Play("ZapImpact", beast.transform.position, 1.8f, 20f, FxArt.BossOrder);
        DamagePopup.ShowText(beast.transform.position + Vector3.up * 0.8f, "ZAPPED!", new Color(1f, 0.9f, 0.3f, 1f));
        GameAudio.Play(Sfx.SkillElectric, 0.7f);
        CameraShake.Shake(false);
        beast.BeginReturn();
    }

    void OnDestroy()
    {
        if (sparks != null)
            sparks.Stop();
    }
}
