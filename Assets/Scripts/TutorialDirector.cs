using System.Collections;
using UnityEngine;

// Guided first steps, played once (Settings > REPLAY TUTORIAL plays them again):
//  Stage 1  READY on the plan card -> drag back and launch (the field freezes until the first
//           shot) -> bounce to hit -> element advantage -> the skill button (in slow motion) ->
//           guard the barrier -> wave clear.
//  Stage 2  bumper colours -> the POWER BUMPER (one is made sure to exist) and what each Beast's
//           power does.
//  Later    the first time the squad has two or more Beasts: the SWAP button.
// Each lesson waits for the player to actually do the thing, with time-outs so it never gets stuck.
public class TutorialDirector : MonoBehaviour
{
    public const string Stage1Key = "BumperBeast.Tutorial.Stage1";
    public const string Stage2Key = "BumperBeast.Tutorial.Stage2";
    public const string SwapKey = "BumperBeast.Tutorial.Swap";

    private const float FrozenScale = 0f;
    // Bubbles sit at the top, under the wave bar, so the field stays clear.
    private const float BubbleTop = 0.865f;
    private const float SlowScale = 0.25f;

    private StageManager stage;
    private CoachMark coach;
    private bool launched;
    private bool enemyHit;
    private bool waveCleared;
    private bool powerBumperHit;
    private bool changedScale;

    public static bool IsDone(string key)
    {
        return PlayerPrefs.GetInt(key, 0) != 0;
    }

    static void MarkDone(string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(Stage1Key);
        PlayerPrefs.DeleteKey(Stage2Key);
        PlayerPrefs.DeleteKey(SwapKey);
        PlayerPrefs.Save();
    }

    public static void StartIfNeeded(StageManager owner)
    {
        if (owner == null)
            return;
        string lesson = owner.stageNumber == 1 && !IsDone(Stage1Key) ? Stage1Key
            : owner.stageNumber == 2 && !IsDone(Stage2Key) ? Stage2Key
            : !IsDone(SwapKey) ? SwapKey : null;
        if (lesson == null)
            return;

        TutorialDirector director = new GameObject("Tutorial").AddComponent<TutorialDirector>();
        director.stage = owner;
        director.StartCoroutine(director.Run(lesson));
    }

    void OnEnable()
    {
        LaunchController.Launched += OnLaunched;
        StageStats.EnemyHit += OnEnemyHit;
        WaveManager.WaveCleared += OnWaveCleared;
        Bumper.PowerBumperHit += OnPowerBumper;
    }

    void OnDisable()
    {
        LaunchController.Launched -= OnLaunched;
        StageStats.EnemyHit -= OnEnemyHit;
        WaveManager.WaveCleared -= OnWaveCleared;
        Bumper.PowerBumperHit -= OnPowerBumper;
        RestoreTime();
    }

    // The stage ended mid-lesson (won or lost): the result card takes over, the coach leaves.
    void Update()
    {
        if (stage != null && stage.IsFinished)
        {
            StopAllCoroutines();
            changedScale = false;   // the stage owns the clock now (frozen behind the card)
            GameTime.ResetBase();
            if (coach != null)
                Destroy(coach.gameObject);
            Destroy(gameObject);
        }
    }

    void OnLaunched(LaunchController launcher) { launched = true; }
    void OnEnemyHit(Vector3 position, bool super, bool knockedOut) { enemyHit = true; }
    void OnWaveCleared(int wave, bool last) { waveCleared = true; }
    void OnPowerBumper(Bumper bumper) { powerBumperHit = true; }

    IEnumerator Run(string lesson)
    {
        coach = CoachMark.Create();
        if (lesson == Stage1Key)
            yield return Stage1();
        else if (lesson == Stage2Key)
            yield return Stage2();
        else
            yield return SwapTip();

        RestoreTime();
        if (coach != null)
            Destroy(coach.gameObject);
        Destroy(gameObject);
    }

    // ------------------------------------------------------------------ stage 1

