using HarmonyLib;
using UnityEngine;

public static class HSPortalTeleporter
{
    static HSPortal ignore;
    static int ignoreOwner;
    static Vector3 pendingVel;
    static bool hasPending;
    static vp_FPController pendingFp;
    static bool loggedSign;
    static float nextTry;

    public static void Tick()
    {
        if (Time.unscaledTime < nextTry) return;
        nextTry = Time.unscaledTime + 0.02f;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        var player = world.GetPrimaryPlayer();
        if (player == null) return;
        var pair = HSPortalWorld.GetPair(player.entityId, false);
        if (pair == null || !pair.Linked) return;

        var mid = CapsuleMid(player);
        ReleaseIgnore(pair, mid);
        TryEnter(world, player, pair.Blue, pair.Orange, mid);
        TryEnter(world, player, pair.Orange, pair.Blue, mid);
    }

    static void TryEnter(World world, EntityPlayerLocal player, HSPortal src, HSPortal dst, Vector3 mid)
    {
        if (src == null || dst == null) return;
        if (ignore != null && ignoreOwner == src.OwnerId && ReferenceEquals(ignore, dst)) return;
        if (!HSPortalMath.InEllipse(src, mid, 0.08f)) return;
        var vel = ReadVelocity(player);
        float into = Vector3.Dot(vel.sqrMagnitude > 0.01f ? vel : (src.Center - mid), -src.Normal);
        float along = Vector3.Dot(mid - src.Center, src.Normal);
        if (into < 0.04f && along > 0.18f) return;
        Teleport(world, player, src, dst, vel);
    }

    static void ReleaseIgnore(HSPortalPair pair, Vector3 mid)
    {
        if (ignore == null) return;
        if (pair.Blue != null && HSPortalMath.InEllipse(pair.Blue, mid, 0.2f)) return;
        if (pair.Orange != null && HSPortalMath.InEllipse(pair.Orange, mid, 0.2f)) return;
        ignore = null;
    }

    static Vector3 CapsuleMid(EntityPlayerLocal player)
    {
        var fp = player.vp_FPController;
        float h = 1.8f;
        if (fp != null && fp.m_CharacterController != null) h = fp.m_CharacterController.height;
        return player.position + Vector3.up * (h * 0.5f);
    }

    static Vector3 ReadVelocity(EntityPlayerLocal player)
    {
        var fp = player.vp_FPController;
        if (fp != null)
        {
            var v = fp.Velocity;
            if (v.sqrMagnitude > 0.0001f) return v;
            return new Vector3(fp.m_MotorThrottle.x + fp.m_ExternalForce.x, fp.m_FallSpeed, fp.m_MotorThrottle.z + fp.m_ExternalForce.z);
        }
        return player.motion;
    }

    static void Teleport(World world, EntityPlayerLocal player, HSPortal src, HSPortal dst, Vector3 vel)
    {
        var fp = player.vp_FPController;
        float radius = 0.4f;
        float height = 1.8f;
        if (fp != null && fp.m_CharacterController != null)
        {
            radius = fp.m_CharacterController.radius;
            height = fp.m_CharacterController.height;
        }

        var newPos = HSPortalMath.TransformPoint(src, dst, player.position);
        newPos += dst.Normal * (radius + 0.18f);
        if (Blocked(world, newPos, radius, height))
        {
            HSPortalDebug.Verbose("Exit blocked at " + newPos);
            return;
        }

        var newVel = HSPortalMath.TransformDirection(src, dst, vel);
        var cam = player.vp_FPCamera;
        Vector3 oldFwd = cam != null && cam.Transform != null ? cam.Transform.forward : player.GetLookRay().direction;
        var newFwd = HSPortalMath.TransformDirection(src, dst, oldFwd);
        float yaw, pitch;
        HSPortalMath.LookYawPitch(newFwd, out yaw, out pitch);

        if (!loggedSign)
        {
            loggedSign = true;
            HSPortalDebug.Info("First teleport fallSpeed=" + (fp != null ? fp.m_FallSpeed.ToString("0.000") : "?")
                + " ccVel=" + vel.ToString("F3")
                + " newVel=" + newVel.ToString("F3")
                + " camPitch=" + (cam != null ? cam.Pitch.ToString("0.00") : "?")
                + " yaw=" + (cam != null ? cam.Yaw.ToString("0.00") : "?")
                + " newPitch=" + pitch.ToString("0.00") + " newYaw=" + yaw.ToString("0.00"));
        }

        player.SetPosition(newPos, true);
        player.SetRotation(new Vector3(pitch, yaw, 0f));
        player.motion = newVel;
        if (fp != null)
        {
            fp.SetPosition(newPos - Origin.position);
            pendingVel = newVel;
            hasPending = true;
            pendingFp = fp;
        }
        if (cam != null) cam.SetRotation(new Vector2(pitch, yaw), true);

        ignore = dst;
        ignoreOwner = dst.OwnerId;
        HSPortalDebug.Verbose("Teleport " + (src.Orange ? "orange" : "blue") + " -> " + (dst.Orange ? "orange" : "blue") + " pos=" + newPos + " vel=" + newVel);
        HSPortalNet.SendTeleport(player.entityId, newPos, yaw, pitch, newVel);
    }

    public static void ApplyPending(vp_FPController fp)
    {
        if (!hasPending || fp == null || !ReferenceEquals(fp, pendingFp)) return;
        hasPending = false;
        pendingFp = null;
        fp.m_MotorThrottle = Vector3.zero;
        fp.m_FallSpeed = pendingVel.y;
        fp.m_ExternalForce = new Vector3(pendingVel.x, 0f, pendingVel.z);
    }

    public static void ApplyRemote(Entity entity, Vector3 pos, float yaw, float pitch, Vector3 vel)
    {
        if (entity == null) return;
        var local = entity as EntityPlayerLocal;
        if (local != null)
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world != null)
            {
                var pair = HSPortalWorld.GetPair(local.entityId, false);
                if (pair != null && pair.Linked)
                {
                    ignore = Vector3.Distance(pos, pair.Blue.Center) < Vector3.Distance(pos, pair.Orange.Center) ? pair.Blue : pair.Orange;
                    ignoreOwner = local.entityId;
                }
            }
            local.SetPosition(pos, true);
            local.SetRotation(new Vector3(pitch, yaw, 0f));
            local.motion = vel;
            var fp = local.vp_FPController;
            if (fp != null)
            {
                fp.SetPosition(pos - Origin.position);
                pendingVel = vel;
                hasPending = true;
                pendingFp = fp;
            }
            var cam = local.vp_FPCamera;
            if (cam != null) cam.SetRotation(new Vector2(pitch, yaw), true);
            return;
        }
        entity.SetPosition(pos, true);
        entity.SetRotation(new Vector3(pitch, yaw, 0f));
        entity.motion = vel;
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
    }
}
