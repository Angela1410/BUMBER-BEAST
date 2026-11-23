#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// where the bumpers go in every stage. each spot was picked from that stage's enemy route:
// - a bumper near each bend of the route, where the enemies bunch up
// - placed so a bounce sends the Beast across the enemy lane, not back down to the launch pad
// - the middle lane stays open, so a straight shot from the launch pad always works
// - at least one bumper width from the walls, and room for the Beast between two bumpers
// about 3 bumpers in chapter 1, 4 in chapter 2, 5 in chapter 3 and 6 in chapters 4 and 5.
// stages 9-20 are copies of stage 5, so they share its route and its spots.
// it runs by itself after a script reload (StageWaveLayouts.ApplyPendingChanges calls it) and rewrites
// a stage once when its layout here changes, so hand edits in the scene stay until then.
// the Scene view shows every stage's route and bumpers (see the gizmos at the bottom)
public static class BumperLayouts
{
    private struct Spot
    {
        public readonly float x, y;
        public readonly BumperType type;
        public Spot(float x, float y, BumperType type) { this.x = x; this.y = y; this.type = type; }
    }

    private static Spot G(float x, float y) { return new Spot(x, y, BumperType.Green); }
    private static Spot B(float x, float y) { return new Spot(x, y, BumperType.Blue); }

    private sealed class Layout
    {
        public readonly int version; // change this to place this stage's bumpers again, once
        public readonly Spot[] spots;
        public Layout(int version, params Spot[] spots) { this.version = version; this.spots = spots; }
    }

    // the routes of stages 9-20 are stage 5's route, so these are shared
    private static readonly Spot[] Chapter3 = { G(2.4f, 2.6f), G(1.4f, 0.4f), G(-1.8f, 1f), B(-3.3f, -1.6f), G(3.2f, -1.4f) };
    private static readonly Spot[] Chapter45 = { G(2.4f, 2.6f), G(1.4f, 0.4f), G(-1.8f, 1f), B(-3.3f, -1.6f), G(3.2f, -1.4f), B(-3.3f, 3.6f) };

    private static readonly Dictionary<int, Layout> Layouts = new Dictionary<int, Layout>
    {
        // ---- chapter 1 ----
        // a straight path down the middle: bumpers on both sides, zig-zagging, so a bounce crosses it
        { 1, new Layout(1, G(-2f, 2.6f), G(2f, 1.2f), G(-2.4f, -0.6f)) },
        // zig-zag path: one bumper outside each bend, and a BLUE one for the stage 2 lesson
        { 2, new Layout(1, G(2.8f, 2.4f), B(-2.7f, 3.4f), G(-2.6f, 0.2f)) },
        // two bends: one bumper outside each, one up on the left to send the Beast across
        { 3, new Layout(1, G(3.3f, 0.3f), G(-3.2f, 0.9f), G(-2.2f, 3f)) },
        // the box-shaped path: one inside the box (enemies on three sides), one at each top corner
        { 4, new Layout(1, G(-1.6f, 2.8f), G(1.6f, 0.4f), G(3.3f, 3.3f)) },

        // ---- chapter 2 (the two spots you placed on Nov 16 that still fit the rules are kept) ----
        { 5, new Layout(1, G(2.4f, 2.6f), G(1.4f, 0.4f), G(-1.8f, 1f), B(-3.3f, -1.6f)) },
        { 6, new Layout(1, G(-1.4f, -0.6f), G(-2.4f, 3.4f), G(1.9f, 0.9f), B(3.2f, -1.5f)) },
        { 7, new Layout(1, G(-1.8f, 0.4f), G(2f, 3.2f), G(1.4f, -0.6f), B(-3.3f, 3.4f)) },
        { 8, new Layout(1, G(-2.4f, 3f), G(2.2f, 2.4f), G(1.3f, 0.3f), B(3.3f, -1.6f)) },

        // ---- chapters 3-5 ----
        { 9, new Layout(1, Chapter3) }, { 10, new Layout(1, Chapter3) }, { 11, new Layout(1, Chapter3) }, { 12, new Layout(1, Chapter3) },
        { 13, new Layout(1, Chapter45) }, { 14, new Layout(1, Chapter45) }, { 15, new Layout(1, Chapter45) }, { 16, new Layout(1, Chapter45) },
        { 17, new Layout(1, Chapter45) }, { 18, new Layout(1, Chapter45) }, { 19, new Layout(1, Chapter45) }, { 20, new Layout(1, Chapter45) },
    };

