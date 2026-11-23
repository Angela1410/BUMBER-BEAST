using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// art for the Beasts other than Choma. fill this in on the Launch Controller
[System.Serializable]
public class BeastSpriteSet
{
    public BeastId beast;
    public Sprite idle;
    public Sprite ball;
}

[System.Serializable]
public class BeastAnimSet
{
    public BeastId beast;
    public Sprite[] idle;
    public Sprite[] curl;
    public Sprite[] spin;
    public float fps = 10f;
    public Sprite[] skillEffect;
}

public class LaunchController : MonoBehaviour
{
    private const float TrajectoryDuration = 3f;
    private const float TrajectoryStep = 0.05f;
    private const float MaximumTrajectoryDistance = 30f;
    private const float TrajectoryDotSpacing = 0.42f;
    private const float MaximumVisibleTrajectoryDistance = 10f;
    private const float TrajectoryContactOffset = 0.02f;
    private const float MaximumVinePullOffset = 1.26f;
    private const int MaximumTrajectoryBounces = 4;

    public bool unlimitedLaunches = true;
    public int maxLaunches = 5;
    public int launchesLeft;

    public float launchPower = 3.6f;
    public float maxDragDistance = 2.5f;
    public float maxLaunchSpeed = 15f;

    public TMP_Text launchesText;

    [Header("Beast Sprites")]
    public Sprite idleSprite; // Choma's normal sprite
    public Sprite ballSprite; // Choma's curled up sprite
    public LaunchPadFeedback launchPadFeedback;
    public BeastSpriteSet[] beastSprites; // art for Thistle and the other Beasts
    public BeastAnimSet[] beastAnimations;

    [Header("Choma Animator (state names must match)")]
    public string idleState = "Choma_Idle";
    public string curlState = "Choma_Curl";
    public string spinState = "Choma_Spin";

    [Header("Launch Animation")]
    public float launchPopAmount = 0.18f;
    public float animationSpeed = 12f;
    public ParticleSystem chargeEffect;
    public TrailRenderer flightTrail; // optional, leave empty if you don't have one

    [Header("Pull-back Spin (sprite-sheet Beasts such as Thistle)")]
    [Tooltip("Spin speed (degrees/second) at a full pull. The Beast revs up as it is pulled back " +
        "and carries the spin into the launch. 0 = no spin.")]
    public float pullSpinDegreesPerSecond = 1080f;
    [Range(0.05f, 0.95f)]
    [Tooltip("Part of the pull spent curling into a ball before the spin frames take over.")]
    public float curlPortionOfPull = 0.3f;
    [Tooltip("How much faster the spin frames cycle at a full pull than in normal flight.")]
    public float pullSpinFrameBoost = 2.5f;

    public bool IsAiming => isDragging;

    // sent every time the Beast is launched from the pad
    public static event System.Action<LaunchController> Launched;

    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    private bool isAnimatingLaunch;

    private Rigidbody2D rb;
    private LineRenderer aimLine;
    private BeastController beastController;
    private Collider2D chomaCollider;
    private readonly List<Vector3> trajectoryPoints = new List<Vector3>(128);
    private readonly RaycastHit2D[] trajectoryHits = new RaycastHit2D[16];
    private SpriteRenderer[] bounceMarkers;
    private SpriteRenderer[] trajectoryDots;
    // a pulsing ring on the first bounce, and one around the bumper the shot is going to hit
    private SpriteRenderer firstBounceRing;
    private SpriteRenderer bumperHighlight;
    private float firstBounceDistance = -1f;

    private Vector2 dragStart;
    // full power at this much finger drag (in world units), so a comfortable pull is enough
    private const float FullPowerDrag = 1.9f;
    private const float EasyMinimumDrag = 0.25f;
    private const float ReleaseGuardSeconds = 0.06f;
    private Vector2 smoothedAim;
    private readonly Vector2[] aimHistory = new Vector2[10];
    private readonly float[] aimHistoryTime = new float[10];
    private int aimHistoryIndex;
    private bool aimSlowActive;
    private bool isDragging = false;
    private float currentDragRatio;
    private float idleAnimationTime;
    private float spinAnimationTime;
    private int idleFrameIndex;
    private int spinFrameIndex;
    private Vector3 aimBasePosition;
    private TMP_Text aimHintText;
    private float pullSpinAngle;
    private float pullSpinSpeed;
    private bool flightSpinActive;
    private Transform[] uprightChildren = new Transform[0];
    private float nextLockedSoundTime;

    void Start()
    {
        unlimitedLaunches = true;

        launchesLeft = Mathf.Max(0, maxLaunches);

        if (GameProgress.Instance != null)
            launchPower += GameProgress.Instance.LaunchPowerLevel * 0.75f;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        beastController = GetComponent<BeastController>();
        chomaCollider = GetComponent<Collider2D>();

        originalScale = transform.localScale;
        aimBasePosition = transform.position;
        isAnimatingLaunch = false;

        if (spriteRenderer != null)
        {
            if (idleSprite == null)
                idleSprite = spriteRenderer.sprite;

            spriteRenderer.sprite = idleSprite;
        }

        if (ballSprite == null && anim == null)
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
        ConfigureAimLine();
        CreateBounceMarkers();
        CreateTrajectoryDots();
        CacheUprightChildren();

        if (launchPadFeedback == null)
            launchPadFeedback = Object.FindAnyObjectByType<LaunchPadFeedback>();

        UpdateLaunchText();

        if (aimLine != null)
        {
            HideTrajectory();
        }

        CreateAimHint();
        SetIdleVisual();
    }

    void OnValidate()
    {
        UpdateLaunchText();
    }

