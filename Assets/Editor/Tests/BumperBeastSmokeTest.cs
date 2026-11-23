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
        yield return new WaitForSeconds(tuning.recallLaunchLock + 0.1f);
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
