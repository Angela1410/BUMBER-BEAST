using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class HudVisualUtility
{
    private static Sprite roundedSprite;
    private static Sprite circleSprite;
    private static Sprite ringSprite;
    private static Sprite glowSprite;
    private static Sprite smokeSprite;
    private static TMP_FontAsset hudFont;
    private static bool hudFontLoaded;

    public static Sprite RoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;

        const int size = 128;
        const int radius = 28;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 corner = new Vector2(
                    point.x < radius ? radius : size - radius,
                    point.y < radius ? radius : size - radius);
                float alpha = 1f;
                if ((point.x < radius || point.x > size - radius) &&
                    (point.y < radius || point.y > size - radius))
                    alpha = Mathf.Clamp01((radius + 0.75f - Vector2.Distance(point, corner)) / 1.5f);

                pixels[index] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = "HUD Rounded Sprite";
        roundedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return roundedSprite;
    }

    public static Sprite CircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f;
        Vector2 center = new Vector2(radius, radius);
        float feather = 1.25f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float distance = Vector2.Distance(point, center);
                float alpha = Mathf.Clamp01((radius - distance + feather) / (feather * 2f));
                pixels[index] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = "HUD Circle Sprite";
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    // A thin circular ring (outer edge at the sprite edge), for button rims.
    public static Sprite RingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 128;
        const float thickness = 0.13f;
        ringSprite = RadialSprite("HUD Ring Sprite", size, radius =>
        {
            float feather = 1.5f / (size * 0.5f);
            float outer = Mathf.Clamp01((1f - radius) / feather + 0.5f);
            float inner = Mathf.Clamp01((radius - (1f - thickness)) / feather + 0.5f);
            return Mathf.Min(outer, inner);
        });
        return ringSprite;
    }

    // Soft round glow that fades to nothing at the edge, for "ready" halos.
    public static Sprite GlowSprite()
    {
        if (glowSprite != null)
            return glowSprite;

        glowSprite = RadialSprite("HUD Glow Sprite", 128, radius =>
        {
            float t = Mathf.Clamp01(1f - radius);
            return t * t;
        });
        return glowSprite;
    }

    // A fuller soft blob than GlowSprite (smooth falloff instead of a quadratic one), for smoke and
    // haze that must still read a little way out from its centre.
    public static Sprite SmokeSprite()
    {
        if (smokeSprite != null)
            return smokeSprite;

        smokeSprite = RadialSprite("HUD Smoke Sprite", 128, radius =>
        {
            float t = Mathf.Clamp01(1f - radius);
            return t * t * (3f - 2f * t);
        });
        return smokeSprite;
    }

    private static Sprite RadialSprite(string name, int size, System.Func<float, float> alphaAtRadius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) / half;
                pixels[y * size + x] = new Color(1f, 1f, 1f, radius >= 1.02f ? 0f : alphaAtRadius(radius));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = name;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = name;
        return sprite;
    }

    public static Image AddImage(GameObject parent, string name, Color color, bool raycastTarget = false)
    {
        Transform existing = parent.transform.Find(name);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(name);
        imageObject.transform.SetParent(parent.transform, false);

        Image image = imageObject.GetComponent<Image>();
        if (image == null)
            image = imageObject.AddComponent<Image>();

        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycastTarget;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    public static TMP_Text AddTmpText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = parent.transform.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name);

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        Graphic existingGraphic = textObject.GetComponent<Graphic>();
        if (tmp == null && existingGraphic != null)
        {
            if (existingGraphic is Text)
                textObject.SetActive(false);

            Transform replacement = parent.transform.Find(name + " TMP");
            textObject = replacement != null ? replacement.gameObject : new GameObject(name + " TMP");
            tmp = textObject.GetComponent<TMP_Text>();
        }

        textObject.transform.SetParent(parent.transform, false);
        if (tmp == null)
            tmp = textObject.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        ApplyTextStyle(tmp);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return tmp;
    }

    public static void ApplyTextStyle(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        tmp.fontStyle = FontStyles.Bold;
        TMP_FontAsset font = HudFont();
        if (font != null)
            tmp.font = font;
        tmp.color = new Color32(255, 244, 214, 255);
        tmp.raycastTarget = false;

        // TMP's outline writes to its material, which only exists once the text
        // object has been active; on inactive text it throws a NullReferenceException.
        if (tmp.gameObject.activeInHierarchy)
        {
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(43, 26, 14, 255);
        }

        GameObject textObject = tmp.gameObject;
        Shadow shadow = textObject.GetComponent<Shadow>();
        if (shadow == null)
            shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(43, 26, 14, 180);
        shadow.effectDistance = new Vector2(2f, -3f);
    }

    private static TMP_FontAsset HudFont()
    {
        if (!hudFontLoaded)
        {
            hudFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
            hudFontLoaded = true;
        }

        return hudFont;
    }

    public static Outline AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
        return outline;
    }
}
