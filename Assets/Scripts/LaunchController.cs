using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class LaunchController : MonoBehaviour
{
    public bool unlimitedLaunches = true;
    public int maxLaunches = 5;
    public int launchesLeft;

    public float launchPower = 3.6f;
    public float maxDragDistance = 2.5f;
    public float maxLaunchSpeed = 15f;

    public TMP_Text launchesText;

    [Header("Beast Sprites")]
    public Sprite idleSprite;
    public Sprite ballSprite;
    public LaunchPadFeedback launchPadFeedback;

    [Header("Launch Animation")]
    public float aimSquashAmount = 0.12f;
    public float aimStretchAmount = 0.08f;
    public float launchPopAmount = 0.18f;
    public float animationSpeed = 12f;
    public ParticleSystem chargeEffect;

    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private bool isAnimatingLaunch;

    private Rigidbody2D rb;
    private LineRenderer aimLine;
    private BeastController beastController;

    private Vector2 dragStart;
    private bool isDragging = false;

    void Start()
    {
        unlimitedLaunches = true;

        launchesLeft = Mathf.Max(0, maxLaunches);

        if (GameProgress.Instance != null)
            launchPower += GameProgress.Instance.LaunchPowerLevel * 0.75f;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        beastController = GetComponent<BeastController>();

        originalScale = transform.localScale;
        isAnimatingLaunch = false;

        if (spriteRenderer != null)
        {
            if (idleSprite == null)
                idleSprite = spriteRenderer.sprite;

            spriteRenderer.sprite = idleSprite;
        }

        if (ballSprite == null)
        {
            Debug.LogWarning(
                "LaunchController has no ball sprite assigned; the Beast will keep its idle sprite while aiming.",
                this
            );
        }

        if (GameProgress.Instance != null)
        {
            GameProgress.Instance.ApplySelectedBeast(gameObject);
            GameProgress.Instance.ApplySelectedBeastVisual(gameObject);

            if (beastController != null)
                beastController.SetActiveBeast(GameProgress.Instance.SelectedBeast);
        }

        aimLine = GetComponentInChildren<LineRenderer>();

        UpdateLaunchText();

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }

        SetIdleVisual();
    }

    void OnValidate()
    {
        UpdateLaunchText();
    }

    void Update()
    {
        UpdateLaunchAnimation();

        // Make sure the Beast looks normal whenever it is back at the launch pad.
        if (beastController != null &&
            beastController.State == BeastState.AtPad &&
            !isDragging &&
            !isAnimatingLaunch)
        {
            SetIdleVisual();
        }

        if (!unlimitedLaunches && launchesLeft <= 0)
            return;

        // Only allow aiming when the Beast is at the launch pad.
        if (beastController != null &&
            beastController.State != BeastState.AtPad)
            return;

        // Don't allow another launch while the Beast is moving.
        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            return;

        // --------------------------------
        // START AIMING
        // --------------------------------
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;

            dragStart = GetMouseWorldPosition();
            isDragging = true;

            SetAimingVisual();

            if (aimLine != null)
                aimLine.enabled = true;
        }

        // --------------------------------
        // AIMING / DRAGGING
        // --------------------------------
        if (isDragging)
        {
            Vector2 currentMousePosition = GetMouseWorldPosition();

            Vector2 launchVector = dragStart - currentMousePosition;

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            Vector2 direction = launchVector.normalized;

            // Aim line
            if (aimLine != null)
            {
                aimLine.SetPosition(0, transform.position);

                aimLine.SetPosition(
                    1,
                    (Vector2)transform.position +
                    direction * dragDistance
                );
            }

            // Choma gets slightly stretched as the player pulls farther.
            float dragRatio = Mathf.Clamp01(
                dragDistance / maxDragDistance
            );

            float squash = 1f - (dragRatio * aimSquashAmount);
            float stretch = 1f + (dragRatio * aimStretchAmount);
            if (chargeEffect != null)
{
    var emission = chargeEffect.emission;
    emission.rateOverTime = Mathf.Lerp(8f, 45f, dragRatio);
}

if (spriteRenderer != null)
{
    spriteRenderer.color = Color.Lerp(
        Color.white,
        new Color(1f, 0.75f, 0.45f),
        dragRatio
    );
}

            transform.localScale = new Vector3(
                originalScale.x * squash,
                originalScale.y * stretch,
                originalScale.z
            );
        }

        // --------------------------------
        // RELEASE / LAUNCH
        // --------------------------------
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Vector2 dragEnd = GetMouseWorldPosition();

            Vector2 launchVector = dragStart - dragEnd;

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            if (dragDistance > 0.1f)
            {
                Vector2 direction = launchVector.normalized;

                float launchSpeed = Mathf.Min(
                    dragDistance * launchPower,
                    maxLaunchSpeed
                );

                // Reset the stretched shape before launching.
                transform.localScale = originalScale;

                // Small launch "pop".
                StopChargeEffect();
PlayLaunchPop();

// Make the launch pad react.
if (launchPadFeedback != null)
    launchPadFeedback.PlayLaunchEffect();

// Launch the Beast.
rb.linearVelocity = direction * launchSpeed;

                if (!unlimitedLaunches)
                {
                    launchesLeft--;
                    UpdateLaunchText();
                }

                if (beastController != null)
                    beastController.NotifyLaunched();
            }
            else
            {
                // Player barely dragged, so cancel the launch.
                SetIdleVisual();
            }

            isDragging = false;

            if (aimLine != null)
                aimLine.enabled = false;
        }
    }

    // ============================================
    // VISUALS
    // ============================================
