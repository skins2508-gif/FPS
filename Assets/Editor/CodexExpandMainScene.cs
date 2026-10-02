using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexExpandMainScene
{
    const string ScenePath = "Assets/FPS/Scenes/MainScene.unity";
    const string HoverBotPath = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
    const string TurretPath = "Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab";

    [InitializeOnLoadMethod]
    static void ScheduleNaturalConnector()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SceneManager.GetActiveScene().path != ScenePath)
                return;
            if (GameObject.Find("Codex_UpperLowerConnector") != null)
                return;

            BuildNaturalConnector();
        };
    }

    [InitializeOnLoadMethod]
    static void ScheduleConnectorDiagnostic()
    {
        EditorApplication.delayCall += () =>
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                return;
            foreach (Transform transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform.name != "Room_03" && transform.name != "Codex_MapExpansion" && transform.name != "Codex_NaturalConnector")
                    continue;
                Bounds bounds = new Bounds(transform.position, Vector3.zero);
                foreach (Collider collider in transform.GetComponentsInChildren<Collider>())
                {
                    bounds.Encapsulate(collider.bounds);
                    if (transform.name == "Room_03" && collider.bounds.size.y < 1.2f && collider.bounds.size.x > 3f && collider.bounds.size.z > 3f)
                        Debug.Log($"CodexFloor: {GetHierarchyPath(collider.transform)} bounds={collider.bounds}");
                }
                Debug.Log($"CodexBounds: {transform.name} pos={transform.position} bounds={bounds}");
            }
        };
    }

    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject previous = GameObject.Find("Codex_MapExpansion");
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        var root = new GameObject("Codex_MapExpansion");

        Material floorMaterial = FindMaterial("Floor");
        Material wallMaterial = FindMaterial("Wall");

        // Three connected floor slabs extend the map to the east.
        CreateBlock(root.transform, "Arena_Floor_01", new Vector3(22f, -0.5f, 0f), new Vector3(20f, 1f, 24f), floorMaterial);
        CreateBlock(root.transform, "Arena_Floor_02", new Vector3(42f, -0.5f, 0f), new Vector3(20f, 1f, 24f), floorMaterial);
        CreateBlock(root.transform, "Arena_Floor_03", new Vector3(62f, -0.5f, 0f), new Vector3(20f, 1f, 24f), floorMaterial);

        // Perimeter walls leave the west side open as the entrance from the original map.
        CreateBlock(root.transform, "North_Wall", new Vector3(42f, 2f, 12f), new Vector3(60f, 4f, 1f), wallMaterial);
        CreateBlock(root.transform, "South_Wall", new Vector3(42f, 2f, -12f), new Vector3(60f, 4f, 1f), wallMaterial);
        CreateBlock(root.transform, "East_Wall", new Vector3(72f, 2f, 0f), new Vector3(1f, 4f, 25f), wallMaterial);

        // Alternating cover creates two combat lanes and breaks long sight lines.
        Vector3[] covers =
        {
            new Vector3(22f, 1f, -5f), new Vector3(29f, 1f, 5f),
            new Vector3(37f, 1.5f, 0f), new Vector3(45f, 1f, -6f),
            new Vector3(52f, 1f, 6f), new Vector3(60f, 1.5f, 0f),
            new Vector3(66f, 1f, -6f)
        };
        for (int i = 0; i < covers.Length; i++)
        {
            Vector3 scale = i == 2 || i == 5 ? new Vector3(3f, 3f, 6f) : new Vector3(4f, 2f, 3f);
            CreateBlock(root.transform, $"Cover_{i + 1:00}", covers[i], scale, wallMaterial);
        }

        GameObject hoverBot = AssetDatabase.LoadAssetAtPath<GameObject>(HoverBotPath);
        GameObject turret = AssetDatabase.LoadAssetAtPath<GameObject>(TurretPath);

        Vector3[] hoverPositions =
        {
            new Vector3(20f, 0.5f, 5f), new Vector3(31f, 0.5f, -5f),
            new Vector3(43f, 0.5f, 6f), new Vector3(55f, 0.5f, -5f),
            new Vector3(66f, 0.5f, 5f)
        };
        Vector3[] turretPositions =
        {
            new Vector3(34f, 0f, 9f), new Vector3(50f, 0f, -9f),
            new Vector3(68f, 0f, 0f)
        };

        SpawnEnemies(hoverBot, hoverPositions, root.transform, "Arena_HoverBot");
        SpawnEnemies(turret, turretPositions, root.transform, "Arena_Turret");

        BakeNavMeshes();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Codex: MainScene expanded and enemies added successfully.");
    }

    public static void OpenArenaEntrance()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject expansion = GameObject.Find("Codex_MapExpansion");

        // The original room's east wall overlaps the new arena entrance.
        // Clear only a doorway-sized volume, while preserving floors and the expansion itself.
        Bounds doorway = new Bounds(new Vector3(12f, 2f, 0f), new Vector3(5f, 3.5f, 7f));
        var blockedObjects = new System.Collections.Generic.HashSet<GameObject>();

        foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!collider.bounds.Intersects(doorway))
                continue;
            if (expansion != null && collider.transform.IsChildOf(expansion.transform))
                continue;
            if (collider.bounds.size.y < 1.5f)
                continue; // Never remove the floor.

            blockedObjects.Add(collider.gameObject);
        }

        foreach (GameObject blockedObject in blockedObjects)
        {
            Debug.Log($"Codex: removed entrance blocker {GetHierarchyPath(blockedObject.transform)}");
            UnityEngine.Object.DestroyImmediate(blockedObject);
        }

        // Add a short bridge so there is no seam between the old floor and the arena floor.
        GameObject bridge = GameObject.Find("Codex_EntranceBridge");
        if (bridge != null)
            UnityEngine.Object.DestroyImmediate(bridge);
        bridge = CreateBlock(expansion != null ? expansion.transform : null, "Codex_EntranceBridge",
            new Vector3(12f, -0.5f, 0f), new Vector3(6f, 1f, 7f), FindMaterial("Floor"));

        BakeNavMeshes();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Codex: arena entrance opened; removed {blockedObjects.Count} blocking object(s).");
    }

    public static void BuildNaturalConnector()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject oldBridge = GameObject.Find("Codex_EntranceBridge");
        if (oldBridge != null)
            UnityEngine.Object.DestroyImmediate(oldBridge);
        GameObject oldConnector = GameObject.Find("Codex_NaturalConnector");
        if (oldConnector != null)
            UnityEngine.Object.DestroyImmediate(oldConnector);

        var connector = new GameObject("Codex_UpperLowerConnector");
        Material floorMaterial = FindMaterial("Floor");
        Material wallMaterial = FindMaterial("Wall");

        // Clear a broad doorway through the old boundary wall. Thin floors are preserved.
        Bounds opening = new Bounds(new Vector3(36f, -1f, 13f), new Vector3(8f, 5f, 15f));
        var blockers = new System.Collections.Generic.HashSet<GameObject>();
        foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!collider.bounds.Intersects(opening))
                continue;
            if (collider.transform.IsChildOf(connector.transform))
                continue;
            if (collider.bounds.size.y < 1.5f)
                continue;
            if (collider.gameObject.name.StartsWith("Arena_"))
                continue;
            blockers.Add(collider.gameObject);
        }
        foreach (GameObject blocker in blockers)
        {
            Debug.Log($"Codex: connector removed {GetHierarchyPath(blocker.transform)}");
            UnityEngine.Object.DestroyImmediate(blocker);
        }

        // The lower room is on the arena's +Z side. Descend about 2m across the shared wall.
        GameObject ramp = CreateBlock(connector.transform, "LowerToArena_Ramp",
            new Vector3(36f, -1.25f, 13f), new Vector3(7f, 0.6f, 13f), floorMaterial);
        ramp.transform.rotation = Quaternion.Euler(9.5f, 0f, 0f);

        // Flat landings remove gaps at both ends of the slope.
        CreateBlock(connector.transform, "Upper_Landing", new Vector3(36f, -0.3f, 6.2f),
            new Vector3(7f, 0.6f, 2.5f), floorMaterial);
        CreateBlock(connector.transform, "Lower_Landing", new Vector3(36f, -2.3f, 19.8f),
            new Vector3(7f, 0.6f, 2.5f), floorMaterial);

        // Low side rails guide the route without blocking the player's view.
        GameObject northRail = CreateBlock(connector.transform, "Ramp_Rail_North",
            new Vector3(32.35f, -0.45f, 13f), new Vector3(0.3f, 1.1f, 13f), wallMaterial);
        northRail.transform.rotation = Quaternion.Euler(9.5f, 0f, 0f);
        GameObject southRail = CreateBlock(connector.transform, "Ramp_Rail_South",
            new Vector3(39.65f, -0.45f, 13f), new Vector3(0.3f, 1.1f, 13f), wallMaterial);
        southRail.transform.rotation = Quaternion.Euler(9.5f, 0f, 0f);

        BakeNavMeshes();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Codex: natural lower-to-arena connector built; removed {blockers.Count} blocker(s).");
    }

    static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.isStatic = true;
        if (material != null)
            block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }

    static void SpawnEnemies(GameObject prefab, Vector3[] positions, Transform parent, string prefix)
    {
        if (prefab == null)
            throw new InvalidOperationException($"Enemy prefab not found for {prefix}");

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.name = $"{prefix}_{i + 1:00}";
            enemy.transform.SetParent(parent);
            enemy.transform.position = positions[i];
            enemy.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 270f : 90f, 0f);
        }
    }

    static Material FindMaterial(string nameFragment)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{nameFragment} t:Material", new[] { "Assets/FPS" }))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material != null)
                return material;
        }
        return null;
    }

    static void BakeNavMeshes()
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            MethodInfo buildMethod = behaviour.GetType().GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public);
            if (buildMethod != null && behaviour.GetType().Name == "NavMeshSurface")
                buildMethod.Invoke(behaviour, null);
        }
    }
}
