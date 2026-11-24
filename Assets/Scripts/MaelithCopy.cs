using System.Collections;
using UnityEngine;

// one of Maelith's see-through water copies: it shimmers, and pops in a splash when it's hit
public class MaelithCopy : MonoBehaviour
{
    private Enemy enemy;
    private SpriteRenderer sr;
    private bool ready;

    IEnumerator Start()
    {
        enemy = GetComponent<Enemy>();
        sr = GetComponent<SpriteRenderer>();
        // wait a frame so Enemy.Start has set its colors, then make it see-through
        yield return null;
        if (enemy == null || sr == null)
            yield break;

        CircleCollider2D body = GetComponent<CircleCollider2D>();
        if (body != null && sr.sprite != null)
        {
            Bounds bounds = sr.sprite.bounds;
            body.radius = Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.5f;
            body.offset = bounds.center;
        }
        enemy.SetHitFlashBaseColor(new Color(0.72f, 0.9f, 1f, 0.55f));
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready || enemy == null || enemy.IsDead || sr == null)
            return;
        Color color = sr.color;
        color.a = 0.45f + 0.15f * Mathf.Sin(Time.time * 5f + transform.position.x);
        sr.color = color;
    }

    void OnDestroy()
    {
        if (enemy != null && enemy.IsDead && gameObject.scene.isLoaded)
        {
            FxArt.Play("SplashWave", transform.position, 2.4f, 20f, FxArt.BossOrder);
            DamagePopup.ShowText(transform.position + Vector3.up * 0.8f, "FAKE!", new Color(0.55f, 0.88f, 1f, 1f));
        }
    }
}
