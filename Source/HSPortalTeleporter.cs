using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class HSPortalTeleporter
{
    class Gate
    {
        public HSPortal portal;
        public float until;
    }

    static readonly Dictionary<int, Gate> gates = new Dictionary<int, Gate>();
    static readonly List<Entity> found = new List<Entity>();
    static Vector3 pendingVel;
    static bool hasPending;
    static int pendingFrames;
    static vp_FPController pendingFp;
    static float nextTry;

    public static void Tick()
    {
        if (Time.unscaledTime < nextTry) return;
        nextTry = Time.unscaledTime + 0.03f;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null || !pair.Linked) continue;
            Sweep(world, pair.Blue, pair.Orange);
            Sweep(world, pair.Orange, pair.Blue);
        }
    }

    static void Sweep(World world, HSPortal src, HSPortal dst)
    {
        if (src == null || dst == null) return;
        found.Clear();
        var ext = new Vector3(src.HalfWidth + 1.2f, src.HalfHeight + 1.4f, src.HalfWidth + 1.2f);
        var bb = new Bounds(src.Center, ext * 2f);
        world.GetEntitiesInBounds(typeof(Entity), bb, found);
        for (int i = 0; i < found.Count; i++)
            TryEntity(world, found[i], src, dst);
    }

    static void TryEntity(World world, Entity e, HSPortal src, HSPortal dst)
    {
        if (e == null || e.IsDead()) return;
        if (e.AttachedToEntity != null) return;
        if (e is EntityFallingBlock) return;

        var local = e as EntityPlayerLocal;
        if (e is EntityPlayer && local == null) return;
        if (local != null && GameManager.IsDedicatedServer) return;
        if (local == null && !HSPortalNet.IsAuthority) return;

        Gate g;
        if (gates.TryGetValue(e.entityId, out g) && g != null && ReferenceEquals(g.portal, src))
        {
            if (Time.unscaledTime < g.until || Overlaps(src, e)) return;
            gates.Remove(e.entityId);
        }
        if (!Overlaps(src, e)) return;
        TeleportEntity(world, e, src, dst);
    }

    static bool Overlaps(HSPortal src, Entity e)
    {
        var feet = e.position;
        var mid = feet + Vector3.up * Mathf.Max(0.35f, e.boundingBox.size.y * 0.45f);
        if (HSPortalMath.InEllipse(src, mid, 0.22f)) return true;
        if (HSPortalMath.InEllipse(src, feet + Vector3.up * 0.25f, 0.22f)) return true;
        if (HSPortalMath.InEllipse(src, feet + Vector3.up * 1.05f, 0.22f)) return true;
        return false;
    }

    static Vector3 ReadVel(Entity e)
    {
        var local = e as EntityPlayerLocal;
        if (local != null && local.vp_FPController != null)
        {
            var fp = local.vp_FPController;
            return new Vector3(fp.m_MotorThrottle.x + fp.m_ExternalForce.x, fp.m_FallSpeed, fp.m_MotorThrottle.z + fp.m_ExternalForce.z);
        }
        var item = e as EntityItem;
        if (item != null && item.itemRB != null)
            return item.itemRB.velocity;
        if (e is EntityAlive)
            return e.motion;
        if (e.physicsRB != null)
            return e.physicsRB.velocity;
        if (e.motion.sqrMagnitude > 0.0001f) return e.motion;
        return e.physicsVel;
    }

    static void WriteVel(Entity e, Vector3 vel)
    {
        var local = e as EntityPlayerLocal;
        if (local != null && local.vp_FPController != null)
        {
            pendingVel = vel;
            hasPending = true;
            pendingFrames = 5;
            pendingFp = local.vp_FPController;
            return;
        }
        var item = e as EntityItem;
        if (item != null && item.itemRB != null)
        {
            item.itemRB.velocity = vel;
            item.itemRB.angularVelocity *= 0.4f;
            return;
        }
        if (e is EntityAlive)
        {
            e.SetVelocity(vel);
            return;
        }
        if (e.physicsRB != null)
        {
            e.physicsRB.velocity = vel;
            e.physicsVel = vel;
            return;
        }
        e.SetVelocity(vel);
        e.physicsVel = vel;
    }

    static bool IsPhysicsProp(Entity e)
    {
        if (e is EntityItem) return true;
        if (e is EntityAlive) return false;
        return e.physicsRB != null;
    }

    static Vector3 LookFwd(Entity e)
    {
        var local = e as EntityPlayerLocal;
        if (local != null && local.vp_FPCamera != null && local.vp_FPCamera.Transform != null)
            return local.vp_FPCamera.Transform.forward;
        var alive = e as EntityAlive;
        if (alive != null)
        {
            try { return alive.GetLookVector(); }
            catch { }
        }
        return Quaternion.Euler(0f, e.rotation.y, 0f) * Vector3.forward;
    }

    static void TeleportEntity(World world, Entity e, HSPortal src, HSPortal dst)
    {
        float radius = 0.35f;
        float height = 1.8f;
        var size = e.boundingBox.size;
        if (size.y > 0.2f) height = size.y;
        if (size.x > 0.1f) radius = Mathf.Clamp(size.x * 0.5f, 0.12f, 0.55f);

        Vector3 exit;
        bool prop = IsPhysicsProp(e);
        if (prop)
            exit = HSPortalMath.ExitPointPhysics(src, dst, e.position);
        else
        {
            exit = HSPortalMath.ExitPoint(dst, radius, height);
            if (Blocked(world, exit, radius, height))
            {
                exit = dst.Center + dst.Normal * (radius + 0.9f);
                if (Mathf.Abs(dst.Normal.y) < 0.55f)
                    exit.y = dst.Center.y - height * 0.52f;
            }
        }

        var newVel = HSPortalMath.TransformVelocity(src, dst, ReadVel(e));
        float yaw, pitch;
        HSPortalMath.ExitLook(src, dst, LookFwd(e), out yaw, out pitch);
        if (!(e is EntityPlayer)) pitch = 0f;

        e.SetPosition(exit, true);
        if (!prop) e.SetRotation(new Vector3(pitch, yaw, 0f));
        WriteVel(e, newVel);

        var local = e as EntityPlayerLocal;
        if (local != null)
        {
            var fp = local.vp_FPController;
            if (fp != null)
                fp.SetPosition(exit - Origin.position);
            var cam = local.vp_FPCamera;
            if (cam != null) cam.SetRotation(new Vector2(pitch, yaw), true);
            HSPortalNet.SendTeleport(e.entityId, exit, yaw, pitch, newVel);
        }

        gates[e.entityId] = new Gate { portal = dst, until = Time.unscaledTime + 0.55f };
        HSPortalGel.IgnoreUntil(Time.unscaledTime + 0.2f);
        HSPortalDebug.Verbose("Portal " + e.GetType().Name + " " + (src.Orange ? "O" : "B") + "->" + (dst.Orange ? "O" : "B") + " vel=" + newVel);
    }

    public static void ApplyPending(vp_FPController fp)
    {
        if (!hasPending || fp == null || !ReferenceEquals(fp, pendingFp)) return;
        fp.m_MotorThrottle = new Vector3(pendingVel.x, 0f, pendingVel.z);
        fp.m_FallSpeed = pendingVel.y;
        fp.m_ExternalForce = Vector3.zero;
        pendingFrames--;
        if (pendingFrames <= 0)
        {
            hasPending = false;
            pendingFp = null;
        }
    }

    public static void ApplyRemote(Entity entity, Vector3 pos, float yaw, float pitch, Vector3 vel)
    {
        if (entity == null) return;
        var local = entity as EntityPlayerLocal;
        if (local != null)
        {
            gates[local.entityId] = new Gate
            {
                portal = Nearest(local.entityId, pos),
                until = Time.unscaledTime + 0.55f
            };
            local.SetPosition(pos, true);
            local.SetRotation(new Vector3(pitch, yaw, 0f));
            WriteVel(local, vel);
            var fp = local.vp_FPController;
            if (fp != null)
                fp.SetPosition(pos - Origin.position);
            var cam = local.vp_FPCamera;
            if (cam != null) cam.SetRotation(new Vector2(pitch, yaw), true);
            HSPortalGel.IgnoreUntil(Time.unscaledTime + 0.2f);
            return;
        }
        entity.SetPosition(pos, true);
        entity.SetRotation(new Vector3(pitch, yaw, 0f));
        WriteVel(entity, vel);
    }

    static HSPortal Nearest(int ownerId, Vector3 pos)
    {
        var pair = HSPortalWorld.GetPair(ownerId, false);
        if (pair == null || !pair.Linked) return null;
        return Vector3.Distance(pos, pair.Blue.Center) < Vector3.Distance(pos, pair.Orange.Center) ? pair.Blue : pair.Orange;
    }

    static bool Blocked(World world, Vector3 feet, float radius, float height)
    {
        if (world == null) return true;
        float y0 = feet.y + 0.1f;
        float y1 = feet.y + height - 0.1f;
        for (float y = y0; y <= y1 + 0.001f; y += 0.45f)
        {
            for (float x = -radius; x <= radius + 0.001f; x += radius)
            {
                for (float z = -radius; z <= radius + 0.001f; z += radius)
                {
                    if (x * x + z * z > radius * radius + 0.05f) continue;
                    var cell = HSPortalMath.WorldToCell(new Vector3(feet.x + x, y, feet.z + z));
                    if (world.GetChunkFromWorldPos(cell) == null) return true;
                    var bv = world.GetBlock(cell);
                    if (bv.isair || bv.Block == null) continue;
                    if (bv.Block.shape != null && bv.Block.shape.IsTerrain()) return true;
                    if (bv.Block.IsCollideMovement) return true;
                }
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(vp_FPController), "FixedMove")]
public static class HSPortalFixedMovePatch
{
    static void Prefix(vp_FPController __instance)
    {
        try { HSPortalTeleporter.ApplyPending(__instance); }
        catch (System.Exception e) { HSPortalDebug.Error("ApplyPending failed", e); }
        try { HSPortalGel.BeforeMove(__instance); }
        catch (System.Exception e) { HSPortalDebug.Error("Gel move failed", e); }
    }
}
