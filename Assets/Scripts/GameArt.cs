using System.Collections.Generic;
using UnityEngine;

// Shared, cached look-ups for the painted stage art in Assets/Resources/Art (the barrier, the
// launcher, Terrathorn) and the glow materials used by the corruption, barrier and launcher
// effects. Everything is created once and reused, so per-frame code never allocates.
public static class GameArt
{
    private const string ArtFolder = "Art/";
    private const string GlowShaderPath = "Shaders/SpriteGlow";

    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static Shader glowShader;
    private static bool glowShaderChecked;
    private static Material alphaGlow;
    private static Material additiveGlow;
    private static Material silhouetteGlow;

    // Painted art by file name; null (without a warning) when the file is not in the project,
    // so callers keep their previous look.
    public static Sprite Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (Sprites.TryGetValue(name, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(ArtFolder + name);
        Sprites[name] = sprite;
        return sprite;
    }

    // Art set of the running stage: chapters 3-5 (stages 9-20) have their own barrier and launcher
    // in Resources/Art/Electric, /Water and /Fire ("" = the shared crystal set when a stage has no
    // StageManager). Chapters 1 and 2 return null: they keep the barrier and launcher drawn for
    // them (the mossy log and vine stumps, the Earth wall and stone posts), so no painted layer
    // is added there.
    public static string StageArtFolder()
    {
        StageManager stage = Object.FindAnyObjectByType<StageManager>();
        if (stage == null)
            return string.Empty;
        switch (ChapterData.ChapterOfStage(stage.stageNumber))
        {
            case 0:
            case 1: return null;
            case 2: return "Electric/";
            case 3: return "Water/";
            case 4: return "Fire/";
            default: return string.Empty;
        }
    }

    // True when the glow shader compiled for this device. Without it the effects fall back to
    // the plain sprite material (no silhouette outlines).
    public static bool HasGlowShader
    {
        get { return GlowShader() != null; }
    }

    // Soft glow that blends normally (keeps its colour on light backgrounds).
    public static Material AlphaGlow(Material fallback)
    {
        if (alphaGlow == null)
            alphaGlow = CreateGlowMaterial("Glow (alpha)", 0f, 0f);
        return alphaGlow != null ? alphaGlow : fallback;
    }

    // Brightening glow for energy (crystals, sparks, motes).
    public static Material AdditiveGlow(Material fallback)
    {
        if (additiveGlow == null)
            additiveGlow = CreateGlowMaterial("Glow (additive)", 0.85f, 0f);
        return additiveGlow != null ? additiveGlow : fallback;
    }

    // Draws only a sprite's shape in the renderer colour; null when the shader is unavailable.
    public static Material SilhouetteGlow()
    {
        if (silhouetteGlow == null)
            silhouetteGlow = CreateGlowMaterial("Glow (silhouette)", 0f, 1f);
        return silhouetteGlow;
    }

    private static Shader GlowShader()
    {
        if (glowShaderChecked)
            return glowShader;
        glowShaderChecked = true;

        Shader shader = Resources.Load<Shader>(GlowShaderPath);
        if (shader == null)
        {
            Debug.LogWarning("Glow shader Assets/Resources/" + GlowShaderPath + ".shader is missing; " +
                "corruption auras and energy effects use the plain sprite material.");
            return null;
        }
        if (!shader.isSupported)
        {
            Debug.LogWarning("Glow shader " + shader.name + " is not supported on this device; " +
                "corruption auras and energy effects use the plain sprite material.");
            return null;
        }

        glowShader = shader;
        return glowShader;
    }

    private static Material CreateGlowMaterial(string name, float additive, float silhouette)
    {
        Shader shader = GlowShader();
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = name };
        material.SetFloat("_Additive", additive);
        material.SetFloat("_Silhouette", silhouette);
        return material;
    }

    // A renderer on its own child object, for effect layers.
    public static SpriteRenderer AddLayer(Transform parent, string name, Sprite sprite, Material material,
        SpriteRenderer sortLike, int orderOffset)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent, false);
        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (material != null)
            renderer.sharedMaterial = material;
        if (sortLike != null)
        {
            renderer.sortingLayerID = sortLike.sortingLayerID;
            renderer.sortingOrder = sortLike.sortingOrder + orderOffset;
        }
        return renderer;
    }
}
