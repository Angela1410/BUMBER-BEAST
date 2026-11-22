using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared pressed/disabled feedback for themed buttons. Uses unscaled time so it
// also works on result screens, where the stage pauses Time.timeScale.
[DisallowMultipleComponent]
public class UiButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float pressedScale = 0.94f;
    public float speed = 18f;

    private Button button;
    private TMP_Text label;
    private Vector3 baseScale = Vector3.one;
    private bool pressed;
    private bool lastInteractable = true;

    void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
    }

    void OnEnable()
    {
        pressed = false;
        transform.localScale = baseScale;
        ApplyInteractableState(true);
    }

    void Update()
    {
        bool interactable = button == null || button.IsInteractable();
        if (interactable != lastInteractable)
            ApplyInteractableState(false);

        Vector3 target = pressed && interactable ? baseScale * pressedScale : baseScale;
        transform.localScale = Vector3.Lerp(
            transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    // Layout code moves/resizes buttons; call this if it also changes their scale.
    public void SetBaseScale(Vector3 scale)
    {
        baseScale = scale;
    }

    void ApplyInteractableState(bool force)
    {
        bool interactable = button == null || button.IsInteractable();
        if (!force && interactable == lastInteractable)
            return;

        lastInteractable = interactable;
        if (label == null)
        {
            Transform labelTransform = transform.Find("ThemeLabel");
            label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        }

        if (label != null)
            label.alpha = interactable ? 1f : 0.55f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        if (button == null || button.IsInteractable())
            GameAudio.Play(Sfx.Click);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pressed = false;
    }
}
