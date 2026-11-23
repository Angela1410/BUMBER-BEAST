using UnityEngine;

// shared element colors, names and icons (made by code once, no art files needed). the enemy
// badges and the wave plan card use them, so the matchups look the same everywhere
public static class ElementVisuals
{
    private const int IconSize = 96;
    private const int Supersample = 4;
    private static readonly Sprite[] icons = new Sprite[5];
    private const int GlyphSize = 128;
    private static readonly Sprite[] glyphSprites = new Sprite[5];
    private static Sprite swapArrowsSprite;

    public static Color GetColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return new Color(0.93f, 0.33f, 0.15f);
            case ElementType.Water: return new Color(0.2f, 0.55f, 0.95f);
            case ElementType.Earth: return new Color(0.66f, 0.45f, 0.24f);
            case ElementType.Electric: return new Color(0.98f, 0.8f, 0.15f);
            default: return new Color(0.36f, 0.74f, 0.25f);
        }
    }

    public static string GetName(ElementType element)
    {
        return element.ToString().ToUpperInvariant();
    }

    // hex color for TMP rich text
    public static string GetHex(ElementType element)
    {
        return ColorUtility.ToHtmlStringRGB(GetColor(element));
    }

    // the element that does double damage to this one
    public static ElementType GetCounter(ElementType defender)
    {
        foreach (ElementType attacker in (ElementType[])System.Enum.GetValues(typeof(ElementType)))
        {
            if (ElementChart.IsSuperEffective(attacker, defender))
                return attacker;
        }
        return defender;
    }

    // round badge: dark edge, element colored circle, white symbol
    public static Sprite GetIcon(ElementType element)
    {
        int index = (int)element;
        if (index < 0 || index >= icons.Length)
            index = (int)ElementType.Grass;
        if (icons[index] != null)
            return icons[index];

        Vector2[] glyph = GetGlyph(element);
        Color disc = GetColor(element);
        Color rim = new Color32(43, 26, 14, 255);
        Color glyphColor = new Color(1f, 0.98f, 0.92f);
        Color[] pixels = new Color[IconSize * IconSize];
        float half = IconSize * 0.5f;
        float glyphScale = IconSize * 0.3f;
        float sampleWeight = 1f / (Supersample * Supersample);

        for (int y = 0; y < IconSize; y++)
        {
            for (int x = 0; x < IconSize; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        float px = x + (sx + 0.5f) / Supersample - half;
                        float py = y + (sy + 0.5f) / Supersample - half;
                        float radius = Mathf.Sqrt(px * px + py * py) / half;
                        Color sample;
                        if (radius > 1f)
                            sample = Color.clear;
                        else if (radius > 0.84f)
                            sample = rim;
                        else if (InsidePolygon(glyph, px / glyphScale, py / glyphScale))
                            sample = glyphColor;
                        else
                            sample = disc;
                        // premultiply so the edges blend without dark borders
                        sum += new Color(sample.r * sample.a, sample.g * sample.a, sample.b * sample.a, sample.a) * sampleWeight;
                    }
                }
                pixels[y * IconSize + x] = sum.a > 0.0001f
                    ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a)
                    : Color.clear;
            }
        }

        Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = "Element Icon " + element;
        icons[index] = Sprite.Create(texture, new Rect(0f, 0f, IconSize, IconSize), new Vector2(0.5f, 0.5f), IconSize);
        icons[index].name = texture.name;
        return icons[index];
    }

    // just the element's symbol (white on clear) for big HUD buttons. tint it with Image.color
    public static Sprite GetGlyphSprite(ElementType element)
    {
        int index = (int)element;
        if (index < 0 || index >= glyphSprites.Length)
            index = (int)ElementType.Grass;
        if (glyphSprites[index] != null)
            return glyphSprites[index];

        Vector2[] glyph = GetGlyph(element);
        float glyphScale = GlyphSize * 0.38f;
        glyphSprites[index] = RasterizeWhite("Element Glyph " + element,
            (px, py) => InsidePolygon(glyph, px / glyphScale, py / glyphScale));
        return glyphSprites[index];
    }

    // two arrows chasing each other in a circle (the "swap" symbol), white on clear
    public static Sprite GetSwapArrowsSprite()
    {
        if (swapArrowsSprite != null)
            return swapArrowsSprite;

        float half = GlyphSize * 0.5f;
        swapArrowsSprite = RasterizeWhite("Swap Arrows", (px, py) =>
        {
            float u = px / half, v = py / half;
            float radius = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;
            bool inBand = radius > 0.5f && radius < 0.76f &&
                ((angle > 28f && angle < 150f) || (angle > 208f && angle < 330f));
            return inBand || InsideArrowHead(u, v, 150f) || InsideArrowHead(u, v, 330f);
        });
        return swapArrowsSprite;
    }

    // arrow head at the end of an arc, centered on the band
    private static bool InsideArrowHead(float u, float v, float endAngle)
    {
        float radians = endAngle * Mathf.Deg2Rad;
        Vector2 normal = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        Vector2 center = normal * 0.63f;
        Vector2 a = center + normal * 0.29f;
        Vector2 b = center - normal * 0.29f;
        Vector2 c = center + tangent * 0.34f;
        return InsideTriangle(new Vector2(u, v), a, b, c);
    }

    private static bool InsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(negative && positive);
    }

    // a smooth white shape: inside(x, y) is checked in pixels from the center (y goes up)
    private static Sprite RasterizeWhite(string name, System.Func<float, float, bool> inside)
    {
        Color[] pixels = new Color[GlyphSize * GlyphSize];
        float half = GlyphSize * 0.5f;
        float sampleWeight = 1f / (Supersample * Supersample);
        for (int y = 0; y < GlyphSize; y++)
        {
            for (int x = 0; x < GlyphSize; x++)
            {
                float coverage = 0f;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        float px = x + (sx + 0.5f) / Supersample - half;
                        float py = y + (sy + 0.5f) / Supersample - half;
                        if (inside(px, py))
                            coverage += sampleWeight;
                    }
                }
                pixels[y * GlyphSize + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        Texture2D texture = new Texture2D(GlyphSize, GlyphSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = name;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, GlyphSize, GlyphSize), new Vector2(0.5f, 0.5f), GlyphSize);
        sprite.name = name;
        return sprite;
    }

    // symbol outlines in a small space (about -1..1, y goes up)
    private static Vector2[] GetGlyph(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:
                return new[]
                {
                    new Vector2(0.05f, 1.25f), new Vector2(0.55f, 0.45f), new Vector2(0.75f, -0.1f),
                    new Vector2(0.65f, -0.65f), new Vector2(0.28f, -1.0f), new Vector2(-0.28f, -1.0f),
                    new Vector2(-0.65f, -0.65f), new Vector2(-0.75f, -0.15f), new Vector2(-0.55f, 0.3f),
                    new Vector2(-0.3f, 0.1f), new Vector2(-0.22f, 0.6f)
                };
            case ElementType.Water:
                return new[]
                {
                    new Vector2(0f, 1.25f), new Vector2(0.45f, 0.5f), new Vector2(0.72f, -0.05f),
                    new Vector2(0.68f, -0.55f), new Vector2(0.4f, -0.9f), new Vector2(0f, -1.02f),
                    new Vector2(-0.4f, -0.9f), new Vector2(-0.68f, -0.55f), new Vector2(-0.72f, -0.05f),
                    new Vector2(-0.45f, 0.5f)
                };
            case ElementType.Earth:
                return new[]
                {
                    new Vector2(-0.85f, -0.75f), new Vector2(0.85f, -0.75f), new Vector2(1.0f, 0.0f),
                    new Vector2(0.5f, 0.8f), new Vector2(-0.35f, 0.85f), new Vector2(-1.0f, 0.1f)
                };
            case ElementType.Electric:
                return new[]
                {
                    new Vector2(0.2f, 1.2f), new Vector2(-0.62f, -0.05f), new Vector2(-0.06f, -0.05f),
                    new Vector2(-0.26f, -1.2f), new Vector2(0.62f, 0.12f), new Vector2(0.06f, 0.12f)
                };
            default:
            {
                // leaf: a pointed oval, tilted
                const int steps = 12;
                Vector2[] leaf = new Vector2[steps * 2];
                float cos = Mathf.Cos(-0.7f), sin = Mathf.Sin(-0.7f);
                for (int i = 0; i < steps; i++)
                {
                    float t = Mathf.Lerp(-1.15f, 1.15f, i / (float)(steps - 1));
                    float width = 0.55f * (1f - (t * t) / (1.15f * 1.15f));
                    leaf[i] = Rotate(new Vector2(width, t), cos, sin);
                    leaf[steps * 2 - 1 - i] = Rotate(new Vector2(-width, t), cos, sin);
                }
                return leaf;
            }
        }
    }

    private static Vector2 Rotate(Vector2 point, float cos, float sin)
    {
        return new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
    }

    private static bool InsidePolygon(Vector2[] polygon, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
}
