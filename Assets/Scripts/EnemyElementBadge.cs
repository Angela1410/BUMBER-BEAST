using UnityEngine;

// Small element icon above an enemy so the player can read the matchup at a glance.
// When the Beast currently on the pad deals double damage to this enemy, the badge pulses
// with a gold ring. Added automatically by Enemy; no setup needed.
[DisallowMultipleComponent]
public sealed class EnemyElementBadge : MonoBehaviour
{
    public float badgeWorldSize = 0.34f;
    public float gapAboveSprite = 0.04f;

    private static readonly Color RingColor = new Color(1f, 0.84f, 0.3f, 1f);

    private Enemy enemy;
    private SpriteRenderer enemyRenderer;
    private Transform badgeRoot;
    private SpriteRenderer iconRenderer;
    private SpriteRenderer ringRenderer;
    private ElementType shownElement;
    private bool built;
    private float pulseOffset;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        enemyRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        Build();
    }

    private void Build()
    {
        if (built || enemy == null)
            return;

        GameObject root = new GameObject("ElementBadge");
        badgeRoot = root.transform;
        badgeRoot.SetParent(transform, false);

        GameObject ringObject = new GameObject("AdvantageRing");
        ringObject.transform.SetParent(badgeRoot, false);
        ringObject.transform.localScale = Vector3.one * 1.42f;
        ringRenderer = ringObject.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = HudVisualUtility.CircleSprite();
        ringRenderer.color = RingColor;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(badgeRoot, false);
        iconRenderer = iconObject.AddComponent<SpriteRenderer>();

        if (enemyRenderer != null)
        {
            ringRenderer.sortingLayerID = enemyRenderer.sortingLayerID;
            iconRenderer.sortingLayerID = enemyRenderer.sortingLayerID;
            // Above the enemy's corruption tint and front motes (EnemyCorruption uses +1).
            ringRenderer.sortingOrder = enemyRenderer.sortingOrder + 3;
            iconRenderer.sortingOrder = enemyRenderer.sortingOrder + 4;
        }

        shownElement = enemy.element;
        iconRenderer.sprite = ElementVisuals.GetIcon(shownElement);
        pulseOffset = Random.value * 6.28f;
        built = true;
        PlaceAboveSprite();
    }

    // Keeps a constant world size whatever the enemy's scale (bosses are 1.5x).
    private void PlaceAboveSprite()
    {
        Vector3 lossy = transform.lossyScale;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        float top = enemyRenderer != null && enemyRenderer.sprite != null
            ? enemyRenderer.sprite.bounds.max.y
            : 0.3f / scaleY;

        badgeRoot.localScale = new Vector3(badgeWorldSize / scaleX, badgeWorldSize / scaleY, 1f);
        badgeRoot.localPosition = new Vector3(0f, top + (gapAboveSprite + badgeWorldSize * 0.5f) / scaleY, 0f);
    }

    void LateUpdate()
    {
        if (!built)
            return;

        if (enemy.element != shownElement)
        {
            shownElement = enemy.element;
            iconRenderer.sprite = ElementVisuals.GetIcon(shownElement);
        }

        BeastController beast = BeastController.Active;
        bool advantage = beast != null &&
            ElementChart.IsSuperEffective(BeastRoster.GetElement(beast.ActiveBeast), shownElement);

        if (ringRenderer.enabled != advantage)
            ringRenderer.enabled = advantage;

        if (advantage)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f + pulseOffset);
            Color ring = RingColor;
            ring.a = Mathf.Lerp(0.45f, 1f, wave);
            ringRenderer.color = ring;
            iconRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, wave);
        }
        else if (iconRenderer.transform.localScale.x != 1f)
        {
            iconRenderer.transform.localScale = Vector3.one;
        }
    }
}
