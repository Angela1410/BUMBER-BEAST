using UnityEngine;

public enum UiIcon
{
    Coin,
    Lock,
    Check,
    Crown,
    Flag,
    ArrowUp,
    Paw,
    Clock,
    Play,
    ChevronLeft,
    ChevronRight,
    Star,
    Plus,
    Gear,
    Person,
    Speaker,
    MusicNote,
    Shake,
    Hand,
    Close
}

// Small icon set for the map, shop and menus (the HUD font has no symbols). Generated once in
// code and cached. Every icon except the coin is a white mask: tint it with Image.color.
public static class UiIcons
{
    private const int Size = 128;
    private const int Supersample = 4;
    private static readonly Sprite[] Cache = new Sprite[System.Enum.GetValues(typeof(UiIcon)).Length];

    public static Sprite Get(UiIcon icon)
    {
        int index = (int)icon;
        if (Cache[index] != null)
            return Cache[index];

        Cache[index] = icon == UiIcon.Coin ? BuildCoin() : BuildMask(icon);
        return Cache[index];
    }

    private static Sprite BuildMask(UiIcon icon)
    {
        Color[] pixels = new Color[Size * Size];
        float weight = 1f / (Supersample * Supersample);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float coverage = 0f;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        float u = (x + (sx + 0.5f) / Supersample) / Size * 2f - 1f;
                        float v = (y + (sy + 0.5f) / Supersample) / Size * 2f - 1f;
                        if (Inside(icon, u, v))
                            coverage += weight;
                    }
                }
                pixels[y * Size + x] = new Color(1f, 1f, 1f, coverage);
            }
        }
        return ToSprite("UI Icon " + icon, pixels);
    }

    // Gold coin with a darker rim and an embossed star.
    private static Sprite BuildCoin()
    {
        Color rim = new Color32(186, 118, 22, 255);
        Color face = new Color32(255, 204, 66, 255);
        Color light = new Color32(255, 234, 140, 255);
        Color emboss = new Color32(214, 148, 30, 255);
        Color[] pixels = new Color[Size * Size];
        float weight = 1f / (Supersample * Supersample);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        float u = (x + (sx + 0.5f) / Supersample) / Size * 2f - 1f;
                        float v = (y + (sy + 0.5f) / Supersample) / Size * 2f - 1f;
                        float radius = Mathf.Sqrt(u * u + v * v);
                        Color sample;
                        if (radius > 0.96f)
                            sample = Color.clear;
                        else if (radius > 0.8f)
                            sample = rim;
                        else if (InsideStar(u, v - 0.02f, 0.5f, 0.22f))
                            sample = emboss;
                        else
                            sample = Color.Lerp(face, light, Mathf.Clamp01((v - u) * 0.5f + 0.2f));
                        sum += new Color(sample.r * sample.a, sample.g * sample.a, sample.b * sample.a, sample.a) * weight;
                    }
                }
                pixels[y * Size + x] = sum.a > 0.0001f
                    ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a)
                    : Color.clear;
            }
        }
        return ToSprite("UI Icon Coin", pixels);
    }

    private static bool Inside(UiIcon icon, float u, float v)
    {
        switch (icon)
        {
            case UiIcon.Lock:
            {
                float shackle = Mathf.Sqrt(u * u + (v - 0.12f) * (v - 0.12f));
                bool arc = shackle > 0.3f && shackle < 0.5f && v > 0.12f;
                bool legs = Mathf.Abs(Mathf.Abs(u) - 0.4f) < 0.1f && v > -0.1f && v <= 0.12f;
                bool body = RoundedBox(u, v + 0.38f, 0.66f, 0.46f, 0.14f);
                bool keyhole = Circle(u, v + 0.3f, 0.12f) || (Mathf.Abs(u) < 0.055f && v < -0.3f && v > -0.62f);
                return arc || legs || (body && !keyhole);
            }
            case UiIcon.Check:
                return Segment(u, v, -0.62f, 0.02f, -0.18f, -0.44f, 0.16f) || Segment(u, v, -0.18f, -0.44f, 0.66f, 0.5f, 0.16f);
            case UiIcon.Crown:
                return Polygon(u, v, -0.8f, -0.42f, 0.8f, -0.42f, 0.8f, 0.36f, 0.4f, 0.02f, 0f, 0.56f, -0.4f, 0.02f, -0.8f, 0.36f) ||
                    Circle(u + 0.8f, v - 0.42f, 0.12f) || Circle(u, v - 0.62f, 0.13f) || Circle(u - 0.8f, v - 0.42f, 0.12f) ||
                    RoundedBox(u, v + 0.6f, 0.8f, 0.12f, 0.05f);
            case UiIcon.Flag:
                return RoundedBox(u + 0.52f, v, 0.07f, 0.86f, 0.05f) ||
                    Polygon(u, v, -0.46f, 0.84f, 0.2f, 0.66f, 0.74f, 0.74f, 0.74f, 0.12f, 0.2f, 0.04f, -0.46f, 0.22f);
            case UiIcon.ArrowUp:
                return Polygon(u, v, 0f, 0.86f, 0.72f, 0.12f, 0.26f, 0.12f, 0.26f, -0.82f, -0.26f, -0.82f, -0.26f, 0.12f, -0.72f, 0.12f);
            case UiIcon.Paw:
                return Ellipse(u, v + 0.3f, 0.44f, 0.36f) || Circle(u + 0.56f, v - 0.14f, 0.17f) || Circle(u + 0.2f, v - 0.46f, 0.18f) ||
                    Circle(u - 0.2f, v - 0.46f, 0.18f) || Circle(u - 0.56f, v - 0.14f, 0.17f);
            case UiIcon.Clock:
            {
                float radius = Mathf.Sqrt(u * u + v * v);
                return (radius > 0.62f && radius < 0.84f) || Segment(u, v, 0f, 0f, 0f, 0.44f, 0.085f) ||
                    Segment(u, v, 0f, 0f, 0.32f, -0.14f, 0.085f) || radius < 0.12f;
            }
            case UiIcon.Play:
                return Polygon(u, v, -0.42f, 0.66f, 0.7f, 0f, -0.42f, -0.66f);
            case UiIcon.ChevronLeft:
                return Segment(u, v, 0.28f, 0.6f, -0.3f, 0f, 0.17f) || Segment(u, v, -0.3f, 0f, 0.28f, -0.6f, 0.17f);
            case UiIcon.ChevronRight:
                return Segment(u, v, -0.28f, 0.6f, 0.3f, 0f, 0.17f) || Segment(u, v, 0.3f, 0f, -0.28f, -0.6f, 0.17f);
            case UiIcon.Star:
                return InsideStar(u, v, 0.92f, 0.4f);
            case UiIcon.Plus:
                return RoundedBox(u, v, 0.66f, 0.15f, 0.08f) || RoundedBox(u, v, 0.15f, 0.66f, 0.08f);
            case UiIcon.Gear:
            {
                float radius = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float outer = Mathf.Cos(angle * 8f) > -0.1f ? 0.86f : 0.66f;
                return radius < outer && radius > 0.27f;
            }
            case UiIcon.Person:
                return Circle(u, v - 0.4f, 0.28f) || (Ellipse(u, v + 0.7f, 0.66f, 0.62f) && v < -0.06f && v > -0.84f);
            case UiIcon.Speaker:
            {
                bool body = RoundedBox(u + 0.5f, v, 0.16f, 0.24f, 0.05f) ||
                    Polygon(u, v, -0.36f, 0.24f, 0.04f, 0.6f, 0.04f, -0.6f, -0.36f, -0.24f);
                float radius = Mathf.Sqrt((u - 0.06f) * (u - 0.06f) + v * v);
                bool inArc = u > 0.14f && Mathf.Abs(v) < (u - 0.06f) * 1.25f;
                bool waves = inArc && ((radius > 0.3f && radius < 0.44f) || (radius > 0.58f && radius < 0.72f));
                return body || waves;
            }
            case UiIcon.MusicNote:
                return Ellipse(u + 0.22f, v + 0.5f, 0.3f, 0.22f) || RoundedBox(u - 0.02f, v - 0.1f, 0.07f, 0.62f, 0.04f) ||
                    Polygon(u, v, -0.05f, 0.72f, 0.56f, 0.44f, 0.56f, 0.2f, -0.05f, 0.42f);
            case UiIcon.Shake:
            {
                bool phone = RoundedBox(u, v, 0.3f, 0.58f, 0.1f) && !RoundedBox(u, v + 0.04f, 0.19f, 0.4f, 0.04f);
                bool lines = (Mathf.Abs(Mathf.Abs(u) - 0.56f) < 0.06f && Mathf.Abs(v) < 0.3f) ||
                    (Mathf.Abs(Mathf.Abs(u) - 0.78f) < 0.06f && Mathf.Abs(v) < 0.18f);
                return phone || lines;
            }
            case UiIcon.Close:
                return Segment(u, v, -0.52f, -0.52f, 0.52f, 0.52f, 0.15f) || Segment(u, v, -0.52f, 0.52f, 0.52f, -0.52f, 0.15f);
            case UiIcon.Hand:
            {
                // A hand pointing up: index finger, palm with curled-finger knuckles, thumb, cuff.
                bool finger = RoundedBox(u + 0.05f, v - 0.38f, 0.14f, 0.44f, 0.14f);
                bool palm = RoundedBox(u - 0.08f, v + 0.36f, 0.38f, 0.38f, 0.2f);
                bool knuckles = Circle(u - 0.24f, v - 0.02f, 0.15f) || Circle(u - 0.4f, v + 0.08f, 0.13f);
                bool thumb = Segment(u, v, -0.24f, -0.28f, -0.56f, 0.04f, 0.13f);
                bool cuff = RoundedBox(u - 0.08f, v + 0.86f, 0.3f, 0.1f, 0.04f);
                return finger || palm || knuckles || thumb || cuff;
            }
            default:
                return false;
        }
    }

    // ---------------------------------------------------------------- shape tests (y up)

    private static bool Circle(float u, float v, float radius)
    {
        return u * u + v * v <= radius * radius;
    }

    private static bool Ellipse(float u, float v, float rx, float ry)
    {
        float x = u / rx, y = v / ry;
        return x * x + y * y <= 1f;
    }

    private static bool RoundedBox(float u, float v, float halfWidth, float halfHeight, float radius)
    {
        float qx = Mathf.Abs(u) - halfWidth + radius;
        float qy = Mathf.Abs(v) - halfHeight + radius;
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius <= 0f;
    }

    private static bool Segment(float u, float v, float ax, float ay, float bx, float by, float radius)
    {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / (dx * dx + dy * dy));
        float px = ax + t * dx - u, py = ay + t * dy - v;
        return px * px + py * py <= radius * radius;
    }

    private static readonly float[] IconStar = StarPoints(0.92f, 0.4f);
    private static readonly float[] CoinStar = StarPoints(0.5f, 0.22f);

    private static bool InsideStar(float u, float v, float outer, float inner)
    {
        return Polygon(u, v, outer > 0.7f ? IconStar : CoinStar);
    }

    // Five-pointed star, first point straight up.
    private static float[] StarPoints(float outer, float inner)
    {
        float[] xy = new float[20];
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? outer : inner;
            xy[i * 2] = Mathf.Cos(angle) * radius;
            xy[i * 2 + 1] = Mathf.Sin(angle) * radius;
        }
        return xy;
    }

    private static bool Polygon(float u, float v, params float[] xy)
    {
        bool inside = false;
        int count = xy.Length / 2;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            float xi = xy[i * 2], yi = xy[i * 2 + 1];
            float xj = xy[j * 2], yj = xy[j * 2 + 1];
            if ((yi > v) != (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    private static Sprite ToSprite(string name, Color[] pixels)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = name;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = name;
        return sprite;
    }
}
