using UnityEngine;

public class LaunchPadFeedback : MonoBehaviour
{
    [Header("Launch Pad Animation")]
    public float bounceAmount = 0.12f;
    public float animationSpeed = 14f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Animator anim;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        anim = GetComponent<Animator>();
        
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

    public void PlayAimingEffect()
    {
        if (anim != null) 
            anim.Play("LaunchPadPullingBack");
    }

    public void PlayLaunchEffect()
    {
        
        if (anim != null) 
            anim.Play("LaunchPad_Snap");

        
        targetScale = originalScale * (1f - bounceAmount);

        
        Invoke(nameof(ReturnScaleToNormal), 0.08f);

        
        Invoke(nameof(ReturnToIdle), 0.4f); 
    }

    private void ReturnScaleToNormal()
    {
        targetScale = originalScale;
    }

    public void ReturnToIdle()
    {
        targetScale = originalScale;
        if (anim != null) 
            anim.Play("LaunchPadIdle");
    }
}