private void StopChargeEffect()
{
    if (chargeEffect != null)
        chargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

    if (spriteRenderer != null)
        spriteRenderer.color = Color.white;
}
    private void SetAimingVisual()
    {
        if (spriteRenderer != null && ballSprite != null)
            spriteRenderer.sprite = ballSprite;

        transform.localScale = originalScale;

        isAnimatingLaunch = false;
        if (chargeEffect != null && !chargeEffect.isPlaying)
    chargeEffect.Play();
    }
    

    private void SetIdleVisual()
    {
        if (spriteRenderer != null && idleSprite != null)
            spriteRenderer.sprite = idleSprite;

        transform.localScale = originalScale;

        isAnimatingLaunch = false;
        StopChargeEffect();
    }

    private void PlayLaunchPop()
    {
        isAnimatingLaunch = true;

        transform.localScale =
            originalScale * (1f + launchPopAmount);
    }

    private void UpdateLaunchAnimation()
    {
        if (!isAnimatingLaunch)
            return;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            originalScale,
            Time.deltaTime * animationSpeed
        );

        if (Vector3.Distance(
            transform.localScale,
            originalScale
        ) < 0.01f)
        {
            transform.localScale = originalScale;
            isAnimatingLaunch = false;
        }
    }

    // ============================================
    // CAMERA / MOUSE
    // ============================================

    Vector2 GetMouseWorldPosition()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            Debug.LogError(
                "LaunchController requires a Main Camera tagged MainCamera in the scene."
            );

            return transform.position;
        }

        Vector3 mousePosition = Input.mousePosition;

        mousePosition.z =
            -camera.transform.position.z;

        return camera.ScreenToWorldPoint(mousePosition);
    }

    // ============================================
    // LAUNCH TEXT
    // ============================================

    void UpdateLaunchText()
    {
        if (launchesText == null)
        {
            GameObject launchesObject =
                GameObject.Find("LaunchesText");

            if (launchesObject != null)
                launchesText =
                    launchesObject.GetComponent<TMP_Text>();
        }

        if (launchesText == null)
            return;

        if (unlimitedLaunches)
        {
            launchesText.text = string.Empty;
            return;
        }

        launchesText.text =
            "Launches: " + launchesLeft;
    }
}