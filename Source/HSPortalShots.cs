using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class HSPortalShots
{
    static readonly Dictionary<int, float> shotIgnore = new Dictionary<int, float>();

    public static void WarpProjectile(ProjectileMoveScript shot)
    {
        if (shot == null) return;
        int id = shot.GetInstanceID();
        float until;
        if (shotIgnore.TryGetValue(id, out until) && Time.unscaledTime < until) return;

        var worldPos = shot.transform.position + Origin.position;
        var prev = shot.previousPosition + Origin.position;
        var ray = new Ray(prev, worldPos - prev);
        float span = (worldPos - prev).magnitude + 0.15f;
        if (span < 0.02f)
        {
            ray = new Ray(worldPos, shot.flyDirection.sqrMagnitude > 0.0001f ? shot.flyDirection : shot.velocity);
            span = 0.35f;
        }

        HSPortal src, dst;
        float t;
        if (!HSPortalMath.TryRedirectRay(ray, Mathf.Max(span, 0.4f), out src, out dst, out t))
        {
            var near = NearestPortal(worldPos);
            if (near == null || !HSPortalMath.InEllipse(near, worldPos, 0.12f)) return;
            if (!HSPortalMath.TryRedirectRay(new Ray(worldPos, -near.Normal), 1.2f, out src, out dst, out t))
                return;
        }
        if (src == null || dst == null) return;

        var vel = shot.velocity.sqrMagnitude > 0.0001f ? shot.velocity : shot.flyDirection;
        if (vel.sqrMagnitude < 0.0001f) vel = ray.direction;
        var newVel = HSPortalMath.TransformVelocity(src, dst, vel);
        if (newVel.sqrMagnitude < 1e-8f && vel.sqrMagnitude > 1e-8f)
            newVel = dst.Normal * vel.magnitude;
        var exit = dst.Center + dst.Normal * 0.35f;
        shot.transform.position = exit - Origin.position;
        shot.previousPosition = shot.transform.position;
        shot.idealPosition = shot.transform.position;
        shot.velocity = newVel;
        shot.flyDirection = newVel.normalized;
        shot.FinalPosition = exit + newVel;
        shotIgnore[id] = Time.unscaledTime + 0.2f;
    }

    public static void WarpThrown(ThrownWeaponMoveScript shot)
    {
        if (shot == null) return;
        int id = shot.GetInstanceID();
        float until;
        if (shotIgnore.TryGetValue(id, out until) && Time.unscaledTime < until) return;

        var worldPos = shot.transform.position + Origin.position;
        var prev = shot.previousPosition.sqrMagnitude > 0.0001f ? shot.previousPosition : worldPos;
        if ((prev - Origin.position).sqrMagnitude < 1f) prev = shot.previousPosition + Origin.position;
        var ray = new Ray(prev, worldPos - prev);
        float span = (worldPos - prev).magnitude + 0.15f;
        if (span < 0.02f)
        {
            ray = new Ray(worldPos, shot.flyDirection.sqrMagnitude > 0.0001f ? shot.flyDirection : shot.velocity);
            span = 0.35f;
        }

        HSPortal src, dst;
        float t;
        if (!HSPortalMath.TryRedirectRay(ray, Mathf.Max(span, 0.4f), out src, out dst, out t))
        {
            var near = NearestPortal(worldPos);
            if (near == null || !HSPortalMath.InEllipse(near, worldPos, 0.12f)) return;
            if (!HSPortalMath.TryRedirectRay(new Ray(worldPos, -near.Normal), 1.2f, out src, out dst, out t))
                return;
        }
        if (src == null || dst == null) return;

        var vel = shot.velocity.sqrMagnitude > 0.0001f ? shot.velocity : shot.flyDirection;
        if (vel.sqrMagnitude < 0.0001f) vel = ray.direction;
        var newVel = HSPortalMath.TransformVelocity(src, dst, vel);
        if (newVel.sqrMagnitude < 1e-8f && vel.sqrMagnitude > 1e-8f)
            newVel = dst.Normal * vel.magnitude;
        var exit = dst.Center + dst.Normal * 0.35f;
        shot.transform.position = exit - Origin.position;
        shot.previousPosition = shot.transform.position;
        shot.velocity = newVel;
        shot.flyDirection = newVel.normalized;
        shot.FinalPosition = exit + newVel;
        shotIgnore[id] = Time.unscaledTime + 0.2f;
    }

    static HSPortal NearestPortal(Vector3 worldPos)
    {
        HSPortal best = null;
        float bestD = 1.5f;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null) continue;
            Check(pair.Blue, worldPos, ref best, ref bestD);
            Check(pair.Orange, worldPos, ref best, ref bestD);
        }
        return best;
    }

    static void Check(HSPortal p, Vector3 worldPos, ref HSPortal best, ref float bestD)
    {
        if (p == null) return;
        float d = Vector3.Distance(p.Center, worldPos);
        if (d < bestD)
        {
            bestD = d;
            best = p;
        }
    }

    public static void RedirectHit(WorldRayHitInfo hit)
    {
        if (hit == null || !hit.bHitValid) return;
        var ray = hit.ray;
        float maxDist = 80f;
        if (hit.hit.distanceSq > 0f) maxDist = Mathf.Sqrt(hit.hit.distanceSq) + 0.2f;
        HSPortal src, dst;
        float t;
        if (!HSPortalMath.TryRedirectRay(ray, maxDist, out src, out dst, out t)) return;
        if (t * t > hit.hit.distanceSq + 0.05f) return;

        var dir = HSPortalMath.TransformDirection(src, dst, ray.direction);
        if (dir.sqrMagnitude < 0.0001f) dir = dst.Normal;
        var origin = dst.Center + dst.Normal * 0.2f;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (!Voxel.Raycast(world, new Ray(origin, dir), 80f, false, false))
        {
            hit.bHitValid = false;
            return;
        }
        var nh = Voxel.voxelRayHitInfo;
        if (nh == null || !nh.bHitValid) return;
        hit.bHitValid = true;
        hit.ray = nh.ray;
        hit.hit = nh.hit;
        hit.fmcHit = nh.fmcHit;
        hit.tag = nh.tag;
        hit.transform = nh.transform;
        hit.lastBlockPos = nh.lastBlockPos;
        hit.hitCollider = nh.hitCollider;
        hit.hitTriangleIdx = nh.hitTriangleIdx;
    }
}

[HarmonyPatch(typeof(ProjectileMoveScript), "FixedUpdate")]
static class HSPortalProjectilePatch
{
    static void Prefix(ProjectileMoveScript __instance)
    {
        try { HSPortalShots.WarpProjectile(__instance); }
        catch (System.Exception e) { HSPortalDebug.Error("Projectile portal failed", e); }
    }
}

[HarmonyPatch(typeof(ThrownWeaponMoveScript), "FixedUpdate")]
static class HSPortalThrownPatch
{
    static void Prefix(ThrownWeaponMoveScript __instance)
    {
        try { HSPortalShots.WarpThrown(__instance); }
        catch (System.Exception e) { HSPortalDebug.Error("Thrown portal failed", e); }
    }
}

[HarmonyPatch(typeof(ItemActionRanged), "GetExecuteActionTarget")]
static class HSPortalHitscanPatch
{
    static void Postfix(ref WorldRayHitInfo __result)
    {
        try { HSPortalShots.RedirectHit(__result); }
        catch (System.Exception e) { HSPortalDebug.Error("Hitscan portal failed", e); }
    }
}
