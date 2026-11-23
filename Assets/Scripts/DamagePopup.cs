using System.Collections;
using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private const float Lifetime = 0.6f;
    private const float RiseDistance = 0.8f;

    public static void Show(Vector2 worldPos, int amount, bool superEffective)
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.position = worldPos;

        TextMeshPro text = popupObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 4f;
        text.fontStyle = FontStyles.Bold;
        text.richText = true;
        text.color = superEffective
            ? new Color(1f, 0.48f, 0.08f, 1f)
            : Color.white;
        text.text = superEffective
            ? "<size=65%>SUPER EFFECTIVE!</size>\n<size=135%>" + amount + "</size>"
            : amount.ToString();

        MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
        if (textRenderer != null)
            textRenderer.sortingOrder = 50;

        popupObject.AddComponent<DamagePopup>().Animate(text, worldPos);
    }

    // shows a short word instead of a number ("BLOCKED", "OVERCHARGE!")
    public static void ShowText(Vector2 worldPos, string message, Color color)
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.position = worldPos;

        TextMeshPro text = popupObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3.2f;
        text.fontStyle = FontStyles.Bold;
        text.richText = false;
        text.color = color;
        text.text = message;

        MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
        if (textRenderer != null)
            textRenderer.sortingOrder = 50;

        popupObject.AddComponent<DamagePopup>().Animate(text, worldPos);
    }

    private void Animate(TextMeshPro text, Vector2 startPosition)
    {
        StartCoroutine(AnimateRoutine(text, startPosition));
    }

    private IEnumerator AnimateRoutine(TextMeshPro text, Vector2 startPosition)
    {
        float elapsed = 0f;
        Color initialColor = text.color;

        while (elapsed < Lifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Lifetime);
            transform.position = startPosition + Vector2.up * (RiseDistance * progress);

            Color fadedColor = initialColor;
            fadedColor.a = 1f - progress;
            text.color = fadedColor;
            yield return null;
        }

        Destroy(gameObject);
    }
}
