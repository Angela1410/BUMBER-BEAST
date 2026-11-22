using UnityEngine;

public class BarrierVisuals : MonoBehaviour
{
    [Tooltip("Seconds the hit animation shows before returning to idle. Matches the hit clip's length.")]
    public float hitReturnDelay = 0.4f;

    private Animator anim;
    private string currentState;

    // The painted guardian wall (Assets/Resources/Art/Barrier_*.png). When it exists it replaces
    // the old animated log; without it the Animator clips play exactly as before.
    private BarrierArt art;
    private int shownHealth = 1;
    private int shownMaxHealth = 1;

    // Bottom edge of the visible barrier art, or NaN before it is laid out.
    public float ArtBottomWorldY
    {
        get { return art != null && art.IsLaidOut ? art.WallBottomWorldY : float.NaN; }
    }

    void Start()
    {
        anim = GetComponent<Animator>();
        art = BarrierArt.TryCreate(gameObject);
        if (art != null)
        {
            if (anim != null)
                anim.enabled = false;   // it would keep writing the log sprites
            art.SetHealth(shownHealth, shownMaxHealth);
            return;
        }

        PlayIdle();
    }

    // Shows the barrier's condition (healthy / damaged / critical). Visual only.
    public void ShowHealth(int current, int max)
    {
        shownHealth = current;
        shownMaxHealth = max;
        if (art != null)
            art.SetHealth(current, max);
    }

    public void PlayIdle()
    {
        if (art != null)
            return;
        ChangeAnimationState("Barrier_Idle");
    }

    public void PlayHit()
    {
        if (art != null)
        {
            art.PlayHit(null);
            return;
        }

        ChangeAnimationState("Barrier_Hit");



        Invoke(nameof(PlayIdle), hitReturnDelay);
    }

    // A hit where an enemy reached the barrier; the painted wall ripples at that point.
    public void PlayHit(Vector3 impact)
    {
        if (art != null)
        {
            art.PlayHit(impact);
            return;
        }

        PlayHit();
    }

    public void PlayDestroyed()
    {
        CancelInvoke(nameof(PlayIdle));
        if (art != null)
        {
            art.PlayDestroyed();
            return;
        }

        // The stage freezes time (timeScale 0) as soon as the barrier falls, so the break
        // animation has to run on unscaled time or it never leaves its first frame.
        if (anim != null)
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        ChangeAnimationState("Barrier_Destroyed");
    }

    private void ChangeAnimationState(string newState)
    {
        if (anim == null) return;
        if (currentState == newState) return;

        anim.Play(newState);
        currentState = newState;
    }
}
