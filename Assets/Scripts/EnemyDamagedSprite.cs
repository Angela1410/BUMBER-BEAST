using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDamagedSprite : MonoBehaviour
{
    [Range(0f, 1f)]
    public float damagedAtHealthRatio = 0.5f;
    public Sprite damagedSprite;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Sprite healthySprite;
    private bool initialized;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized)
            return;

        if (enemy == null)
            enemy = GetComponent<Enemy>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        healthySprite = spriteRenderer.sprite;
        initialized = true;
        Refresh();
    }

    public void Refresh()
    {
        if (!initialized)
            Initialize();
        if (!initialized || enemy == null || spriteRenderer == null)
            return;

        Sprite target = damagedSprite != null &&
            enemy.HealthRatio <= damagedAtHealthRatio
            ? damagedSprite
            : healthySprite;
        if (target != null && spriteRenderer.sprite != target)
            spriteRenderer.sprite = target;
    }
}
