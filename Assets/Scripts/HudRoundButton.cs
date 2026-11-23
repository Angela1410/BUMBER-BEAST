using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the look shared by the buttons in a stage (skill and swap): a dark edge, a soft "ready" glow,
// an element colored rim and face with a shine, an icon in the middle, a round cooldown sweep,
// and a name ribbon at the bottom. every layer is placed relative to the button, so resizing
// the button resizes the art, only the font sizes need SetTextScale
public sealed class HudRoundButton
{
    public RectTransform Root { get; private set; }
    public Image Bezel { get; private set; }
    public Image Glow { get; private set; }
    public Image Rim { get; private set; }
    public Image Face { get; private set; }
    public Image Gloss { get; private set; }
    public Image Icon { get; private set; }
    public Image Cooldown { get; private set; }
    public TMP_Text CenterText { get; private set; }
    public Image Ribbon { get; private set; }
    public Outline RibbonOutline { get; private set; }
    public TMP_Text RibbonText { get; private set; }

    private Shadow bezelShadow;
    private Shadow iconShadow;

    public static readonly Color32 RibbonColor = new Color32(34, 42, 31, 250);

    // ribbonWidth is a multiple of the button width (the ribbon can be wider than the circle).
    // reuseLabel: a label that already exists to move into the ribbon, so a scene label is never drawn twice
    public static HudRoundButton Build(GameObject buttonObject, TMP_Text reuseLabel, float ribbonWidth)
    {
        HudRoundButton visual = new HudRoundButton();
        visual.Root = buttonObject.transform as RectTransform;

        visual.Bezel = buttonObject.GetComponent<Image>();
        if (visual.Bezel == null)
            visual.Bezel = buttonObject.AddComponent<Image>();
        visual.Bezel.sprite = HudVisualUtility.CircleSprite();
        visual.Bezel.type = Image.Type.Simple;
        visual.Bezel.color = UiTheme.Ink;
        visual.Bezel.raycastTarget = true;
        // older versions of these buttons had an Outline on the edge, keep it invisible
        Outline oldOutline = buttonObject.GetComponent<Outline>();
        if (oldOutline != null)
            oldOutline.effectDistance = Vector2.zero;
        visual.bezelShadow = UiTheme.GetOrAddEffect<Shadow>(buttonObject);
        visual.bezelShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        visual.bezelShadow.useGraphicAlpha = true;

        visual.Glow = Layer(buttonObject, "Glow", HudVisualUtility.GlowSprite(), new Color(1f, 0.85f, 0.35f, 0f),
            new Vector2(-0.24f, -0.24f), new Vector2(1.24f, 1.24f));
        visual.Rim = Layer(buttonObject, "Rim", HudVisualUtility.RingSprite(), Color.white,
            new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f));
        visual.Face = Layer(buttonObject, "Face", HudVisualUtility.CircleSprite(), UiTheme.Orange,
            new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
        visual.Gloss = Layer(buttonObject, "Gloss", HudVisualUtility.CircleSprite(), new Color(1f, 1f, 1f, 0.2f),
            new Vector2(0.24f, 0.54f), new Vector2(0.76f, 0.85f));
        visual.Icon = Layer(buttonObject, "Icon", null, new Color32(255, 248, 230, 255),
            new Vector2(0.27f, 0.3f), new Vector2(0.73f, 0.76f));
        visual.Icon.preserveAspect = true;
        visual.iconShadow = UiTheme.GetOrAddEffect<Shadow>(visual.Icon.gameObject);
        visual.iconShadow.effectColor = new Color32(43, 26, 14, 150);

        visual.Cooldown = Layer(buttonObject, "Cooldown", HudVisualUtility.CircleSprite(), new Color32(20, 14, 10, 205),
            new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
        visual.Cooldown.type = Image.Type.Filled;
        visual.Cooldown.fillMethod = Image.FillMethod.Radial360;
        visual.Cooldown.fillOrigin = (int)Image.Origin360.Top;
        visual.Cooldown.fillClockwise = false;
        visual.Cooldown.fillAmount = 0f;
        visual.Cooldown.enabled = false;

        visual.CenterText = HudVisualUtility.AddTmpText(buttonObject, "CenterText", string.Empty, 64f, TextAlignmentOptions.Center);
        Stretch(visual.CenterText.rectTransform, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.9f));

        float half = ribbonWidth * 0.5f;
        visual.Ribbon = Layer(buttonObject, "Ribbon", HudVisualUtility.RoundedSprite(), RibbonColor,
            new Vector2(0.5f - half, -0.13f), new Vector2(0.5f + half, 0.15f));
        visual.Ribbon.type = Image.Type.Sliced;
        visual.RibbonOutline = HudVisualUtility.AddOutline(visual.Ribbon.gameObject, UiTheme.Ink, new Vector2(4f, -4f));

        TMP_Text label = reuseLabel;
        if (label != null)
        {
            label.transform.SetParent(visual.Ribbon.transform, false);
            label.gameObject.SetActive(true);
            HudVisualUtility.ApplyTextStyle(label);
        }
        else
        {
            label = HudVisualUtility.AddTmpText(visual.Ribbon.gameObject, "Label", string.Empty, 30f, TextAlignmentOptions.Center);
        }
        label.name = "Label";
        label.richText = true;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.rectTransform.localScale = Vector3.one;
        label.rectTransform.localRotation = Quaternion.identity;
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        visual.RibbonText = label;
        return visual;
    }

    // call this when the button's size changes. size = the circle's width, fonts in canvas units
    public void SetTextScale(float size, float ribbonFont, float minimumRibbonFont)
    {
        float unit = size / 210f;
        RectTransform labelRect = RibbonText.rectTransform;
        labelRect.offsetMin = new Vector2(12f * unit, 3f * unit);
        labelRect.offsetMax = new Vector2(-12f * unit, -3f * unit);
        SetSize(RibbonText, ribbonFont, minimumRibbonFont);
        SetSize(CenterText, size * 0.33f, size * 0.16f);
        RibbonOutline.effectDistance = new Vector2(4f * unit, -4f * unit);
        bezelShadow.effectDistance = new Vector2(0f, -7f * unit);
        iconShadow.effectDistance = new Vector2(0f, -4f * unit);
    }

    public void SetElement(Color element, bool dim)
    {
        Color face = dim ? Color.Lerp(element, new Color(0.24f, 0.24f, 0.22f), 0.55f) : element;
        Face.color = face;
        Rim.color = Color.Lerp(face, Color.white, dim ? 0.2f : 0.5f);
        Gloss.color = new Color(1f, 1f, 1f, dim ? 0.08f : 0.2f);
    }

    static void SetSize(TMP_Text text, float max, float min)
    {
        text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(12f, max);
        text.fontSizeMin = Mathf.Clamp(min, 10f, text.fontSizeMax);
        text.fontSize = text.fontSizeMax;
    }

    static Image Layer(GameObject parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image image = HudVisualUtility.AddImage(parent, name, color, false);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        Stretch(image.rectTransform, anchorMin, anchorMax);
        image.transform.SetAsLastSibling();
        return image;
    }

    public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
