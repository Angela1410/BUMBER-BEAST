using UnityEngine;

// plays a row of sprites like a flip book (skill and boss effects). it can play once and go away,
// or loop (auras), follow something around, and fade out at the end. FxArt makes most of them
public sealed class SpriteFlipbookFx : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer spriteRenderer;
    private float frameDuration;
    private float elapsed;
    private int frameIndex;
    private bool loop;
    private float endsAt = -1f;
    private float fadeSeconds = 0.2f;
    private float startedAt;
    private Transform follow;
    private bool following;
    private Vector3 followOffset;
    private Color baseColor = Color.white;

    public SpriteRenderer Renderer => spriteRenderer;

    // the old call (Thistle's vines): plays once, "radius" is half the size
    public static SpriteFlipbookFx Play(
        Sprite[] sprites,
        Vector3 position,
        float radius,
        float framesPerSecond)
    {
        return Create(sprites, position, radius * 2f, framesPerSecond, 20, Color.white, false);
    }

    // size = how big the picture is in the world (its longer side)
    public static SpriteFlipbookFx Create(Sprite[] sprites, Vector3 position, float size, float framesPerSecond,
        int sortingOrder, Color color, bool looping)
    {
        if (sprites == null || sprites.Length == 0 || sprites[0] == null)
            return null;

        GameObject effect = new GameObject("SpriteFlipbookFx");
        effect.transform.position = position;
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[0];
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;

        float spriteSize = Mathf.Max(
            renderer.sprite.bounds.size.x,
            renderer.sprite.bounds.size.y);
        float scale = Mathf.Max(0.01f, size) / Mathf.Max(0.01f, spriteSize);
        effect.transform.localScale = Vector3.one * scale;

        SpriteFlipbookFx flipbook = effect.AddComponent<SpriteFlipbookFx>();
        flipbook.Initialize(sprites, framesPerSecond, renderer, looping, color);
        return flipbook;
    }

    void Initialize(Sprite[] sprites, float framesPerSecond, SpriteRenderer renderer, bool looping, Color color)
    {
        frames = sprites;
        spriteRenderer = renderer;
        frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        loop = looping;
        baseColor = color;
        startedAt = Time.time;
    }

    // stays on top of "target" (plus offset). it goes away when the target does
    public SpriteFlipbookFx Follow(Transform target, Vector3 offset)
    {
        follow = target;
        following = target != null;
        followOffset = offset;
        if (target != null)
            transform.position = target.position + offset;
        return this;
    }

    // a looping effect ends after this many seconds, with a short fade
    public SpriteFlipbookFx StopAfter(float seconds, float fade = 0.2f)
    {
        endsAt = Time.time + Mathf.Max(0f, seconds);
        fadeSeconds = Mathf.Max(0.01f, fade);
        return this;
    }

    public void SetColor(Color color)
    {
        baseColor = color;
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }

    public void Stop()
    {
        if (this != null)
            Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        if (following)
        {
            if (follow == null)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = follow.position + followOffset;
        }

        if (endsAt >= 0f)
        {
            float left = endsAt - Time.time;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            Color color = baseColor;
            color.a *= Mathf.Clamp01(left / fadeSeconds) * Mathf.Clamp01((Time.time - startedAt) / 0.08f + 0.2f);
            spriteRenderer.color = color;
        }

        elapsed += Time.deltaTime;
        if (elapsed < frameDuration)
            return;

        int framesToAdvance = Mathf.FloorToInt(elapsed / frameDuration);
        elapsed -= framesToAdvance * frameDuration;
        frameIndex += framesToAdvance;

        if (frameIndex >= frames.Length)
        {
            if (!loop)
            {
                Destroy(gameObject);
                return;
            }
            frameIndex %= frames.Length;
        }

        if (frames[frameIndex] != null)
            spriteRenderer.sprite = frames[frameIndex];
    }
}
