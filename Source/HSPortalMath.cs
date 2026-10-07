using UnityEngine;

public static class HSPortalMath
{
    public const float HalfWidth = 0.58f;
    public const float HalfHeight = 0.98f;
    public const float PlaceRange = 80f;
    public const float SurfaceOffset = 0.03f;
    public const float Thickness = 0.55f;

    public static Vector3 FaceNormal(BlockFace face)
    {
        switch (face)
        {
            case BlockFace.Top: return Vector3.up;
            case BlockFace.Bottom: return Vector3.down;
            case BlockFace.North: return Vector3.forward;
            case BlockFace.South: return Vector3.back;
            case BlockFace.East: return Vector3.right;
            case BlockFace.West: return Vector3.left;
            default: return Vector3.zero;
        }
    }

    public static Vector3i FaceStep(BlockFace face)
    {
        switch (face)
        {
            case BlockFace.Top: return new Vector3i(0, 1, 0);
            case BlockFace.Bottom: return new Vector3i(0, -1, 0);
            case BlockFace.North: return new Vector3i(0, 0, 1);
            case BlockFace.South: return new Vector3i(0, 0, -1);
            case BlockFace.East: return new Vector3i(1, 0, 0);
            case BlockFace.West: return new Vector3i(-1, 0, 0);
            default: return Vector3i.zero;
        }
    }

