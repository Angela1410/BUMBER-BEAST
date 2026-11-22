#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BumperBeastTools
{
    private const string MenuRoot = "Bumper Beast/";

    [MenuItem(MenuRoot + "Add Bumper/Green")]
    private static void AddGreenBumper()
    {
        AddBumper(BumperType.Green);
    }

    [MenuItem(MenuRoot + "Add Bumper/Blue")]
    private static void AddBlueBumper()
    {
        AddBumper(BumperType.Blue);
    }

    [MenuItem(MenuRoot + "Add Bumper/Red")]
    private static void AddRedBumper()
    {
        AddBumper(BumperType.Red);
    }

    [MenuItem(MenuRoot + "Add Enemy Route Point")]
    private static void AddEnemyRoutePoint()
    {
        Transform parent = GetOrCreateContainer("EnemyRoute");
        Transform point = AddNumberedChild(parent, "RoutePoint", GetNextDirection(parent));
        RegisterAndSyncRoute(parent, point);
    }

    [MenuItem(MenuRoot + "Add Bumper Spawn Point")]
    private static void AddBumperSpawnPoint()
    {
        Transform parent = GetOrCreateContainer("BumperFixedSpawnPoints");
        Transform point = AddNumberedChild(parent, "BumperSpawnPoint", GetNextDirection(parent));
        RegisterAndSyncSpawnPoints(parent, point);
    }

    private static void AddBumper(BumperType bumperType)
    {
        GameObject prefab = FindBumperPrefab();
        if (prefab == null)
            return;

        Vector3 position = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;
        position.z = 0f;

        GameObject bumper = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (bumper == null)
            return;

        Undo.RegisterCreatedObjectUndo(bumper, "Add " + bumperType + " Bumper");
        bumper.transform.position = position;
        Bumper bumperComponent = bumper.GetComponentInChildren<Bumper>();
        if (bumperComponent != null)
            bumperComponent.type = bumperType;

        Selection.activeGameObject = bumper;
        EditorUtility.SetDirty(bumper);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            bumper.scene);
    }

    private static GameObject FindBumperPrefab()
    {
        BumperManager manager = Object.FindAnyObjectByType<BumperManager>();
        if (manager != null && manager.bumperPrefab != null)
            return manager.bumperPrefab;

        foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
        {
            if (!assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                continue;

            if (prefab.GetComponentInChildren<Bumper>(true) != null)
                return prefab;
        }

        return null;
    }

    private static Transform GetOrCreateContainer(string objectName)
    {
        GameObject containerObject = GameObject.Find(objectName);
        if (containerObject != null)
            return containerObject.transform;

        Vector3 position = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;
        position.z = 0f;

        containerObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(containerObject, "Create " + objectName);
        containerObject.transform.position = position;
        return containerObject.transform;
    }

    private static Transform AddNumberedChild(Transform parent, string prefix, Vector3 position)
    {
        int nextNumber = GetNextNumber(parent, prefix);
        GameObject pointObject = new GameObject(prefix + "_" + nextNumber.ToString("00"));
        Undo.RegisterCreatedObjectUndo(pointObject, "Add " + prefix);
        pointObject.transform.SetParent(parent, false);
        pointObject.transform.position = position;
        Selection.activeGameObject = pointObject;
        return pointObject.transform;
    }

    private static int GetNextNumber(Transform parent, string prefix)
    {
        int highest = 0;
        for (int index = 0; index < parent.childCount; index++)
        {
            string name = parent.GetChild(index).name;
            if (!name.StartsWith(prefix + "_", System.StringComparison.Ordinal))
                continue;

            string suffix = name.Substring(prefix.Length + 1);
            if (int.TryParse(suffix, out int number))
                highest = Mathf.Max(highest, number);
        }

        return highest + 1;
    }

    private static Vector3 GetNextDirection(Transform parent)
    {
        if (parent.childCount == 0)
            return parent.position;

        Transform last = parent.GetChild(parent.childCount - 1);
        if (parent.childCount == 1)
            return last.position + Vector3.right;

        Transform previous = parent.GetChild(parent.childCount - 2);
        Vector3 direction = last.position - previous.position;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.right;

        return last.position + direction.normalized;
    }

    private static void RegisterAndSyncRoute(Transform parent, Transform point)
    {
        WaveManager[] managers = Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include);
        foreach (WaveManager manager in managers)
        {
            if (manager == null)
                continue;

            var points = new List<Transform>();
            for (int index = 0; index < parent.childCount; index++)
                points.Add(parent.GetChild(index));

            manager.routeWaypoints = points.ToArray();
            EditorUtility.SetDirty(manager);
        }

        MarkSceneDirty(point);
    }

    private static void RegisterAndSyncSpawnPoints(Transform parent, Transform point)
    {
        BumperManager[] managers = Object.FindObjectsByType<BumperManager>(FindObjectsInactive.Include);
        foreach (BumperManager manager in managers)
        {
            if (manager == null)
                continue;

            var points = new List<Transform>();
            for (int index = 0; index < parent.childCount; index++)
                points.Add(parent.GetChild(index));

            manager.fixedSpawnPoints = points.ToArray();
            EditorUtility.SetDirty(manager);
        }

        MarkSceneDirty(point);
    }

    private static void MarkSceneDirty(Transform transform)
    {
        if (transform == null)
            return;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            transform.gameObject.scene);
    }
}
#endif
