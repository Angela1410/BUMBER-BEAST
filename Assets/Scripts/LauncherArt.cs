using UnityEngine;

// the painted slingshot for chapters 3-5 (stages 9-20). LaunchPadFeedback makes it on the LaunchPad
// from that chapter's art in Resources/Art/Electric, /Water or /Fire (or the shared Launcher.png).
// stages 1-8 keep their own launch pad and vine.
// the Beast rests at the LaunchPad's position (where it always comes back after a shot), so the art
// is placed with its rune platform under the Beast's feet. the pad's own transform, the launch point,
// aiming and the path math don't change. two energy bands go from the post crystals to the Beast and
// stretch as it's pulled back, and the crystals and the rune flash when you launch
public class LauncherArt : MonoBehaviour
{
    // one painted launcher: its art folder in Resources/Art, where the post crystals and the rune are
    // (fractions from the bottom left of that folder's Launcher.png), and its colors
    private sealed class Style
    {
        public string folder;
        public Vector2 leftCrystal;
        public Vector2 rightCrystal;
        public Vector2 platform;
        public Color bandCore;
        public Color bandGlow;
        public Color crystalGlow;
    }

    // stages 1-8: the teal crystal slingshot (1024 x 736)
    private static readonly Style Shared = new Style
    {
        folder = "",
        leftCrystal = new Vector2(0.1206f, 0.8783f),
        rightCrystal = new Vector2(0.8777f, 0.8783f),
        platform = new Vector2(0.5f, 0.2427f),
        bandCore = new Color(0.72f, 1f, 0.96f, 1f),
        bandGlow = new Color(0.22f, 0.95f, 0.84f, 1f),
        crystalGlow = new Color(0.36f, 1f, 0.9f, 1f)
    };

    // chapter 3 (stages 9-12): copper tesla coil posts with lightning crystals (1024 x 708)
    private static readonly Style Electric = new Style
    {
        folder = "Electric/",
        leftCrystal = new Vector2(0.1418f, 0.8948f),
        rightCrystal = new Vector2(0.8579f, 0.8940f),
        platform = new Vector2(0.4989f, 0.2350f),
        bandCore = new Color(1f, 0.97f, 0.7f, 1f),
        bandGlow = new Color(1f, 0.82f, 0.2f, 1f),
        crystalGlow = new Color(1f, 0.88f, 0.3f, 1f)
    };

    // chapter 4 (stages 13-16): coral and driftwood posts with pearls (1024 x 682)
    private static readonly Style Water = new Style
    {
        folder = "Water/",
        leftCrystal = new Vector2(0.1335f, 0.8665f),
        rightCrystal = new Vector2(0.8676f, 0.8702f),
        platform = new Vector2(0.4973f, 0.2526f),
        bandCore = new Color(0.75f, 0.97f, 1f, 1f),
        bandGlow = new Color(0.2f, 0.75f, 1f, 1f),
        crystalGlow = new Color(0.35f, 0.9f, 1f, 1f)
    };

    // chapter 5 (stages 17-20): dark lava rock posts with fire crystals (1024 x 743)
    private static readonly Style Fire = new Style
    {
        folder = "Fire/",
        leftCrystal = new Vector2(0.1539f, 0.8722f),
        rightCrystal = new Vector2(0.8503f, 0.8740f),
        platform = new Vector2(0.5008f, 0.2207f),
        bandCore = new Color(1f, 0.85f, 0.55f, 1f),
        bandGlow = new Color(1f, 0.45f, 0.1f, 1f),
        crystalGlow = new Color(1f, 0.55f, 0.18f, 1f)
    };

    private const float DefaultWorldWidth = 3.8f;
    private const float MinimumWidthFraction = 0.65f;
    // how far the post crystals can go over the bottom of the barrier wall
    private const float BarrierOverlapAllowance = 0.2f;
    private const float RetractSeconds = 0.18f;

    private Style style = Shared;
    private Transform root;
    private SpriteRenderer art;
    private SpriteRenderer leftGlow;
    private SpriteRenderer rightGlow;
    private SpriteRenderer platformGlow;
    private LineRenderer[] bands;
    private Vector3 restLossyScale;
    private Transform beast;
    private SpriteRenderer beastRenderer;
    private BeastController beastController;
    private BarrierVisuals barrier;
    private bool laidOut;
    private float laidOutBarrierBottom = float.NaN;
    private float pullRatio;
    private Vector3 pulledPosition;
    private int pullFrame = -10;
    private float launchTime = -10f;
    private Vector3 lastTip;

    public static LauncherArt TryCreate(GameObject pad, Material lineMaterial)
    {
        // use the chapter's own launcher if we have its art, otherwise the shared one.
        // chapters 1-2 (null folder) keep the scene's own launch pad and vine
        string folder = GameArt.StageArtFolder();
        if (folder == null)
            return null;
        Style style = folder == Electric.folder ? Electric
            : folder == Water.folder ? Water
            : folder == Fire.folder ? Fire
            : Shared;
        Sprite sprite = GameArt.Find(style.folder + "Launcher");
        if (sprite == null && style != Shared)
        {
            style = Shared;
            sprite = GameArt.Find("Launcher");
        }
        if (sprite == null)
            return null;

        LauncherArt launcher = pad.AddComponent<LauncherArt>();
        launcher.style = style;
        launcher.Build(sprite, lineMaterial);
        return launcher;
    }