    void Update()
    {
        UpdateLaunchAnimation();
        SyncAnimatorForBeast();

        // make sure the Beast looks normal whenever it's back on the launch pad
        if (beastController != null &&
            beastController.State == BeastState.AtPad &&
            !isDragging &&
            !isAnimatingLaunch)
        {
            SetIdleVisual();
        }

        UpdateBeastSpriteAnimation();

        if (!unlimitedLaunches && launchesLeft <= 0)
            return;

        // you can only aim when the Beast is on the launch pad
        if (beastController != null &&
            beastController.State != BeastState.AtPad)
        {
            if (isDragging)
                CancelAiming();
            return;
        }

        // no new launch while the Beast is still moving
        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            return;

        // --- START AIMING ---
        if (PointerDownThisFrame())
        {
            if (IsPointerOverUI())
                return;

            // the wave hasn't started yet: you have to tap READY on the plan card first
            WaveManager waves = WaveManager.Current;
            if (waves != null && waves.IsAwaitingReady)
            {
                waves.NudgeReady();
                return;
            }

            // a SWAP recall locks the next launch for a moment (the swap button shows the time left)
            if (beastController != null && beastController.IsLaunchLocked)
            {
                if (Time.unscaledTime >= nextLockedSoundTime)
                {
                    GameAudio.Play(Sfx.Denied);
                    nextLockedSoundTime = Time.unscaledTime + 0.4f;
                }
                return;
            }

            // Settings > DRAG ANYWHERE: touching anywhere on the field starts the pull, otherwise
            // you have to touch the Beast itself
            Vector2 pressPosition = GetMouseWorldPosition();
            if (!GameSettings.DragAnywhere && chomaCollider != null && !chomaCollider.OverlapPoint(pressPosition))
                return;

            isDragging = true;
            currentDragRatio = 0f;
            aimBasePosition = transform.position;
            dragStart = pressPosition;
            smoothedAim = Vector2.zero;
            for (int index = 0; index < aimHistoryTime.Length; index++)
                aimHistoryTime[index] = -1f;
            BeginAimSlowMotion();
            GameAudio.Play(Sfx.Stretch);
            if (launchPadFeedback == null)
                launchPadFeedback = Object.FindAnyObjectByType<LaunchPadFeedback>();

            SetAimingVisual();
        }

        // --- AIMING / DRAGGING ---
        if (isDragging)
        {
            if (launchPadFeedback == null)
                launchPadFeedback = Object.FindAnyObjectByType<LaunchPadFeedback>();

            Vector2 currentMousePosition = GetMouseWorldPosition();

            // the aim is where the drag started minus where the finger is now, so pulling down aims up.
            // it moves toward the finger smoothly (Settings > AIM SENSITIVITY, low = steadier),
            // using unscaled time so it feels the same in slow motion
            Vector2 rawAim = dragStart - currentMousePosition;
            smoothedAim = Vector2.Lerp(smoothedAim, rawAim,
                1f - Mathf.Exp(-GameSettings.AimResponse * Time.unscaledDeltaTime));
            RecordAim(smoothedAim);
            // turn the drag into a launch direction and strength
            Vector2 launchVector = AimToLaunch(smoothedAim);

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            Vector2 direction = launchVector.normalized;
            if (launchVector.sqrMagnitude < 0.0001f)
                direction = Vector2.up;
            // aim assist: a line that just misses a bumper gets pulled onto it
            direction = ApplyBumperMagnet(direction);

            float pullDown = Mathf.Clamp(
                dragStart.y - currentMousePosition.y,
                0f,
                MaximumVinePullOffset);
            Vector3 pulledPosition = aimBasePosition + Vector3.down * pullDown;
            pulledPosition.z = aimBasePosition.z;
            if (rb != null)
            {
                rb.position = pulledPosition;
                rb.linearVelocity = Vector2.zero;
            }
            transform.position = pulledPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = originalScale;

            float dragRatio = Mathf.Clamp01(
                dragDistance / maxDragDistance
            );
            currentDragRatio = dragRatio;

            // Beasts made from sprite sheets spin faster as you pull them back
            if (SpinsOnPull())
            {
                float spinRatio = Mathf.InverseLerp(curlPortionOfPull, 1f, dragRatio);
                pullSpinSpeed = pullSpinDegreesPerSecond * spinRatio * spinRatio;
                pullSpinAngle = Mathf.Repeat(pullSpinAngle + pullSpinSpeed * Time.deltaTime, 360f);
                ApplySpinRotation();
            }

            // Choma's curl follows the pull (frozen and moved along by the drag distance)
            if (UseAnimator())
            {
                anim.speed = 0f;
                anim.Play(curlState, 0, Mathf.Clamp(dragRatio, 0f, 0.99f));
            }

            float launchSpeed = Mathf.Min(dragDistance * launchPower, maxLaunchSpeed);
            if (beastController != null)
                launchSpeed = Mathf.Min(launchSpeed, beastController.maximumSpeed);
            UpdateTrajectory(direction, launchSpeed);

            // keep the vine attached to the Beast while it follows the finger
            if (launchPadFeedback != null)
                launchPadFeedback.SetPull(dragRatio, pulledPosition, Vector2.down, spriteRenderer);

            if (chargeEffect != null)
            {
                var emission = chargeEffect.emission;
                emission.rateOverTime = Mathf.Lerp(8f, 45f, dragRatio);
            }

            // keep each Beast's normal colors while you pull it
            if (spriteRenderer != null)
                spriteRenderer.color = CurrentBaseColor();

        }

        // --- RELEASE / LAUNCH ---
        // when the finger lifts, we launch the Beast
        if (PointerUpThisFrame() && isDragging)
        {
            // use the aim from just before the finger lifted, because lifting often slides it a little
            Vector2 launchVector = AimToLaunch(AimBeforeRelease());
            EndAimSlowMotion();

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            // only launch if the player really pulled back
            if (dragDistance > 0.1f)
            {
                // the same bumper magnet as the aim line, so the shot goes where the line showed
                Vector2 direction = ApplyBumperMagnet(launchVector.normalized);

                // a longer pull = a faster launch, up to the max speed
                float launchSpeed = Mathf.Min(
                    dragDistance * launchPower,
                    maxLaunchSpeed
                );

                // launch from where it was pulled to, so the Beast doesn't snap back first
                transform.localScale = originalScale;
                // a spinning Beast keeps spinning in the air and slows down there
                flightSpinActive = SpinsOnPull() && pullSpinSpeed > 0f;
                if (!flightSpinActive)
                    transform.rotation = Quaternion.identity;
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;

                StopChargeEffect();

                // small launch "pop"
                PlayLaunchPop();
                GameAudio.Play(Sfx.Launch, Mathf.Lerp(0.6f, 1f, dragDistance / Mathf.Max(0.01f, maxDragDistance)));

                // make the launch pad snap
                if (launchPadFeedback != null)
                    launchPadFeedback.PlayLaunchEffect();

                // Choma spins while flying
                if (UseAnimator())
                {
                    anim.speed = 1f;
                    anim.Play(spinState);
                }

                // start the flight trail, if there is one
                if (flightTrail != null)
                {
                    flightTrail.Clear();
                    flightTrail.emitting = true;
                }

                // launch! give the Beast its speed in the aim direction, then the physics does the flying and bouncing
                rb.linearVelocity = direction * launchSpeed;

                if (!unlimitedLaunches)
                {
                    launchesLeft--;
                    UpdateLaunchText();
                }

                if (beastController != null)
                    beastController.NotifyLaunched();

                if (aimHintText != null)
                    aimHintText.gameObject.SetActive(false);

                // combos and the tutorial start counting again on every launch
                StageStats.OnLaunch();
                if (Launched != null)
                    Launched(this);
            }
            else
            {
                // the player barely dragged, so cancel the launch
                if (launchPadFeedback != null)
                    launchPadFeedback.ReturnToIdle();

                SetIdleVisual();
            }

            isDragging = false;
            HideTrajectory();
        }
    }

