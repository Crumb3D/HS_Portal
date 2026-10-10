using HarmonyLib;
using UnityEngine;

/// <summary>
/// Same hit wiring as HS_Doors: ModelEntity shots land on the Unity mesh.
/// Without T_Block + RootTransformRefParent on every child, the admin digger
/// "tings" and the voxel never takes damage.
/// Mesh must sit at prefab origin so TowardsPlacer rotates around the cell
/// centre. The bundle still ships children at +0.5; recenter at ghost + place.
/// </summary>
public class BlockHSPortalCube : Block
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        WireHit(_ebcd);
    }

    public static void WireHit(BlockEntityData ebcd)
    {
        if (ebcd == null || ebcd.transform == null) return;
        try
        {
            var root = ebcd.transform;
            RecenterPrefab(root);
            StripChildColliders(root);
            EnsureRootCollider(root);
            SetHitTag(root, root);
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Warn("Cube hit wire failed: " + e.Message);
        }
    }

    public static void RecenterPrefab(Transform root)
    {
        if (root == null) return;
        foreach (Transform t in root)
        {
            if (t == null) continue;
            var p = t.localPosition;
            if (Mathf.Abs(p.x - 0.5f) < 0.06f && Mathf.Abs(p.y - 0.5f) < 0.06f && Mathf.Abs(p.z - 0.5f) < 0.06f)
                t.localPosition = p - new Vector3(0.5f, 0.5f, 0.5f);
        }
    }

    static void StripChildColliders(Transform root)
    {
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || cols[i].transform == root) continue;
            UnityEngine.Object.Destroy(cols[i]);
        }
    }

    static void EnsureRootCollider(Transform root)
    {
        var box = root.GetComponent<BoxCollider>();
        if (box == null) box = root.gameObject.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = Vector3.one;
        box.isTrigger = false;
    }

    static void SetHitTag(Transform root, Transform t)
    {
        t.tag = "T_Block";
        foreach (Transform child in t)
            SetHitTag(root, child);
        if (root == t) return;
        var href = t.GetComponent<RootTransformRefParent>();
        if (href == null) href = t.gameObject.AddComponent<RootTransformRefParent>();
        href.RootTransform = root;
    }
}

[HarmonyPatch(typeof(BlockShapeModelEntity), "CloneModel", new[] { typeof(BlockValue), typeof(Transform) })]
static class HSPortalCubeGhostPatch
{
    static void Postfix(BlockValue _blockValue, Transform __result)
    {
        try
        {
            if (__result == null || !(_blockValue.Block is BlockHSPortalCube)) return;
            BlockHSPortalCube.RecenterPrefab(__result);
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Warn("Cube ghost recenter failed: " + e.Message);
        }
    }
}
