using System.Collections.Generic;
using UnityEngine;

// shared lookups for the painted stage art in Assets/Resources/Art (barrier, launcher, Terrathorn)
// and the glow materials for the corruption, barrier and launcher effects. everything is made once
// and reused, so code that runs every frame doesn't make new stuff
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

    // painted art by file name. gives null (no warning) if the file isn't in the project,
    // so the caller keeps its old look
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

    // which art set the current stage uses: chapters 3-5 (stages 9-20) have their own barrier and
    // launcher in Resources/Art/Electric, /Water and /Fire ("" = the shared crystal set if the stage
    // has no StageManager). chapters 1 and 2 return null: they keep their own drawn barrier and
    // launcher (the mossy log and vine stumps, the Earth wall and stone posts), so no painted layer is added
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

    // true if the glow shader works on this device. without it the effects use the normal
    // sprite material (no outline glow)
    public static bool HasGlowShader
    {
        get { return GlowShader() != null; }
    }

    // soft glow that blends normally (keeps its color on light backgrounds)
    public static Material AlphaGlow(Material fallback)
    {
        if (alphaGlow == null)
            alphaGlow = CreateGlowMaterial("Glow (alpha)", 0f, 0f);
        return alphaGlow != null ? alphaGlow : fallback;
    }

    // brightening glow for energy (crystals, sparks, particles)
    public static Material AdditiveGlow(Material fallback)
    {
        if (additiveGlow == null)
            additiveGlow = CreateGlowMaterial("Glow (additive)", 0.85f, 0f);
        return additiveGlow != null ? additiveGlow : fallback;
    }

    // draws only the sprite's shape in the renderer color, null if the shader can't be used
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

    // a sprite on its own child object, for effect layers
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
