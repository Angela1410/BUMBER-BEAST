using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyRootVisual : MonoBehaviour
{
    public Color rootedTint = new Color32(106, 220, 102, 255);
    [Range(0f, 1f)]
    public float tintStrength = 0.55f;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        Refresh();
    }

    public void Refresh()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        bool rooted = enemy != null && enemy.IsRooted;
        spriteRenderer.color = rooted
            ? Color.Lerp(baseColor, rootedTint, tintStrength)
            : baseColor;
    }
}
