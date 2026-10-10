using UnityEngine;

/// <summary>
/// Same hit wiring as HS_Doors: ModelEntity shots land on the Unity mesh.
/// Without T_Block + RootTransformRefParent on every child, the admin digger
/// "tings" and the voxel never takes damage.
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
            StripChildColliders(root);
            EnsureRootCollider(root);
            SetHitTag(root, root);
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Warn("Cube hit wire failed: " + e.Message);
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
        box.center = new Vector3(0.5f, 0.5f, 0.5f);
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
