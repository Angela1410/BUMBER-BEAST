using System;
using System.Collections.Generic;

// draws the PLACEHOLDER art (enemies, bosses and Beasts that don't have painted sprites yet).
// every shape is a math distance function on a -1..1 canvas (y goes up), drawn with an outline
// and smooth edges, so one sample per pixel already looks clean.
// it doesn't use any UnityEngine types, so the same drawings can be made outside Unity to check them.
// PlaceholderArtGenerator turns them into PNG sprites in Assets/Resources
public static class PlaceholderPainter
{
    // --- colors ---

    public struct Rgba
    {
        public float r, g, b, a;

        public Rgba(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Rgba Hex(uint rgb)
        {
            return Hex(rgb, 1f);
        }

        public static Rgba Hex(uint rgb, float alpha)
        {
            return new Rgba(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
        }

        public Rgba Mix(Rgba other, float t)
        {
            t = Clamp01(t);
            return new Rgba(r + (other.r - r) * t, g + (other.g - g) * t, b + (other.b - b) * t, a + (other.a - a) * t);
        }

        public Rgba Times(float k)
        {
            return new Rgba(Clamp01(r * k), Clamp01(g * k), Clamp01(b * k), a);
        }

        public Rgba Alpha(float alpha)
        {
            return new Rgba(r, g, b, alpha);
        }
    }

    public static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);
    public static readonly Rgba DefaultInk = Rgba.Hex(0x231A33);

    public delegate float Shape(float x, float y);
    public delegate Rgba Paint(float x, float y);

    private struct Layer
    {
        public Shape shape;
        public Paint paint;
        public float outline;
        public Rgba ink;
    }

    // --- sketch ---

    public sealed class Sketch
    {
        private readonly List<Layer> layers = new List<Layer>();
        public Rgba Ink = DefaultInk;
        public float Outline = 0.035f;

        public Sketch Add(Shape shape, Rgba color)
        {
            return Add(shape, Solid(color), Outline);
        }

        public Sketch Add(Shape shape, Paint paint)
        {
            return Add(shape, paint, Outline);
        }

        public Sketch Add(Shape shape, Rgba color, float outline)
        {
            return Add(shape, Solid(color), outline);
        }

        public Sketch Add(Shape shape, Paint paint, float outline)
        {
            layers.Add(new Layer { shape = shape, paint = paint, outline = outline, ink = Ink });
            return this;
        }

        // flat detail with no outline (stripes, shine, glow, pupils)
        public Sketch Detail(Shape shape, Rgba color)
        {
            return Add(shape, Solid(color), 0f);
        }

        public Sketch Detail(Shape shape, Paint paint)
        {
            return Add(shape, paint, 0f);
        }

        // RGBA32 bytes, bottom row first (the way Unity's Texture2D wants them)
        public byte[] Render(int size)
        {
            byte[] data = new byte[size * size * 4];
            float pixel = 2f / size;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float pr = 0f, pg = 0f, pb = 0f, pa = 0f; // premultiplied
                    for (int i = 0; i < layers.Count; i++)
                    {
                        Layer layer = layers[i];
                        float distance = layer.shape(u, v);
                        if (distance > layer.outline + pixel)
                            continue;
                        if (layer.outline > 0f)
                            Over(ref pr, ref pg, ref pb, ref pa, layer.ink, Cover(distance - layer.outline, pixel));
                        float fill = Cover(distance, pixel);
                        if (fill > 0f)
                            Over(ref pr, ref pg, ref pb, ref pa, layer.paint(u, v), fill);
                    }

                    int o = (y * size + x) * 4;
                    if (pa > 0.0001f)
                    {
                        data[o] = ToByte(pr / pa);
                        data[o + 1] = ToByte(pg / pa);
                        data[o + 2] = ToByte(pb / pa);
                    }
                    data[o + 3] = ToByte(pa);
                }
            }
            return data;
        }

        private static float Cover(float distance, float pixel)
        {
            return Clamp01(0.5f - distance / pixel);
        }