    private void CreateAimHint()
    {
        // the stage 1 tutorial already teaches aiming, this hint is for players who skipped it
        if (SceneManager.GetActiveScene().name != "stage-1" ||
            (GameProgress.Instance != null && GameProgress.Instance.HighestClearedStage > 0) ||
            !TutorialDirector.IsDone(TutorialDirector.Stage1Key))
            return;

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        aimHintText = HudVisualUtility.AddTmpText(
            canvas.gameObject,
            "AimInstructionHint",
            GameSettings.DragAnywhere ? "DRAG ANYWHERE TO AIM  -  RELEASE TO LAUNCH" : "DRAG TO AIM  -  RELEASE TO LAUNCH",
            32f,
            TextAlignmentOptions.Center);
        aimHintText.enableAutoSizing = true;
        aimHintText.fontSizeMin = 18f;
        aimHintText.fontSizeMax = 32f;
        aimHintText.alignment = TextAlignmentOptions.Center;

        Rect safeArea = Screen.safeArea;
        float widthScale = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
        float heightScale = canvasRect.rect.height / Mathf.Max(1f, Screen.height);
        float safeLeft = safeArea.x * widthScale;
        float safeRight = (Screen.width - safeArea.xMax) * widthScale;
        float safeTop = (Screen.height - safeArea.yMax) * heightScale;

        aimHintText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        aimHintText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        aimHintText.rectTransform.pivot = new Vector2(0.5f, 1f);
        aimHintText.rectTransform.sizeDelta = new Vector2(
            Mathf.Min(900f, Mathf.Max(0f, canvasRect.rect.width - safeLeft - safeRight)), 76f);
        aimHintText.rectTransform.anchoredPosition = new Vector2(
            (safeLeft - safeRight) * 0.5f,
            -235f - safeTop);
    }

    // --- ART AND COLOR FOR EACH BEAST ---

    BeastId CurrentBeast()
    {
        return beastController != null
            ? beastController.ActiveBeast
            : BeastId.Choma;
    }

    // only Choma has an Animator, the other Beasts use plain sprites
    bool UseAnimator()
    {
        return anim != null &&
               anim.enabled &&
               anim.runtimeAnimatorController != null &&
               CurrentBeast() == BeastId.Choma;
    }

    void SyncAnimatorForBeast()
    {
        if (anim == null)
            return;

        bool shouldRun = CurrentBeast() == BeastId.Choma;
        if (anim.enabled != shouldRun)
            anim.enabled = shouldRun;
    }

    BeastSpriteSet GetSpriteSet()
    {
        if (beastController == null || beastSprites == null)
            return null;

        foreach (BeastSpriteSet set in beastSprites)
        {
            if (set != null && set.beast == beastController.ActiveBeast)
                return set;
        }

        return null;
    }

    BeastAnimSet GetAnimationSet()
    {
        return GetAnimationSet(CurrentBeast());
    }

    BeastAnimSet GetAnimationSet(BeastId beast)
    {
        if (beastAnimations != null)
        {
            foreach (BeastAnimSet set in beastAnimations)
            {
                if (set != null && set.beast == beast)
                    return set;
            }
        }

        return GetPlaceholderAnimationSet(beast);
    }

    // Mossback, Voltis and Ripple use the pictures in Assets/Resources/Placeholders (Mossback and Voltis
    // got their painted art in v1.4, saved over the old placeholders with the same names, from
    // Assets/Art/NewBeasts): idle, plus the ball for curling and spinning (the pull back spin turns
    // the whole sprite). art we assign always wins
    private static readonly Dictionary<BeastId, BeastAnimSet> placeholderAnimationSets =
        new Dictionary<BeastId, BeastAnimSet>();

    BeastAnimSet GetPlaceholderAnimationSet(BeastId beast)
    {
        if (PlaceholderArt.HasPaintedArt(beast) || HasAssignedSprites(beast))
            return null;
        if (placeholderAnimationSets.TryGetValue(beast, out BeastAnimSet cached))
            return cached;

        Sprite idle = PlaceholderArt.BeastIdle(beast);
        Sprite ball = PlaceholderArt.BeastBall(beast);
        Sprite curled = ball != null ? ball : idle;
        BeastAnimSet placeholder = idle == null ? null : new BeastAnimSet
        {
            beast = beast,
            idle = new[] { idle },
            curl = new[] { curled },
            spin = new[] { curled },
            fps = 10f
        };
        placeholderAnimationSets[beast] = placeholder;
        return placeholder;
    }

    bool HasAssignedSprites(BeastId beast)
    {
        if (beastSprites == null)
            return false;
        foreach (BeastSpriteSet set in beastSprites)
        {
            if (set != null && set.beast == beast && set.idle != null)
                return true;
        }
        return false;
    }

    Sprite CurrentIdleSprite()
    {
        BeastAnimSet animationSet = GetAnimationSet();
        if (animationSet != null && animationSet.idle != null &&
            animationSet.idle.Length > 0 && animationSet.idle[0] != null)
            return animationSet.idle[0];

        BeastSpriteSet set = GetSpriteSet();
        return (set != null && set.idle != null) ? set.idle : idleSprite;
    }

    Sprite CurrentBallSprite()
    {
        BeastAnimSet animationSet = GetAnimationSet();
        if (animationSet != null && animationSet.curl != null &&
            animationSet.curl.Length > 0 && animationSet.curl[0] != null)
            return animationSet.curl[0];

        BeastSpriteSet set = GetSpriteSet();
        return (set != null && set.ball != null) ? set.ball : ballSprite;
    }

