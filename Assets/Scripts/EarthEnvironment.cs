using UnityEngine;

// the stage background: a generated one (or a painted one) plus floating particles.
// made for the Earth chapter and reused by chapters 3-5 with a Theme (storm sparks, bubbles,
// embers). the generated part lives on a hidden child that isn't saved, so the scene file never
// stores generated textures and nothing gets marked as changed while editing
[ExecuteAlways]
public class EarthEnvironment : MonoBehaviour
{
    [Tooltip("Which generated landscape to paint when no background is assigned.")]
    public BackdropTheme theme = BackdropTheme.Earth;
    [Tooltip("Earth: 0 Dustway Trail, 1 Rubble Weave, 2 Fault Line, 3 Terrathorn's Quarry. Other themes: stage in chapter.")]
    public int variant;
    [Tooltip("Painted stage background. When set it replaces the generated canyon and is scaled " +
        "uniformly to cover the main camera's view (never stretched).")]
    public Sprite background;
    [Tooltip("Size of the generated canyon when no painted background is assigned.")]
    public Vector2 worldSize = new Vector2(16f, 28f);
    public int sortingOrder = -10;
    public Color tint = Color.white;

    // ChapterBuilder writes this when it gives stages 9-20 their painted background (0 = never),
    // so the art is set once and changes we make in the Inspector later are kept
    [HideInInspector] public int paintedBackdropVersion;

    [Header("Ambient Dust (Play Mode)")]
    public int dustCount = 14;
    public Color dustColor = new Color(1f, 0.86f, 0.62f, 0.35f);

    private const string GeneratedName = "EarthBackdrop (generated)";
    private SpriteRenderer generatedRenderer;
    private Transform[] dustMotes;
    private Vector3[] dustSeeds;
    private int builtVariant = int.MinValue;
    private BackdropTheme builtTheme;
    private Sprite builtBackground;
    private Camera fittedCamera;
    private float fittedAspect;
    private float fittedOrthographicSize;

    void OnEnable()
    {
        Build();
        if (Application.isPlaying)
            CreateDust();
    }

    void OnDisable()
    {
        if (generatedRenderer != null)
            generatedRenderer.enabled = false;
    }

    void OnValidate()
    {
        builtVariant = int.MinValue;
    }

    void Update()
    {
        if (builtVariant != variant || builtTheme != theme || builtBackground != background ||
            generatedRenderer == null || CameraViewChanged())
            Build();

        if (dustMotes == null)
            return;

        float time = Time.time;
        float halfWidth = worldSize.x * 0.35f;
        float halfHeight = worldSize.y * 0.32f;
        for (int index = 0; index < dustMotes.Length; index++)
        {
            Transform mote = dustMotes[index];
            if (mote == null)
                continue;

            Vector3 seed = dustSeeds[index];
            float y = Mathf.Repeat(seed.y + time * (0.12f + seed.z * 0.1f), halfHeight * 2f) - halfHeight;
            float x = seed.x + Mathf.Sin(time * (0.4f + seed.z) + index) * 0.6f;
            mote.localPosition = new Vector3(Mathf.Clamp(x, -halfWidth, halfWidth), y, -0.01f);
        }
    }

    void Build()
    {
        if (generatedRenderer == null)
        {
            Transform existing = transform.Find(GeneratedName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(GeneratedName);
            child.hideFlags = HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy;
            child.transform.SetParent(transform, false);
            generatedRenderer = child.GetComponent<SpriteRenderer>();
            if (generatedRenderer == null)
                generatedRenderer = child.AddComponent<SpriteRenderer>();
            generatedRenderer.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        }

        Sprite sprite = background != null
            ? background
            : ProceduralBackdrop.GetSprite(theme, variant, 256, 448, 256f / Mathf.Max(1f, worldSize.x));
        generatedRenderer.sprite = sprite;
        generatedRenderer.color = tint;
        generatedRenderer.sortingOrder = sortingOrder;
        generatedRenderer.enabled = true;

        // keep the background in world units no matter what this object's own scale is
        Vector3 parentScale = transform.lossyScale;
        Vector2 spriteSize = sprite.bounds.size;
        Vector2 targetSize = worldSize;
        Vector3 childPosition = Vector3.zero;
        fittedCamera = null;

        if (background != null)
        {
            // painted art keeps its shape: scale it evenly until it covers the camera view,
            // centered on the camera so the safe area offset never shows an edge
            Camera camera = Camera.main;
            float cover;
            if (camera != null && camera.orthographic)
            {
                float viewHeight = camera.orthographicSize * 2f;
                float viewWidth = viewHeight * camera.aspect;
                cover = Mathf.Max(viewWidth / Mathf.Max(0.001f, spriteSize.x),
                    viewHeight / Mathf.Max(0.001f, spriteSize.y));
                Vector3 centre = transform.InverseTransformPoint(camera.transform.position);
                childPosition = new Vector3(centre.x, centre.y, 0f);
                fittedCamera = camera;
                fittedAspect = camera.aspect;
                fittedOrthographicSize = camera.orthographicSize;
            }
            else
            {
                cover = Mathf.Max(worldSize.x / Mathf.Max(0.001f, spriteSize.x),
                    worldSize.y / Mathf.Max(0.001f, spriteSize.y));
            }
            targetSize = spriteSize * cover;
        }

        generatedRenderer.transform.localPosition = childPosition;
        generatedRenderer.transform.localScale = new Vector3(
            targetSize.x / Mathf.Max(0.001f, spriteSize.x * Mathf.Abs(parentScale.x)),
            targetSize.y / Mathf.Max(0.001f, spriteSize.y * Mathf.Abs(parentScale.y)),
            1f);
        builtVariant = variant;
        builtTheme = theme;
        builtBackground = background;
    }

    // a painted background was fitted to a camera view that changed since then
    // (resolution, rotation or an Inspector change), so fit it again
    bool CameraViewChanged()
    {
        if (fittedCamera == null)
        {
            // not fitted yet: only worth rebuilding once there's an orthographic main camera
            Camera camera = background != null ? Camera.main : null;
            return camera != null && camera.orthographic;
        }
        return !Mathf.Approximately(fittedCamera.aspect, fittedAspect) ||
            !Mathf.Approximately(fittedCamera.orthographicSize, fittedOrthographicSize);
    }

    void CreateDust()
    {
        if (dustMotes != null || dustCount <= 0)
            return;

        Sprite dot = HudVisualUtility.CircleSprite();
        dustMotes = new Transform[dustCount];
        dustSeeds = new Vector3[dustCount];
        System.Random random = new System.Random(1234 + variant * 17);
        Vector3 parentScale = transform.lossyScale;

        for (int index = 0; index < dustCount; index++)
        {
            GameObject mote = new GameObject("Dust" + index);
            mote.hideFlags = HideFlags.DontSave;
            mote.transform.SetParent(generatedRenderer.transform.parent, false);
            float size = 0.05f + (float)random.NextDouble() * 0.07f;
            mote.transform.localScale = new Vector3(
                size / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                size / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                1f);

            SpriteRenderer renderer = mote.AddComponent<SpriteRenderer>();
            renderer.sprite = dot;
            Color color = dustColor;
            color.a *= 0.5f + (float)random.NextDouble() * 0.5f;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder + 1;

            dustMotes[index] = mote.transform;
            dustSeeds[index] = new Vector3(
                ((float)random.NextDouble() * 2f - 1f) * worldSize.x * 0.32f,
                (float)random.NextDouble() * worldSize.y,
                (float)random.NextDouble());
        }
    }
}