        private static void Over(ref float r, ref float g, ref float b, ref float a, Rgba color, float coverage)
        {
            float alpha = color.a * coverage;
            if (alpha <= 0f)
                return;
            r = color.r * alpha + r * (1f - alpha);
            g = color.g * alpha + g * (1f - alpha);
            b = color.b * alpha + b * (1f - alpha);
            a = alpha + a * (1f - alpha);
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Round(Clamp01(value) * 255f);
        }
    }

    // --- shapes ---

    public static Shape Circle(float cx, float cy, float r)
    {
        return (x, y) => Len(x - cx, y - cy) - r;
    }

    public static Shape Ellipse(float cx, float cy, float rx, float ry)
    {
        return (x, y) =>
        {
            float px = x - cx, py = y - cy;
            float k0 = Len(px / rx, py / ry);
            float k1 = Len(px / (rx * rx), py / (ry * ry));
            return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -Math.Min(rx, ry);
        };
    }

    public static Shape Capsule(float ax, float ay, float bx, float by, float r)
    {
        return (x, y) => SegmentDistance(x, y, ax, ay, bx, by, out _) - r;
    }

    // a stroke that goes from radius ra at A to rb at B (horns, claws, flames)
    public static Shape Taper(float ax, float ay, float bx, float by, float ra, float rb)
    {
        return (x, y) =>
        {
            float distance = SegmentDistance(x, y, ax, ay, bx, by, out float t);
            return distance - (ra + (rb - ra) * t);
        };
    }

    public static Shape Box(float cx, float cy, float halfWidth, float halfHeight, float round)
    {
        return (x, y) =>
        {
            float qx = Math.Abs(x - cx) - halfWidth + round;
            float qy = Math.Abs(y - cy) - halfHeight + round;
            float outside = Len(Math.Max(qx, 0f), Math.Max(qy, 0f));
            return outside + Math.Min(Math.Max(qx, qy), 0f) - round;
        };
    }

    // closed polygon from x0, y0, x1, y1, ...
    public static Shape Polygon(params float[] xy)
    {
        int count = xy.Length / 2;
        float[] px = new float[count];
        float[] py = new float[count];
        for (int i = 0; i < count; i++)
        {
            px[i] = xy[i * 2];
            py[i] = xy[i * 2 + 1];
        }
        return (x, y) => PolygonDistance(px, py, x, y);
    }

    public static Shape Star(float cx, float cy, float inner, float outer, int points, float rotationDegrees)
    {
        float[] xy = new float[points * 4];
        for (int i = 0; i < points * 2; i++)
        {
            double angle = (rotationDegrees + 90.0) * Math.PI / 180.0 + i * Math.PI / points;
            float radius = i % 2 == 0 ? outer : inner;
            xy[i * 2] = cx + (float)Math.Cos(angle) * radius;
            xy[i * 2 + 1] = cy + (float)Math.Sin(angle) * radius;
        }
        return Polygon(xy);
    }

    public static Shape Union(params Shape[] shapes)
    {
        return (x, y) =>
        {
            float best = float.MaxValue;
            for (int i = 0; i < shapes.Length; i++)
                best = Math.Min(best, shapes[i](x, y));
            return best;
        };
    }

    // smooth union: k is how round the join is
    public static Shape Blend(Shape a, Shape b, float k)
    {
        return (x, y) =>
        {
            float da = a(x, y), db = b(x, y);
            float h = Clamp01(0.5f + 0.5f * (db - da) / k);
            return db + (da - db) * h - k * h * (1f - h);
        };
    }

    public static Shape Cut(Shape a, Shape b)
    {
        return (x, y) => Math.Max(a(x, y), -b(x, y));
    }

    public static Shape Clip(Shape a, Shape b)
    {
        return (x, y) => Math.Max(a(x, y), b(x, y));
    }

    // copies a shape drawn on the right side (x > 0) to both sides
    public static Shape Mirror(Shape shape)
    {
        return (x, y) => shape(Math.Abs(x), y);
    }

    public static Shape Grow(Shape shape, float amount)
    {
        return (x, y) => shape(x, y) - amount;
    }

    public static Shape Rotate(Shape shape, float cx, float cy, float degrees)
    {
        double radians = -degrees * Math.PI / 180.0;
        float cos = (float)Math.Cos(radians), sin = (float)Math.Sin(radians);
        return (x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            return shape(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
        };
    }

    // everything below the line y = level (for half-moon mouths and bellies)
    public static Shape Below(float level)
    {
        return (x, y) => y - level;
    }

    public static Shape Above(float level)
    {
        return (x, y) => level - y;
    }

    // a thick line through points (lightning zig-zags, cracks, smiles)
    public static Shape Path(float radius, params float[] xy)
    {
        int count = xy.Length / 2;
        return (x, y) =>
        {
            float best = float.MaxValue;
            for (int i = 0; i < count - 1; i++)
            {
                float distance = SegmentDistance(x, y, xy[i * 2], xy[i * 2 + 1], xy[i * 2 + 2], xy[i * 2 + 3], out _);
                best = Math.Min(best, distance);
            }
            return best - radius;
        };
    }

    // --- paints ---

    public static Paint Solid(Rgba color)
    {
        return (x, y) => color;
    }

    // light from the top left, shadow toward the bottom right
    public static Paint Lit(Rgba color, float cx, float cy, float radius)
    {
        Rgba dark = color.Times(0.68f);
        Rgba light = color.Mix(White, 0.3f);
        return (x, y) =>
        {
            float t = Clamp01(0.5f + ((cx - x) * 0.45f + (y - cy) * 0.8f) / (radius * 1.7f));
            return dark.Mix(light, t);
        };
    }

    public static Paint Gradient(Rgba bottom, Rgba top, float y0, float y1)
    {
        return (x, y) => bottom.Mix(top, (y - y0) / (y1 - y0));
    }

    public static Paint Radial(Rgba centre, Rgba edge, float cx, float cy, float radius)
    {
        return (x, y) => centre.Mix(edge, Len(x - cx, y - cy) / radius);
    }

    // --- face parts ---

    // two eyes mirrored around x = 0. lookX < 0 looks inward (angry cross-eyed), > 0 looks outward
    public static void Eyes(Sketch sketch, float spacing, float cy, float rx, float ry, Rgba white, Rgba pupil,
        float pupilRadius, float lookX, float lookY)
    {
        sketch.Add(Mirror(Ellipse(spacing, cy, rx, ry)), white, 0.03f);
        sketch.Detail(Mirror(Circle(spacing + lookX, cy + lookY, pupilRadius)), pupil);
        sketch.Detail(Mirror(Circle(spacing + lookX + pupilRadius * 0.35f, cy + lookY + pupilRadius * 0.4f,
            pupilRadius * 0.34f)), White);
    }

    // angry eyebrows that cut into the top of the eyes (inner end low)
    public static void AngryBrows(Sketch sketch, float spacing, float cy, float rx, float ry, float thickness)
    {
        sketch.Detail(Mirror(Capsule(spacing - rx * 0.95f, cy + ry * 0.55f, spacing + rx * 1.05f, cy + ry * 1.2f, thickness)),
            sketch.Ink);
    }

    // friendly raised eyebrows (for Beasts)
    public static void HappyBrows(Sketch sketch, float spacing, float cy, float rx, float thickness)
    {
        sketch.Detail(Mirror(Path(thickness, spacing - rx * 0.8f, cy, spacing, cy + thickness * 1.6f, spacing + rx * 0.8f, cy)),
            sketch.Ink);
    }

    // wide dark grin with jagged teeth along the top
    public static void Grin(Sketch sketch, float cy, float halfWidth, float depth, Rgba mouth, Rgba teeth, int toothCount)
    {
        Shape mouthShape = Clip(Ellipse(0f, cy, halfWidth, depth), Below(cy + depth * 0.12f));
        sketch.Add(mouthShape, mouth, 0.028f);
        float step = halfWidth * 1.8f / toothCount;
        float start = -halfWidth * 0.9f;
        Shape[] spikes = new Shape[toothCount];
        for (int i = 0; i < toothCount; i++)
        {
            float x0 = start + i * step;
            spikes[i] = Polygon(x0, cy + depth, x0 + step, cy + depth, x0 + step * 0.5f, cy - depth * 0.45f);
        }
        sketch.Detail(Clip(Union(spikes), mouthShape), teeth);
    }

    // just fangs (two teeth from the corners of a closed mouth)
    public static void FangMouth(Sketch sketch, float cy, float halfWidth, float thickness, Rgba fang)
    {
        sketch.Detail(Path(thickness, -halfWidth, cy + thickness, 0f, cy - thickness * 0.6f, halfWidth, cy + thickness), sketch.Ink);
        sketch.Add(Mirror(Polygon(halfWidth * 0.35f, cy, halfWidth * 0.62f, cy, halfWidth * 0.48f, cy - halfWidth * 0.42f)),
            fang, 0.02f);
    }

    // a friendly curved smile
    public static void Smile(Sketch sketch, float cy, float halfWidth, float curve, float thickness)
    {
        const int steps = 8;
        float[] xy = new float[(steps + 1) * 2];
        for (int i = 0; i <= steps; i++)
        {
            float x = -halfWidth + 2f * halfWidth * i / steps;
            float n = x / halfWidth;
            xy[i * 2] = x;
            xy[i * 2 + 1] = cy - curve * (1f - n * n);
        }
        sketch.Detail(Path(thickness, xy), sketch.Ink);
    }

    // open happy mouth with a tongue (for Beasts)
    public static void OpenSmile(Sketch sketch, float cy, float halfWidth, float depth, Rgba mouth, Rgba tongue)
    {
        Shape mouthShape = Clip(Ellipse(0f, cy, halfWidth, depth), Below(cy));
        sketch.Add(mouthShape, mouth, 0.026f);
        sketch.Detail(Clip(Ellipse(0f, cy - depth * 0.95f, halfWidth * 0.6f, depth * 0.55f), mouthShape), tongue);
    }

    public static void Shine(Sketch sketch, float cx, float cy, float rx, float ry, float alpha)
    {
        sketch.Detail(Rotate(Ellipse(cx, cy, rx, ry), cx, cy, 35f), White.Alpha(alpha));
    }

    // --- list of drawings ---

    public static readonly string[] EnemyNames =
    {
        "Enemy_SparkMite", "Enemy_VoltBeetle", "Enemy_DripSlime", "Enemy_ShellCrab", "Enemy_EmberImp", "Enemy_MagmaBrute"
    };

    public static readonly string[] BossNames = { "Boss_Voltrex", "Boss_Maelith", "Boss_Pyrax" };

    public static readonly string[] BeastNames =
    {
        "Beast_Mossback_Idle", "Beast_Mossback_Ball",
        "Beast_Voltis_Idle", "Beast_Voltis_Ball",
        "Beast_Ripple_Idle", "Beast_Ripple_Ball"
    };

    public static Sketch Draw(string name)
    {
        switch (name)
        {
            case "Enemy_SparkMite": return SparkMite();
            case "Enemy_VoltBeetle": return VoltBeetle();
            case "Enemy_DripSlime": return DripSlime();
            case "Enemy_ShellCrab": return ShellCrab();
            case "Enemy_EmberImp": return EmberImp();
            case "Enemy_MagmaBrute": return MagmaBrute();
            case "Boss_Voltrex": return Voltrex();
            case "Boss_Maelith": return Maelith();
            case "Boss_Pyrax": return Pyrax();
            case "Beast_Mossback_Idle": return MossbackIdle();
            case "Beast_Mossback_Ball": return MossbackBall();
            case "Beast_Voltis_Idle": return VoltisIdle();
            case "Beast_Voltis_Ball": return VoltisBall();
            case "Beast_Ripple_Idle": return RippleIdle();
            case "Beast_Ripple_Ball": return RippleBall();
            default: return null;
        }
    }

    // --- enemies ---

    // Electric, fast: an angry spiky spark ball. (only drawn if the stage 8 file goes missing,
    // the first PNG we made is kept as it is)
    private static Sketch SparkMite()
    {
        Sketch s = new Sketch();
        Shape body = Star(0f, 0f, 0.62f, 0.9f, 10, 0f);
        s.Add(body, Gradient(Rgba.Hex(0xEC9216), Rgba.Hex(0xFFE860), -0.9f, 0.9f));
        s.Detail(Circle(0f, 0f, 0.48f), Radial(Rgba.Hex(0xFFFAD6, 0.55f), Rgba.Hex(0xFFFAD6, 0f), 0f, 0f, 0.48f));
        s.Detail(Clip(Cut(Grow(body, -0.02f), Grow(body, -0.1f)), Clip(Above(0.1f), (x, y) => x + 0.1f)),
            Rgba.Hex(0x78E2FF, 0.7f));
        Eyes(s, 0.22f, 0.05f, 0.13f, 0.15f, White, s.Ink, 0.075f, -0.005f, -0.02f);
        AngryBrows(s, 0.22f, 0.05f, 0.13f, 0.15f, 0.045f);
        Grin(s, -0.24f, 0.2f, 0.12f, Rgba.Hex(0x60162A), Rgba.Hex(0xFFFCEC), 5);
        return s;
    }

    // Electric, tough: a round storm beetle with spark antennas
    private static Sketch VoltBeetle()
    {
        Sketch s = new Sketch();
        Rgba leg = Rgba.Hex(0x2E2A48);
        s.Add(Mirror(Capsule(0.45f, -0.05f, 0.82f, -0.3f, 0.065f)), leg);
        s.Add(Mirror(Capsule(0.42f, -0.32f, 0.74f, -0.66f, 0.065f)), leg);
        s.Add(Mirror(Capsule(0.28f, -0.46f, 0.44f, -0.86f, 0.065f)), leg);
        s.Add(Mirror(Capsule(0.12f, 0.36f, 0.36f, 0.8f, 0.035f)), leg);
        s.Add(Mirror(Circle(0.38f, 0.83f, 0.085f)), Radial(White, Rgba.Hex(0xFFD23C), 0.38f, 0.83f, 0.09f));

        Shape shell = Ellipse(0f, 0.14f, 0.68f, 0.58f);
        s.Add(shell, Lit(Rgba.Hex(0xF6C52E), 0f, 0.14f, 0.68f));
        s.Detail(Clip(Mirror(Path(0.055f, 0.16f, 0.66f, 0.42f, 0.42f, 0.24f, 0.24f, 0.52f, 0f, 0.3f, -0.22f)), shell),
            Rgba.Hex(0x2E2A48));
        s.Detail(Clip(Capsule(0f, 0.74f, 0f, -0.2f, 0.022f), shell), s.Ink);
        Shine(s, -0.3f, 0.48f, 0.16f, 0.08f, 0.55f);

        Shape head = Ellipse(0f, -0.4f, 0.42f, 0.3f);
        s.Add(head, Lit(Rgba.Hex(0x4B4878), 0f, -0.4f, 0.42f));
        s.Add(Mirror(Polygon(0.08f, -0.6f, 0.22f, -0.62f, 0.12f, -0.8f)), Rgba.Hex(0xEDE3CC), 0.022f);
        Eyes(s, 0.17f, -0.36f, 0.1f, 0.11f, Rgba.Hex(0xFFF7D0), Rgba.Hex(0x1E1A33), 0.055f, -0.015f, -0.01f);
        AngryBrows(s, 0.17f, -0.36f, 0.1f, 0.11f, 0.03f);
        return s;
    }

    // Water: a shiny water drop slime
    private static Sketch DripSlime()
    {
        Sketch s = new Sketch();
        Rgba blue = Rgba.Hex(0x3E9BF0);
        s.Add(Circle(-0.62f, 0.5f, 0.08f), Rgba.Hex(0xA9DCFF));
        s.Add(Circle(0.66f, 0.28f, 0.06f), Rgba.Hex(0xA9DCFF));
        s.Add(Circle(0.55f, 0.7f, 0.045f), Rgba.Hex(0xA9DCFF));

        Shape body = Blend(Ellipse(0f, -0.3f, 0.66f, 0.5f), Polygon(-0.3f, 0.05f, 0f, 0.86f, 0.3f, 0.05f), 0.28f);
        s.Add(body, Lit(blue, 0f, -0.1f, 0.7f));
        s.Detail(Clip(Ellipse(0f, -0.48f, 0.42f, 0.26f), body), Rgba.Hex(0x9FD4FF, 0.55f));
        Shine(s, -0.3f, 0.22f, 0.18f, 0.08f, 0.7f);
        s.Detail(Circle(-0.12f, 0.46f, 0.045f), White.Alpha(0.75f));
        s.Add(Mirror(Circle(0.48f, -0.74f, 0.1f)), Lit(blue, 0.48f, -0.74f, 0.1f));

        Eyes(s, 0.21f, -0.16f, 0.13f, 0.16f, White, Rgba.Hex(0x14284A), 0.07f, -0.02f, -0.02f);
        AngryBrows(s, 0.21f, -0.16f, 0.13f, 0.16f, 0.032f);
        Grin(s, -0.47f, 0.2f, 0.11f, Rgba.Hex(0x16305A), Rgba.Hex(0xF2FBFF), 5);
        return s;
    }

    // Water, tough: a crab with a shell raising both claws
    private static Sketch ShellCrab()
    {
        Sketch s = new Sketch();
        Rgba shellColor = Rgba.Hex(0x2FB3B8);
        Rgba limb = Rgba.Hex(0x1F8A93);
        s.Add(Mirror(Capsule(0.42f, -0.36f, 0.78f, -0.62f, 0.06f)), limb);
        s.Add(Mirror(Capsule(0.3f, -0.44f, 0.56f, -0.84f, 0.06f)), limb);
        s.Add(Mirror(Capsule(0.38f, -0.04f, 0.64f, 0.26f, 0.09f)), limb);

        Shape claw = Cut(Circle(0.66f, 0.44f, 0.25f), Polygon(0.66f, 0.44f, 0.98f, 0.74f, 0.98f, 0.36f));
        s.Add(Mirror(claw), Lit(Rgba.Hex(0x45C9C9), 0.66f, 0.44f, 0.25f));
        s.Add(Mirror(Capsule(0.17f, 0.12f, 0.22f, 0.44f, 0.04f)), limb);

        Shape shell = Ellipse(0f, -0.14f, 0.64f, 0.46f);
        s.Add(shell, Lit(shellColor, 0f, -0.14f, 0.64f));
        s.Detail(Clip(Mirror(Path(0.022f, 0.0f, 0.3f, 0.3f, 0.05f, 0.5f, -0.25f)), shell), Rgba.Hex(0x1B7880));
        s.Detail(Clip(Ellipse(0f, -0.42f, 0.4f, 0.2f), shell), Rgba.Hex(0xBDEFEA, 0.7f));
        Shine(s, -0.3f, 0.12f, 0.16f, 0.07f, 0.5f);

        s.Add(Mirror(Circle(0.23f, 0.5f, 0.11f)), White, 0.03f);
        s.Detail(Mirror(Circle(0.21f, 0.48f, 0.06f)), Rgba.Hex(0x10303A));
        s.Detail(Mirror(Circle(0.23f, 0.51f, 0.022f)), White);
        s.Detail(Mirror(Capsule(0.13f, 0.62f, 0.34f, 0.67f, 0.028f)), s.Ink);
        Grin(s, -0.3f, 0.2f, 0.1f, Rgba.Hex(0x0E3D44), Rgba.Hex(0xF4FFFC), 5);
        return s;
    }

    // Fire, fast: a grinning living flame
    private static Sketch EmberImp()
    {
        Sketch s = new Sketch();
        s.Add(Mirror(Capsule(0.42f, -0.36f, 0.66f, -0.18f, 0.06f)), Rgba.Hex(0xD9481E));
        s.Add(Circle(0.36f, 0.8f, 0.05f), Rgba.Hex(0xFFD45A));
        s.Add(Circle(-0.5f, 0.66f, 0.04f), Rgba.Hex(0xFFD45A));

        Shape flame = Blend(Ellipse(0f, -0.3f, 0.56f, 0.5f),
            Union(Taper(0f, 0f, 0.05f, 0.88f, 0.34f, 0.02f),
                Taper(0.22f, -0.05f, 0.46f, 0.52f, 0.2f, 0.02f),
                Taper(-0.22f, -0.05f, -0.44f, 0.46f, 0.2f, 0.02f)), 0.16f);
        s.Add(flame, Gradient(Rgba.Hex(0xE2401C), Rgba.Hex(0xFFC93C), -0.8f, 0.8f));
        Shape core = Blend(Ellipse(0f, -0.38f, 0.34f, 0.3f), Taper(0f, -0.2f, 0.02f, 0.4f, 0.2f, 0.02f), 0.12f);
        s.Detail(core, Rgba.Hex(0xFFF0A0, 0.8f));

        Eyes(s, 0.19f, -0.2f, 0.11f, 0.13f, Rgba.Hex(0xFFFBE0), Rgba.Hex(0x3A0C06), 0.06f, -0.02f, -0.02f);
        AngryBrows(s, 0.19f, -0.2f, 0.11f, 0.13f, 0.03f);
        Grin(s, -0.5f, 0.18f, 0.1f, Rgba.Hex(0x5A1206), Rgba.Hex(0xFFF3D6), 4);
        return s;
    }

    // Fire, tough: a rock brute with glowing lava cracks
    private static Sketch MagmaBrute()
    {
        Sketch s = new Sketch();
        Rgba rock = Rgba.Hex(0x5A403C);
        Rgba lava = Rgba.Hex(0xFF8A2A);
        s.Add(Mirror(Circle(0.74f, -0.3f, 0.2f)), Lit(rock, 0.74f, -0.3f, 0.2f));
        Shape body = Polygon(-0.62f, -0.62f, -0.78f, -0.1f, -0.6f, 0.5f, -0.2f, 0.74f, 0.28f, 0.72f, 0.64f, 0.46f,
            0.8f, -0.06f, 0.6f, -0.64f, 0.1f, -0.8f, -0.24f, -0.78f);
        s.Add(Grow(body, 0.04f), Lit(rock, 0f, 0f, 0.8f));
        s.Detail(Clip(Union(
            Path(0.03f, -0.5f, 0.42f, -0.3f, 0.2f, -0.38f, -0.02f),
            Path(0.03f, 0.56f, 0.3f, 0.36f, 0.12f, 0.46f, -0.1f),
            Path(0.028f, -0.1f, -0.62f, 0.05f, -0.48f, -0.04f, -0.36f)), body), lava);
        s.Detail(Clip(Ellipse(0f, 0.36f, 0.5f, 0.18f), body), Rgba.Hex(0x7A5A52, 0.55f));

        s.Add(Mirror(Polygon(0.08f, 0.02f, 0.38f, 0.14f, 0.36f, -0.05f, 0.12f, -0.1f)), Rgba.Hex(0xFFE06A), 0.03f);
        s.Detail(Mirror(Capsule(0.18f, -0.02f, 0.3f, 0.03f, 0.03f)), Rgba.Hex(0xC23A10));
        s.Detail(Mirror(Capsule(0.04f, 0.24f, 0.42f, 0.32f, 0.045f)), s.Ink);
        Grin(s, -0.32f, 0.3f, 0.14f, Rgba.Hex(0xFF7A1E), Rgba.Hex(0xFFE9B0), 5);
        return s;
    }

    // --- bosses ---

    // Electric boss: a thunder beast with a storm crown
    private static Sketch Voltrex()
    {
        Sketch s = new Sketch();
        s.Outline = 0.032f;
        Rgba bolt = Rgba.Hex(0xFFD93A);
        s.Add(Polygon(-0.12f, 0.5f, -0.34f, 0.94f, -0.06f, 0.78f, 0.0f, 0.97f, 0.08f, 0.78f, 0.36f, 0.94f, 0.14f, 0.5f), bolt);
        s.Add(Mirror(Polygon(0.42f, 0.36f, 0.8f, 0.64f, 0.62f, 0.38f, 0.92f, 0.32f, 0.58f, 0.16f)), bolt);

        Shape body = Star(0f, -0.06f, 0.6f, 0.86f, 12, 0f);
        s.Add(body, Lit(Rgba.Hex(0x5A3AA8), 0f, -0.06f, 0.86f));
        s.Detail(Circle(0f, -0.08f, 0.5f), Radial(Rgba.Hex(0x9C7AF0, 0.9f), Rgba.Hex(0x5A3AA8, 0f), 0f, -0.08f, 0.52f));
        s.Detail(Mirror(Path(0.03f, 0.48f, 0.02f, 0.4f, -0.1f, 0.5f, -0.18f, 0.42f, -0.3f)), bolt);
        Shine(s, -0.36f, 0.34f, 0.16f, 0.07f, 0.4f);

        Eyes(s, 0.21f, 0.04f, 0.15f, 0.13f, Rgba.Hex(0xFFF4B0), Rgba.Hex(0x2A1250), 0.065f, -0.03f, -0.02f);
        AngryBrows(s, 0.21f, 0.04f, 0.15f, 0.13f, 0.04f);
        Grin(s, -0.3f, 0.32f, 0.16f, Rgba.Hex(0x22103E), Rgba.Hex(0xFFF7DA), 7);
        return s;
    }

    // Water boss: a tide queen with a crown and tentacles
    private static Sketch Maelith()
    {
        Sketch s = new Sketch();
        s.Outline = 0.032f;
        Rgba tentacle = Rgba.Hex(0x2E6FD0);
        s.Add(Mirror(Path(0.075f, 0.18f, -0.4f, 0.28f, -0.66f, 0.18f, -0.9f)), tentacle);
        s.Add(Mirror(Path(0.07f, 0.46f, -0.34f, 0.66f, -0.56f, 0.6f, -0.82f)), tentacle);
        s.Add(Mirror(Polygon(0.56f, 0.24f, 0.96f, 0.46f, 0.86f, 0.1f, 0.96f, -0.12f, 0.58f, -0.12f)), Rgba.Hex(0x7FE6E0));

        Shape crown = Union(Taper(0f, 0.56f, 0f, 0.98f, 0.1f, 0.015f),
            Taper(-0.24f, 0.52f, -0.42f, 0.88f, 0.08f, 0.015f), Taper(0.24f, 0.52f, 0.42f, 0.88f, 0.08f, 0.015f));
        s.Add(crown, Gradient(Rgba.Hex(0xE6607E), Rgba.Hex(0xFFC0CC), 0.5f, 0.98f));

        Shape head = Ellipse(0f, 0.08f, 0.66f, 0.6f);
        s.Add(head, Lit(Rgba.Hex(0x1F87C8), 0f, 0.08f, 0.66f));
        s.Detail(Clip(Ellipse(0f, -0.26f, 0.46f, 0.22f), head), Rgba.Hex(0x8FE0FF, 0.45f));
        s.Detail(Mirror(Circle(0.42f, 0.38f, 0.05f)), Rgba.Hex(0xBFF4FF, 0.85f));
        s.Detail(Circle(0.52f, 0.22f, 0.035f), Rgba.Hex(0xBFF4FF, 0.85f));
        Shine(s, -0.32f, 0.42f, 0.18f, 0.08f, 0.5f);

        Eyes(s, 0.22f, 0.1f, 0.14f, 0.15f, Rgba.Hex(0xE8FFFF), Rgba.Hex(0x0A2A4A), 0.065f, -0.03f, -0.02f);
        AngryBrows(s, 0.22f, 0.1f, 0.14f, 0.15f, 0.038f);
        Grin(s, -0.24f, 0.28f, 0.13f, Rgba.Hex(0x0A2240), Rgba.Hex(0xF0FFFF), 6);
        return s;
    }

    // Fire boss: a horned volcano dragon with a burning mane
    private static Sketch Pyrax()
    {
        Sketch s = new Sketch();
        s.Outline = 0.032f;
        Shape mane = Union(
            Taper(0f, 0.3f, 0.02f, 0.98f, 0.36f, 0.02f),
            Taper(0.3f, 0.26f, 0.66f, 0.84f, 0.26f, 0.02f), Taper(-0.3f, 0.26f, -0.66f, 0.84f, 0.26f, 0.02f),
            Taper(0.5f, 0.0f, 0.94f, 0.36f, 0.22f, 0.02f), Taper(-0.5f, 0.0f, -0.94f, 0.36f, 0.22f, 0.02f));
        s.Add(mane, Gradient(Rgba.Hex(0xE84A1C), Rgba.Hex(0xFFD24A), 0.1f, 0.95f));
        s.Add(Mirror(Taper(0.34f, 0.42f, 0.7f, 0.96f, 0.1f, 0.02f)), Rgba.Hex(0x3A2622));

        Shape head = Blend(Ellipse(0f, 0.02f, 0.6f, 0.56f), Box(0f, -0.48f, 0.4f, 0.22f, 0.16f), 0.2f);
        Rgba rock = Rgba.Hex(0x6A3A30);
        s.Add(head, Lit(rock, 0f, -0.05f, 0.65f));
        s.Detail(Clip(Union(Path(0.026f, -0.5f, 0.16f, -0.32f, 0.3f, -0.36f, 0.46f),
            Path(0.026f, 0.52f, 0.1f, 0.36f, 0.26f, 0.42f, 0.44f)), head), Rgba.Hex(0xFF8A2A));
        Shine(s, -0.3f, 0.34f, 0.15f, 0.07f, 0.3f);

        s.Add(Mirror(Polygon(0.08f, 0.12f, 0.42f, 0.24f, 0.4f, 0.04f, 0.12f, -0.04f)), Rgba.Hex(0xFFE36A), 0.03f);
        s.Detail(Mirror(Capsule(0.2f, 0.08f, 0.32f, 0.12f, 0.032f)), Rgba.Hex(0xB8300C));
        s.Detail(Mirror(Capsule(0.04f, 0.32f, 0.44f, 0.44f, 0.05f)), s.Ink);
        s.Detail(Mirror(Ellipse(0.1f, -0.3f, 0.04f, 0.025f)), s.Ink);
        Grin(s, -0.5f, 0.34f, 0.16f, Rgba.Hex(0xFF6A16), Rgba.Hex(0xFFF0C8), 7);
        return s;
    }

    // --- Beasts ---

    // Earth Beast: a little mossy turtle
    private static Sketch MossbackIdle()
    {
        Sketch s = new Sketch();
        Rgba skin = Rgba.Hex(0xE2B47A);
        s.Add(Mirror(Ellipse(0.5f, -0.66f, 0.2f, 0.13f)), Lit(skin, 0.5f, -0.66f, 0.2f));
        s.Add(Mirror(Ellipse(0.7f, -0.12f, 0.16f, 0.12f)), Lit(skin, 0.7f, -0.12f, 0.16f));

        Shape shell = Ellipse(0f, 0.12f, 0.74f, 0.6f);
        s.Add(shell, Lit(Rgba.Hex(0x9C6A40), 0f, 0.12f, 0.74f));
        s.Detail(Clip(Union(Path(0.026f, -0.46f, -0.2f, -0.2f, 0.2f, 0.2f, 0.2f, 0.46f, -0.2f),
            Path(0.026f, -0.2f, 0.2f, -0.06f, 0.6f), Path(0.026f, 0.2f, 0.2f, 0.06f, 0.6f)), shell), Rgba.Hex(0x6E4628));
        Shape moss = Clip(Union(Circle(-0.3f, 0.62f, 0.2f), Circle(0.0f, 0.7f, 0.22f), Circle(0.3f, 0.6f, 0.2f),
            Circle(-0.52f, 0.4f, 0.14f), Circle(0.52f, 0.42f, 0.14f)), Grow(shell, 0.05f));
        s.Add(moss, Lit(Rgba.Hex(0x6DB548), 0f, 0.6f, 0.5f), 0.025f);
        s.Add(Taper(0.02f, 0.78f, 0.12f, 0.96f, 0.035f, 0.02f), Rgba.Hex(0x4E9A34), 0.02f);
        s.Add(Rotate(Ellipse(0.2f, 0.95f, 0.1f, 0.05f), 0.2f, 0.95f, 25f), Rgba.Hex(0x8FD860), 0.02f);

        Shape head = Ellipse(0f, -0.36f, 0.4f, 0.34f);
        s.Add(head, Lit(skin, 0f, -0.36f, 0.4f));
        s.Detail(Mirror(Ellipse(0.25f, -0.48f, 0.08f, 0.05f)), Rgba.Hex(0xF08A8A, 0.6f));
        Eyes(s, 0.15f, -0.3f, 0.085f, 0.11f, White, Rgba.Hex(0x2A1A10), 0.06f, 0f, -0.01f);
        Smile(s, -0.5f, 0.1f, 0.05f, 0.022f);
        return s;
    }

    private static Sketch MossbackBall()
    {
        Sketch s = new Sketch();
        Shape shell = Circle(0f, 0f, 0.84f);
        s.Add(shell, Lit(Rgba.Hex(0x9C6A40), 0f, 0f, 0.84f));
        s.Detail(Clip(Union(Path(0.03f, -0.6f, -0.2f, -0.25f, 0.18f, 0.25f, 0.18f, 0.6f, -0.2f),
            Path(0.03f, -0.25f, 0.18f, -0.1f, 0.7f), Path(0.03f, 0.25f, 0.18f, 0.1f, 0.7f),
            Path(0.03f, -0.25f, -0.62f, 0f, -0.3f, 0.25f, -0.62f)), shell), Rgba.Hex(0x6E4628));
        Shape moss = Clip(Union(Circle(-0.36f, 0.56f, 0.26f), Circle(0.05f, 0.7f, 0.26f), Circle(0.42f, 0.5f, 0.24f)),
            Grow(shell, 0.03f));
        s.Add(moss, Lit(Rgba.Hex(0x6DB548), 0f, 0.6f, 0.5f), 0.025f);
        Shine(s, -0.42f, 0.2f, 0.16f, 0.07f, 0.35f);
        return s;
    }

    // Electric Beast: a bright fox with lightning ears and tail
    private static Sketch VoltisIdle()
    {
        Sketch s = new Sketch();
        Rgba fur = Rgba.Hex(0xFFCF3A);
        Rgba tip = Rgba.Hex(0x3A2E5A);
        s.Add(Polygon(0.3f, -0.42f, 0.66f, -0.06f, 0.52f, -0.02f, 0.86f, 0.42f, 0.62f, 0.32f, 0.72f, 0.62f,
            0.36f, 0.08f, 0.5f, 0.1f, 0.18f, -0.26f), Lit(fur, 0.5f, 0.1f, 0.5f));

        Shape body = Ellipse(0f, -0.5f, 0.42f, 0.36f);
        s.Add(body, Lit(fur, 0f, -0.5f, 0.42f));
        s.Detail(Clip(Ellipse(0f, -0.56f, 0.24f, 0.26f), body), Rgba.Hex(0xFFF4D0));
        s.Add(Mirror(Ellipse(0.2f, -0.82f, 0.12f, 0.08f)), Lit(fur, 0.2f, -0.82f, 0.12f));

        Shape ear = Polygon(0.16f, 0.36f, 0.34f, 0.96f, 0.4f, 0.7f, 0.56f, 0.78f, 0.5f, 0.3f);
        s.Add(Mirror(ear), Lit(fur, 0.36f, 0.6f, 0.3f));
        s.Detail(Clip(Mirror(ear), Above(0.74f)), tip);

        Shape head = Ellipse(0f, 0.08f, 0.5f, 0.42f);
        s.Add(head, Lit(fur, 0f, 0.08f, 0.5f));
        s.Detail(Clip(Mirror(Ellipse(0.22f, -0.12f, 0.2f, 0.14f)), head), Rgba.Hex(0xFFF4D0));
        s.Detail(Mirror(Circle(0.34f, -0.04f, 0.07f)), Rgba.Hex(0xFF7A4A, 0.75f));
        Eyes(s, 0.2f, 0.12f, 0.1f, 0.13f, White, Rgba.Hex(0x2A1E3A), 0.07f, 0f, -0.01f);
        s.Detail(Ellipse(0f, -0.04f, 0.05f, 0.035f), tip);
        OpenSmile(s, -0.11f, 0.1f, 0.09f, Rgba.Hex(0x6A2A2A), Rgba.Hex(0xFF8A8A));
        return s;
    }

    private static Sketch VoltisBall()
    {
        Sketch s = new Sketch();
        Rgba fur = Rgba.Hex(0xFFCF3A);
        s.Add(Mirror(Polygon(0.2f, 0.66f, 0.36f, 0.98f, 0.5f, 0.6f)), Rgba.Hex(0x3A2E5A));
        Shape ball = Circle(0f, 0f, 0.84f);
        s.Add(ball, Lit(fur, 0f, 0f, 0.84f));
        s.Detail(Clip(Polygon(-0.06f, 0.86f, -0.42f, 0.04f, -0.06f, 0.06f, -0.2f, -0.86f, 0.4f, 0.12f, 0.02f, 0.1f, 0.24f, 0.86f), ball),
            Rgba.Hex(0xFFF4D0));
        s.Detail(Clip(Path(0.028f, -0.72f, -0.3f, -0.4f, -0.5f, -0.1f, -0.66f), ball), Rgba.Hex(0xE0A020));
        Shine(s, -0.42f, 0.36f, 0.16f, 0.07f, 0.45f);
        return s;
    }

    // Water Beast: a round otter with a water drop crest and a fin tail
    private static Sketch RippleIdle()
    {
        Sketch s = new Sketch();
        Rgba fur = Rgba.Hex(0x4FA6F2);
        Rgba belly = Rgba.Hex(0xD6F0FF);
        s.Add(Polygon(0.3f, -0.6f, 0.78f, -0.36f, 0.94f, -0.62f, 0.84f, -0.18f, 0.5f, -0.3f), Lit(Rgba.Hex(0x3A8AD8), 0.7f, -0.4f, 0.3f));

        Shape body = Ellipse(0f, -0.46f, 0.46f, 0.4f);
        s.Add(body, Lit(fur, 0f, -0.46f, 0.46f));
        s.Detail(Clip(Ellipse(0f, -0.52f, 0.28f, 0.28f), body), belly);
        s.Add(Mirror(Ellipse(0.22f, -0.82f, 0.13f, 0.08f)), Lit(fur, 0.22f, -0.82f, 0.13f));

        Shape crest = Blend(Circle(0f, 0.58f, 0.13f), Polygon(-0.1f, 0.6f, 0.04f, 0.96f, 0.1f, 0.6f), 0.06f);
        s.Add(crest, Lit(Rgba.Hex(0x8FD6FF), 0f, 0.62f, 0.2f));
        s.Add(Mirror(Circle(0.4f, 0.4f, 0.12f)), Lit(fur, 0.4f, 0.4f, 0.12f));
        s.Detail(Mirror(Circle(0.4f, 0.4f, 0.06f)), Rgba.Hex(0x2E78C0));

        Shape head = Ellipse(0f, 0.08f, 0.5f, 0.42f);
        s.Add(head, Lit(fur, 0f, 0.08f, 0.5f));
        s.Detail(Clip(Ellipse(0f, -0.12f, 0.3f, 0.2f), head), belly);
        s.Detail(Mirror(Ellipse(0.32f, -0.04f, 0.07f, 0.045f)), Rgba.Hex(0xFF9AAA, 0.6f));
        Eyes(s, 0.18f, 0.12f, 0.09f, 0.12f, White, Rgba.Hex(0x10243E), 0.065f, 0f, -0.01f);
        s.Detail(Ellipse(0f, -0.04f, 0.055f, 0.04f), Rgba.Hex(0x10243E));
        Smile(s, -0.13f, 0.08f, 0.045f, 0.02f);
        s.Detail(Mirror(Path(0.009f, 0.2f, -0.07f, 0.36f, -0.05f)), Rgba.Hex(0x10243E, 0.7f));
        return s;
    }

    private static Sketch RippleBall()
    {
        Sketch s = new Sketch();
        Rgba fur = Rgba.Hex(0x4FA6F2);
        Shape ball = Circle(0f, 0f, 0.84f);
        s.Add(ball, Lit(fur, 0f, 0f, 0.84f));
        s.Detail(Clip(Ellipse(0.1f, -0.2f, 0.5f, 0.46f), ball), Rgba.Hex(0xD6F0FF));
        s.Detail(Clip(Path(0.045f, -0.6f, 0.1f, -0.36f, 0.3f, -0.1f, 0.1f, 0.14f, 0.3f, 0.4f, 0.1f, 0.62f, 0.3f), ball),
            Rgba.Hex(0x2E78C0));
        s.Add(Blend(Circle(-0.2f, 0.62f, 0.12f), Polygon(-0.3f, 0.64f, -0.22f, 0.98f, -0.1f, 0.64f), 0.05f),
            Lit(Rgba.Hex(0x8FD6FF), -0.2f, 0.66f, 0.2f), 0.028f);
        Shine(s, -0.44f, 0.34f, 0.15f, 0.07f, 0.5f);
        return s;
    }

    // --- math helpers ---

    private static float Len(float x, float y)
    {
        return (float)Math.Sqrt(x * x + y * y);
    }

    private static float Clamp01(float value)
    {
        return value < 0f ? 0f : value > 1f ? 1f : value;
    }

    private static float SegmentDistance(float x, float y, float ax, float ay, float bx, float by, out float t)
    {
        float dx = bx - ax, dy = by - ay;
        float lengthSquared = dx * dx + dy * dy;
        t = lengthSquared > 1e-8f ? Clamp01(((x - ax) * dx + (y - ay) * dy) / lengthSquared) : 0f;
        return Len(ax + t * dx - x, ay + t * dy - y);
    }

    private static float PolygonDistance(float[] px, float[] py, float x, float y)
    {
        int count = px.Length;
        float best = (x - px[0]) * (x - px[0]) + (y - py[0]) * (y - py[0]);
        float sign = 1f;
        for (int i = 0, j = count - 1; i < count; j = i, i++)
        {
            float ex = px[j] - px[i], ey = py[j] - py[i];
            float wx = x - px[i], wy = y - py[i];
            float lengthSquared = ex * ex + ey * ey;
            float t = lengthSquared > 1e-8f ? Clamp01((wx * ex + wy * ey) / lengthSquared) : 0f;
            float bx = wx - ex * t, by = wy - ey * t;
            best = Math.Min(best, bx * bx + by * by);
            bool c1 = y >= py[i], c2 = y < py[j], c3 = ex * wy > ey * wx;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                sign = -sign;
        }
        return sign * (float)Math.Sqrt(best);
    }
}
