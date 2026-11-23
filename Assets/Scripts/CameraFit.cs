using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// fits the camera to the phone so the whole play field is always on screen.
// the stages were made for a 9:16 screen (camera size 9, the side walls right at the screen edges).
// taller phones: we zoom out until the full width fits, so the extra space goes to the top and bottom.
// wider screens (tablets): the height stays and the side walls move out to the screen edges.
// either way the side walls end up exactly at the edges of what you see.
// it adds itself to the camera of every stage (any scene with a wall-left), so the scenes don't change
[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    // half the width between the side walls as the stages were built (5.05). this is the narrowest
    // the field ever gets, so anything inside it is on screen on every phone
    public static float DesignHalfWidth { get; private set; } = 5.05f;
    // half the width between the side walls right now (wider on tablets)
    public static float HalfWidth { get; private set; } = 5.05f;
    public static float FieldCenterX { get; private set; }
    // the inside edges of the top and bottom walls
    public static float FieldTop { get; private set; } = 8.98f;
    public static float FieldBottom { get; private set; } = -8.98f;

    private sealed class Wall
    {
        public Transform transform;
        public BoxCollider2D box;
        public Vector2 center; // in the world, as the scene has it
        public Vector2 size;   // in the world, as the scene has it
    }

    private sealed class Backdrop
    {
        public Transform transform;
        public Vector3 startScale;
        public Bounds startBounds;
    }

    private Camera cam;
    private float designSize;
    private Wall left, right, top, bottom;
    private readonly List<Backdrop> backdrops = new List<Backdrop>();
    private float fittedAspect = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // runs after the scene's Awake calls and before any Start, so everything starts on the fitted field
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindWall(scene, "wall-left") == null)
            return; // not a stage
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic || camera.GetComponent<CameraFit>() != null)
            return;
        camera.gameObject.AddComponent<CameraFit>();
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        designSize = cam.orthographicSize;
        Scene scene = gameObject.scene;
        left = MakeWall(FindWall(scene, "wall-left"));
        right = MakeWall(FindWall(scene, "wall-right"));
        top = MakeWall(FindWall(scene, "wall-top"));
        bottom = MakeWall(FindWall(scene, "wall-bottom"));

        // measure the field from the walls the scene was built with
        if (left != null && right != null)
        {
            float leftInside = left.center.x + left.size.x * 0.5f;
            float rightInside = right.center.x - right.size.x * 0.5f;
            DesignHalfWidth = Mathf.Max(1f, (rightInside - leftInside) * 0.5f);
            FieldCenterX = (rightInside + leftInside) * 0.5f;
        }
        if (top != null)
            FieldTop = top.center.y - top.size.y * 0.5f;
        if (bottom != null)
            FieldBottom = bottom.center.y + bottom.size.y * 0.5f;

        FindBackdrops(scene);
        Fit();
    }

    // the window can change size (web, Windows, the editor) or the phone can turn
    void LateUpdate()
    {
        if (!Mathf.Approximately(cam.aspect, fittedAspect))
            Fit();
    }

    void Fit()
    {
        float aspect = cam.aspect;
        if (aspect <= 0.01f)
            return;
        fittedAspect = aspect;

        // never closer than the stage was made for, zoomed out as far as the full width needs
        float size = Mathf.Max(designSize, DesignHalfWidth / aspect);
        cam.orthographicSize = size;
        HalfWidth = size * aspect;

        // side walls: inside edge exactly at the screen edge, tall enough to cover the whole screen
        float height = size * 2f + 4f;
        PlaceSideWall(left, FieldCenterX - HalfWidth, -1f, height);
        PlaceSideWall(right, FieldCenterX + HalfWidth, 1f, height);

        // top and bottom walls reach under the side walls, so the corners have no gap
        float thickness = left != null ? left.size.x : 1f;
        StretchWall(top, HalfWidth * 2f + thickness * 2f);
        StretchWall(bottom, HalfWidth * 2f + thickness * 2f);

        FitBackdrops(size, HalfWidth);
    }

    void PlaceSideWall(Wall wall, float insideEdge, float side, float height)
    {
        if (wall == null)
            return;
        Vector3 position = wall.transform.position;
        // the collider's middle is half a wall away from its inside edge
        position.x = insideEdge + side * wall.size.x * 0.5f - WorldOffset(wall).x;
        wall.transform.position = position;
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(wall.transform.lossyScale.y));
        wall.box.size = new Vector2(wall.box.size.x, Mathf.Max(wall.box.size.y, height / scaleY));
    }

    void StretchWall(Wall wall, float width)
    {
        if (wall == null)
            return;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(wall.transform.lossyScale.x));
        wall.box.size = new Vector2(Mathf.Max(wall.box.size.x, width / scaleX), wall.box.size.y);
        // keep it centred on the field
        Vector3 position = wall.transform.position;
        position.x = FieldCenterX - WorldOffset(wall).x;
        wall.transform.position = position;
    }

    static Vector2 WorldOffset(Wall wall)
    {
        Vector3 scale = wall.transform.lossyScale;
        return new Vector2(wall.box.offset.x * scale.x, wall.box.offset.y * scale.y);
    }

    // the big painted background sprite of stages 1-4 (the later stages use EarthEnvironment,
    // which fits itself). we grow it evenly until it covers the taller view, never stretch it
    void FindBackdrops(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.activeInHierarchy || root.GetComponent<EarthEnvironment>() != null)
                continue;
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null || !renderer.enabled || renderer.sprite == null || renderer.sortingOrder > -10)
                continue;
            Bounds bounds = renderer.bounds;
            // small things this far back are decorations, not the background
            if (bounds.size.x < DesignHalfWidth || bounds.size.y < designSize)
                continue;
            backdrops.Add(new Backdrop
            {
                transform = root.transform,
                startScale = root.transform.localScale,
                startBounds = bounds
            });
        }
    }

    void FitBackdrops(float size, float halfWidth)
    {
        Vector2 view = cam.transform.position;
        foreach (Backdrop backdrop in backdrops)
        {
            if (backdrop.transform == null)
                continue;
            // the sprite grows around its pivot (the object's position), so check every side
            Vector2 pivot = backdrop.transform.position;
            Bounds start = backdrop.startBounds;
            float grow = 1f;
            grow = Mathf.Max(grow, Needed(pivot.x, start.min.x, view.x - halfWidth, true));
            grow = Mathf.Max(grow, Needed(pivot.x, start.max.x, view.x + halfWidth, false));
            grow = Mathf.Max(grow, Needed(pivot.y, start.min.y, view.y - size, true));
            grow = Mathf.Max(grow, Needed(pivot.y, start.max.y, view.y + size, false));
            backdrop.transform.localScale = backdrop.startScale * grow;
        }
    }

    // how much a sprite edge (at "edge", grown around "pivot") must grow to reach "target"
    static float Needed(float pivot, float edge, float target, bool lowSide)
    {
        float reach = edge - pivot;
        float wanted = target - pivot;
        if (lowSide ? reach >= -0.001f : reach <= 0.001f)
            return 1f; // this side can't grow toward the target
        float grow = wanted / reach;
        return grow > 1f ? grow + 0.01f : 1f;
    }

    static GameObject FindWall(Scene scene, string prefix)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith(prefix) && root.GetComponent<BoxCollider2D>() != null)
                return root;
        }
        return null;
    }

    static Wall MakeWall(GameObject wallObject)
    {
        if (wallObject == null)
            return null;
        BoxCollider2D box = wallObject.GetComponent<BoxCollider2D>();
        Transform wallTransform = wallObject.transform;
        Vector3 scale = wallTransform.lossyScale;
        return new Wall
        {
            transform = wallTransform,
            box = box,
            center = wallTransform.TransformPoint(box.offset),
            size = new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y))
        };
    }

    // --- KEEPING THINGS INSIDE THE FIELD ---

    // where a round thing (a bumper, a bramble, a boulder) may sit: inside the narrowest field, with at
    // least one bumper width of free space to each wall (GameTuning: Bumper Wall Gap), so it's always on
    // screen and the Beast can fly between it and the wall
    public static Vector2 ClampToField(Vector2 position, float radius)
    {
        float gap = Mathf.Max(0f, GameTuning.Get.bumperWallGap) * radius * 2f;
        float maxX = Mathf.Max(0f, DesignHalfWidth - radius - gap);
        float x = Mathf.Clamp(position.x, FieldCenterX - maxX, FieldCenterX + maxX);
        float low = FieldBottom + radius + gap;
        float high = FieldTop - radius - gap;
        float y = low < high ? Mathf.Clamp(position.y, low, high) : position.y;
        return new Vector2(x, y);
    }

    // the size of a round collider in the world (a bumper is about 0.55)
    public static float WorldRadius(Component thing, float fallback)
    {
        CircleCollider2D circle = thing != null ? thing.GetComponent<CircleCollider2D>() : null;
        if (circle == null)
            return fallback;
        Vector3 scale = circle.transform.lossyScale;
        return circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }
}