    IEnumerator Stage1()
    {
        WaveManager waves = null;
        yield return WaitForPlanCard(w => waves = w);
        if (waves == null)
            yield break;

        coach.Say("Welcome, <color=#FFCC42>" + GameSettings.PlayerName + "</color>! Thorn Pups are coming.\n" +
            "The card shows who beats them. Tap <color=#FFCC42>READY!</color>", BubbleTop);
        coach.PointAt(waves.ReadyButtonRect);
        while (waves != null && waves.IsAwaitingReady)
            yield return null;
        coach.HideAll();

        // Give the first enemy a moment on the field, then freeze it for the aiming lesson.
        float until = Time.unscaledTime + 8f;
        while (!PowerBurst.HasTargets() && Time.unscaledTime < until && !launched)
            yield return null;
        yield return new WaitForSecondsRealtime(1.1f);

        BeastController beast = BeastController.Active;
        if (!launched && beast != null)
        {
            SetTime(FrozenScale);
            Vector3 pad = beast.transform.position;
            coach.Say(GameSettings.DragAnywhere
                ? "<color=#FFCC42>DRAG BACK</color> anywhere to aim, then let go!\nThe dots show your path."
                : "Touch your Beast, <color=#FFCC42>DRAG BACK</color>, then let go!\nThe dots show your path.", BubbleTop);
            coach.DragGesture(pad + new Vector3(0f, 0.2f, 0f), pad + new Vector3(0.5f, -1.7f, 0f));
            while (!launched)
                yield return null;
            RestoreTime();
        }
        coach.HideAll();

        coach.Say("Bounce off walls and bumpers.\nHit several in one shot for a <color=#FFCC42>COMBO!</color>", BubbleTop);
        until = Time.unscaledTime + 7f;
        while (!enemyHit && Time.unscaledTime < until)
            yield return null;

        if (enemyHit)
        {
            Enemy target = NearestEnemy(beast != null ? beast.transform.position : Vector3.zero);
            coach.Say("<color=#FF8A3D>FIRE</color> beats <color=#7CD957>GRASS</color>: DOUBLE damage!\n" +
                "A <color=#FFCC42>gold badge</color> means your Beast is strong there.", BubbleTop);
            if (target != null)
                coach.PointAtWorld(target.transform);
            else
                coach.HidePointer();
            yield return new WaitForSecondsRealtime(4f);
        }
        coach.HideAll();

        // The skill: wait until it can be used mid-flight, then slow time and point at the button.
        beast = BeastController.Active;
        until = Time.unscaledTime + 35f;
        while (beast != null && !beast.CanUseSkill && Time.unscaledTime < until && !waveCleared)
        {
            yield return null;
            beast = BeastController.Active;
        }
        BeastSkillButton skill = FindAnyObjectByType<BeastSkillButton>();
        if (beast != null && beast.CanUseSkill && skill != null)
        {
            SetTime(SlowScale);
            coach.Say("Tap <color=#FFCC42>" + BeastRoster.GetAbilityName(beast.ActiveBeast).ToUpperInvariant() +
                "</color> while flying!\nEvery Beast has its own skill.", BubbleTop);
            coach.PointAt(skill.transform as RectTransform);
            until = Time.unscaledTime + 7f;
            while (beast != null && beast.IsSkillReady && beast.State == BeastState.Flying && Time.unscaledTime < until)
                yield return null;
            RestoreTime();
            coach.HideAll();
            yield return new WaitForSecondsRealtime(0.6f);
        }

        GameObject barrier = GameObject.Find("Square");
        coach.Say("Guard your <color=#FFCC42>BARRIER</color>!\nKeep it whole for <color=#FFCC42>3 STARS</color>.", BubbleTop);
        if (barrier != null)
            coach.PointAtWorld(barrier.transform);
        yield return new WaitForSecondsRealtime(4f);
        coach.HideAll();

        while (!waveCleared && stage != null)
            yield return null;
        MarkDone(Stage1Key);
        coach.Say("Wave cleared! Check the next card,\nthen tap READY.", BubbleTop);
        yield return new WaitForSecondsRealtime(3.5f);
        coach.HideAll();
    }

    // ------------------------------------------------------------------ stage 2