    void Build(Sprite sprite, Material lineMaterial)
    {
        restLossyScale = transform.lossyScale;
        SpriteRenderer padRenderer = GetComponent<SpriteRenderer>();
        Material spriteMaterial = padRenderer != null ? padRenderer.sharedMaterial : null;
        if (padRenderer != null)
            padRenderer.enabled = false; // the old stump art, its Animator keeps running but you can't see it

        GameObject rootObject = new GameObject("LauncherArt");
        rootObject.transform.SetParent(transform, false);
        root = rootObject.transform;

        art = GameArt.AddLayer(root, "Launcher", sprite, spriteMaterial, padRenderer, 0);

        Material glow = GameArt.AdditiveGlow(spriteMaterial);
        Sprite soft = HudVisualUtility.GlowSprite();
        Vector2 size = sprite.bounds.size;
        leftGlow = GameArt.AddLayer(root, "LeftCrystalGlow", soft, glow, padRenderer, 1);
        leftGlow.transform.localPosition = AnchorToLocal(style.leftCrystal, size);
        rightGlow = GameArt.AddLayer(root, "RightCrystalGlow", soft, glow, padRenderer, 1);
        rightGlow.transform.localPosition = AnchorToLocal(style.rightCrystal, size);
        platformGlow = GameArt.AddLayer(root, "PlatformGlow", soft, glow, padRenderer, 1);
        platformGlow.transform.localPosition = AnchorToLocal(style.platform, size);

        Material bandMaterial = GameArt.AdditiveGlow(lineMaterial);
        bands = new LineRenderer[4];
        for (int i = 0; i < bands.Length; i++)
        {
            GameObject bandObject = new GameObject(i < 2 ? "EnergyBand" + i : "EnergyBandGlow" + (i - 2));
            bandObject.transform.SetParent(transform, false);
            LineRenderer band = bandObject.AddComponent<LineRenderer>();
            band.useWorldSpace = true;
            band.positionCount = 3;
            band.numCapVertices = 4;
            band.numCornerVertices = 4;
            band.textureMode = LineTextureMode.Stretch;
            if (bandMaterial != null)
                band.sharedMaterial = bandMaterial;
            band.enabled = false;
            bands[i] = band;
        }
    }

    static Vector3 AnchorToLocal(Vector2 anchor, Vector2 size)
    {
        return new Vector3((anchor.x - 0.5f) * size.x, (anchor.y - 0.5f) * size.y, 0f);
    }

    // the rune platform goes under the resting Beast's feet. the launcher gets smaller if its crystals
    // would reach too far over the barrier wall above it
    void Layout()
    {
        if (beast == null)
        {
            LaunchController launcher = Object.FindAnyObjectByType<LaunchController>();
            if (launcher != null)
            {
                beast = launcher.transform;
                beastRenderer = launcher.GetComponent<SpriteRenderer>();
                beastController = launcher.GetComponent<BeastController>();
            }
        }
        if (barrier == null)
            barrier = Object.FindAnyObjectByType<BarrierVisuals>();

        float feet = 0.5f;
        if (beastRenderer != null && beastRenderer.sprite != null)
            feet = beastRenderer.sprite.bounds.extents.y * Mathf.Abs(beast.lossyScale.y) * 0.85f;

        Vector2 spriteSize = art.sprite.bounds.size;
        float aspect = spriteSize.y / Mathf.Max(0.001f, spriteSize.x);
        float width = DefaultWorldWidth;
        float height = width * aspect;
        Vector3 platformWorld = new Vector3(transform.position.x, transform.position.y - feet, transform.position.z);

        float barrierBottom = barrier != null ? barrier.ArtBottomWorldY : float.NaN;
        if (!float.IsNaN(barrierBottom))
        {
            float limit = barrierBottom + BarrierOverlapAllowance;
            float top = platformWorld.y + (1f - style.platform.y) * height;
            if (top > limit)
            {
                height = Mathf.Max(DefaultWorldWidth * MinimumWidthFraction * aspect,
                    (limit - platformWorld.y) / (1f - style.platform.y));
                width = height / aspect;
            }
        }
        laidOutBarrierBottom = barrierBottom;

        float scale = width / Mathf.Max(0.001f, spriteSize.x);
        root.localScale = new Vector3(
            scale / Mathf.Max(0.0001f, Mathf.Abs(restLossyScale.x)),
            scale / Mathf.Max(0.0001f, Mathf.Abs(restLossyScale.y)), 1f);
        Vector3 centre = platformWorld + new Vector3((0.5f - style.platform.x) * width, (0.5f - style.platform.y) * height, 0f);
        Vector3 local = centre - transform.position;
        root.localPosition = new Vector3(
            local.x / Mathf.Max(0.0001f, restLossyScale.x),
            local.y / Mathf.Max(0.0001f, restLossyScale.y), 0f);

        // drawn behind the Beast: art, then glows, then the bands right under it
        int beastOrder = beastRenderer != null ? beastRenderer.sortingOrder : art.sortingOrder + 5;
        int layer = beastRenderer != null ? beastRenderer.sortingLayerID : art.sortingLayerID;
        SetSorting(art, layer, beastOrder - 4);
        SetSorting(leftGlow, layer, beastOrder - 3);
        SetSorting(rightGlow, layer, beastOrder - 3);
        SetSorting(platformGlow, layer, beastOrder - 3);
        for (int i = 0; i < bands.Length; i++)
        {
            bands[i].sortingLayerID = layer;
            bands[i].sortingOrder = beastOrder - (i < 2 ? 1 : 2);
        }

        laidOut = true;
    }

