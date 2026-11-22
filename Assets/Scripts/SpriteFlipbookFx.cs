using UnityEngine;

public sealed class SpriteFlipbookFx : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer spriteRenderer;
    private float frameDuration;
    private float elapsed;
    private int frameIndex;

    public static SpriteFlipbookFx Play(
        Sprite[] sprites,
        Vector3 position,
        float radius,
        float framesPerSecond)
    {
        if (sprites == null || sprites.Length == 0 || sprites[0] == null)
            return null;

        GameObject effect = new GameObject("SpriteFlipbookFx");
        effect.transform.position = position;
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[0];
        renderer.sortingOrder = 20;

        float spriteDiameter = Mathf.Max(
            renderer.sprite.bounds.size.x,
            renderer.sprite.bounds.size.y);
        float targetDiameter = Mathf.Max(0.01f, radius * 2f);
        float scale = targetDiameter / Mathf.Max(0.01f, spriteDiameter);
        effect.transform.localScale = Vector3.one * scale;

        SpriteFlipbookFx flipbook = effect.AddComponent<SpriteFlipbookFx>();
        flipbook.Initialize(sprites, framesPerSecond, renderer);
        return flipbook;
    }

    void Initialize(Sprite[] sprites, float framesPerSecond, SpriteRenderer renderer)
    {
        frames = sprites;
        spriteRenderer = renderer;
        frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < frameDuration)
            return;

        int framesToAdvance = Mathf.FloorToInt(elapsed / frameDuration);
        elapsed -= framesToAdvance * frameDuration;
        frameIndex += framesToAdvance;

        if (frameIndex >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        if (frames[frameIndex] != null)
            spriteRenderer.sprite = frames[frameIndex];
    }
}