    IEnumerator Stage2()
    {
        WaveManager waves = null;
        yield return WaitForPlanCard(w => waves = w);
        if (waves == null)
            yield break;

        Bumper power = EnsurePowerBumper();
        coach.Say("<color=#7CD957>GREEN</color> speeds you up, <color=#6FB8FF>BLUE</color> slows you down.\n" +
            "The glowing <color=#FF6A4D>POWER BUMPER</color> unleashes your Beast!", BubbleTop);
        if (power != null)
            coach.PointAtWorld(power.transform);
        while (waves != null && waves.IsAwaitingReady)
            yield return null;

        coach.Say("Bounce into the <color=#FF6A4D>POWER BUMPER</color>\nwhile enemies are on the field!", BubbleTop);
        float until = Time.unscaledTime + 90f;
        float retarget = 0f;
        while (!powerBumperHit && Time.unscaledTime < until && stage != null)
        {
            // Bumpers break and respawn: keep pointing at whichever Power Bumper exists.
            if (Time.unscaledTime >= retarget)
            {
                retarget = Time.unscaledTime + 1f;
                Bumper current = FindPowerBumper();
                if (current != power)
                {
                    power = current;
                    if (power != null)
                        coach.PointAtWorld(power.transform);
                    else
                        coach.HidePointer();
                }
            }
            yield return null;
        }
        coach.HideAll();
        if (!powerBumperHit)
            yield break;

        MarkDone(Stage2Key);
        yield return new WaitForSecondsRealtime(1.5f);
        coach.Say("<color=#FF8A3D>Choma</color> fireballs · <color=#7CD957>Thistle</color> roots all · <color=#C9A06B>Mossback</color> quake\n" +
            "<color=#FFE45C>Voltis</color> thunder · <color=#6FB8FF>Ripple</color> tidal wave", BubbleTop);
        yield return new WaitForSecondsRealtime(5f);
        coach.HideAll();
    }

    static Bumper FindPowerBumper()
    {
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper != null && bumper.IsPowerBumper)
                return bumper;
        }
        return null;
    }

    // The lesson needs a Power Bumper: turn the bumper nearest the middle of the field into one.
    static Bumper EnsurePowerBumper()
    {
        Bumper existing = FindPowerBumper();
        if (existing != null)
            return existing;
        Camera camera = Camera.main;
        Vector3 middle = camera != null ? camera.transform.position : Vector3.zero;
        Bumper best = null;
        float bestDistance = float.MaxValue;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper == null || bumper.isBramble)
                continue;
            float distance = ((Vector2)(bumper.transform.position - middle)).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = bumper;
            }
        }
        if (best != null)
            best.MakePowerBumper();
        return best;
    }

    // ------------------------------------------------------------------ swap tip

    IEnumerator SwapTip()
    {
        WaveManager waves = null;
        yield return WaitForPlanCard(w => waves = w);
        PadSwapSystem pad = FindAnyObjectByType<PadSwapSystem>();
        if (waves == null || pad == null || pad.ActiveSquad.Count < 2 || pad.swapButton == null)
            yield break;

        MarkDone(SwapKey);
        BeastId before = pad.ActiveSquadBeast;
        coach.Say("Tap here to <color=#FFCC42>SWAP</color> Beasts.\nBring the one that beats this wave!", BubbleTop);
        coach.PointAt(pad.swapButton.transform as RectTransform);
        float until = Time.unscaledTime + 9f;
        while (pad != null && pad.ActiveSquadBeast == before && waves != null && waves.IsAwaitingReady && Time.unscaledTime < until)
            yield return null;
        coach.HideAll();
    }

    // ------------------------------------------------------------------ helpers

    IEnumerator WaitForPlanCard(System.Action<WaveManager> found)
    {
        float until = Time.unscaledTime + 20f;
        while (Time.unscaledTime < until)
        {
            WaveManager waves = WaveManager.Current;
            if (waves != null && waves.ReadyButtonRect != null)
            {
                found(waves);
                yield break;
            }
            yield return null;
        }
        found(null);
    }

    static Enemy NearestEnemy(Vector3 from)
    {
        Enemy best = null;
        float bestDistance = float.MaxValue;
        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            float distance = ((Vector2)(enemy.transform.position - from)).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }
        return best;
    }

    // Freezes or slows the game for a lesson by changing the stage speed (GameTime). Hit-stops and the
    // pause menu return to that speed, so ending the lesson always brings the game back to normal.
    void SetTime(float scale)
    {
        if (stage != null && stage.IsFinished)
            return;
        GameTime.SetBase(scale);
        changedScale = true;
        if (!GamePaused())
            Time.timeScale = scale;
    }

    void RestoreTime()
    {
        if (!changedScale)
            return;
        changedScale = false;
        GameTime.ResetBase();
        // The result card and the pause menu own the clock while they are up; otherwise back to normal
        // now (a hit-stop in progress also ends on the normal speed).
        if ((stage == null || !stage.IsFinished) && !GamePaused())
            Time.timeScale = 1f;
    }

    static bool GamePaused()
    {
        PauseMenu pause = FindAnyObjectByType<PauseMenu>();
        return pause != null && pause.IsPaused;
    }
}
