using UnityEngine;

public class LaunchPadFeedback : MonoBehaviour
{
    [Header("Launch Pad Animation")]
    public float bounceAmount = 0.12f;
    public float animationSpeed = 14f;

    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * animationSpeed
        );

        if (Vector3.Distance(
            transform.localScale,
            targetScale
        ) < 0.01f)
        {
            transform.localScale = targetScale;
        }
    }

    public void PlayLaunchEffect()
    {
        targetScale = originalScale * (1f - bounceAmount);

        Invoke(nameof(ReturnToNormal), 0.08f);
    }

    private void ReturnToNormal()
    {
        targetScale = originalScale;
    }
}