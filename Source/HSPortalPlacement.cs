using System;
using System.Collections.Generic;
using UnityEngine;

public static class HSPortalPlacement
{
    const float SlideStep = 0.08f;
    const float SlideMax = 0.9f;
    const float Sample = 0.22f;

    public static bool TryPlace(World world, EntityPlayer player, bool orange, out string fail)
    {
        fail = null;
        HSPortal portal;
        if (!TryBuild(world, player, orange, out portal, out fail)) return false;
        HSPortalWorld.Put(portal);
        HSPortalDebug.Info("Placed " + (orange ? "orange" : "blue") + " at " + portal.Center + " n=" + portal.Normal + " owner=" + portal.OwnerId);
        return true;
    }

    public static bool TryBuild(World world, EntityPlayer player, bool orange, out HSPortal portal, out string fail)
    {
        portal = null;
        fail = Localization.Get("hsportalDenied");
        if (world == null || player == null) return false;
        Ray ray;
        try { ray = player.GetLookRay(); }
        catch { return false; }
        return TryBuildFromRay(world, player.entityId, ray, orange, out portal, out fail);
    }

    public static bool TryBuildFromRay(World world, int ownerId, Ray ray, bool orange, out HSPortal portal, out string fail)
    {
        portal = null;
        fail = Localization.Get("hsportalDenied");
        if (world == null || ray.direction.sqrMagnitude < 0.0001f) return false;
        if (!Voxel.Raycast(world, ray, HSPortalMath.PlaceRange, false, false)) return false;
        var hit = Voxel.voxelRayHitInfo;
        if (hit == null || !hit.bHitValid) return false;
        return TryBuildAt(world, ownerId, ray.direction, orange, hit, out portal, out fail);
    }

    public static bool TryBuildAt(World world, int ownerId, Vector3 look, bool orange, WorldRayHitInfo hit, out HSPortal portal, out string fail)
    {
        portal = null;
        fail = Localization.Get("hsportalDenied");
        if (world == null || hit == null || !hit.bHitValid) return false;
        var face = hit.hit.blockFace;
        var n = HSPortalMath.FaceNormal(face);
        if (n.sqrMagnitude < 0.5f) return false;
        if (look.sqrMagnitude < 0.0001f) look = n;
        var up = HSPortalMath.PortalUp(n, look);
        var planePt = HSPortalMath.FaceCenter(hit.hit.blockPos, face);
        var seed = HSPortalMath.ProjectOnPlane(hit.hit.pos, planePt, n);
        Vector3 center;
        Vector3i[] cells;
        if (!Fit(world, seed, n, up, face, out center, out cells)) return false;
        center = center + n * HSPortalMath.SurfaceOffset;

        var other = Other(ownerId, orange);
        if (other != null && Overlaps(center, n, other)) return false;

        portal = new HSPortal();
        portal.OwnerId = ownerId;
        portal.Orange = orange;
        portal.Center = center;
        portal.Normal = n;
        portal.Up = up;
        portal.HalfWidth = HSPortalMath.HalfWidth;
        portal.HalfHeight = HSPortalMath.HalfHeight;
        portal.Face = face;
        portal.Cells = cells;
        portal.FinishAxes();
        fail = null;
        return true;
    }

    static HSPortal Other(int ownerId, bool orange)
    {
        var pair = HSPortalWorld.GetPair(ownerId, false);
        if (pair == null) return null;
        return orange ? pair.Blue : pair.Orange;
    }

    static bool Overlaps(Vector3 center, Vector3 n, HSPortal other)
    {
        if (Vector3.Dot(n, other.Normal) < 0.95f) return false;
        var d = center - other.Center;
        if (Mathf.Abs(Vector3.Dot(d, n)) > 0.2f) return false;
        return d.sqrMagnitude < 1.6f;
    }

    static bool Fit(World world, Vector3 seed, Vector3 n, Vector3 up, BlockFace face, out Vector3 center, out Vector3i[] cells)
    {
        center = seed;
        cells = null;
        var rot = HSPortalMath.PortalRotation(n, up);
        var right = rot * Vector3.right;
        var u = rot * Vector3.up;
        Vector3i[] best = null;
        Vector3 bestC = seed;
        float bestDist = float.MaxValue;
        for (float sx = -SlideMax; sx <= SlideMax + 0.001f; sx += SlideStep)
        {
            for (float sy = -SlideMax; sy <= SlideMax + 0.001f; sy += SlideStep)
            {
                var c = seed + right * sx + u * sy;
                Vector3i[] found;
                if (!Covered(world, c, n, right, u, face, out found)) continue;
                float dist = (c - seed).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = found;
                    bestC = c;
                    if (dist < 0.0001f)
                    {
                        center = bestC;
                        cells = best;
                        return true;
                    }
                }
            }
        }
        if (best == null) return false;
        center = bestC;
        cells = best;
        return true;
    }

    static bool Covered(World world, Vector3 center, Vector3 n, Vector3 right, Vector3 up, BlockFace face, out Vector3i[] cells)
    {
        cells = null;
        var set = new List<Vector3i>();
        float hw = HSPortalMath.HalfWidth;
        float hh = HSPortalMath.HalfHeight;
        for (float x = -hw; x <= hw + 0.001f; x += Sample)
        {
            for (float y = -hh; y <= hh + 0.001f; y += Sample)
            {
                if ((x * x) / (hw * hw) + (y * y) / (hh * hh) > 1.02f) continue;
                var pt = center + right * x + up * y;
                var inside = pt - n * 0.08f;
                var cell = HSPortalMath.WorldToCell(inside);
                if (!Contains(set, cell)) set.Add(cell);
                if (!IsLegalFace(world, cell, face)) return false;
            }
        }
        if (set.Count == 0) return false;
        cells = set.ToArray();
        return true;
    }

    static bool Contains(List<Vector3i> list, Vector3i v)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].x == v.x && list[i].y == v.y && list[i].z == v.z) return true;
        return false;
    }

    public static bool IsLegalFace(World world, Vector3i cell, BlockFace face)
    {
        if (world == null) return false;
        if (world.GetChunkFromWorldPos(cell) == null) return false;
        var bv = world.GetBlock(cell);
        if (bv.isair || bv.Block == null) return false;
        var b = bv.Block;
        if (b.shape == null || b.shape.IsTerrain()) return false;
        if (!b.shape.IsSolidCube)
        {
            var name = b.GetBlockName();
            if (name == null || name.IndexOf(":Cube", StringComparison.OrdinalIgnoreCase) < 0) return false;
        }
        if (!b.IsCollideMovement) return false;
        var step = HSPortalMath.FaceStep(face);
        var neighbour = new Vector3i(cell.x + step.x, cell.y + step.y, cell.z + step.z);
        if (world.GetChunkFromWorldPos(neighbour) != null)
        {
            var nb = world.GetBlock(neighbour);
            if (!nb.isair && nb.Block != null && nb.Block.IsCollideMovement && nb.Block.shape != null && !nb.Block.shape.IsTerrain() && nb.Block.shape.IsSolidCube)
                return false;
        }
        return true;
    }
}
