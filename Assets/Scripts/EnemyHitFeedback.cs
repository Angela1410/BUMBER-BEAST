using System.Collections;
using UnityEngine;

public class EnemyHitFeedback : MonoBehaviour
{
    [Header("Hit Effect")]
    public Color flashColor = new Color(1f, 0.55f, 0.45f);
    public float flashDuration = 0.1f;

    [Header("Hit Punch")]
    public float punchScale = 1.15f;
    public float hitRecoilAngle = 9f;

    [Header("Walk Motion")]
    [Range(0f, 0.05f)] public float walkBobAmount = 0.025f;
    public float walkTiltAngle = 2.5f;
    public float walkCyclesPerSecond = 2.2f;

    [Header("Optional Spark")]
    public ParticleSystem hitEffect; // you can reuse BumperHitEffect

    private Coroutine running;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private bool isWalking;
    private float walkPhase;

    void Awake()
    {
        SetBasePose(transform.localScale, transform.localRotation);
    }

    void LateUpdate()
    {
        if (running != null)
            return;

        if (!isWalking)
        {
            Restore();
            return;
        }

        walkPhase += Time.deltaTime * walkCyclesPerSecond * Mathf.PI * 2f;
        float step = Mathf.Sin(walkPhase);
        float squash = Mathf.Abs(step) * walkBobAmount;
        transform.localScale = new Vector3(
            baseScale.x * (1f + step * walkBobAmount),
            baseScale.y * (1f - squash),
            baseScale.z);
        transform.localRotation = baseRotation *
            Quaternion.Euler(0f, 0f, step * walkTiltAngle);
    }

    public void SetWalking(bool walking)
    {
        isWalking = walking;
        if (!walking && running == null)
            Restore();
    }

    public void SetBasePose(Vector3 scale, Quaternion rotation)
    {
        baseScale = scale;
        baseRotation = rotation;
        if (running == null)
            Restore();
    }

    // Enemy.TakeDamage calls this, so bumps and skill hits show the same effect
    public void PlayHit(Vector2 point)
    {
        if (hitEffect != null)
        {
            ParticleSystem fx = Instantiate(hitEffect, point, Quaternion.identity);
            ParticleSystem.MainModule main = fx.main;
            main.startColor = flashColor;
            fx.Play();
            Destroy(fx.gameObject, 1f);
        }

        // if a punch animation is already running, put its scale back before starting again
        if (running != null)
        {
            StopCoroutine(running);
            Restore();
        }

        running = StartCoroutine(PunchRoutine(point.x < transform.position.x ? 1f : -1f));
    }

    IEnumerator PunchRoutine(float recoilDirection)
    {
        float duration = Mathf.Max(0.01f, flashDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float punch = Mathf.Sin(progress * Mathf.PI);
            transform.localScale = new Vector3(
                baseScale.x * Mathf.Lerp(1f, 1f / Mathf.Max(1f, punchScale), punch),
                baseScale.y * Mathf.Lerp(1f, punchScale, punch),
                baseScale.z);
            transform.localRotation = baseRotation *
                Quaternion.Euler(0f, 0f, recoilDirection * hitRecoilAngle * punch);
            yield return null;
        }

        Restore();
        running = null;
    }

    void Restore()
    {
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;
    }
}
