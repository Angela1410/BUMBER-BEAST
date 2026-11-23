using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PortraitButtonLayout
{
    private const float AbilitySize = 210f;
    private const float SwapSize = 156f;
    private const float ButtonGap = 66f; // room for the swap button's name ribbon
    private const float RightInset = 80f;
    private const float BottomInset = 150f;

    // puts a HUD button in the bottom right corner.
    // stackIndex 0 = skill button (bottom), 1 = swap button (right above it).
    // returns false if the canvas has no size yet, so the caller can try again next frame
    public static bool Apply(RectTransform buttonRect, int stackIndex)
    {
        if (buttonRect == null || !(buttonRect.parent is RectTransform canvasRect))
            return false;

        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;
        if (canvasWidth <= 0f || canvasHeight <= 0f)
            return false;

        float referenceScale = Mathf.Min(canvasWidth / 1080f, canvasHeight / 1920f);
        float size = stackIndex == 0 ? AbilitySize * referenceScale : SwapSize * referenceScale;
        float gap = ButtonGap * referenceScale;

        Rect safeArea = Screen.safeArea;
        float widthScale = canvasWidth / Mathf.Max(1f, Screen.width);
        float heightScale = canvasHeight / Mathf.Max(1f, Screen.height);
        float safeLeft = safeArea.x * widthScale;
        float safeRight = (Screen.width - safeArea.xMax) * widthScale;
        float safeBottom = safeArea.y * heightScale;
        float safeTop = (Screen.height - safeArea.yMax) * heightScale;

        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);

        float rightInset = Mathf.Clamp(
            Mathf.Max(RightInset * referenceScale, safeRight),
            0f,
            Mathf.Max(0f, canvasWidth - size - safeLeft));
        float bottomInset = Mathf.Clamp(
            Mathf.Max(BottomInset * referenceScale, safeBottom),
            0f,
            Mathf.Max(0f, canvasHeight - size - safeTop));

        float x;
        float y;

        if (stackIndex == 0)
        {
            x = -rightInset;
            y = bottomInset;
        }
        else
        {
            // same center line as the skill button, sitting above it with a gap
            float centerOffset = (AbilitySize - SwapSize) * 0.5f * referenceScale;
            x = -rightInset - centerOffset;
            y = bottomInset + AbilitySize * referenceScale + gap;
        }

        buttonRect.anchoredPosition = new Vector2(x, y);
        buttonRect.sizeDelta = new Vector2(size, size);

        foreach (Text legacyText in buttonRect.GetComponentsInChildren<Text>(true))
        {
            legacyText.resizeTextForBestFit = true;
            legacyText.resizeTextMinSize = 16;
            legacyText.resizeTextMaxSize = 28;
        }

        return true;
    }

    // 1 on the 1080x1920 reference canvas, used to scale fonts with the layout
    public static float ReferenceScale(RectTransform buttonRect)
    {
        if (buttonRect == null || !(buttonRect.parent is RectTransform canvasRect))
            return 1f;
        float width = canvasRect.rect.width, height = canvasRect.rect.height;
        if (width <= 0f || height <= 0f)
            return 1f;
        return Mathf.Min(width / 1080f, height / 1920f);
    }

    // hides every other text under a button so only the label we keep is drawn.
    // this is what removes the doubled "FLAME BURST / SWAP BEAST" text
    public static void HideOtherLabels(Transform root, TMP_Text keep)
    {
        if (root == null)
            return;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == keep)
                continue;

            // never hide a parent of the label we're keeping
            if (keep != null && keep.transform.IsChildOf(text.transform))
                continue;

            text.gameObject.SetActive(false);
        }

        foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
            legacy.gameObject.SetActive(false);
    }
}
