using System;
using System.Collections.Generic;
using UnityEngine;

public static class HSPortalPlacement
{

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
        var local = player as EntityPlayerLocal;
        if (local != null && local.HitInfo != null && local.HitInfo.bHitValid)
        {
            if (TryBuildAt(world, player.entityId, ray.direction, orange, local.HitInfo, out portal, out fail))
                return true;
        }
        return TryBuildFromRay(world, player.entityId, ray, orange, out portal, out fail);
    }

    public static bool TryBuildFromRay(World world, int ownerId, Ray ray, bool orange, out HSPortal portal, out string fail)
    {
        portal = null;
        fail = Localization.Get("hsportalDenied");
        if (world == null || ray.direction.sqrMagnitude < 0.0001f) return false;
        var shot = new Ray(ray.origin - ray.direction * 0.12f, ray.direction);
        if (!Voxel.Raycast(world, shot, HSPortalMath.PlaceRange, -555528205, 69, 0f)
            && !Voxel.Raycast(world, shot, HSPortalMath.PlaceRange, false, false))
            return false;
        var hit = Voxel.voxelRayHitInfo;
        if (hit == null || !hit.bHitValid) return false;
        return TryBuildAt(world, ownerId, ray.direction, orange, hit, out portal, out fail);
    }

    public static bool TryBuildAt(World world, int ownerId, Vector3 look, bool orange, WorldRayHitInfo hit, out HSPortal portal, out string fail)
    {
        portal = null;
        fail = Localization.Get("hsportalDenied");
        if (world == null || hit == null || !hit.bHitValid) return false;
        Vector3i hitCell;
        BlockFace face;
        Vector3 hitPos;
        if (!ResolveHit(world, hit, out hitCell, out face, out hitPos))
        {
            Spark(hit.hit.pos, orange);
            return false;
        }
        var n = HSPortalMath.FaceNormal(face);
        if (n.sqrMagnitude < 0.5f)
        {
            Spark(hitPos, orange);
            return false;
        }
        if (IsMetal(world, hitCell) && !HSPortalGel.AllowsPortal(hitCell, face))
        {
            fail = Localization.Get("hsportalDeniedMetal");
            Spark(hitPos, orange);
            return false;
        }
        if (!IsSolidSupport(world, hitCell))
        {
            fail = Localization.Get("hsportalDenied");
            HSPortalDebug.Info("Deny support " + BlockName(world, hitCell) + " " + hitCell + " face=" + face);
            Spark(hitPos, orange);
            return false;
        }
        if (IsFaceObstructed(world, hitCell, face))
        {
            fail = Localization.Get("hsportalDeniedBlocked");
            Spark(hitPos, orange);
            return false;
        }
        Vector3i a, b;
        if (!SnapTwoBlocks(world, hitCell, face, hitPos, out a, out b))
        {
            fail = Localization.Get("hsportalDeniedSpace");
            Spark(hitPos, orange);
            return false;
        }
        var cA = HSPortalMath.FaceCenter(a, face);
        var cB = HSPortalMath.FaceCenter(b, face);
        var center = (cA + cB) * 0.5f + n * HSPortalMath.SurfaceOffset;
        var up = cB - cA;
        if (Mathf.Abs(n.y) < 0.55f)
        {
            if (up.y < 0f) up = -up;
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
        }
        else
        {
            var alongLook = Vector3.ProjectOnPlane(look, n);
            if (up.sqrMagnitude < 0.0001f || Vector3.Dot(up, alongLook) < 0f)
                up = alongLook.sqrMagnitude > 0.0001f ? alongLook : HSPortalMath.PortalUp(n, look);
        }
        up.Normalize();

        var other = Other(ownerId, orange);
        if (other != null && Overlaps(center, n, other))
        {
            Spark(hitPos, orange);
            return false;
        }

        portal = new HSPortal();
        portal.OwnerId = ownerId;
        portal.Orange = orange;
        portal.Center = center;
        portal.Normal = n;
        portal.Up = up;
        portal.HalfWidth = 0.48f;
        portal.HalfHeight = 0.98f;
        portal.Face = face;
        portal.Cells = new[] { a, b };
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

    static Vector3 HitWorldPos(WorldRayHitInfo hit, Vector3i cell)
    {
        var pos = hit.hit.pos;
        var mid = new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
        if ((pos - mid).sqrMagnitude > 16f)
            pos = pos + Origin.position;
        return pos;
    }

    static bool ResolveHit(World world, WorldRayHitInfo hit, out Vector3i cell, out BlockFace face, out Vector3 pos)
    {
        cell = hit.hit.blockPos;
        face = hit.hit.blockFace;
        pos = HitWorldPos(hit, cell);
        if (!IsSolidSupport(world, cell) && !IsMetal(world, cell))
        {
            if (IsSolidSupport(world, hit.lastBlockPos) || IsMetal(world, hit.lastBlockPos))
                cell = hit.lastBlockPos;
            else
            {
                var n = HSPortalMath.FaceNormal(face);
                if (n.sqrMagnitude < 0.5f) n = HSPortalMath.FaceNormal(DominantFace(cell, pos));
                var look = hit.ray.direction.sqrMagnitude > 0.0001f ? hit.ray.direction.normalized : n;
                var tries = new[]
                {
                    HSPortalMath.WorldToCell(pos - n * 0.12f),
                    HSPortalMath.WorldToCell(pos + n * 0.12f),
                    HSPortalMath.WorldToCell(pos - n * 0.55f),
                    HSPortalMath.WorldToCell(pos + look * 0.2f),
                    HSPortalMath.WorldToCell(pos + look * 0.55f)
                };
                for (int i = 0; i < tries.Length; i++)
                {
                    if (IsSolidSupport(world, tries[i]) || IsMetal(world, tries[i]))
                    {
                        cell = tries[i];
                        break;
                    }
                }
            }
            pos = HitWorldPos(hit, cell);
        }
        face = FaceFromHit(world, cell, face, pos);
        return IsSolidSupport(world, cell) || IsMetal(world, cell);
    }

    static BlockFace FaceFromHit(World world, Vector3i cell, BlockFace face, Vector3 pos)
    {
        var local = pos - new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
        float ax = Mathf.Abs(local.x);
        float ay = Mathf.Abs(local.y);
        float az = Mathf.Abs(local.z);
        if (ay >= ax && ay >= az && ay > 0.38f)
        {
            var yFace = local.y >= 0f ? BlockFace.Top : BlockFace.Bottom;
            if (IsPortalSurface(world, cell, yFace)) return yFace;
        }
        if (face == BlockFace.None || face == BlockFace.Middle || HSPortalMath.FaceNormal(face).sqrMagnitude < 0.5f)
            return DominantFace(cell, pos);
        return face;
    }

    static BlockFace DominantFace(Vector3i cell, Vector3 worldPos)
    {
        var local = worldPos - new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
        float ax = Mathf.Abs(local.x);
        float ay = Mathf.Abs(local.y);
        float az = Mathf.Abs(local.z);
        if (ay >= ax && ay >= az)
            return local.y >= 0f ? BlockFace.Top : BlockFace.Bottom;
        if (ax >= az)
            return local.x >= 0f ? BlockFace.East : BlockFace.West;
        return local.z >= 0f ? BlockFace.North : BlockFace.South;
    }

    static bool SnapTwoBlocks(World world, Vector3i hit, BlockFace face, Vector3 hitPos, out Vector3i a, out Vector3i b)
    {
        a = hit;
        b = hit;
        if (!IsPortalSurface(world, hit, face)) return false;
        var n = HSPortalMath.FaceNormal(face);
        bool wall = Mathf.Abs(n.y) < 0.55f;
        Vector3i best = hit;
        float bestScore = -9999f;
        bool found = false;
        Vector3i[] steps;
        if (wall)
            steps = new[] { new Vector3i(0, 1, 0), new Vector3i(0, -1, 0) };
        else
            steps = new[] { new Vector3i(1, 0, 0), new Vector3i(-1, 0, 0), new Vector3i(0, 0, 1), new Vector3i(0, 0, -1) };
        var mid = new Vector3(hit.x + 0.5f, hit.y + 0.5f, hit.z + 0.5f);
        var local = hitPos - mid;
        local -= n * Vector3.Dot(local, n);
        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            if (Mathf.Abs(Vector3.Dot(n, new Vector3(s.x, s.y, s.z))) > 0.5f) continue;
            var nb = new Vector3i(hit.x + s.x, hit.y + s.y, hit.z + s.z);
            if (!IsPortalSurface(world, nb, face)) continue;
            float score = Vector3.Dot(local, new Vector3(s.x, s.y, s.z));
            if (wall && s.y > 0) score += 0.15f;
            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                best = nb;
            }
        }
        if (!found) return false;
        b = best;
        return true;
    }

    public static void Spark(Vector3 worldPos, bool orange)
    {
        try
        {
            var col = orange ? new Color(1f, 0.42f, 0.06f, 1f) : new Color(0.2f, 0.55f, 1f, 1f);
            var names = new[] { "nozzleflash", "nozzleflashuzi", "p_sparks_fuse", "spark" };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var pe = new ParticleEffect(names[i], worldPos, Quaternion.identity, 1f, col);
                    if (GameManager.IsDedicatedServer)
                    {
                        if (GameManager.Instance != null)
                            GameManager.Instance.SpawnParticleEffectServer(pe, -1);
                    }
                    else
                        ParticleEffect.SpawnParticleEffect(pe, -1, true, true);
                    break;
                }
                catch { }
            }
            try { Audio.Manager.BroadcastPlay(worldPos, "electric_fence_impact"); } catch { }
        }
        catch (Exception e)
        {
            HSPortalDebug.Verbose("Spark failed: " + e.Message);
        }
    }

    public static bool TryAimSurface(World world, Ray ray, float range, out Vector3i cell, out BlockFace face, out Vector3 pos)
    {
        cell = Vector3i.zero;
        face = BlockFace.None;
        pos = Vector3.zero;
        if (world == null || ray.direction.sqrMagnitude < 0.0001f) return false;
        var shot = new Ray(ray.origin - ray.direction * 0.12f, ray.direction);
        if (!Voxel.Raycast(world, shot, range, -555528205, 69, 0f)
            && !Voxel.Raycast(world, shot, range, false, false))
            return false;
        var hit = Voxel.voxelRayHitInfo;
        if (hit == null || !hit.bHitValid) return false;
        return ResolveHit(world, hit, out cell, out face, out pos);
    }

    public static bool IsPortalSurface(World world, Vector3i cell, BlockFace face)
    {
        if (!IsLegalFace(world, cell, face)) return false;
        if (!IsMetal(world, cell)) return true;
        return HSPortalGel.AllowsPortal(cell, face);
    }

    static bool IsMetal(World world, Vector3i cell)
    {
        if (world == null) return false;
        var bv = world.GetBlock(cell);
        if (bv.isair || bv.Block == null) return false;
        var mat = bv.Block.blockMaterial;
        if (mat == null) return false;
        return IsMetalId(mat.id) || IsMetalId(mat.SurfaceCategory);
    }

    static bool IsMetalId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var s = id.ToLowerInvariant();
        if (s.IndexOf("wood", StringComparison.Ordinal) >= 0) return false;
        if (s.IndexOf("stone", StringComparison.Ordinal) >= 0) return false;
        if (s.IndexOf("concrete", StringComparison.Ordinal) >= 0) return false;
        if (s.IndexOf("dirt", StringComparison.Ordinal) >= 0) return false;
        if (s.IndexOf("asphalt", StringComparison.Ordinal) >= 0) return false;
        if (s.IndexOf("metal", StringComparison.Ordinal) >= 0) return true;
        if (s.IndexOf("steel", StringComparison.Ordinal) >= 0) return true;
        if (s == "iron" || s.StartsWith("miron", StringComparison.Ordinal) || s.StartsWith("iron", StringComparison.Ordinal)) return true;
        if (s.IndexOf("brass", StringComparison.Ordinal) >= 0) return true;
        if (s == "lead" || s.StartsWith("mlead", StringComparison.Ordinal)) return true;
        if (s.IndexOf("stainless", StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    public static bool IsLegalFace(World world, Vector3i cell, BlockFace face)
    {
        if (!IsSolidSupport(world, cell)) return false;
        return !IsFaceObstructed(world, cell, face);
    }

    static string BlockName(World world, Vector3i cell)
    {
        if (world == null) return "null";
        var bv = world.GetBlock(cell);
        if (bv.isair || bv.Block == null) return "air";
        return bv.Block.GetBlockName() ?? "?";
    }

    // Terrain, flagged cubes, :Cube shapes, or any collide block that fills most of the cell.
    // POI walls are often BlockShapeNew without a "Solid" child, so IsSolidCube is false.
    static bool IsSolidSupport(World world, Vector3i cell)
    {
        if (world == null) return false;
        if (world.GetChunkFromWorldPos(cell) == null) return false;
        var bv = world.GetBlock(cell);
        if (bv.isair || bv.Block == null) return false;
        var b = bv.Block;
        if (!b.IsCollideMovement || b.shape == null) return false;
        if (b.shape.IsTerrain() || b.shape.IsSolidCube) return true;
        var name = b.GetBlockName();
        if (name != null && name.IndexOf(":Cube", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        try
        {
            var arr = b.shape.GetBounds(bv);
            if (arr == null || arr.Length == 0) return false;
            var box = arr[0];
            for (int i = 1; i < arr.Length; i++) box.Encapsulate(arr[i]);
            var s = box.size;
            int big = 0;
            if (s.x >= 0.7f) big++;
            if (s.y >= 0.7f) big++;
            if (s.z >= 0.7f) big++;
            return big >= 2 && Mathf.Min(s.x, Mathf.Min(s.y, s.z)) >= 0.35f;
        }
        catch
        {
            return false;
        }
    }

    // Workbench / chest / a real cube on the face. Air-density terrain above a
    // road or pad is empty space — do not treat it as a blocker.
    public static bool IsFaceObstructed(World world, Vector3i cell, BlockFace face)
    {
        var step = HSPortalMath.FaceStep(face);
        var neighbour = new Vector3i(cell.x + step.x, cell.y + step.y, cell.z + step.z);
        if (world.GetChunkFromWorldPos(neighbour) == null) return true;
        var nb = world.GetBlock(neighbour);
        if (nb.isair || nb.Block == null) return false;
        if (nb.Block.shape != null && nb.Block.shape.IsTerrain())
            return world.GetDensity(neighbour) < 0;
        return nb.Block.IsCollideMovement;
    }
}