    public static Vector3 FaceCenter(Vector3i cell, BlockFace face)
    {
        var n = FaceNormal(face);
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f) + n * 0.5f;
    }

    public static Vector3 PortalUp(Vector3 normal, Vector3 look)
    {
        if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.85f)
        {
            var flat = Vector3.ProjectOnPlane(look, normal);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(Vector3.right, normal);
            return flat.normalized;
        }
        return Vector3.up;
    }

    public static Quaternion PortalRotation(Vector3 normal, Vector3 up)
    {
        if (normal.sqrMagnitude < 0.0001f) normal = Vector3.forward;
        var n = normal.normalized;
        var u = Vector3.ProjectOnPlane(up.sqrMagnitude < 0.0001f ? Vector3.up : up, n);
        if (u.sqrMagnitude < 0.0001f) u = Vector3.ProjectOnPlane(Vector3.up, n);
        if (u.sqrMagnitude < 0.0001f) u = Vector3.ProjectOnPlane(Vector3.forward, n);
        return Quaternion.LookRotation(n, u.normalized);
    }

    public static Vector3 ProjectOnPlane(Vector3 point, Vector3 planePoint, Vector3 normal)
    {
        return point - normal * Vector3.Dot(point - planePoint, normal);
    }

    public static Vector3 Yaw180Local(Vector3 local)
    {
        return new Vector3(-local.x, local.y, -local.z);
    }

    public static Vector3 TransformPoint(HSPortal src, HSPortal dst, Vector3 worldPos)
    {
        var rel = Quaternion.Inverse(src.Rotation) * (worldPos - src.Center);
        // Front of src stays in front of dest. Flip X so walking through continues forward
        // without mirroring you into the wall.
        rel = new Vector3(-rel.x, rel.y, rel.z);
        return dst.Center + dst.Rotation * rel;
    }

    public static Vector3 TransformDirection(HSPortal src, HSPortal dst, Vector3 worldDir)
    {
        var rel = Quaternion.Inverse(src.Rotation) * worldDir;
        return dst.Rotation * Yaw180Local(rel);
    }

    public static Vector3 TransformVelocity(HSPortal src, HSPortal dst, Vector3 vel)
    {
        float into = Vector3.Dot(vel, -src.Normal);
        float speed = vel.magnitude;
        if (into < speed * 0.4f) into = Mathf.Max(into, speed);
        if (into < 1.25f) into = 1.25f;
        if (into > 50f) into = 50f;
        var outVel = dst.Normal * into;
        if (Mathf.Abs(src.Normal.y) < 0.55f && Mathf.Abs(dst.Normal.y) < 0.55f)
            outVel.y = vel.y;
        return outVel;
    }

    public static Vector3 ExitPoint(HSPortal dst, float radius, float height)
    {
        if (dst.Normal.y > 0.55f)
            return dst.Center + dst.Normal * 0.3f;
        if (dst.Normal.y < -0.55f)
            return dst.Center + dst.Normal * Mathf.Max(0.4f, height * 0.55f);
        var p = dst.Center + dst.Normal * (radius + 0.55f);
        p.y = dst.Center.y - height * 0.52f;
        return p;
    }

    public static Vector3 ExitPointPhysics(HSPortal src, HSPortal dst, Vector3 worldPos)
    {
        var p = TransformPoint(src, dst, worldPos);
        float along = Vector3.Dot(p - dst.Center, dst.Normal);
        if (along < 0.35f) p += dst.Normal * (0.4f - along);
        return p;
    }

    public static void ExitLook(HSPortal src, HSPortal dst, Vector3 inFwd, out float yaw, out float pitch)
    {
        if (Mathf.Abs(dst.Normal.y) < 0.55f)
        {
            var flat = Vector3.ProjectOnPlane(dst.Normal, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            LookYawPitch(flat, out yaw, out pitch);
            pitch = 0f;
            return;
        }
        var mapped = TransformDirection(src, dst, inFwd.sqrMagnitude < 0.0001f ? -src.Normal : inFwd);
        var flat2 = Vector3.ProjectOnPlane(mapped, Vector3.up);
        if (flat2.sqrMagnitude < 0.0001f) flat2 = Vector3.ProjectOnPlane(dst.Right, Vector3.up);
        if (flat2.sqrMagnitude < 0.0001f) flat2 = Vector3.forward;
        mapped = (flat2.normalized + Vector3.up * (dst.Normal.y > 0f ? 0.2f : -0.35f)).normalized;
        LookYawPitch(mapped, out yaw, out pitch);
    }

    public static bool RayHitsPortal(HSPortal portal, Ray ray, float maxDist, out float t)
    {
        t = 0f;
        if (portal == null) return false;
        float denom = Vector3.Dot(ray.direction, portal.Normal);
        if (Mathf.Abs(denom) < 0.0001f) return false;
        t = Vector3.Dot(portal.Center - ray.origin, portal.Normal) / denom;
        if (t < 0.02f || t > maxDist) return false;
        return InEllipse(portal, ray.origin + ray.direction * t, 0.1f);
    }

    public static bool TryRedirectRay(Ray ray, float maxDist, out HSPortal src, out HSPortal dst, out float t)
    {
        src = null;
        dst = null;
        t = maxDist;
        bool hit = false;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null || !pair.Linked) continue;
            float tb;
            if (RayHitsPortal(pair.Blue, ray, maxDist, out tb) && tb < t)
            {
                t = tb;
                src = pair.Blue;
                dst = pair.Orange;
                hit = true;
            }
            float to;
            if (RayHitsPortal(pair.Orange, ray, maxDist, out to) && to < t)
            {
                t = to;
                src = pair.Orange;
                dst = pair.Blue;
                hit = true;
            }
        }
        return hit;
    }

    public static Quaternion TransformRotation(HSPortal src, HSPortal dst, Quaternion worldRot)
    {
        var q = dst.Rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(src.Rotation);
        return q * worldRot;
    }

    public static void LookYawPitch(Vector3 forward, out float yaw, out float pitch)
    {
        var f = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    public static bool InEllipse(HSPortal portal, Vector3 worldPos, float pad)
    {
        var d = worldPos - portal.Center;
        float along = Vector3.Dot(d, portal.Normal);
        if (along < -0.2f || along > 1.2f + pad) return false;
        var planar = d - portal.Normal * along;
        float x = Vector3.Dot(planar, portal.Right);
        float y = Vector3.Dot(planar, portal.Up);
        float hw = portal.HalfWidth + pad;
        float hh = portal.HalfHeight + pad;
        return (x * x) / (hw * hw) + (y * y) / (hh * hh) <= 1f;
    }

    public static Vector3i FaceRight(BlockFace face)
    {
        switch (face)
        {
            case BlockFace.East:
            case BlockFace.West: return new Vector3i(0, 0, 1);
            default: return new Vector3i(1, 0, 0);
        }
    }

    public static Vector3i FaceUp(BlockFace face)
    {
        switch (face)
        {
            case BlockFace.Top:
            case BlockFace.Bottom: return new Vector3i(0, 0, 1);
            default: return new Vector3i(0, 1, 0);
        }
    }

    public static Vector3i WorldToCell(Vector3 p)
    {
        return new Vector3i(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
    }
}