    // a picture of the Beast for HUD portraits (the swap button). null if it has no art yet
    public Sprite GetPortraitSprite(BeastId beast)
    {
        BeastAnimSet animationSet = GetAnimationSet(beast);
        if (animationSet != null && animationSet.idle != null &&
            animationSet.idle.Length > 0 && animationSet.idle[0] != null)
            return animationSet.idle[0];

        if (beastSprites != null)
        {
            foreach (BeastSpriteSet set in beastSprites)
            {
                if (set != null && set.beast == beast && set.idle != null)
                    return set.idle;
            }
        }

        return beast == BeastId.Choma ? idleSprite : null;
    }

    public Sprite[] GetSkillEffectFrames(BeastId beast)
    {
        BeastAnimSet animationSet = GetAnimationSet(beast);
        return animationSet != null ? animationSet.skillEffect : null;
    }

    Color CurrentBaseColor()
    {
        BeastSpriteSet set = GetSpriteSet();
        BeastAnimSet animationSet = GetAnimationSet();

        // real art keeps its own colors, so we only tint placeholders
        if ((set != null && set.idle != null) || animationSet != null)
            return Color.white;

        return beastController != null
            ? BeastRoster.GetColor(beastController.ActiveBeast)
            : Color.white;
    }

    // PadSwapSystem calls this when the player swaps Beasts
    public void PlaySwapPop()
    {
        SyncAnimatorForBeast();
        ResetBeastSpriteAnimation();

        if (spriteRenderer != null)
        {
            if (UseAnimator())
            {
                anim.speed = 1f;
                anim.Play(idleState);
            }
            else
            {
                Sprite idle = CurrentIdleSprite();
                if (idle != null)
                    spriteRenderer.sprite = idle;
            }

            spriteRenderer.color = CurrentBaseColor();
        }

        PlayLaunchPop();
    }

    bool SpinsOnPull()
    {
        return pullSpinDegreesPerSecond > 0f && !UseAnimator() && GetAnimationSet() != null;
    }

    // after launching: keep the pull back spin, slow it down, and stop exactly upright
    // (at the next full turn) so the Beast never lands tilted
    void UpdateFlightSpin()
    {
        if (!flightSpinActive)
            return;

        const float minimumSpeed = 360f;
        float slowdown = Mathf.Max(minimumSpeed, pullSpinDegreesPerSecond) * 1.5f;
        pullSpinSpeed = Mathf.Max(minimumSpeed, pullSpinSpeed - slowdown * Time.deltaTime);
        float nextAngle = pullSpinAngle + pullSpinSpeed * Time.deltaTime;
        if (pullSpinSpeed <= minimumSpeed && nextAngle >= 360f)
        {
            ClearSpin();
            return;
        }

        pullSpinAngle = nextAngle >= 360f ? nextAngle - 360f : nextAngle;
        ApplySpinRotation();
    }

    void ClearSpin()
    {
        flightSpinActive = false;
        pullSpinSpeed = 0f;
        pullSpinAngle = 0f;
        transform.rotation = Quaternion.identity;
        KeepChildrenUpright();
    }

