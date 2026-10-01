using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ApplyAk47Model
{
    // Reapplies the currently imported AK-47 asset after model replacement.
    const string ModelPath = "Assets/Custom/Weapons/AK47/ak47.fbx";
    const string BlasterPrefabPath = "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab";

    [InitializeOnLoadMethod]
    static void ScheduleApply()
    {
        EditorApplication.delayCall += Apply;
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("AK47: model import failed.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BlasterPrefabPath);
        try
        {
            Transform gunRoot = FindChild(prefabRoot.transform, "GunRoot");
            Transform oldVisual = FindChild(prefabRoot.transform, "WeaponMesh_Pistol");
            if (gunRoot == null || oldVisual == null)
            {
                Debug.LogError("AK47: GunRoot or original weapon visual was not found.");
                return;
            }

            Transform previousAk = FindChild(gunRoot, "AK47_Model");
            if (previousAk != null)
                Object.DestroyImmediate(previousAk.gameObject);

            Bounds targetBounds = GetBoundsInRoot(oldVisual, gunRoot);
            GameObject ak = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            ak.name = "AK47_Model";
            ak.transform.SetParent(gunRoot, false);
            ak.transform.localPosition = Vector3.zero;
            // The source model is authored side-on: stock at -X, muzzle at +X.
            // Rotate +X toward the player's forward (+Z) without rolling/flipping the silhouette.
            ak.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            ak.transform.localScale = Vector3.one;

            Bounds initialBounds = GetBoundsInRoot(ak.transform, gunRoot);
            float targetLength = Mathf.Max(targetBounds.size.x, targetBounds.size.y, targetBounds.size.z);
            float modelLength = Mathf.Max(initialBounds.size.x, initialBounds.size.y, initialBounds.size.z);
            float uniformScale = modelLength > 0.0001f ? targetLength / modelLength : 1f;
            ak.transform.localScale = Vector3.one * uniformScale;

            Bounds fittedBounds = GetBoundsInRoot(ak.transform, gunRoot);
            ak.transform.localPosition += targetBounds.center - fittedBounds.center;

            // Keep gameplay objects and muzzle intact; hide only the old rendered gun.
            oldVisual.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BlasterPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"AK47: applied to default blaster. Scale={uniformScale:F4}, Rotation={ak.transform.localEulerAngles}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static Quaternion FindBestRotation(Transform model, Transform root, Vector3 targetSize)
    {
        Quaternion best = Quaternion.identity;
        float bestScore = float.MaxValue;
        var candidates = new List<Quaternion>();
        for (int x = 0; x < 4; x++)
        for (int y = 0; y < 4; y++)
        for (int z = 0; z < 4; z++)
            candidates.Add(Quaternion.Euler(x * 90f, y * 90f, z * 90f));

        foreach (Quaternion candidate in candidates)
        {
            model.localRotation = candidate;
            Bounds bounds = GetBoundsInRoot(model, root);
            float modelMax = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float targetMax = Mathf.Max(targetSize.x, targetSize.y, targetSize.z);
            if (modelMax < 0.0001f || targetMax < 0.0001f)
                continue;
            Vector3 a = bounds.size / modelMax;
            Vector3 b = targetSize / targetMax;
            float score = (a - b).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    static Bounds GetBoundsInRoot(Transform visual, Transform root)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        bool initialized = false;
        Bounds result = default;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 local = root.InverseTransformPoint(corner);
                if (!initialized)
                {
                    result = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else
                    result.Encapsulate(local);
            }
        }
        return result;
    }

    static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindChild(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
