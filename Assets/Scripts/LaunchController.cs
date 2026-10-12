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
    public float visualPullbackDistance = 0.8f; 
    public ParticleSystem chargeEffect;

    private string currentState;
    private Animator anim;
    private Vector3 originalScale;
    
    
    private Vector3 trueHomePosition; 
    
    private SpriteRenderer spriteRenderer;
    private bool isAnimatingLaunch;

    private Rigidbody2D rb;
    private LineRenderer aimLine;
    private BeastController beastController;

    private Vector2 dragStart;
    private bool isDragging = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        unlimitedLaunches = true;
        launchesLeft = Mathf.Max(0, maxLaunches);

        if (GameProgress.Instance != null)
            launchPower += GameProgress.Instance.LaunchPowerLevel * 0.75f;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        beastController = GetComponent<BeastController>();

        originalScale = transform.localScale;
        
        
        trueHomePosition = transform.position; 
        
        isAnimatingLaunch = false;

        if (spriteRenderer != null)
        {
            if (idleSprite == null)
                idleSprite = spriteRenderer.sprite;
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

        
        if (beastController != null &&
            beastController.State == BeastState.AtPad &&
            !isDragging &&
            !isAnimatingLaunch)
        {
            transform.position = trueHomePosition; 
            SetIdleVisual();
        }

        if (!unlimitedLaunches && launchesLeft <= 0)
            return;

        if (beastController != null &&
            beastController.State != BeastState.AtPad)
            return;

        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            return;

        
        
        
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

        
        
        
        if (isDragging)
        {
            Vector2 currentMousePosition = GetMouseWorldPosition();
            Vector2 launchVector = dragStart - currentMousePosition;

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            Vector2 direction = launchVector.normalized;

            float dragRatio = Mathf.Clamp01(dragDistance / maxDragDistance);

            
            transform.position = trueHomePosition - (Vector3)(direction * (dragRatio * visualPullbackDistance));

            if (aimLine != null)
            {
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(
                    1,
                    (Vector2)transform.position + direction * dragDistance
                );
            }

            if (dragRatio >= 0.8f)
            {
                ChangeAnimationState("Choma_Spin");
            }
            else
            {
                ChangeAnimationState("Choma_Curl");
            }

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
                float launchSpeed = Mathf.Min(dragDistance * launchPower, maxLaunchSpeed);

                transform.localScale = originalScale;

                
                transform.position = trueHomePosition;

                StopChargeEffect();
                PlayLaunchPop();

                if (launchPadFeedback != null)
                    launchPadFeedback.PlayLaunchEffect();

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
                
                transform.position = trueHomePosition; 
                SetIdleVisual();
            }

            isDragging = false;

            if (aimLine != null)
                aimLine.enabled = false;
        }
    }

    private void ChangeAnimationState(string newState)
    {
        if (anim == null) return;
        if (currentState == newState) return;

        anim.Play(newState);
        currentState = newState;
    }

    private void StopChargeEffect()
    {
        if (chargeEffect != null)
            chargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }
    
    private void SetAimingVisual()
    {
        ChangeAnimationState("Choma_Curl");
        transform.localScale = originalScale;
        isAnimatingLaunch = false;
        
        if (chargeEffect != null && !chargeEffect.isPlaying)
            chargeEffect.Play();

        if (launchPadFeedback != null)
            launchPadFeedback.PlayAimingEffect();
    }

    private void SetIdleVisual()
    {
        ChangeAnimationState("Choma_Idle");
        transform.localScale = originalScale;
        isAnimatingLaunch = false;
        StopChargeEffect();

        if (launchPadFeedback != null)
            launchPadFeedback.ReturnToIdle();
    }

    private void PlayLaunchPop()
    {
        ChangeAnimationState("Choma_Spin"); 
        isAnimatingLaunch = true;
        transform.localScale = originalScale * (1f + launchPopAmount);
    }

    private void UpdateLaunchAnimation()
    {
        if (!isAnimatingLaunch) return;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            originalScale,
            Time.deltaTime * animationSpeed
        );

        if (Vector3.Distance(transform.localScale, originalScale) < 0.01f)
        {
            transform.localScale = originalScale;
            isAnimatingLaunch = false;
        }
    }

    Vector2 GetMouseWorldPosition()
    {
        Camera camera = Camera.main;
        if (camera == null) return transform.position;

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = -camera.transform.position.z;
        return camera.ScreenToWorldPoint(mousePosition);
    }

    void UpdateLaunchText()
    {
        if (launchesText == null)
        {
            GameObject launchesObject = GameObject.Find("LaunchesText");
            if (launchesObject != null) launchesText = launchesObject.GetComponent<TMP_Text>();
        }

        if (launchesText == null) return;

        if (unlimitedLaunches)
        {
            launchesText.text = string.Empty;
            return;
        }

        launchesText.text = "Launches: " + launchesLeft;
    }
}