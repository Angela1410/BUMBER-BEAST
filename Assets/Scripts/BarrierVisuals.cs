using UnityEngine;

public class BarrierVisuals : MonoBehaviour
{
    private Animator anim;
    private string currentState;

    void Start()
    {
        anim = GetComponent<Animator>();
        PlayIdle();
    }

    public void PlayIdle()
    {
        ChangeAnimationState("Barrier_Idle");
    }

    public void PlayHit()
    {
        ChangeAnimationState("Barrier_Hit");
        
        
        
        Invoke(nameof(PlayIdle), 0.4f);
    }

    public void PlayDestroyed()
    {
        ChangeAnimationState("Barrier_Destroyed");
        CancelInvoke(nameof(PlayIdle)); 
    }

    private void ChangeAnimationState(string newState)
    {
        if (anim == null) return;
        if (currentState == newState) return;

        anim.Play(newState);
        currentState = newState;
    }
}