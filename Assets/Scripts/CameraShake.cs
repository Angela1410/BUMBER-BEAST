using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private const float NormalMagnitude = 0.06f;
    private const float MajorMagnitude = 0.14f;
    private const float NormalDuration = 0.16f;
    private const float MajorDuration = 0.24f;

    private Coroutine shakeRoutine;
    private Vector3 originalLocalPosition;

    public static void Shake(bool major)
    {
        // Settings > SCREEN SHAKE is off
        if (!GameSettings.ScreenShake)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        CameraShake shaker = mainCamera.GetComponent<CameraShake>();
        if (shaker == null)
            shaker = mainCamera.gameObject.AddComponent<CameraShake>();

        shaker.Play(major);
    }

    private void Play(bool major)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            transform.localPosition = originalLocalPosition;
        }

        originalLocalPosition = transform.localPosition;
        float magnitude = major ? MajorMagnitude : NormalMagnitude;
        float duration = major ? MajorDuration : NormalDuration;
        shakeRoutine = StartCoroutine(ShakeRoutine(magnitude, duration));
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float remaining = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * (magnitude * remaining);
            transform.localPosition = originalLocalPosition + (Vector3)offset;
            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        shakeRoutine = null;
    }
}
