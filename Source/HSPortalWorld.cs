using System.Collections.Generic;
using UnityEngine;

public class HSPortal
{
    public int OwnerId;
    public bool Orange;
    public Vector3 Center;
    public Vector3 Normal;
    public Vector3 Up;
    public Vector3 Right;
    public Quaternion Rotation;
    public float HalfWidth;
    public float HalfHeight;
    public BlockFace Face;
    public Vector3i[] Cells;

    public void FinishAxes()
    {
        Rotation = HSPortalMath.PortalRotation(Normal, Up);
        Right = Rotation * Vector3.right;
        Up = Rotation * Vector3.up;
        Normal = Rotation * Vector3.forward;
    }
}

public class HSPortalPair
{
    public HSPortal Blue;
    public HSPortal Orange;

    public bool Linked { get { return Blue != null && Orange != null; } }

    public HSPortal Get(bool orange)
    {
        return orange ? Orange : Blue;
    }

    public void Set(HSPortal portal)
    {
        if (portal == null) return;
        if (portal.Orange) Orange = portal;
        else Blue = portal;
    }

    public void Clear(bool orange)
    {
        if (orange) Orange = null;
        else Blue = null;
    }

    public void ClearBoth()
    {
        Blue = null;
        Orange = null;
    }
}

public static class HSPortalWorld
{
    static readonly Dictionary<int, HSPortalPair> pairs = new Dictionary<int, HSPortalPair>();

    public static IEnumerable<KeyValuePair<int, HSPortalPair>> All
    {
        get { return pairs; }
    }

    public static HSPortalPair GetPair(int ownerId, bool create)
    {
        HSPortalPair p;
        if (pairs.TryGetValue(ownerId, out p)) return p;
        if (!create) return null;
        p = new HSPortalPair();
        pairs[ownerId] = p;
        return p;
    }

    public static void Put(HSPortal portal)
    {
        if (portal == null) return;
        var pair = GetPair(portal.OwnerId, true);
        pair.Set(portal);
        HSPortalVisual.RebuildOwner(portal.OwnerId);
    }

    public static void ClearOwner(int ownerId)
    {
        HSPortalPair p;
        if (!pairs.TryGetValue(ownerId, out p) || p == null) return;
        p.ClearBoth();
        pairs.Remove(ownerId);
        HSPortalVisual.RebuildOwner(ownerId);
    }

    public static void ClearColour(int ownerId, bool orange)
    {
        var pair = GetPair(ownerId, false);
        if (pair == null) return;
        pair.Clear(orange);
        if (pair.Blue == null && pair.Orange == null) pairs.Remove(ownerId);
        HSPortalVisual.RebuildOwner(ownerId);
    }

    public static void ClearAll()
    {
        pairs.Clear();
    }

    public static void ReplacePair(int ownerId, HSPortal blue, HSPortal orange)
    {
        if (blue == null && orange == null)
        {
            ClearOwner(ownerId);
            return;
        }
        var pair = GetPair(ownerId, true);
        pair.Blue = blue;
        pair.Orange = orange;
        HSPortalVisual.RebuildOwner(ownerId);
    }

    public static void Tick()
    {
        if (!HSPortalNet.IsAuthority) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        var drop = new List<KeyValuePair<int, bool>>();
        foreach (var kv in pairs)
        {
            var pair = kv.Value;
            if (pair == null) continue;
            if (pair.Blue != null && SurfaceGone(world, pair.Blue)) drop.Add(new KeyValuePair<int, bool>(kv.Key, false));
            if (pair.Orange != null && SurfaceGone(world, pair.Orange)) drop.Add(new KeyValuePair<int, bool>(kv.Key, true));
        }
        for (int i = 0; i < drop.Count; i++)
        {
            var d = drop[i];
            HSPortalDebug.Info("Fizzle " + (d.Value ? "orange" : "blue") + " owner " + d.Key);
            ClearColour(d.Key, d.Value);
            HSPortalNet.BroadcastState(d.Key);
            HSPortalNet.TellOwner(d.Key, Localization.Get("hsportalFizzled"));
        }
    }

    static bool SurfaceGone(World world, HSPortal portal)
    {
        if (portal == null || portal.Cells == null || portal.Cells.Length == 0) return true;
        for (int i = 0; i < portal.Cells.Length; i++)
        {
            var cell = portal.Cells[i];
            if (world.GetChunkFromWorldPos(cell) == null) return false;
            if (!HSPortalPlacement.IsLegalFace(world, cell, portal.Face)) return true;
        }
        return false;
    }
}