    // the rules every spot is checked against (also drawn in the Scene view)
    private const float BumperRadius = 0.552f; // 0.46 collider x 1.2 scale
    private const float BeastRadius = 0.5f;
    private const float LaneHalfWidth = BumperRadius + BeastRadius + 0.15f;
    private const float LowestSpot = -1.6f;  // above the barrier
    private const float HighestSpot = 3.6f;  // below where the enemies walk in
    private const float PathClearance = 1f;  // bumper middle to the enemy path
    private const float Spacing = 2.2f;      // bumper middles, so the Beast fits between them
    private const string Template = "Assets/Prefab/Bumper_Left.prefab";

    private static readonly Regex StampPattern = new Regex(@"^\s*layoutStamp:\s*(\d+)", RegexOptions.Multiline);

    static float MaxX => CameraFit.DesignHalfWidth - BumperRadius - BumperRadius * 2f * GameTuning.Get.bumperWallGap;

    // each stage remembers which layout it has (stage x 100 + version), so a copied scene gets its own
    static int Stamp(int stage, Layout layout) { return stage * 100 + layout.version; }

    // the stages whose bumpers are older than their layout here get them placed again
    public static void ApplyPending(List<string> problems)
    {
        StringBuilder report = new StringBuilder();
        int saved = 0;
        foreach (KeyValuePair<int, Layout> entry in Layouts)
        {
            string path = "Assets/Scenes/" + GameHubController.GetStageSceneName(entry.Key) + ".unity";
            if (!File.Exists(path))
                continue;
            string text = File.ReadAllText(path);
            if (!ChapterBuilder.IsReady(entry.Key, text))
                continue;
            Match match = StampPattern.Match(text);
            int stamp = match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : 0;
            if (stamp == Stamp(entry.Key, entry.Value))
                continue;
            if (Apply(entry.Key, path, problems))
            {
                saved++;
                report.Append("\nstage ").Append(entry.Key).Append(": ").Append(entry.Value.spots.Length).Append(" bumpers");
            }
        }
        if (saved > 0)
            Debug.Log("Bumper Beast: bumpers placed in " + saved + " stages." + report);
    }

