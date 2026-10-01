using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

public static class FixTurretTopDamage
{
    const string TurretPrefabPath = "Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab";

    [InitializeOnLoadMethod]
    static void ScheduleFix()
    {
        EditorApplication.delayCall += Apply;
    }

    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TurretPrefabPath);
        try
        {
            int fixedCount = 0;
            foreach (Transform target in root.GetComponentsInChildren<Transform>(true))
            {
                string normalizedName = target.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (!normalizedName.Contains("turrettop"))
                    continue;

                if (target.GetComponent<Damageable>() == null)
                    target.gameObject.AddComponent<Damageable>();

                if (target.GetComponent<Collider>() == null)
                {
                    Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        Bounds localBounds = CalculateLocalBounds(target, renderers);
                        BoxCollider hitCollider = target.gameObject.AddComponent<BoxCollider>();
                        hitCollider.center = localBounds.center;
                        hitCollider.size = localBounds.size * 1.05f;
                    }
                }

                fixedCount++;
            }

            // Also enlarge the authored top hitbox so animated edge regions remain hittable.
            Transform hitboxTop = FindChild(root.transform, "Hitbox Top");
            if (hitboxTop != null && hitboxTop.TryGetComponent(out BoxCollider box))
            {
                box.size = new Vector3(1.05f, 1.05f, 1.05f);
                box.center = new Vector3(0f, 0f, 0f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, TurretPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"TurretTOP: direct damage receiver applied to {fixedCount} object(s).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
    {
        Bounds result = new Bounds();
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 worldCorner = new Vector3(x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                if (!initialized)
                {
                    result = new Bounds(localCorner, Vector3.zero);
                    initialized = true;
                }
                else
                    result.Encapsulate(localCorner);
            }
        }
        return result;
    }

    static Transform FindChild(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindChild(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