    static void SetSorting(SpriteRenderer renderer, int layer, int order)
    {
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
    }

    public void SetPull(float ratio, Vector3 beastPosition)
    {
        pullRatio = Mathf.Clamp01(ratio);
        pulledPosition = beastPosition;
        pullFrame = Time.frameCount;
    }

    public void ReturnToIdle()
    {
        pullRatio = 0f;
        pullFrame = -10;
    }

    public void PlayLaunch()
    {
        launchTime = Time.time;
        pullRatio = 0f;
        pullFrame = -10;
    }

    void LateUpdate()
    {
        if (art == null)
            return;
        if (!laidOut || (barrier != null && !SameValue(barrier.ArtBottomWorldY, laidOutBarrierBottom)))
            Layout();

        float now = Time.time;
        float sinceLaunch = now - launchTime;
        bool pulling = pullFrame >= Time.frameCount - 1;
        float pull = pulling ? pullRatio : 0f;
        float flare = sinceLaunch < 0.4f ? 1f - sinceLaunch / 0.4f : 0f;
        float pulse = 0.85f + 0.15f * Mathf.Sin(now * 2.6f);

        Vector2 size = art.sprite.bounds.size;
        float crystalSize = size.y * 0.2f * (1f + 0.55f * pull + 0.9f * flare);
        leftGlow.transform.localScale = new Vector3(crystalSize, crystalSize, 1f);
        rightGlow.transform.localScale = new Vector3(crystalSize, crystalSize, 1f);
        Color crystal = style.crystalGlow;
        crystal.a = Mathf.Clamp01(0.5f * pulse + 0.35f * pull + 0.5f * flare);
        leftGlow.color = crystal;
        rightGlow.color = crystal;

        platformGlow.transform.localScale = new Vector3(size.x * 0.5f * (1f + 0.25f * flare), size.y * 0.2f, 1f);
        Color platform = style.crystalGlow;
        platform.a = Mathf.Clamp01(0.32f + 0.12f * Mathf.Sin(now * 1.7f) + 0.25f * pull + 0.6f * flare);
        platformGlow.color = platform;

        UpdateBands(pulling, pull, sinceLaunch);
    }

    void UpdateBands(bool pulling, float pull, float sinceLaunch)
    {
        Vector3 left = leftGlow.transform.position;
        Vector3 right = rightGlow.transform.position;
        Vector3 rest = Vector3.Lerp(left, right, 0.5f) + Vector3.down * 0.15f;
        Vector3 tip;
        float strength = 1f;

        bool beastOnPad = beast != null && beastController != null &&
            (beastController.State == BeastState.AtPad ||
             (beastController.State == BeastState.Returning &&
              Vector2.Distance(beast.position, transform.position) < 0.5f));

        if (pulling)
        {
            tip = pulledPosition;
        }
        else if (sinceLaunch < RetractSeconds)
        {
            // snap: the bands whip back to the middle and fade
            float t = sinceLaunch / RetractSeconds;
            tip = Vector3.Lerp(lastTip, rest, t * t);
            strength = 1f - t;
        }
        else if (beastOnPad)
        {
            tip = beast.position;
        }
        else
        {
            for (int i = 0; i < bands.Length; i++)
                bands[i].enabled = false;
            return;
        }

        lastTip = tip;
        tip.z = left.z;
        float sag = 0.08f * (1f - pull);
        for (int i = 0; i < bands.Length; i++)
        {
            LineRenderer band = bands[i];
            Vector3 anchor = i % 2 == 0 ? left : right;
            Vector3 middle = Vector3.Lerp(anchor, tip, 0.5f) + Vector3.down * sag;
            band.SetPosition(0, anchor);
            band.SetPosition(1, middle);
            band.SetPosition(2, tip);

            bool core = i < 2;
            float width = core ? 0.05f + 0.035f * pull : 0.16f + 0.08f * pull;
            band.startWidth = width;
            band.endWidth = width * 0.8f;
            Color color = core ? style.bandCore : style.bandGlow;
            color.a = (core ? 0.95f : 0.3f + 0.3f * pull) * strength;
            band.startColor = color;
            band.endColor = color;
            band.enabled = true;
        }
    }

    static bool SameValue(float a, float b)
    {
        if (float.IsNaN(a) || float.IsNaN(b))
            return float.IsNaN(a) && float.IsNaN(b);
        return Mathf.Abs(a - b) < 0.01f;
    }
}
