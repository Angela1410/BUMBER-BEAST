using System.Collections.Generic;
using UnityEngine;

public enum BackdropTheme
{
    Grass,
    Earth,
    Electric,
    Water,
    Fire
}

// makes painted-looking layered ground textures with code, so chapters without background art
// still get their own look. Earth versions 0-3 are the four Earth stages (Dustway, Rubble,
// Fault Line, Quarry)
public static class ProceduralBackdrop
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    private struct Palette
    {
        public Color32[] bands;
        public Color32 path;
        public Color32 crack;
        public Color32 glow;
        public float glowAmount;
        public float crackAmount;
        public Color32 pebble;
        public float topLight;
    }

    public static Sprite GetSprite(BackdropTheme theme, int variant, int width, int height, float pixelsPerUnit)
    {
        string key = theme + ":" + variant + ":" + width + "x" + height + "@" + pixelsPerUnit;
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Texture2D texture = Generate(theme, variant, width, height);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "Backdrop " + key;
        sprite.hideFlags = HideFlags.DontSave;
        Cache[key] = sprite;
        return sprite;
    }

    public static Texture2D Generate(BackdropTheme theme, int variant, int width, int height)
    {
        Palette palette = GetPalette(theme, variant);
        float seed = 13.7f + variant * 41.3f + (int)theme * 97.1f;
        Color[] pixels = new Color[width * height];
        int bandCount = palette.bands.Length;
        float bandsOnScreen = 15f + variant * 2f;
        float aspect = height / (float)Mathf.Max(1, width);

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            float pathCenter = 0.5f + Mathf.Sin(v * 5.2f + seed) * 0.11f + Mathf.Sin(v * 11f + seed * 0.3f) * 0.03f;

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                // a small, soft wobble so the layers look like tilted rock layers, not blobs
                float warp = (Fbm(u * 2.2f + seed, v * 1.6f * aspect * 0.5f + seed * 0.37f) - 0.5f) * 0.07f;

                float bandPosition = (v + warp + u * 0.035f) * bandsOnScreen;
                int bandIndex = Mathf.FloorToInt(bandPosition);
                float bandFraction = bandPosition - bandIndex;
                Color bandA = palette.bands[Mod(bandIndex, bandCount)];
                Color bandB = palette.bands[Mod(bandIndex + 1, bandCount)];
                Color color = Color.Lerp(bandA, bandB, SmoothStep(0.72f, 1f, bandFraction));
                // thin dark line between layers, plus fine horizontal lines inside each layer
                color *= 1f - 0.1f * SmoothStep(0.93f, 1f, bandFraction);
                float striation = Fbm(u * 3f + seed * 1.3f, (v + warp) * bandsOnScreen * 5f + 0.37f) - 0.5f;
                color *= 1f + striation * 0.16f;

                // worn dusty path where the enemies walk
                float pathDistance = Mathf.Abs(u - pathCenter);
                float pathMask = SmoothStep(0.2f, 0.11f, pathDistance);
                float pathRim = SmoothStep(0.21f, 0.17f, pathDistance) - pathMask;
                color = Color.Lerp(color, palette.path, pathMask * 0.6f);
                color *= 1f - Mathf.Clamp01(pathRim) * 0.1f;

                // canyon walls: darker toward the edges of the screen
                float edge = SmoothStep(0.22f, 0f, Mathf.Min(u, 1f - u));
                color *= 1f - edge * (0.3f + Fbm(u * 18f + seed, v * 2.5f + 0.5f) * 0.18f);

                // grain and soft light from the top
                float grain = (Mathf.PerlinNoise(u * 97.3f + seed, v * 173.1f + 0.21f) - 0.5f) * 0.06f;
                color += new Color(grain, grain, grain, 0f);
                color *= Mathf.Lerp(0.84f, 1f + palette.topLight, v);
                // darker corners
                float vignette = SmoothStep(0.95f, 0.35f, Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.55f)) * 1.25f);
                color *= Mathf.Lerp(0.74f, 1f, vignette);

                color.a = 1f;
                pixels[y * width + x] = color;
            }
        }

        DrawCracks(pixels, width, height, palette, seed);
        DrawPebbles(pixels, width, height, palette, seed);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.name = "Backdrop " + theme + " " + variant;
        texture.hideFlags = HideFlags.DontSave;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // jagged, branching cracks drawn as lines (noise ridges made a visible grid)
    private static void DrawCracks(Color[] pixels, int width, int height, Palette palette, float seed)
    {
        if (palette.crackAmount <= 0f && palette.glowAmount <= 0f)
            return;

        System.Random random = new System.Random(Mathf.RoundToInt(seed * 37f));
        float scale = width / 256f;
        int count = Mathf.RoundToInt(4f + palette.crackAmount * 5f);
        for (int index = 0; index < count; index++)
        {
            float x = (float)random.NextDouble() * width;
            float y = height * (0.08f + (float)random.NextDouble() * 0.84f);
            float angle = (float)(random.NextDouble() * System.Math.PI * 2.0);
            DrawCrack(pixels, width, height, palette, random, x, y, angle, 14 + random.Next(14), 1.6f * scale, scale, 1);
        }
    }

    private static void DrawCrack(
        Color[] pixels, int width, int height, Palette palette, System.Random random,
        float x, float y, float angle, int steps, float thickness, float scale, int branchesLeft)
    {
        for (int step = 0; step < steps; step++)
        {
            angle += ((float)random.NextDouble() - 0.5f) * 1.1f;
            float length = (4f + (float)random.NextDouble() * 7f) * scale;
            float nx = x + Mathf.Sin(angle + 1.5708f) * length;
            float ny = y + Mathf.Sin(angle) * length;
            float taper = Mathf.Lerp(1f, 0.35f, step / (float)steps);
            DrawSegment(pixels, width, height, palette, x, y, nx, ny, thickness * taper);

            if (branchesLeft > 0 && random.NextDouble() < 0.12)
            {
                DrawCrack(pixels, width, height, palette, random, nx, ny,
                    angle + (random.NextDouble() < 0.5 ? -1f : 1f) * (0.6f + (float)random.NextDouble() * 0.6f),
                    Mathf.Max(3, (steps - step) / 2), thickness * taper * 0.7f, scale, branchesLeft - 1);
            }

            x = nx;
            y = ny;
            if (x < -10f || y < -10f || x > width + 10f || y > height + 10f)
                return;
        }
    }

    private static void DrawSegment(
        Color[] pixels, int width, int height, Palette palette,
        float ax, float ay, float bx, float by, float thickness)
    {
        float glowRadius = palette.glowAmount > 0f ? thickness * 4f + 2f : 0f;
        float reach = Mathf.Max(thickness + 1.5f, glowRadius);
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - reach));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(ax, bx) + reach));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - reach));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + reach));
        float abx = bx - ax;
        float aby = by - ay;
        float lengthSquared = Mathf.Max(0.0001f, abx * abx + aby * aby);

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float t = Mathf.Clamp01(((px + 0.5f - ax) * abx + (py + 0.5f - ay) * aby) / lengthSquared);
                float dx = px + 0.5f - (ax + abx * t);
                float dy = py + 0.5f - (ay + aby * t);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                int pixelIndex = py * width + px;
                Color color = pixels[pixelIndex];

                if (glowRadius > 0f && distance < glowRadius)
                {
                    float glow = SmoothStep(glowRadius, thickness, distance) * palette.glowAmount * 0.35f;
                    color = Color.Lerp(color, palette.glow, glow);
                }

                float core = SmoothStep(thickness + 0.9f, Mathf.Max(0f, thickness - 0.4f), distance) * palette.crackAmount;
                if (core > 0f)
                {
                    Color crackColor = palette.glowAmount > 0f
                        ? Color.Lerp(palette.crack, palette.glow, 0.45f * palette.glowAmount)
                        : (Color)palette.crack;
                    color = Color.Lerp(color, crackColor, core);
                }

                color.a = 1f;
                pixels[pixelIndex] = color;
            }
        }
    }

    private static float Fbm(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 0.55f +
            Mathf.PerlinNoise(x * 2.03f + 17.13f, y * 2.03f + 3.71f) * 0.3f +
            Mathf.PerlinNoise(x * 4.11f + 41.37f, y * 4.11f + 9.23f) * 0.15f;
    }

    private static void DrawPebbles(Color[] pixels, int width, int height, Palette palette, float seed)
    {
        System.Random random = new System.Random(Mathf.RoundToInt(seed * 100f));
        int count = Mathf.RoundToInt(width * height / 2600f);
        float scale = width / 256f;

        for (int index = 0; index < count; index++)
        {
            float cx = (float)random.NextDouble() * width;
            float cy = (float)random.NextDouble() * height;
            bool rock = random.NextDouble() < 0.12;
            float radius = (rock ? 5f + (float)random.NextDouble() * 4f : 1.4f + (float)random.NextDouble() * 2.6f) * scale;
            float squash = 0.55f + (float)random.NextDouble() * 0.25f;
            float shade = 0.85f + (float)random.NextDouble() * 0.3f;
            // pebbles take the ground's color so they look like part of the scene instead of floating on it
            int centerIndex = Mathf.Clamp((int)cy, 0, height - 1) * width + Mathf.Clamp((int)cx, 0, width - 1);
            Color baseColor = Color.Lerp(pixels[centerIndex], palette.pebble, 0.45f) * shade;
            baseColor.a = 1f;

            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 2));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radius + 2));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 2));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radius + 2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + 0.5f - cx) / radius;
                    float dy = (y + 0.5f - cy) / (radius * squash);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    int pixelIndex = y * width + x;

                    // shadow under each pebble
                    float shadowDy = (y + 0.5f - (cy - radius * 0.35f)) / (radius * squash);
                    float shadowDistance = Mathf.Sqrt(dx * dx + shadowDy * shadowDy);
                    if (shadowDistance < 1.15f && distance >= 1f)
                        pixels[pixelIndex] *= 0.82f;

                    if (distance >= 1f)
                        continue;

                    // light on the top left, dark outline
                    float light = Mathf.Clamp01(0.55f - dx * 0.35f + dy * 0.45f);
                    Color pebble = baseColor * Mathf.Lerp(0.8f, 1.25f, light);
                    float rim = SmoothStep(0.78f, 1f, distance);
                    pebble = Color.Lerp(pebble, (Color)UiTheme.Ink, rim * 0.65f);
                    float alpha = SmoothStep(1f, 0.9f, distance);
                    Color result = Color.Lerp(pixels[pixelIndex], pebble, alpha);
                    result.a = 1f;
                    pixels[pixelIndex] = result;
                }
            }
        }
    }

    private static Palette GetPalette(BackdropTheme theme, int variant)
    {
        switch (theme)
        {
            case BackdropTheme.Earth:
                switch (Mathf.Abs(variant) % 4)
                {
                    case 1: // Rubble Weave: red sandstone
                        return new Palette
                        {
                            bands = new Color32[] { new Color32(182, 110, 78, 255), new Color32(160, 94, 66, 255), new Color32(198, 130, 92, 255), new Color32(140, 82, 58, 255), new Color32(172, 104, 74, 255) },
                            path = new Color32(216, 168, 126, 255),
                            crack = new Color32(84, 48, 34, 255),
                            crackAmount = 0.8f,
                            pebble = new Color32(150, 120, 104, 255),
                            topLight = 0.08f
                        };
                    case 2: // Fault Line: dark brown evening, amber cracks
                        return new Palette
                        {
                            bands = new Color32[] { new Color32(124, 88, 64, 255), new Color32(106, 74, 56, 255), new Color32(140, 100, 72, 255), new Color32(94, 66, 50, 255), new Color32(118, 82, 60, 255) },
                            path = new Color32(166, 126, 92, 255),
                            crack = new Color32(60, 36, 24, 255),
                            glow = new Color32(255, 168, 64, 255),
                            glowAmount = 0.9f,
                            crackAmount = 0.9f,
                            pebble = new Color32(132, 112, 98, 255),
                            topLight = 0.02f
                        };
                    case 3: // Terrathorn's Quarry: dark granite with glowing cracks
                        return new Palette
                        {
                            bands = new Color32[] { new Color32(102, 84, 72, 255), new Color32(86, 72, 62, 255), new Color32(116, 96, 80, 255), new Color32(76, 62, 54, 255), new Color32(96, 80, 68, 255) },
                            path = new Color32(146, 122, 100, 255),
                            crack = new Color32(44, 30, 24, 255),
                            glow = new Color32(255, 132, 52, 255),
                            glowAmount = 0.55f,
                            crackAmount = 1f,
                            pebble = new Color32(120, 112, 106, 255),
                            topLight = 0f
                        };
                    default: // Dustway Trail: warm yellow-brown daylight
                        return new Palette
                        {
                            bands = new Color32[] { new Color32(196, 150, 96, 255), new Color32(176, 128, 80, 255), new Color32(208, 168, 112, 255), new Color32(160, 112, 70, 255), new Color32(188, 140, 88, 255) },
                            path = new Color32(226, 194, 144, 255),
                            crack = new Color32(98, 64, 40, 255),
                            crackAmount = 0.6f,
                            pebble = new Color32(158, 136, 116, 255),
                            topLight = 0.12f
                        };
                }
            case BackdropTheme.Electric:
                // stormy gray highlands with glowing charged cracks
                return ForStage(new Palette
                {
                    bands = new Color32[] { new Color32(82, 80, 118, 255), new Color32(70, 68, 104, 255), new Color32(96, 92, 132, 255), new Color32(62, 60, 94, 255) },
                    path = new Color32(150, 146, 176, 255),
                    crack = new Color32(36, 32, 58, 255),
                    glow = new Color32(255, 230, 90, 255),
                    glowAmount = 0.8f,
                    crackAmount = 0.8f,
                    pebble = new Color32(128, 124, 150, 255),
                    topLight = 0.06f
                }, variant);
            case BackdropTheme.Water:
                return ForStage(new Palette
                {
                    bands = new Color32[] { new Color32(52, 104, 128, 255), new Color32(44, 90, 116, 255), new Color32(64, 120, 140, 255), new Color32(38, 80, 104, 255) },
                    path = new Color32(118, 164, 170, 255),
                    crack = new Color32(24, 52, 70, 255),
                    glow = new Color32(150, 230, 255, 255),
                    glowAmount = 0.4f,
                    crackAmount = 0.5f,
                    pebble = new Color32(120, 140, 150, 255),
                    topLight = 0.1f
                }, variant);
            case BackdropTheme.Fire:
                return ForStage(new Palette
                {
                    bands = new Color32[] { new Color32(120, 56, 40, 255), new Color32(98, 44, 34, 255), new Color32(140, 68, 44, 255), new Color32(84, 38, 30, 255) },
                    path = new Color32(168, 96, 64, 255),
                    crack = new Color32(40, 18, 14, 255),
                    glow = new Color32(255, 120, 40, 255),
                    glowAmount = 1f,
                    crackAmount = 0.9f,
                    pebble = new Color32(110, 90, 84, 255),
                    topLight = 0f
                }, variant);
            default:
                return new Palette
                {
                    bands = new Color32[] { new Color32(74, 122, 66, 255), new Color32(64, 108, 58, 255), new Color32(86, 136, 72, 255), new Color32(58, 98, 54, 255) },
                    path = new Color32(150, 140, 96, 255),
                    crack = new Color32(34, 56, 30, 255),
                    crackAmount = 0.4f,
                    pebble = new Color32(130, 136, 120, 255),
                    topLight = 0.1f
                };
        }
    }

    // the four stages of a generated chapter: daylight, bright, evening, and the darker boss arena
    // where the glowing cracks are strongest
    private static Palette ForStage(Palette palette, int variant)
    {
        int stage = Mathf.Abs(variant) % 4;
        float brightness = stage == 1 ? 1.08f : stage == 2 ? 0.9f : stage == 3 ? 0.76f : 1f;
        if (Mathf.Approximately(brightness, 1f))
            return palette;

        Color32[] bands = new Color32[palette.bands.Length];
        for (int index = 0; index < bands.Length; index++)
            bands[index] = Scale(palette.bands[index], brightness);
        palette.bands = bands;
        palette.path = Scale(palette.path, brightness);
        palette.pebble = Scale(palette.pebble, brightness);
        palette.glowAmount = Mathf.Clamp01(palette.glowAmount + (stage == 3 ? 0.35f : stage == 2 ? 0.15f : 0f));
        palette.crackAmount = Mathf.Clamp01(palette.crackAmount + (stage == 3 ? 0.2f : 0.1f));
        palette.topLight += stage == 1 ? 0.05f : -0.03f;
        return palette;
    }

    private static Color32 Scale(Color32 color, float factor)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255),
            color.a);
    }

    private static int Mod(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
