#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// a quick check that the stages still work: it plays a few stages for real and looks at the camera,
// the walls and the bumpers on different phone shapes. run it from Window > General > Test Runner
// (EditMode tab, "Run All"). pictures of each screen shape go to Logs/SmokeTest (not saved in git)
public class BumperBeastSmokeTest
{
    // width x height of the screens we try: 9:16, 9:19.5, 9:20, 9:21 and a 3:4 tablet
    private static readonly Vector2Int[] Screens =
    {
        new Vector2Int(540, 960), new Vector2Int(540, 1170), new Vector2Int(540, 1200),
        new Vector2Int(540, 1260), new Vector2Int(720, 960)
    };

    private static readonly string[] Stages =
    {
        "Assets/Scenes/stage-1.unity", "Assets/Scenes/stage-4.unity",
        "Assets/Scenes/stage-5_Earth.unity", "Assets/Scenes/stage-12_Electric.unity",
        "Assets/Scenes/stage-20_Fire.unity"
    };

    private readonly List<string> problems = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly StringBuilder report = new StringBuilder();

    [UnityTest]
    public IEnumerator StagesFitEveryScreen()
    {
        yield return new EnterPlayMode();
        Application.logMessageReceived += CollectErrors;
        LogAssert.ignoreFailingMessages = true;
        string shots = Environment("BUMPER_SMOKE_SHOTS", Path.GetFullPath("Logs/SmokeTest"));
        Directory.CreateDirectory(shots);

        foreach (string stage in Stages)
        {
            AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode(stage,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!loading.isDone)
                yield return null;
            for (int frame = 0; frame < 10; frame++)
                yield return null;

            string name = Path.GetFileNameWithoutExtension(stage);
            Camera camera = Camera.main;
            if (camera == null || camera.GetComponent<CameraFit>() == null)
            {
                problems.Add(name + ": the camera has no CameraFit");
                continue;
            }
            CheckLayout(name);

            foreach (Vector2Int screen in Screens)
            {
                RenderTexture target = new RenderTexture(screen.x, screen.y, 24);
                camera.targetTexture = target;
                for (int frame = 0; frame < 3; frame++)
                    yield return null;
                Physics2D.SyncTransforms();
                CheckFit(name, camera, screen);
                Capture(camera, target, Path.Combine(shots, name + "_" + screen.x + "x" + screen.y + ".png"));
                camera.targetTexture = null;
                Object.Destroy(target);
            }
        }

        yield return CheckShot();
        CheckPowerMeter();
        CheckBarrierHeal();
        yield return CheckStagger();
        CheckAim();
        yield return CheckIdleAndArt(shots);
        yield return CheckBeastsTab(shots);

        Application.logMessageReceived -= CollectErrors;
        string summary = "SMOKE TEST REPORT\n" + report + "\nerrors logged: " + errors.Count +
            (errors.Count > 0 ? "\n" + string.Join("\n", errors) : string.Empty) +
            "\nproblems: " + problems.Count + (problems.Count > 0 ? "\n" + string.Join("\n", problems) : string.Empty);
        Debug.Log(summary);
        File.WriteAllText(Path.Combine(shots, "report.txt"), summary);
        // leaving play mode can reload the scripts, so the result is kept in the editor session
        UnityEditor.SessionState.SetString(ResultKey, string.Join("\n", problems));
        yield return new ExitPlayMode();

        string found = UnityEditor.SessionState.GetString(ResultKey, string.Empty);
        UnityEditor.SessionState.EraseString(ResultKey);
        Assert.IsTrue(found.Length == 0, found);
    }

    private const string ResultKey = "BumperBeast.SmokeTest.Problems";

