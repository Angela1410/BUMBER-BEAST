using UnityEngine;

public class BarrierVisuals : MonoBehaviour
{
    [Tooltip("Seconds the hit animation shows before returning to idle. Matches the hit clip's length.")]
    public float hitReturnDelay = 0.4f;

    private Animator anim;
    private string currentState;

    // the painted wall (Assets/Resources/Art/Barrier_*.png). if it exists it replaces the old animated log,
    // if not the Animator clips play like before
    private BarrierArt art;
    private int shownHealth = 1;
    private int shownMaxHealth = 1;

    // bottom edge of the barrier art we can see, or NaN before it's placed
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
                anim.enabled = false; // otherwise it keeps drawing the log sprites
            art.SetHealth(shownHealth, shownMaxHealth);
            return;
        }

        PlayIdle();
    }

    // shows how healthy the barrier is (healthy / damaged / critical). only changes how it looks
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

    // an enemy reached the barrier, the painted wall ripples at that spot
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

        // the stage stops time (timeScale 0) as soon as the barrier falls, so the break animation
        // has to use unscaled time or it gets stuck on the first frame
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