    [MenuItem("Bumper Beast/Place Bumpers Again (All Stages)")]
    public static void ApplyAllFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        List<string> problems = new List<string>();
        foreach (KeyValuePair<int, Layout> entry in Layouts)
        {
            string path = "Assets/Scenes/" + GameHubController.GetStageSceneName(entry.Key) + ".unity";
            if (File.Exists(path))
                Apply(entry.Key, path, problems);
        }
        Report(problems);
    }

    public static void Report(List<string> problems)
    {
        if (problems.Count > 0)
            Debug.LogWarning("Bumper Beast bumpers, needs attention:\n" + string.Join("\n", problems));
    }

    static bool Apply(int stage, string path, List<string> problems)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (wasOpen && scene.isDirty)
        {
            problems.Add("stage " + stage + " is open with unsaved changes, so its bumpers were not moved. Save it and reload scripts.");
            return false;
        }
        if (!wasOpen)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        bool done = false;
        if (PlaceInScene(scene, stage, problems))
        {
            done = EditorSceneManager.SaveScene(scene);
            if (!done)
                problems.Add("Unable to save " + path + ".");
        }

        if (!wasOpen)
            EditorSceneManager.CloseScene(scene, true);
        return done;
    }

    // puts this stage's bumpers in an open scene (the old setup menu items use this too), without saving
    public static bool PlaceInScene(Scene scene, int stage, List<string> problems)
    {
        if (!Layouts.TryGetValue(stage, out Layout layout))
            return false;
        BumperManager manager = FindInScene<BumperManager>(scene);
        if (manager == null)
        {
            problems.Add(scene.path + " has no BumperManager.");
            return false;
        }

        CheckSpots(stage, layout, scene, problems);
        PlaceBumpers(scene, manager, layout, problems);
        manager.layoutStamp = Stamp(stage, layout);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    static void PlaceBumpers(Scene scene, BumperManager manager, Layout layout, List<string> problems)
    {
        // the stage's own bumpers (not Thornmaw's brambles), in name order
        List<Bumper> bumpers = new List<Bumper>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Bumper bumper in root.GetComponentsInChildren<Bumper>(true))
            {
                if (!bumper.isBramble)
                    bumpers.Add(bumper);
            }
        }
        bumpers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        Bumper template = bumpers.Count > 0 ? bumpers[0] : null;
        Bumper prefabBumper = AssetDatabase.LoadAssetAtPath<Bumper>(Template);
        for (int index = 0; index < layout.spots.Length; index++)
        {
            Bumper bumper;
            if (index < bumpers.Count)
                bumper = bumpers[index];
            else
            {
                // one more than the scene has: a copy of the stage's first bumper (same look), or the prefab
                GameObject copy = template != null
                    ? Object.Instantiate(template.gameObject)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefabBumper != null ? prefabBumper.gameObject : null, scene);
                if (copy == null)
                {
                    problems.Add(scene.name + ": no bumper to copy, " + Template + " is missing.");
                    return;
                }
                SceneManager.MoveGameObjectToScene(copy, scene);
                bumper = copy.GetComponent<Bumper>();
            }

            Spot spot = layout.spots[index];
            Transform t = bumper.transform;
            if (t.parent != null)
                t.SetParent(null, true);
            t.position = new Vector3(spot.x, spot.y, t.position.z);
            bumper.type = spot.type;
            bumper.hp = Mathf.Max(1, prefabBumper != null ? prefabBumper.hp : 3);
            // some stage bumpers lost their blue and red pictures: take them from the prefab
            if (prefabBumper != null)
            {
                if (bumper.greenSprite == null) bumper.greenSprite = prefabBumper.greenSprite;
                if (bumper.blueSprite == null) bumper.blueSprite = prefabBumper.blueSprite;
                if (bumper.redSprite == null) bumper.redSprite = prefabBumper.redSprite;
            }
            bumper.gameObject.name = "Bumper_" + (index + 1).ToString("00");
            bumper.gameObject.SetActive(true);
            bumper.RefreshVisuals();
            EditorUtility.SetDirty(bumper);
            EditorUtility.SetDirty(t);
        }

        // bumpers the new layout doesn't need
        for (int index = layout.spots.Length; index < bumpers.Count; index++)
            Object.DestroyImmediate(bumpers[index].gameObject);

        // the old random respawn spots are not used any more (a broken bumper comes back in its own spot)
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "BumperFixedSpawnPoints" || root.name.StartsWith("BumperSpawnPoint_"))
                Object.DestroyImmediate(root);
        }
    }

    // logs any spot that breaks a rule (if someone edits the table above)
    static void CheckSpots(int stage, Layout layout, Scene scene, List<string> problems)
    {
        Vector2[] route = Route(FindInScene<WaveManager>(scene));
        for (int i = 0; i < layout.spots.Length; i++)
        {
            Vector2 p = new Vector2(layout.spots[i].x, layout.spots[i].y);
            string where = "stage " + stage + " bumper " + (i + 1) + " at " + p;
            if (Mathf.Abs(p.x) > MaxX + 0.001f)
                problems.Add(where + " is closer than one bumper width to a wall.");
            if (Mathf.Abs(p.x) < LaneHalfWidth - 0.001f)
                problems.Add(where + " blocks the straight shot up the middle.");
            if (p.y < LowestSpot - 0.001f || p.y > HighestSpot + 0.001f)
                problems.Add(where + " is too low or too high.");
            if (route != null && PathDistance(p, route) < PathClearance - 0.001f)
                problems.Add(where + " is on the enemy path.");
            for (int j = i + 1; j < layout.spots.Length; j++)
            {
                if (Vector2.Distance(p, new Vector2(layout.spots[j].x, layout.spots[j].y)) < Spacing - 0.001f)
                    problems.Add(where + " is too close to bumper " + (j + 1) + " (the Beast can't fit between).");
            }
        }
    }

    static Vector2[] Route(WaveManager waves)
    {
        if (waves == null || waves.routeWaypoints == null)
            return null;
        List<Vector2> points = new List<Vector2>();
        foreach (Transform point in waves.routeWaypoints)
        {
            if (point != null)
                points.Add(point.position);
        }
        return points.ToArray();
    }

    static float PathDistance(Vector2 p, Vector2[] route)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < route.Length; i++)
        {
            Vector2 a = route[i], ab = route[i + 1] - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }

    // --- SCENE VIEW: open any stage and you see its route, the safe zone and the bumpers ---

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawStageGuide(WaveManager waves, GizmoType gizmoType)
    {
        Vector2[] route = Route(waves);
        if (route == null || route.Length < 2)
            return;

        // the route in red, a dot on each bend
        Handles.color = new Color(1f, 0.3f, 0.25f, 0.95f);
        for (int i = 0; i + 1 < route.Length; i++)
            Handles.DrawAAPolyLine(4f, route[i], route[i + 1]);
        for (int i = 1; i + 1 < route.Length; i++)
            Handles.DrawSolidDisc(route[i], Vector3.forward, 0.12f);

        // the open middle lane and where bumpers may go (one bumper width from the walls)
        float maxX = MaxX;
        Handles.color = new Color(1f, 1f, 1f, 0.06f);
        Handles.DrawAAConvexPolygon(new Vector3(-LaneHalfWidth, LowestSpot - 2f), new Vector3(LaneHalfWidth, LowestSpot - 2f),
            new Vector3(LaneHalfWidth, HighestSpot + 1f), new Vector3(-LaneHalfWidth, HighestSpot + 1f));
        Handles.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Handles.DrawDottedLine(new Vector3(-maxX, LowestSpot), new Vector3(-maxX, HighestSpot), 4f);
        Handles.DrawDottedLine(new Vector3(maxX, LowestSpot), new Vector3(maxX, HighestSpot), 4f);
        Handles.DrawDottedLine(new Vector3(-maxX, LowestSpot), new Vector3(maxX, LowestSpot), 4f);
        Handles.DrawDottedLine(new Vector3(-maxX, HighestSpot), new Vector3(maxX, HighestSpot), 4f);

        // every bumper: its color, its name, and red if it breaks a rule
        foreach (Bumper bumper in Object.FindObjectsByType<Bumper>())
        {
            if (bumper.isBramble || bumper.gameObject.scene != waves.gameObject.scene)
                continue;
            Vector2 p = bumper.transform.position;
            bool ok = Mathf.Abs(p.x) <= maxX + 0.01f && Mathf.Abs(p.x) >= LaneHalfWidth - 0.01f &&
                PathDistance(p, route) >= PathClearance - 0.01f;
            Handles.color = !ok ? Color.red
                : bumper.type == BumperType.Blue ? new Color(0.35f, 0.6f, 1f)
                : bumper.type == BumperType.Red ? new Color(1f, 0.4f, 0.3f)
                : new Color(0.45f, 0.9f, 0.35f);
            Handles.DrawWireDisc(p, Vector3.forward, BumperRadius, 3f);
            Handles.Label(p + Vector2.up * (BumperRadius + 0.3f), bumper.name + (ok ? string.Empty : "  (breaks a rule)"));
        }
    }
}
#endif
