using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public static class HSPortalWornBoots
{
    const string Marker = "HSPortalWornBoot";
    static GameObject prefab;
    static bool busy;
    static bool loggedBones;

    public static void Tick()
    {
        if (GameManager.IsDedicatedServer) return;
        if (busy) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null) return;
            var p = world.GetPrimaryPlayer();
            if (p == null) return;
            Apply(p.emodel as EModelSDCS);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Worn boots tick failed", e);
        }
    }

    public static void Apply(EModelSDCS emodel)
    {
        if (emodel == null || busy) return;
        if (GameManager.IsDedicatedServer) return;
        try
        {
            var player = emodel.playerEntity;
            if (player == null) return;
            Sync(emodel.baseRig, emodel.boneCatalog, IsWearing(player));
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Worn boots attach failed", e);
        }
    }

    static bool IsWearing(EntityAlive player)
    {
        if (player == null || player.equipment == null) return false;
        var item = player.equipment.GetSlotItem((int)EquipmentSlots.Feet);
        if (item == null || item.ItemClass == null) return false;
        return item.ItemClass.Name == "hsportalBoots";
    }

    static void Sync(GameObject rig, SDCSUtils.TransformCatalog catalog, bool wear)
    {
        if (rig == null) return;
        var leftFoot = FindBone(catalog, rig.transform, true);
        var rightFoot = FindBone(catalog, rig.transform, false);
        if (!loggedBones)
        {
            loggedBones = true;
            HSPortalDebug.Info("Worn boots bones L=" + (leftFoot != null ? leftFoot.name : "null") + " R=" + (rightFoot != null ? rightFoot.name : "null"));
        }
        bool has = HasMarker(leftFoot) || HasMarker(rightFoot);
        if (!wear)
        {
            if (has)
            {
                Clear(leftFoot);
                Clear(rightFoot);
            }
            return;
        }
        if (has) return;
        HideBaseFeet(rig);
        if (leftFoot == null || rightFoot == null)
        {
            HSPortalDebug.Warn("Worn boots: no foot bones on " + rig.name);
            return;
        }
        var src = Prefab();
        if (src == null)
        {
            HSPortalDebug.Warn("Worn boots: LongFallBoots prefab missing");
            return;
        }
        busy = true;
        try
        {
            Place(src, "BootL", leftFoot);
            Place(src, "BootR", rightFoot);
        }
        finally
        {
            busy = false;
        }
    }

    static bool HasMarker(Transform foot)
    {
        if (foot == null) return false;
        for (int i = 0; i < foot.childCount; i++)
        {
            var c = foot.GetChild(i);
            if (c != null && c.name == Marker) return true;
        }
        return false;
    }

    static void Place(GameObject src, string child, Transform foot)
    {
        var piece = FindNamed(src.transform, child);
        if (piece == null)
        {
            HSPortalDebug.Warn("Worn boots: missing " + child);
            return;
        }
        var go = UnityEngine.Object.Instantiate(piece.gameObject, foot, false);
        go.name = Marker;
        go.transform.SetParent(foot, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, -0.04f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        SetLayer(go, foot.gameObject.layer);
        StripDanger(go);
        HSPortalTint.Paint(go);
    }

    static void StripDanger(GameObject go)
    {
        var cams = go.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cams.Length; i++)
            if (cams[i] != null) UnityEngine.Object.Destroy(cams[i]);
        var listeners = go.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            if (listeners[i] != null) UnityEngine.Object.Destroy(listeners[i]);
        var cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) UnityEngine.Object.Destroy(cols[i]);
        var rbs = go.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
            if (rbs[i] != null) UnityEngine.Object.Destroy(rbs[i]);
        var anims = go.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
            if (anims[i] != null) UnityEngine.Object.Destroy(anims[i]);
    }

    static void Clear(Transform foot)
    {
        if (foot == null) return;
        for (int i = foot.childCount - 1; i >= 0; i--)
        {
            var c = foot.GetChild(i);
            if (c != null && c.name == Marker)
                UnityEngine.Object.Destroy(c.gameObject);
        }
    }

    static void HideBaseFeet(GameObject rig)
    {
        var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < smrs.Length; i++)
        {
            var smr = smrs[i];
            if (smr == null) continue;
            if (smr.name.Equals("feet", StringComparison.OrdinalIgnoreCase) ||
                (smr.transform.parent != null && smr.transform.parent.name.Equals("feet", StringComparison.OrdinalIgnoreCase)))
                smr.enabled = false;
        }
    }

    static void SetLayer(GameObject go, int layer)
    {
        var trs = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            trs[i].gameObject.layer = layer;
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            if (trs[i].name == name) return trs[i];
        return null;
    }

    static Transform FindBone(SDCSUtils.TransformCatalog catalog, Transform rig, bool left)
    {
        var anim = rig != null ? rig.GetComponentInChildren<Animator>(true) : null;
        if (anim != null && anim.isHuman)
        {
            var human = anim.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            if (human != null) return human;
        }
        var names = left
            ? new[] { "LeftFoot", "mixamorig:LeftFoot", "Left_Foot", "foot_l", "Foot_L", "l_foot" }
            : new[] { "RightFoot", "mixamorig:RightFoot", "Right_Foot", "foot_r", "Foot_R", "r_foot" };
        if (catalog != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform t;
                if (catalog.TryGetValue(names[i], out t) && t != null) return t;
            }
        }
        if (rig == null) return null;
        var all = rig.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var n = all[i].name;
            if (n.IndexOf("Foot", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (n.IndexOf("Toe", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            bool isLeft = n.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isRight = n.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0;
            if (left && isLeft) return all[i];
            if (!left && isRight) return all[i];
        }
        return null;
    }

    static GameObject Prefab()
    {
        if (prefab != null) return prefab;
        try
        {
            foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (b == null) continue;
                var go = b.LoadAsset<GameObject>("LongFallBoots");
                if (go != null)
                {
                    prefab = go;
                    return prefab;
                }
            }
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("Worn boots bundle scan failed: " + e.Message);
        }
        return prefab;
    }
}