    void ApplySpinRotation()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, -pullSpinAngle);
        KeepChildrenUpright();
    }

    // only the Beast's own sprite turns, otherwise particles attached to it would spin around too
    void KeepChildrenUpright()
    {
        for (int i = 0; i < uprightChildren.Length; i++)
        {
            if (uprightChildren[i] != null)
                uprightChildren[i].rotation = Quaternion.identity;
        }
    }

    void CacheUprightChildren()
    {
        List<Transform> children = new List<Transform>();
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.transform != transform && !children.Contains(particles.transform))
                children.Add(particles.transform);
        }
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Trajectory") && !children.Contains(child))
                children.Add(child);
        }
        uprightChildren = children.ToArray();
    }

    void ResetBeastSpriteAnimation()
    {
        currentDragRatio = 0f;
        idleAnimationTime = 0f;
        spinAnimationTime = 0f;
        idleFrameIndex = 0;
        spinFrameIndex = 0;
    }

    void UpdateBeastSpriteAnimation()
    {
        if (UseAnimator() || spriteRenderer == null)
            return;

        BeastAnimSet animationSet = GetAnimationSet();
        if (animationSet == null)
            return;

        spriteRenderer.color = CurrentBaseColor();

        if (isDragging)
        {
            Sprite[] curlFrames = animationSet.curl;
            Sprite[] spinFrames = animationSet.spin;
            bool canSpin = SpinsOnPull() && spinFrames != null && spinFrames.Length > 0;

            // first part of the pull: curl into a ball. after that: the spin frames, going
            // faster the further the Beast is pulled
            if (canSpin && currentDragRatio > curlPortionOfPull)
            {
                float spinRatio = Mathf.InverseLerp(curlPortionOfPull, 1f, currentDragRatio);
                float pullFps = Mathf.Max(1f, animationSet.fps) *
                    Mathf.Lerp(0.6f, Mathf.Max(1f, pullSpinFrameBoost), spinRatio);
                spinAnimationTime += Time.deltaTime;
                float pullFrameDuration = 1f / pullFps;
                if (spinAnimationTime >= pullFrameDuration)
                {
                    int steps = Mathf.FloorToInt(spinAnimationTime / pullFrameDuration);
                    spinAnimationTime -= steps * pullFrameDuration;
                    spinFrameIndex = (spinFrameIndex + steps) % spinFrames.Length;
                }
                if (spinFrames[spinFrameIndex] != null)
                    spriteRenderer.sprite = spinFrames[spinFrameIndex];
                return;
            }

            if (curlFrames == null || curlFrames.Length == 0)
                return;

            float curlRatio = canSpin
                ? Mathf.Clamp01(currentDragRatio / curlPortionOfPull)
                : currentDragRatio;
            int frameIndex = Mathf.Clamp(
                Mathf.RoundToInt(curlRatio * (curlFrames.Length - 1)),
                0,
                curlFrames.Length - 1);
            if (curlFrames[frameIndex] != null)
                spriteRenderer.sprite = curlFrames[frameIndex];
            return;
        }

        bool atPad = beastController == null ||
            beastController.State == BeastState.AtPad;
        if (!atPad)
            UpdateFlightSpin();
        Sprite[] frames = atPad ? animationSet.idle : animationSet.spin;
        if (frames == null || frames.Length == 0)
            return;

        float frameDuration = 1f / Mathf.Max(1f, animationSet.fps);
        if (atPad)
        {
            idleAnimationTime += Time.deltaTime;
            if (idleAnimationTime >= frameDuration)
            {
                int steps = Mathf.FloorToInt(idleAnimationTime / frameDuration);
                idleAnimationTime -= steps * frameDuration;
                idleFrameIndex = (idleFrameIndex + steps) % frames.Length;
            }

            if (frames[idleFrameIndex] != null)
                spriteRenderer.sprite = frames[idleFrameIndex];
        }
        else
        {
            spinAnimationTime += Time.deltaTime;
            if (spinAnimationTime >= frameDuration)
            {
                int steps = Mathf.FloorToInt(spinAnimationTime / frameDuration);
                spinAnimationTime -= steps * frameDuration;
                spinFrameIndex = (spinFrameIndex + steps) % frames.Length;
            }

            if (frames[spinFrameIndex] != null)
                spriteRenderer.sprite = frames[spinFrameIndex];
        }
    }

    // --- VISUALS ---

    private void StopChargeEffect()
    {
        if (chargeEffect != null)
            chargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (spriteRenderer != null)
            spriteRenderer.color = CurrentBaseColor();
    }

    private void ConfigureAimLine()
    {
        if (aimLine == null)
            return;

        aimLine.positionCount = 0;
        aimLine.startWidth = 0.055f;
        aimLine.endWidth = 0.035f;
        aimLine.numCornerVertices = 4;
        aimLine.numCapVertices = 4;
        aimLine.sortingOrder = 20;
        aimLine.startColor = new Color(0.55f, 1f, 0.28f, 1f);
        aimLine.endColor = new Color(0.3f, 0.85f, 0.2f, 0.55f);
        aimLine.useWorldSpace = true;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null &&
            (aimLine.sharedMaterial == null || aimLine.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
        {
            aimLine.sharedMaterial = new Material(spriteShader);
        }
    }

    private void CreateBounceMarkers()
    {
        if (aimLine == null)
            return;

        Sprite markerSprite = HudVisualUtility.CircleSprite();
        bounceMarkers = new SpriteRenderer[MaximumTrajectoryBounces];

        for (int i = 0; i < bounceMarkers.Length; i++)
        {
            GameObject marker = new GameObject("TrajectoryBounceMarker" + (i + 1));
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * 0.16f;

            SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = markerSprite;
            markerRenderer.color = new Color32(255, 232, 96, 255);
            markerRenderer.sortingLayerID = aimLine.sortingLayerID;
            markerRenderer.sortingOrder = aimLine.sortingOrder + 1;
            markerRenderer.enabled = false;
            bounceMarkers[i] = markerRenderer;
        }

        firstBounceRing = CreateTrajectoryRing("TrajectoryFirstBounce", new Color32(255, 232, 96, 255));
        bumperHighlight = CreateTrajectoryRing("TrajectoryBumperHighlight", new Color32(255, 255, 255, 230));
    }

    SpriteRenderer CreateTrajectoryRing(string ringName, Color color)
    {
        GameObject ring = new GameObject(ringName);
        ring.transform.SetParent(transform, false);
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = HudVisualUtility.RingSprite();
        ringRenderer.color = color;
        ringRenderer.sortingLayerID = aimLine.sortingLayerID;
        ringRenderer.sortingOrder = aimLine.sortingOrder + 2;
        ringRenderer.enabled = false;
        return ringRenderer;
    }

    // places a ring at a spot, this many world units across (whatever the Beast's own scale is)
    void PlaceRing(SpriteRenderer ring, Vector2 at, float diameter, Color color)
    {
        if (ring == null)
            return;
        float spriteSize = ring.sprite != null ? Mathf.Max(0.01f, ring.sprite.bounds.size.x) : 1f;
        float parentScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        ring.transform.position = new Vector3(at.x, at.y, aimBasePosition.z - 0.12f);
        ring.transform.rotation = Quaternion.identity;
        ring.transform.localScale = Vector3.one * (diameter / spriteSize / parentScale);
        ring.color = color;
        ring.enabled = true;
    }

    private void CreateTrajectoryDots()
    {
        if (aimLine == null)
            return;

        int dotCount = Mathf.CeilToInt(MaximumVisibleTrajectoryDistance / TrajectoryDotSpacing);
        trajectoryDots = new SpriteRenderer[dotCount];
        Sprite dotSprite = HudVisualUtility.CircleSprite();

        for (int i = 0; i < trajectoryDots.Length; i++)
        {
            GameObject dot = new GameObject("TrajectoryDot" + (i + 1));
            dot.transform.SetParent(transform, false);

            SpriteRenderer dotRenderer = dot.AddComponent<SpriteRenderer>();
            dotRenderer.sprite = dotSprite;
            dotRenderer.color = new Color32(255, 246, 202, 220);
            dotRenderer.sortingLayerID = aimLine.sortingLayerID;
            dotRenderer.sortingOrder = aimLine.sortingOrder;
            dotRenderer.enabled = false;
            trajectoryDots[i] = dotRenderer;
        }
    }

    private void UpdateTrajectory(Vector2 launchDirection, float launchSpeed)
    {
        if (aimLine == null || launchSpeed <= 0f || trajectoryDots == null)
        {
            HideTrajectory();
            return;
        }

        trajectoryPoints.Clear();
        Vector2 position = rb != null ? rb.position : (Vector2)aimBasePosition;
        Vector2 colliderOffset = chomaCollider != null
            ? (Vector2)chomaCollider.bounds.center - position
            : Vector2.zero;
        float radius = GetTrajectoryRadius();
        float speedLimit = beastController != null
            ? Mathf.Min(maxLaunchSpeed, beastController.maximumSpeed)
            : maxLaunchSpeed;
        Vector2 velocity = launchDirection * Mathf.Min(launchSpeed, speedLimit);
        float remainingTime = TrajectoryDuration;
        float traveledDistance = 0f;
        int bounceCount = 0;
        int markerCount = 0;
        int collisionMask = Physics2D.GetLayerCollisionMask(gameObject.layer);
        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(collisionMask);
        contactFilter.useTriggers = false;

        trajectoryPoints.Add(new Vector3(position.x, position.y, aimBasePosition.z - 0.05f));

        for (int i = 0; i < bounceMarkers.Length; i++)
            bounceMarkers[i].enabled = false;
        firstBounceDistance = -1f;
        Bumper aimedBumper = null;

        while (remainingTime > 0f && traveledDistance < MaximumTrajectoryDistance)
        {
            float stepTime = Mathf.Min(TrajectoryStep, remainingTime);
            velocity += Physics2D.gravity * (rb != null ? rb.gravityScale : 0f) * stepTime;
            if (rb != null)
                velocity /= 1f + Mathf.Max(0f, rb.linearDamping) * stepTime;

            float timeLeftInStep = stepTime;
            while (timeLeftInStep > 0.0001f &&
                   traveledDistance < MaximumTrajectoryDistance)
            {
                float speed = velocity.magnitude;
                if (speed <= 0.001f)
                {
                    timeLeftInStep = 0f;
                    remainingTime = 0f;
                    break;
                }

                Vector2 direction = velocity / speed;
                float distance = Mathf.Min(
                    speed * timeLeftInStep,
                    MaximumTrajectoryDistance - traveledDistance);
                int hitCount = Physics2D.CircleCast(
                    position + colliderOffset,
                    radius,
                    direction,
                    contactFilter,
                    trajectoryHits,
                    distance);

                int nearestHitIndex = -1;
                float nearestHitDistance = float.PositiveInfinity;
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hitCollider = trajectoryHits[i].collider;
                    if (hitCollider == null || hitCollider.isTrigger ||
                        hitCollider == chomaCollider ||
                        hitCollider.attachedRigidbody == rb)
                        continue;

                    if (trajectoryHits[i].distance < nearestHitDistance)
                    {
                        nearestHitDistance = trajectoryHits[i].distance;
                        nearestHitIndex = i;
                    }
                }

                if (nearestHitIndex < 0)
                {
                    position += direction * distance;
                    traveledDistance += distance;
                    timeLeftInStep = 0f;
                    AddTrajectoryPoint(position);
                    continue;
                }

                RaycastHit2D hit = trajectoryHits[nearestHitIndex];
                float hitDistance = Mathf.Clamp(hit.distance, 0f, distance);
                position += direction * hitDistance;
                traveledDistance += hitDistance;
                AddTrajectoryPoint(position);

                if (markerCount < bounceMarkers.Length &&
                    traveledDistance <= MaximumVisibleTrajectoryDistance)
                {
                    // the first bounce gets a big bright dot (and a ring, below), later ones small ones
                    bool first = markerCount == 0;
                    SpriteRenderer marker = bounceMarkers[markerCount++];
                    marker.transform.position = new Vector3(
                        hit.point.x,
                        hit.point.y,
                        aimBasePosition.z - 0.1f);
                    marker.transform.localScale = Vector3.one * (first ? 0.22f : 0.12f) /
                        Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
                    marker.color = first ? new Color32(255, 232, 96, 255) : new Color32(255, 232, 96, 150);
                    marker.enabled = true;
                    if (first)
                        firstBounceDistance = traveledDistance;
                }

                // the first bumper on the path lights up, so you know the shot will hit it
                if (aimedBumper == null && traveledDistance <= MaximumVisibleTrajectoryDistance)
                {
                    Bumper hitBumper = hit.collider.GetComponentInParent<Bumper>();
                    if (hitBumper != null && !hitBumper.isBramble)
                        aimedBumper = hitBumper;
                }

                float timeToHit = hitDistance / speed;
                timeLeftInStep = Mathf.Max(0f, timeLeftInStep - timeToHit);
                bounceCount++;

                float restitution = GetBounceFactor(hit.collider);
                if (bounceCount >= MaximumTrajectoryBounces || restitution <= 0.01f)
                {
                    remainingTime = 0f;
                    timeLeftInStep = 0f;
                    break;
                }

                velocity = Vector2.Reflect(velocity, hit.normal) * restitution;
                ApplyBumperEffect(hit.collider, position, hit.normal, ref velocity);
                position += hit.normal * TrajectoryContactOffset;
                if (hitDistance <= TrajectoryContactOffset)
                {
                    float escapeTime = Mathf.Min(timeLeftInStep, 0.002f);
                    timeLeftInStep -= escapeTime;
                }
            }

            remainingTime = Mathf.Max(0f, remainingTime - stepTime);
        }

        aimLine.positionCount = 0;
        aimLine.enabled = false;
        PlaceTrajectoryDots();

        for (int i = markerCount; i < bounceMarkers.Length; i++)
            bounceMarkers[i].enabled = false;

        // a pulsing ring on the first bounce, and one around the bumper the shot will hit
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
        if (markerCount > 0)
            PlaceRing(firstBounceRing, bounceMarkers[0].transform.position, 0.46f + 0.08f * pulse,
                new Color(1f, 0.91f, 0.38f, 0.75f + 0.25f * pulse));
        else if (firstBounceRing != null)
            firstBounceRing.enabled = false;

        if (aimedBumper != null)
        {
            float size = (CameraFit.WorldRadius(aimedBumper, 0.55f) + 0.14f) * 2f;
            PlaceRing(bumperHighlight, aimedBumper.transform.position, size * (1f + 0.06f * pulse),
                new Color(1f, 1f, 1f, 0.55f + 0.4f * pulse));
        }
        else if (bumperHighlight != null)
            bumperHighlight.enabled = false;
    }

    private void PlaceTrajectoryDots()
    {
        int dotIndex = 0;
        float targetDistance = TrajectoryDotSpacing;
        float distanceAlongPath = 0f;

        for (int i = 1; i < trajectoryPoints.Count && dotIndex < trajectoryDots.Length; i++)
        {
            Vector3 start = trajectoryPoints[i - 1];
            Vector3 end = trajectoryPoints[i];
            float segmentLength = Vector2.Distance(start, end);

            while (segmentLength > 0f &&
                   targetDistance <= distanceAlongPath + segmentLength &&
                   targetDistance <= MaximumVisibleTrajectoryDistance &&
                   dotIndex < trajectoryDots.Length)
            {
                float segmentPosition = (targetDistance - distanceAlongPath) / segmentLength;
                SpriteRenderer dot = trajectoryDots[dotIndex++];
                dot.transform.position = Vector3.Lerp(start, end, segmentPosition);

                float progress = targetDistance / MaximumVisibleTrajectoryDistance;
                float size = Mathf.Lerp(0.09f, 0.045f, progress);
                dot.transform.localScale = Vector3.one * size;
                Color color = new Color(1f, 0.96f, 0.79f, Mathf.Lerp(0.9f, 0.25f, progress));
                // after the first bounce the dots fade, so the first part of the shot stands out
                if (firstBounceDistance >= 0f && targetDistance > firstBounceDistance)
                    color.a *= 0.5f;
                dot.color = color;
                dot.enabled = true;
                targetDistance += TrajectoryDotSpacing;
            }

            distanceAlongPath += segmentLength;
            if (distanceAlongPath >= MaximumVisibleTrajectoryDistance)
                break;
        }

        for (int i = dotIndex; i < trajectoryDots.Length; i++)
            trajectoryDots[i].enabled = false;
    }

    private float GetTrajectoryRadius()
    {
        if (chomaCollider == null)
            return 0f;

        CircleCollider2D circle = chomaCollider as CircleCollider2D;
        if (circle == null)
            return Mathf.Max(chomaCollider.bounds.extents.x, chomaCollider.bounds.extents.y);

        Vector3 scale = circle.transform.lossyScale;
        return circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }

    private float GetBounceFactor(Collider2D hitCollider)
    {
        PhysicsMaterial2D chomaMaterial = chomaCollider != null
            ? chomaCollider.sharedMaterial
            : null;
        PhysicsMaterial2D obstacleMaterial = hitCollider.sharedMaterial;
        float chomaBounce = chomaMaterial != null ? chomaMaterial.bounciness : 0f;
        float obstacleBounce = obstacleMaterial != null ? obstacleMaterial.bounciness : 0f;
        return Mathf.Max(chomaBounce, obstacleBounce);
    }

    private void ApplyBumperEffect(
        Collider2D hitCollider,
        Vector2 hitPosition,
        Vector2 hitNormal,
        ref Vector2 velocity)
    {
        Bumper bumper = hitCollider.GetComponentInParent<Bumper>();
        if (bumper == null)
            return;

        if (bumper.isBramble)
        {
            velocity *= bumper.brambleSlowMultiplier;
        }
        else if (bumper.type == BumperType.Blue)
        {
            velocity *= bumper.blueSlowMultiplier;
        }
        else if (bumper.type == BumperType.Green)
        {
            Vector2 awayFromBumper = hitPosition - (Vector2)bumper.transform.position;
            if (awayFromBumper.sqrMagnitude <= 0.0001f)
                awayFromBumper = hitNormal;
            if (awayFromBumper.sqrMagnitude <= 0.0001f)
                awayFromBumper = Vector2.up;
            awayFromBumper.Normalize();

            float currentSpeed = velocity.magnitude;
            if (currentSpeed <= 0.01f)
            {
                velocity = awayFromBumper;
                currentSpeed = 0f;
            }
            else if (Vector2.Dot(velocity, awayFromBumper) <= 0f)
            {
                velocity = Vector2.Reflect(velocity, awayFromBumper);
            }

            if (Vector2.Dot(velocity, awayFromBumper) <= 0.01f)
                velocity += awayFromBumper * Mathf.Max(0.5f, velocity.magnitude * 0.25f);

            float newSpeed = Mathf.Min(
                Mathf.Max(currentSpeed * bumper.speedMultiplier, bumper.minimumBoostSpeed),
                bumper.maxSpeed);
            velocity = velocity.normalized * newSpeed;
        }

        if (beastController != null)
        {
            float speedLimit = bumper.isBramble || bumper.type != BumperType.Green
                ? beastController.maximumSpeed
                : Mathf.Max(beastController.maximumSpeed, bumper.maxSpeed);
            velocity = Vector2.ClampMagnitude(velocity, speedLimit);
        }
    }

    private void AddTrajectoryPoint(Vector2 position)
    {
        Vector3 point = new Vector3(position.x, position.y, aimBasePosition.z - 0.05f);
        if (trajectoryPoints.Count == 0 ||
            Vector2.Distance(trajectoryPoints[trajectoryPoints.Count - 1], position) > 0.001f)
            trajectoryPoints.Add(point);
    }

    private void HideTrajectory()
    {
        if (aimLine != null)
        {
            aimLine.enabled = false;
            aimLine.positionCount = 0;
        }
        if (firstBounceRing != null)
            firstBounceRing.enabled = false;
        if (bumperHighlight != null)
            bumperHighlight.enabled = false;

        if (bounceMarkers == null)
        {
            if (trajectoryDots != null)
            {
                foreach (SpriteRenderer dot in trajectoryDots)
                {
                    if (dot != null)
                        dot.enabled = false;
                }
            }
            return;
        }

        foreach (SpriteRenderer marker in bounceMarkers)
        {
            if (marker != null)
                marker.enabled = false;
        }

        if (trajectoryDots != null)
        {
            foreach (SpriteRenderer dot in trajectoryDots)
            {
                if (dot != null)
                    dot.enabled = false;
            }
        }
    }

    // --- AIM SMOOTHING, RELEASE GUARD, SLOW MOTION ---

    // turns a finger drag into a launch vector: full power at a comfortable pull (FullPowerDrag).
    // on EASY any real drag launches at full power, so only the direction matters
    Vector2 AimToLaunch(Vector2 aim)
    {
        float fullDrag = Mathf.Min(maxDragDistance, FullPowerDrag);
        Vector2 launch = aim * (maxDragDistance / Mathf.Max(0.01f, fullDrag));
        if (Difficulty.FullPowerShots && aim.magnitude > EasyMinimumDrag)
            launch = aim.normalized * maxDragDistance;
        return launch;
    }

    // BUMPER MAGNET (aim assist): if the aim line passes very close to a bumper without touching it,
    // turn it a few degrees so the shot hits the bumper. how close and how far it may turn are in
    // GameTuning (Aiming), and it follows Settings > AIM ASSIST and the difficulty (none on HELL)
    Vector2 ApplyBumperMagnet(Vector2 direction)
    {
        float strength = GameSettings.AimAssist ? Difficulty.AssistStrength : 0f;
        GameTuning tuning = GameTuning.Get;
        float maxTurn = tuning.magnetDegrees * strength;
        if (maxTurn <= 0f || tuning.magnetReach <= 0f || direction.sqrMagnitude < 0.0001f)
            return direction;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        if (chomaCollider != null)
            origin += (Vector2)chomaCollider.bounds.center - (Vector2)transform.position;
        float beastRadius = GetTrajectoryRadius();
        float bestTurn = float.MaxValue;
        Vector2 best = direction;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper == null || bumper.isBramble)
                continue;
            Vector2 toBumper = (Vector2)bumper.transform.position - origin;
            if (Vector2.Dot(toBumper, direction) <= 0f)
                continue; // behind the Beast
            // how far the line passes from the bumper's middle (+ = the bumper is on the left)
            float side = direction.x * toBumper.y - direction.y * toBumper.x;
            float touch = CameraFit.WorldRadius(bumper, 0.55f) + beastRadius;
            float miss = Mathf.Abs(side) - touch;
            if (miss <= 0f || miss > tuning.magnetReach)
                continue; // it already hits this one, or it's not close
            // turn until the line goes a bit inside the bumper's edge, so the hit is sure
            float distance = toBumper.magnitude;
            float now = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(side) / distance));
            float wanted = Mathf.Asin(Mathf.Clamp01(touch * 0.7f / distance));
            float turn = (now - wanted) * Mathf.Rad2Deg;
            if (turn <= 0f || turn > maxTurn || turn >= bestTurn)
                continue;
            bestTurn = turn;
            float angle = Mathf.Sign(side) * turn * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            best = new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
        return best;
    }

    void RecordAim(Vector2 aim)
    {
        aimHistoryIndex = (aimHistoryIndex + 1) % aimHistory.Length;
        aimHistory[aimHistoryIndex] = aim;
        aimHistoryTime[aimHistoryIndex] = Time.unscaledTime;
    }

    // the aim from a moment before letting go, unless the player flicked so fast it was still tiny
    Vector2 AimBeforeRelease()
    {
        float cutoff = Time.unscaledTime - ReleaseGuardSeconds;
        Vector2 best = smoothedAim;
        float bestTime = -1f;
        for (int index = 0; index < aimHistory.Length; index++)
        {
            float time = aimHistoryTime[index];
            if (time >= 0f && time <= cutoff && time > bestTime)
            {
                bestTime = time;
                best = aimHistory[index];
            }
        }
        return bestTime >= 0f && best.magnitude >= smoothedAim.magnitude * 0.6f ? best : smoothedAim;
    }

    // slows the stage down while the player lines up a shot (depends on difficulty), but never
    // during a tutorial freeze or the pause menu
    void BeginAimSlowMotion()
    {
        float scale = Difficulty.AimSlowMotion;
        if (scale >= 0.99f || !Mathf.Approximately(GameTime.BaseScale, 1f))
            return;
        GameTime.SetBase(scale);
        if (Time.timeScale > 0.5f)
            Time.timeScale = scale;
        aimSlowActive = true;
    }

    void EndAimSlowMotion()
    {
        if (!aimSlowActive)
            return;
        aimSlowActive = false;
        float scale = Difficulty.AimSlowMotion;
        if (!Mathf.Approximately(GameTime.BaseScale, scale))
            return;
        GameTime.ResetBase();
        if (Mathf.Approximately(Time.timeScale, scale))
            Time.timeScale = 1f;
    }

    void OnDisable()
    {
        EndAimSlowMotion();
    }

    private void CancelAiming()
    {
        EndAimSlowMotion();
        isDragging = false;
        HideTrajectory();
        if (launchPadFeedback != null)
            launchPadFeedback.ReturnToIdle();
        SetIdleVisual();
    }

    private void SetAimingVisual()
    {
        if (UseAnimator())
        {
            anim.speed = 0f;
            anim.Play(curlState, 0, 0f);
        }
        else
        {
            Sprite aimSprite = CurrentBallSprite();

            if (spriteRenderer != null && aimSprite != null)
                spriteRenderer.sprite = aimSprite;
        }

        transform.localScale = originalScale;
        ClearSpin();
        transform.position = aimBasePosition;
        currentDragRatio = 0f;
        if (spriteRenderer != null)
            spriteRenderer.color = CurrentBaseColor();

        isAnimatingLaunch = false;

        if (chargeEffect != null)
        {
            var main = chargeEffect.main;
            main.startColor = BeastRoster.GetEffectColor(CurrentBeast());

            if (!chargeEffect.isPlaying)
                chargeEffect.Play();
        }
    }

    private void SetIdleVisual()
    {
        if (UseAnimator())
        {
            anim.speed = 1f;

            // this runs every frame while on the pad, so only start the idle clip if it isn't already
            // playing (otherwise it would keep restarting)
            if (!anim.GetCurrentAnimatorStateInfo(0).IsName(idleState))
                anim.Play(idleState);
        }
        else
        {
            BeastAnimSet animationSet = GetAnimationSet();
            Sprite idle = animationSet != null &&
                animationSet.idle != null &&
                idleFrameIndex < animationSet.idle.Length
                ? animationSet.idle[idleFrameIndex]
                : CurrentIdleSprite();

            if (spriteRenderer != null && idle != null)
                spriteRenderer.sprite = idle;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = CurrentBaseColor();

        transform.localScale = originalScale;
        ClearSpin();
        transform.position = aimBasePosition;

        isAnimatingLaunch = false;

        StopChargeEffect();

        if (flightTrail != null)
        {
            flightTrail.emitting = false;
            flightTrail.Clear();
        }
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

    // --- CAMERA / MOUSE ---

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

        Vector3 mousePosition = GetPointerScreenPosition();

        mousePosition.z =
            -camera.transform.position.z;

        return camera.ScreenToWorldPoint(mousePosition);
    }

    private bool PointerDownThisFrame()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).phase == TouchPhase.Began;

        return Input.GetMouseButtonDown(0);
    }

    private bool PointerUpThisFrame()
    {
        if (Input.touchCount > 0)
        {
            TouchPhase phase = Input.GetTouch(0).phase;
            return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
        }

        return Input.GetMouseButtonUp(0);
    }

    private Vector3 GetPointerScreenPosition()
    {
        return Input.touchCount > 0
            ? (Vector3)Input.GetTouch(0).position
            : Input.mousePosition;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return Input.touchCount > 0
            ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
            : EventSystem.current.IsPointerOverGameObject();
    }

    // --- LAUNCH TEXT ---

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
