using UnityEngine;

public class LaunchPadFeedback : MonoBehaviour
{
    [Header("Launch Pad Animation")]
    public float bounceAmount = 0.12f;
    public float animationSpeed = 14f;
    public float vineWidth = 0.08f;
    public Color vineColor = new Color(0.18f, 0.42f, 0.12f, 1f);

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Animator anim;
    private bool isPulling;
    private LineRenderer vineLine;
    private SpriteRenderer padSpriteRenderer;

    // the painted crystal slingshot (Assets/Resources/Art/Launcher.png). if it's there it replaces
    // the old stump art and the vine, if not everything works like before
    private LauncherArt launcherArt;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        anim = GetComponent<Animator>();
        padSpriteRenderer = GetComponent<SpriteRenderer>();
        EnsureVineLine();
        launcherArt = LauncherArt.TryCreate(gameObject, vineLine != null ? vineLine.sharedMaterial : null);

        ReturnToIdle();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * animationSpeed
        );

        if (Vector3.Distance(transform.localScale, targetScale) < 0.01f)
        {
            transform.localScale = targetScale;
        }
    }

    // called every frame while the player drags.
    // ratio: 0 = no pull, 1 = full pull. it moves through the pulling back clip
    public void SetPull(float ratio)
    {
        SetPull(ratio, transform.position);
    }

    public void SetPull(float ratio, Vector3 chomaPosition)
    {
        SetPull(ratio, chomaPosition, Vector2.zero);
    }

    public void SetPull(float ratio, Vector3 chomaPosition, Vector2 pullDirection)
    {
        SetPull(ratio, chomaPosition, pullDirection, null);
    }

    public void SetPull(
        float ratio,
        Vector3 chomaPosition,
        Vector2 pullDirection,
        SpriteRenderer chomaSpriteRenderer)
    {
        if (launcherArt != null)
        {
            // the energy bands follow the Beast, there's no vine or pull back clip to move through
            launcherArt.SetPull(ratio, chomaPosition);
            isPulling = ratio >= 0.05f;
            return;
        }

        EnsureVineLine();
        vineLine.enabled = ratio >= 0.05f;

        if (vineLine.enabled && chomaSpriteRenderer != null)
        {
            vineLine.sortingLayerID = chomaSpriteRenderer.sortingLayerID;
            // the vine ends at Choma's center, so it always has to be drawn below Choma, otherwise it draws
            // a green line across his sprite (like on stage 1, where the pad's order is 1 and Choma's is 0).
            // it only sits just above the pad when that's still below Choma
            int belowChomaOrder = chomaSpriteRenderer.sortingOrder - 1;
            vineLine.sortingOrder = padSpriteRenderer != null
                ? Mathf.Min(padSpriteRenderer.sortingOrder + 1, belowChomaOrder)
                : belowChomaOrder;
        }

        if (vineLine.enabled)
        {
            Vector3 anchor = padSpriteRenderer != null
                ? BoundsEdgeToward(padSpriteRenderer.bounds, chomaPosition)
                : transform.position;
            // end at Choma's center so the vine stays connected while the curl animation changes the
            // sprite size. Choma is drawn on top of it
            Vector3 vineTip = chomaPosition;
            Vector3 midpoint = Vector3.Lerp(anchor, vineTip, 0.5f);
            float curve = Mathf.Clamp01(ratio) * 0.12f;
            if (pullDirection.sqrMagnitude > 0.001f)
                midpoint += (Vector3)(pullDirection.normalized * curve);

            vineLine.positionCount = 3;
            vineLine.SetPosition(0, anchor);
            vineLine.SetPosition(1, midpoint);
            vineLine.SetPosition(2, vineTip);
        }

        if (anim == null)
            return;

        if (ratio < 0.05f)
        {
            if (isPulling)
                ReturnToIdle();
            return;
        }

        isPulling = true;
        anim.speed = 0f;
        anim.Play("LaunchPadPullingBack", 0, Mathf.Clamp(ratio, 0f, 0.99f));
    }

    public void PlayAimingEffect()
    {
        if (anim != null && launcherArt == null)
        {
            anim.speed = 1f;
            anim.Play("LaunchPadPullingBack");
        }
    }

    public void PlayLaunchEffect()
    {
        isPulling = false;
        CancelInvoke();

        if (launcherArt != null)
        {
            launcherArt.PlayLaunch();
        }
        else if (anim != null)
        {
            anim.speed = 1f;
            anim.Play("LaunchPad_Snap");
        }

        targetScale = originalScale * (1f - bounceAmount);

        Invoke(nameof(ReturnScaleToNormal), 0.08f);
        Invoke(nameof(ReturnToIdle), 0.4f);
        HideVine();
    }

    private void ReturnScaleToNormal()
    {
        targetScale = originalScale;
    }

    public void ReturnToIdle()
    {
        isPulling = false;
        targetScale = originalScale;

        if (launcherArt != null)
        {
            launcherArt.ReturnToIdle();
        }
        else if (anim != null)
        {
            anim.speed = 1f;
            anim.Play("LaunchPadIdle");
        }

        HideVine();
    }

    private void EnsureVineLine()
    {
        if (vineLine != null)
            return;

        Transform vineTransform = transform.Find("LauncherVine");
        GameObject vineObject = vineTransform != null
            ? vineTransform.gameObject
            : new GameObject("LauncherVine");

        vineObject.transform.SetParent(transform, false);
        vineLine = vineObject.GetComponent<LineRenderer>();
        if (vineLine == null)
            vineLine = vineObject.AddComponent<LineRenderer>();

        vineLine.useWorldSpace = true;
        vineLine.positionCount = 3;
        vineLine.startWidth = vineWidth;
        vineLine.endWidth = vineWidth * 0.75f;
        vineLine.sortingOrder = -1;
        vineLine.numCornerVertices = 4;
        vineLine.numCapVertices = 4;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
            vineLine.material = new Material(spriteShader);
        vineLine.startColor = vineColor;
        vineLine.endColor = vineColor;
        vineLine.enabled = false;
    }

    private void HideVine()
    {
        EnsureVineLine();
        vineLine.enabled = false;
    }

    private static Vector3 BoundsEdgeToward(Bounds bounds, Vector3 target)
    {
        Vector3 direction = target - bounds.center;
        if (direction.sqrMagnitude < 0.0001f)
            return bounds.center;

        float horizontalDistance = Mathf.Abs(direction.x) > 0.0001f
            ? bounds.extents.x / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float verticalDistance = Mathf.Abs(direction.y) > 0.0001f
            ? bounds.extents.y / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        float distance = Mathf.Min(horizontalDistance, verticalDistance);

        return bounds.center + direction * distance;
    }
}
