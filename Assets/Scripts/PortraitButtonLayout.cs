using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PortraitButtonLayout
{
    private const float MaximumWidth = 380f;
    private const float MaximumHeight = 150f;
    private const float ButtonGap = 18f;
    private const float EdgeMargin = 24f;

    public static void Apply(RectTransform buttonRect, int stackIndex)
    {
        if (buttonRect == null || !(buttonRect.parent is RectTransform canvasRect))
            return;

        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;
        if (canvasWidth <= 0f || canvasHeight <= 0f)
            return;

        float width = Mathf.Min(MaximumWidth, canvasWidth * 0.42f);
        float height = Mathf.Min(MaximumHeight, canvasHeight * 0.09f);
        float margin = Mathf.Min(EdgeMargin, canvasWidth * 0.04f);
        float gap = Mathf.Min(ButtonGap, canvasHeight * 0.012f);

        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(
            -margin,
            margin + stackIndex * (height + gap)
        );
        buttonRect.sizeDelta = new Vector2(width, height);

        foreach (Text legacyText in buttonRect.GetComponentsInChildren<Text>(true))
        {
            legacyText.resizeTextForBestFit = true;
            legacyText.resizeTextMinSize = 16;
            legacyText.resizeTextMaxSize = 28;
        }

        foreach (TMP_Text tmpText in buttonRect.GetComponentsInChildren<TMP_Text>(true))
        {
            tmpText.enableAutoSizing = true;
            tmpText.fontSizeMin = 16f;
            tmpText.fontSizeMax = 28f;
        }
    }
}
