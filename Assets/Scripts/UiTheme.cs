using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UiButtonRole
{
    Primary, // main action (orange)
    Secondary, // navigation / supporting action (teal)
    Neutral, // cards and quiet actions (dark panel)
    Success // cleared / owned (green)
}

// one place for the colors, text styles and button states used by the map, shop, chapter
// select and stage result screens, so every screen matches the HUD in the stage
// (cream bold text, dark brown outlines)
public static class UiTheme
{
    public static readonly Color32 Background = new Color32(24, 40, 27, 255);
    public static readonly Color32 Panel = new Color32(40, 49, 39, 255);
    public static readonly Color32 PanelDark = new Color32(30, 37, 30, 255);
    public static readonly Color32 PanelBorder = new Color32(137, 145, 111, 255);
    public static readonly Color32 Ink = new Color32(43, 26, 14, 255);
    public static readonly Color32 Cream = new Color32(255, 244, 214, 255);
    public static readonly Color32 MutedText = new Color32(205, 196, 168, 255);
    public static readonly Color32 Gold = new Color32(255, 204, 66, 255);
    public static readonly Color32 Orange = new Color32(237, 125, 45, 255);
    public static readonly Color32 Teal = new Color32(47, 145, 145, 255);
    public static readonly Color32 Green = new Color32(104, 173, 63, 255);
    public static readonly Color32 Locked = new Color32(85, 103, 91, 255);
    public static readonly Color32 Danger = new Color32(196, 90, 70, 255);

    public static Color RoleColor(UiButtonRole role)
    {
        switch (role)
        {
            case UiButtonRole.Secondary: return Teal;
            case UiButtonRole.Neutral: return Panel;
            case UiButtonRole.Success: return Green;
            default: return Orange;
        }
    }

    // styles a button and makes sure it has exactly one TMP label.
    // existing onClick listeners don't change
    public static TMP_Text StyleButton(Button button, UiButtonRole role, bool circular = false, float fontSize = 30f)
    {
        if (button == null)
            return null;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = circular ? HudVisualUtility.CircleSprite() : HudVisualUtility.RoundedSprite();
            image.type = circular ? Image.Type.Simple : Image.Type.Sliced;
            image.color = RoleColor(role);
            image.raycastTarget = true;
            HudVisualUtility.AddOutline(image.gameObject, Ink, new Vector2(3f, -3f));
            Shadow shadow = GetOrAddEffect<Shadow>(image.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -6f);
        }

        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 240, 210, 255);
        colors.pressedColor = new Color32(205, 200, 190, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(130, 130, 130, 200);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text label = GetOrCreateLabel(button, fontSize);
        if (button.GetComponent<UiButtonFeedback>() == null)
            button.gameObject.AddComponent<UiButtonFeedback>();
        return label;
    }

    // returns the button's one TMP label, converts an old Text label if needed,
    // and hides every other label so nothing is drawn twice
    public static TMP_Text GetOrCreateLabel(Button button, float fontSize = 30f)
    {
        if (button == null)
            return null;

        Transform labelTransform = button.transform.Find("ThemeLabel");
        TMP_Text label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (label != null)
            return label; // already styled, don't hide extra card content again

        {
            // reuse a TMP label that's already there (the stage result buttons already use TMP)
            foreach (TMP_Text candidate in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (candidate.transform.parent == button.transform)
                {
                    label = candidate;
                    break;
                }
            }
        }

        string initialText = string.Empty;
        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
            initialText = legacy.text;

        if (label == null)
        {
            GameObject labelObject = new GameObject("ThemeLabel", typeof(RectTransform));
            labelObject.transform.SetParent(button.transform, false);
            label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = initialText;
        }

        label.gameObject.name = "ThemeLabel";
        label.gameObject.SetActive(true);
        StyleLabel(label, fontSize, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);
        rect.localScale = Vector3.one;

        PortraitButtonLayout.HideOtherLabels(button.transform, label);
        return label;
    }

    public static void SetLabel(Button button, string text)
    {
        if (button == null)
            return;

        TMP_Text label = GetOrCreateLabel(button);
        if (label != null && label.text != text)
            label.text = text;
    }

    public static void StyleLabel(TMP_Text label, float fontSize, TextAlignmentOptions alignment)
    {
        if (label == null)
            return;

        HudVisualUtility.ApplyTextStyle(label);
        label.alignment = alignment;
        label.enableAutoSizing = true;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Min(18f, fontSize);
        label.fontSize = fontSize;
        label.raycastTarget = false;
    }

    public static Image StylePanel(GameObject target, Color color, Color border, float borderWidth = 3f)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.AddComponent<Image>();

        image.sprite = HudVisualUtility.RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        HudVisualUtility.AddOutline(target, border, new Vector2(borderWidth, -borderWidth));
        Shadow shadow = GetOrAddEffect<Shadow>(target);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -8f);
        return image;
    }

    // Outline and Shadow both come from Shadow, so GetComponent<Shadow>() can return the Outline.
    // so we look for the exact type instead
    public static T GetOrAddEffect<T>(GameObject target) where T : Shadow
    {
        foreach (Shadow effect in target.GetComponents<Shadow>())
        {
            if (effect.GetType() == typeof(T))
                return (T)effect;
        }

        return target.AddComponent<T>();
    }

    // plain text on purpose: the HUD font has no coin or bullet symbol
    public static string Coin(int amount)
    {
        return "<color=#FFCC42>" + amount + " COINS</color>";
    }
}
