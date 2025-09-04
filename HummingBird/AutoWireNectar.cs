#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
// alias to avoid the ambiguity
using UObj = UnityEngine.Object;

public static class AutoWireNectar
{
    [MenuItem("Tools/ML-Agents/Auto-wire Nectar Colliders (robust)")]
    public static void Run()
    {
        int fixedCount = 0, missing = 0;

        Flower[] flowers;
#if UNITY_2023_1_OR_NEWER
        flowers = UnityEngine.Object.FindObjectsByType<Flower>(FindObjectsSortMode.None);
#else
        flowers = UObj.FindObjectsOfType<Flower>(true);
#endif

        foreach (var flower in flowers)
        {
            if (flower == null || flower.nectarCollider != null) continue;

            var cols = flower.transform.GetComponentsInChildren<Collider>(true);
            Collider col =
                cols.FirstOrDefault(c => string.Equals(c.tag, "nectar", System.StringComparison.OrdinalIgnoreCase)) ??
                cols.FirstOrDefault(c => c.gameObject.name.IndexOf("nectar", System.StringComparison.OrdinalIgnoreCase) >= 0) ??
                cols.FirstOrDefault(c => c.isTrigger) ??
                cols.FirstOrDefault();

            if (col != null) { flower.nectarCollider = col; EditorUtility.SetDirty(flower); fixedCount++; }
            else { Debug.LogError($"[FlowerArea] '{flower.name}' still missing a Nectar Collider.", flower); missing++; }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Auto-wire complete. Fixed: {fixedCount}, Still missing: {missing}");
    }
}
#endif
