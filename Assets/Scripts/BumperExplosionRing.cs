using UnityEngine;

// the quick burst when a red bumper explodes: a flash that grows to the explosion size while it
// fades out. it deletes itself when it's done
public sealed class BumperExplosionRing : MonoBehaviour
{
    private const float Duration = 0.28f;

    private SpriteRenderer flash;
    private SpriteRenderer ring;
    private float radius;
    private Color color;
    private float elapsed;

    public static void Spawn(Vector3 position, float radius, Color color, int sortingOrder)
    {
        GameObject root = new GameObject("BumperExplosion");
        root.transform.position = new Vector3(position.x, position.y, position.z - 0.01f);
        BumperExplosionRing burst = root.AddComponent<BumperExplosionRing>();
        burst.radius = Mathf.Max(0.1f, radius);
        burst.color = color;

        Sprite circle = HudVisualUtility.CircleSprite();
        burst.flash = CreateCircle(root.transform, "Flash", circle, sortingOrder + 1);
        burst.ring = CreateCircle(root.transform, "Ring", circle, sortingOrder);
        burst.Apply(0f);
    }

    static SpriteRenderer CreateCircle(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Duration);
        Apply(t);
        if (t >= 1f)
            Destroy(gameObject);
    }

    void Apply(float t)
    {
        float eased = 1f - (1f - t) * (1f - t);
        float spriteSize = flash.sprite != null ? Mathf.Max(0.001f, flash.sprite.bounds.size.x) : 1f;

        // flash: bright center that shrinks a bit and fades fast
        float flashDiameter = radius * 1.1f * (1f - 0.3f * eased);
        flash.transform.localScale = Vector3.one * (flashDiameter / spriteSize);
        Color flashColor = Color.Lerp(color, Color.white, 0.6f);
        flashColor.a = 0.85f * (1f - t);
        flash.color = flashColor;

        // ring: grows out to the full size while it gets thinner and disappears
        float ringDiameter = Mathf.Lerp(radius * 0.4f, radius * 2f, eased);
        ring.transform.localScale = Vector3.one * (ringDiameter / spriteSize);
        Color ringColor = color;
        ringColor.a = 0.55f * (1f - eased);
        ring.color = ringColor;
    }
}