    // fix 1: the side walls sit exactly at the screen edges, the whole field fits, and every bumper is
    // on screen with at least one bumper width of space to the walls
    void CheckFit(string stage, Camera camera, Vector2Int screen)
    {
        float aspect = (float)screen.x / screen.y;
        float halfWidth = camera.orthographicSize * camera.aspect;
        float expectedSize = Mathf.Max(9f, CameraFit.DesignHalfWidth / aspect);
        string shape = stage + " " + screen.x + "x" + screen.y;
        if (Mathf.Abs(camera.orthographicSize - expectedSize) > 0.01f)
            problems.Add(shape + ": camera size " + camera.orthographicSize + ", expected " + expectedSize);
        if (halfWidth < CameraFit.DesignHalfWidth - 0.01f)
            problems.Add(shape + ": the field is cut off (view half width " + halfWidth + ")");

        Collider2D left = Wall("wall-left"), right = Wall("wall-right");
        if (left == null || right == null)
            problems.Add(shape + ": side walls not found");
        else
        {
            if (Mathf.Abs(left.bounds.max.x + halfWidth) > 0.02f || Mathf.Abs(right.bounds.min.x - halfWidth) > 0.02f)
                problems.Add(shape + ": walls at " + left.bounds.max.x + " / " + right.bounds.min.x +
                    " but the screen edges are at +-" + halfWidth);
        }

        int bumpers = 0;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper == null)
                continue;
            bumpers++;
            float radius = CameraFit.WorldRadius(bumper, 0.55f);
            Vector2 position = bumper.transform.position;
            float wallSpace = CameraFit.DesignHalfWidth - Mathf.Abs(position.x - CameraFit.FieldCenterX) - radius;
            if (wallSpace < radius * 2f - 0.01f)
                problems.Add(shape + ": " + bumper.name + " at " + position + " is only " + wallSpace.ToString("0.00") +
                    " from a wall");
            if (Mathf.Abs(position.y - camera.transform.position.y) + radius > camera.orthographicSize)
                problems.Add(shape + ": " + bumper.name + " at " + position + " is off screen");
        }
        report.AppendLine(shape + ": camera size " + camera.orthographicSize.ToString("0.00") + ", walls at +-" +
            halfWidth.ToString("0.00") + ", " + bumpers + " bumpers OK");
    }

    // fix 2: bounce power stops at its maximum, a SWAP recall locks the next launch for a moment,
    // and a real shot comes back by itself, with a quick flight back to the pad
    IEnumerator CheckShot()
    {
        AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/stage-5_Earth.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone)
            yield return null;
        for (int frame = 0; frame < 10; frame++)
            yield return null;

        BeastController beast = BeastController.Active;
        Rigidbody2D body = beast != null ? beast.GetComponent<Rigidbody2D>() : null;
        if (beast == null || body == null)
        {
            problems.Add("shot: no Beast in stage 5");
            yield break;
        }
        GameTuning tuning = GameTuning.Get;

        beast.NotifyLaunched();
        for (int hit = 0; hit < 10; hit++)
            beast.GainBouncePower();
        if (beast.BouncePower != tuning.bouncePowerMax)
            problems.Add("shot: bounce power went to " + beast.BouncePower + ", the most is " + tuning.bouncePowerMax);
        beast.BeginReturn();
        while (beast.State != BeastState.AtPad)
            yield return null;
        if (beast.BouncePower != 0)
            problems.Add("shot: bounce power stayed after the shot (" + beast.BouncePower + ")");

        beast.LockLaunch(tuning.recallLaunchLock);
        if (!beast.IsLaunchLocked)
            problems.Add("shot: the recall lock did not start");
        // (WaitForSeconds doesn't wait in this kind of test, so we count the time ourselves)
        float lockEnds = Time.time + tuning.recallLaunchLock + 0.1f;
        while (Time.time < lockEnds)
            yield return null;
        if (beast.IsLaunchLocked)
            problems.Add("shot: the recall lock did not end");

        // a real launch: straight up, then wait for the Beast to come back by itself
        body.linearVelocity = new Vector2(2f, 8.5f);
        beast.NotifyLaunched();
        float launched = Time.time, returning = -1f, landed = -1f;
        int bounces = 0;
        while (Time.time - launched < tuning.returnAfterSeconds + 2f)
        {
            if (returning < 0f && beast.State == BeastState.Returning)
                returning = Time.time;
            if (beast.State == BeastState.Flying || beast.State == BeastState.UsingSkill)
                bounces = beast.ShotBounces;
            if (returning >= 0f && beast.State == BeastState.AtPad)
            {
                landed = Time.time;
                break;
            }
            yield return null;
        }
        if (returning < 0f || landed < 0f)
            problems.Add("shot: the Beast did not come back to the pad by itself");
        else
        {
            if (returning - launched > tuning.returnAfterSeconds + 0.2f)
                problems.Add("shot: it flew " + (returning - launched).ToString("0.00") + " s before coming back");
            if (landed - returning > 0.4f)
                problems.Add("shot: the flight back took " + (landed - returning).ToString("0.00") + " s");
            report.AppendLine("shot: flew " + (returning - launched).ToString("0.00") + " s with " + bounces +
                " bounces, flight back " + (landed - returning).ToString("0.00") + " s");
        }
    }

    // fix 3: green hits fill the POWER meter, and a full meter turns a green bumper into a red Power Bumper
    void CheckPowerMeter()
    {
        PowerMeter meter = PowerMeter.Current;
        if (meter == null || GameObject.Find("PowerPanel") == null)
        {
            problems.Add("power meter: not in the stage");
            return;
        }
        Bumper green = null;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper != null && bumper.type == BumperType.Green && !bumper.isBramble)
                green = bumper;
        }
        if (green == null)
        {
            problems.Add("power meter: no green bumper in stage 5");
            return;
        }
        for (int hit = 0; hit < GameTuning.Get.powerMeterHits; hit++)
            PowerMeter.GreenHit(green);
        Bumper power = null;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper != null && bumper.IsPowerBumper)
                power = bumper;
        }
        if (power == null)
            problems.Add("power meter: full, but no Power Bumper appeared");
        else
        {
            SpriteRenderer look = power.GetComponent<SpriteRenderer>();
            if (power.redSprite == null || look == null || look.sprite != power.redSprite)
                problems.Add("power meter: the Power Bumper doesn't use the red picture");
            report.AppendLine("power meter: " + GameTuning.Get.powerMeterHits + " green hits made " + power.name + " the Power Bumper");
        }
    }

    // fix 4: every stage has its own fixed bumpers: the right number, outside the middle lane
    // (so a straight shot always works) and off the enemy path
    void CheckLayout(string stage)
    {
        int expected = stage == "stage-1" || stage == "stage-4" ? 3
            : stage == "stage-5_Earth" ? 4
            : stage == "stage-12_Electric" ? 5 : 6;
        WaveManager waves = WaveManager.Current;
        List<Vector2> route = new List<Vector2>();
        if (waves != null && waves.routeWaypoints != null)
        {
            foreach (Transform point in waves.routeWaypoints)
            {
                if (point != null)
                    route.Add(point.position);
            }
        }

        int count = 0;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper == null || bumper.isBramble)
                continue;
            count++;
            Vector2 p = bumper.transform.position;
            if (Mathf.Abs(p.x - CameraFit.FieldCenterX) < 1.19f)
                problems.Add(stage + ": " + bumper.name + " at " + p + " blocks the straight shot up the middle");
            float path = float.MaxValue;
            for (int i = 0; i + 1 < route.Count; i++)
            {
                Vector2 a = route[i], ab = route[i + 1] - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                path = Mathf.Min(path, Vector2.Distance(p, a + ab * t));
            }
            if (path < 0.99f)
                problems.Add(stage + ": " + bumper.name + " at " + p + " sits on the enemy path");
        }
        if (count != expected)
            problems.Add(stage + ": " + count + " bumpers, the layout has " + expected);
        report.AppendLine(stage + ": " + count + " bumpers in their layout spots");
    }

    // fix 5: clearing a wave gives 1 barrier health back (not above full, not after the last wave)
    void CheckBarrierHeal()
    {
        StageManager stage = Object.FindAnyObjectByType<StageManager>();
        System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        System.Reflection.FieldInfo health = typeof(StageManager).GetField("barrierHealth", hidden);
        System.Reflection.MethodInfo cleared = typeof(StageManager).GetMethod("OnWaveCleared", hidden);
        if (stage == null || health == null || cleared == null)
        {
            problems.Add("barrier heal: StageManager not found");
            return;
        }
        int full = stage.barrierMaxHealth;
        health.SetValue(stage, full - 2);
        cleared.Invoke(stage, new object[] { 0, false });
        int afterOne = (int)health.GetValue(stage);
        cleared.Invoke(stage, new object[] { 1, true });
        int afterLast = (int)health.GetValue(stage);
        health.SetValue(stage, full);
        cleared.Invoke(stage, new object[] { 0, false });
        int whenFull = (int)health.GetValue(stage);
        if (afterOne != full - 1 || afterLast != full - 1 || whenFull != full)
            problems.Add("barrier heal: " + (full - 2) + " -> " + afterOne + " after a wave, " + afterLast +
                " after the last wave, " + whenFull + " when full (max " + full + ")");
        else
            report.AppendLine("barrier heal: " + (full - 2) + " -> " + afterOne + " after a wave, nothing after the last wave or when full");
    }

    // fix 5: a hit enemy stands still for a moment, then walks on
    IEnumerator CheckStagger()
    {
        WaveManager waves = WaveManager.Current;
        Enemy prefab = null;
        if (waves != null && waves.waves != null)
        {
            foreach (EnemyWave wave in waves.waves)
            {
                if (wave != null && wave.spawnTable != null && wave.spawnTable.Length > 0 && wave.spawnTable[0].prefab != null)
                {
                    prefab = wave.spawnTable[0].prefab;
                    break;
                }
            }
        }
        if (prefab == null)
        {
            problems.Add("stagger: no enemy to test with");
            yield break;
        }

        Enemy enemy = Object.Instantiate(prefab, new Vector3(0f, 4f, 0f), Quaternion.identity);
        enemy.SetRoute(waves.routeWaypoints, 0f, 1);
        for (int frame = 0; frame < 5; frame++)
            yield return null;
        enemy.TakeDamage(1);
        Vector2 hitAt = enemy.transform.position;
        float stopFor = GameTuning.Get.staggerSeconds;
        float until = Time.time + stopFor * 0.7f;
        while (Time.time < until && enemy != null)
            yield return null;
        float stood = enemy != null ? Vector2.Distance(hitAt, enemy.transform.position) : -1f;
        until = Time.time + stopFor + 0.3f;
        while (Time.time < until && enemy != null)
            yield return null;
        float walked = enemy != null ? Vector2.Distance(hitAt, enemy.transform.position) : -1f;
        if (stood < 0f || stood > 0.01f || walked < 0.05f)
            problems.Add("stagger: moved " + stood.ToString("0.000") + " while staggered and " + walked.ToString("0.000") + " after");
        else
            report.AppendLine("stagger: stood still for " + stopFor + " s after a hit, then walked on");
        if (enemy != null)
            Object.Destroy(enemy.gameObject);
    }

    // fix 6: full-power shots, the bumper magnet and the bumper highlight on the aim line.
    // it only reads your settings (they're saved on this computer), it never changes them
    void CheckAim()
    {
        LaunchController launcher = Object.FindAnyObjectByType<LaunchController>();
        Rigidbody2D body = launcher != null ? launcher.GetComponent<Rigidbody2D>() : null;
        System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        System.Reflection.MethodInfo aimToLaunch = typeof(LaunchController).GetMethod("AimToLaunch", hidden);
        System.Reflection.MethodInfo magnet = typeof(LaunchController).GetMethod("ApplyBumperMagnet", hidden);
        System.Reflection.MethodInfo trajectory = typeof(LaunchController).GetMethod("UpdateTrajectory", hidden);
        System.Reflection.MethodInfo hide = typeof(LaunchController).GetMethod("HideTrajectory", hidden);
        System.Reflection.FieldInfo highlight = typeof(LaunchController).GetField("bumperHighlight", hidden);
        if (launcher == null || body == null || aimToLaunch == null || magnet == null || trajectory == null || highlight == null)
        {
            problems.Add("aim: LaunchController parts not found");
            return;
        }

        // a short pull still launches at full power when the setting is on
        Vector2 shortPull = (Vector2)aimToLaunch.Invoke(launcher, new object[] { new Vector2(0f, 0.5f) });
        bool full = Mathf.Abs(shortPull.magnitude - launcher.maxDragDistance) < 0.01f;
        if (Difficulty.FullPowerShots != full)
            problems.Add("aim: full-power shots is " + Difficulty.FullPowerShots + " but a short pull gave " + shortPull.magnitude);

        // the nearest bumper above the pad: aim so the line just misses it
        Bumper target = null;
        foreach (Bumper bumper in Bumper.All)
        {
            if (bumper != null && !bumper.isBramble && bumper.transform.position.y > body.position.y &&
                (target == null || Vector2.Distance(bumper.transform.position, body.position) <
                    Vector2.Distance(target.transform.position, body.position)))
                target = bumper;
        }
        if (target == null)
        {
            problems.Add("aim: no bumper to aim at");
            return;
        }
        Vector2 toBumper = (Vector2)target.transform.position - body.position;
        float touch = CameraFit.WorldRadius(target, 0.55f) + CameraFit.WorldRadius(launcher, 0.5f);
        float distance = toBumper.magnitude;
        float missAngle = Mathf.Asin(Mathf.Clamp01((touch + 0.1f) / distance));
        Vector2 aim = Rotate(toBumper.normalized, missAngle * Mathf.Rad2Deg);
        Vector2 pulled = (Vector2)magnet.Invoke(launcher, new object[] { aim });
        float sideAfter = Mathf.Abs(pulled.x * toBumper.y - pulled.y * toBumper.x);
        float needed = (missAngle - Mathf.Asin(Mathf.Clamp01(touch * 0.7f / distance))) * Mathf.Rad2Deg;
        float allowed = GameSettings.AimAssist ? GameTuning.Get.magnetDegrees * Difficulty.AssistStrength : 0f;
        bool shouldPull = allowed >= needed;
        if (shouldPull && sideAfter >= touch)
            problems.Add("aim: the magnet did not pull the line onto " + target.name);
        if (!shouldPull && Vector2.Angle(aim, pulled) > 0.01f)
            problems.Add("aim: the magnet turned the line with assist " + allowed.ToString("0.0") + " degrees");
        report.AppendLine("aim: full power " + Difficulty.FullPowerShots + ", magnet " +
            (shouldPull ? "pulled a near miss onto " + target.name : "off for this difficulty/setting (needs " +
            needed.ToString("0.0") + ", has " + allowed.ToString("0.0") + " degrees)"));

        // aiming straight at the bumper lights it up
        trajectory.Invoke(launcher, new object[] { toBumper.normalized, 9f });
        SpriteRenderer ring = highlight.GetValue(launcher) as SpriteRenderer;
        if (ring == null || !ring.enabled || Vector2.Distance(ring.transform.position, target.transform.position) > 0.01f)
            problems.Add("aim: aiming at " + target.name + " did not light it up");
        else
            report.AppendLine("aim: the aim line lights up " + target.name + " when it's going to hit it");
        if (hide != null)
            hide.Invoke(launcher, null);
    }

    // fix 7 + 8: the new Mossback and Voltis art on the pad (pictures), and the Beast gently bobs while it waits
    IEnumerator CheckIdleAndArt(string shots)
    {
        BeastController beast = BeastController.Active;
        LaunchController launcher = beast != null ? beast.GetComponent<LaunchController>() : null;
        Camera camera = Camera.main;
        if (beast == null || launcher == null || camera == null)
        {
            problems.Add("idle: no Beast in stage 5");
            yield break;
        }
        while (beast.State != BeastState.AtPad)
            yield return null;

        float lowest = float.MaxValue, highest = float.MinValue;
        float until = Time.time + 2f;
        while (Time.time < until)
        {
            float y = beast.transform.position.y;
            lowest = Mathf.Min(lowest, y);
            highest = Mathf.Max(highest, y);
            yield return null;
        }
        float bob = highest - lowest;
        if (bob < 0.01f || bob > 0.2f)
            problems.Add("idle: the Beast moved " + bob.ToString("0.000") + " up and down on the pad (expected a gentle bob)");
        else
            report.AppendLine("idle: bobs " + bob.ToString("0.000") + " units on the pad");

        foreach (BeastId id in new[] { BeastId.Voltis, BeastId.Mossback })
        {
            beast.SetActiveBeast(id);
            launcher.PlaySwapPop();
            for (int frame = 0; frame < 20; frame++)
                yield return null;
            SpriteRenderer look = beast.GetComponent<SpriteRenderer>();
            Sprite art = PlaceholderArt.BeastIdle(id);
            if (art == null || look == null || (look.sprite != art && look.sprite != PlaceholderArt.BeastBall(id)))
                problems.Add("art: " + id + " on the pad doesn't use Beast_" + id + "_Idle");
            else if (Mathf.Abs(art.bounds.size.x - 1.22f) > 0.02f)
                problems.Add("art: " + id + " is " + art.bounds.size.x.ToString("0.00") + " wide, Choma is 1.22");
            RenderTexture target = new RenderTexture(540, 1200, 24);
            camera.targetTexture = target;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            Capture(camera, target, Path.Combine(shots, "pad_" + id + ".png"));
            camera.targetTexture = null;
            Object.Destroy(target);
        }
        report.AppendLine("art: Voltis and Mossback use the new pictures on the pad (1.22 wide, like Choma)");
    }

    // fix 8: the Beasts tab portraits breathe and bob (picture of the tab too)
    IEnumerator CheckBeastsTab(string shots)
    {
        AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/GameHub.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone)
            yield return null;
        for (int frame = 0; frame < 30; frame++)
            yield return null;

        GameHubController hub = Object.FindAnyObjectByType<GameHubController>();
        System.Reflection.MethodInfo openBeasts = typeof(GameHubController).GetMethod("OpenBeasts",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (hub == null || openBeasts == null)
        {
            problems.Add("beasts tab: the hub or its Beasts tab was not found");
            yield break;
        }
        openBeasts.Invoke(hub, null);
        for (int frame = 0; frame < 10; frame++)
            yield return null;

        BeastIdleMotion[] idles = Object.FindObjectsByType<BeastIdleMotion>();
        int moving = 0;
        foreach (BeastIdleMotion idle in idles)
        {
            RectTransform rect = idle.transform as RectTransform;
            if (!idle.enabled || rect == null)
                continue;
            Vector2 before = rect.anchoredPosition;
            Vector3 scaleBefore = rect.localScale;
            float until = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < until)
                yield return null;
            if (rect != null && ((rect.anchoredPosition - before).sqrMagnitude > 0.0001f || rect.localScale != scaleBefore))
                moving++;
        }
        if (idles.Length < 5 || moving == 0)
            problems.Add("beasts tab: " + idles.Length + " portraits have the idle animation, " + moving + " moving");
        else
            report.AppendLine("beasts tab: " + idles.Length + " portraits with the idle animation, " + moving + " moving");

        Camera camera = Camera.main;
        if (camera != null)
        {
            RenderTexture target = new RenderTexture(540, 1200, 24);
            camera.targetTexture = target;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            Capture(camera, target, Path.Combine(shots, "beasts_tab.png"));
            camera.targetTexture = null;
            Object.Destroy(target);
        }
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    static Collider2D Wall(string prefix)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.StartsWith(prefix))
                return root.GetComponent<Collider2D>();
        }
        return null;
    }

    // saves what the camera sees (the HUD is drawn on top by moving its canvases onto the camera)
    static void Capture(Camera camera, RenderTexture target, string path)
    {
        List<Canvas> moved = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>())
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder += 1000;
                moved.Add(canvas);
            }
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        picture.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(path, picture.EncodeToPNG());
        Object.Destroy(picture);
        foreach (Canvas canvas in moved)
        {
            canvas.sortingOrder -= 1000;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }

    void CollectErrors(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(type + ": " + message);
    }

    static string Environment(string name, string fallback)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}
#endif
