using UnityEngine;

// help for new players, because playtesters kept asking "what do I do?" and "what do I drag?".
// in chapter 1, when you stand still on the launch pad while enemies are coming, a hand shows the move
// (pull down like a slingshot, then let go) with a short tip. it disappears the moment you touch the
// screen and comes back if you wait again. it never shows while the tutorial is talking.
// which stages and how long to wait: GameTuning (Help for new players)
public class LaunchHint : MonoBehaviour
{
    // the tip sits a bit above the launch pad (0 = bottom of the screen, 1 = top)
    private const float TipHeight = 0.36f;

    private CoachMark coach;
    private float idleTime;
    private bool showing;
    private StageManager stage;

    // StageManager calls this once per stage
    public static void Attach(StageManager owner)
    {
        if (owner == null || owner.stageNumber > GameTuning.Get.hintUpToStage)
            return;
        if (owner.GetComponent<LaunchHint>() == null)
            owner.gameObject.AddComponent<LaunchHint>().stage = owner;
    }

    // the one sentence that explains launching (the stage 1 lesson uses it too)
    public static string HowToLaunch(BeastId beast)
    {
        string name = BeastRoster.GetName(beast);
        return GameSettings.DragAnywhere
            ? "<color=#FFCC42>PULL DOWN</color> like a slingshot, then <color=#FFCC42>LET GO</color>!\n" +
                name + " flies up at the enemies."
            : "Touch " + name + " and <color=#FFCC42>PULL DOWN</color>, then <color=#FFCC42>LET GO</color>!\n" +
                "It flies up at the enemies like a slingshot.";
    }

    public bool IsShowing => showing;

    void Update()
    {
        // any touch (or click) hides it right away, and the waiting starts over
        bool touching = Input.touchCount > 0 || Input.GetMouseButton(0);
        if (touching || !PlayerIsWaiting())
        {
            idleTime = 0f;
            Hide();
            return;
        }

        idleTime += Time.unscaledDeltaTime;
        if (!showing && idleTime >= Mathf.Max(0.5f, GameTuning.Get.hintAfterIdleSeconds))
            Show();
    }

    // a wave is on, enemies are on the field, the Beast waits on the pad and nobody is aiming
    bool PlayerIsWaiting()
    {
        if (stage == null || stage.IsFinished || Time.timeScale <= 0f || TutorialDirector.IsCoaching)
            return false;
        WaveManager waves = WaveManager.Current;
        BeastController beast = BeastController.Active;
        if (waves == null || !waves.IsWaveInProgress || beast == null || Enemy.ActiveEnemies.Count == 0)
            return false;
        if (beast.State != BeastState.AtPad || beast.IsLaunchLocked)
            return false;
        LaunchController launcher = beast.GetComponent<LaunchController>();
        return launcher == null || !launcher.IsAiming;
    }

    void Show()
    {
        BeastController beast = BeastController.Active;
        if (beast == null)
            return;
        if (coach == null)
            coach = CoachMark.Create();
        Vector3 pad = beast.transform.position;
        coach.Say(HowToLaunch(beast.ActiveBeast), TipHeight, false);
        coach.DragGesture(pad + new Vector3(0f, 0.2f, 0f), pad + new Vector3(0.3f, -1.6f, 0f));
        showing = true;
    }

    void Hide()
    {
        if (!showing)
            return;
        if (coach != null)
            coach.HideAll();
        showing = false;
    }

    void OnDestroy()
    {
        if (coach != null)
            Destroy(coach.gameObject);
    }
}
