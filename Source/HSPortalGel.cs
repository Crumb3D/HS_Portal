using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HSPortalGelSplat
{
    public Vector3 Center;
    public Vector3 Normal;
    public Vector3i Cell;
    public BlockFace Face;
    public byte Color;
    public float Radius;
    public int Seed;
}

public static class HSPortalGel
{
    public const byte None = 0;
    public const byte Blue = 1;
    public const byte Orange = 2;
    public const byte White = 3;
    public const byte Cleanse = 4;
    public const float PaintRange = 12f;
    public const float SplatRadius = 1.08f;
    public const float SpeedMul = 2.8f;
    public const float BounceMin = 2.4f;
    public const float BounceImpact = 8f;
    public const float BounceMax = 5.5f;
    public const float OrangeCap = 0.95f;
    public const float WallPush = 1.15f;
    static float ignoreUntil;
    static bool wasOnBlue;
    static float bounceLock;
    static float orangeBoost;
    static Vector3 orangeRetain;
    static float wallLock;

    static readonly List<HSPortalGelSplat> splats = new List<HSPortalGelSplat>();
    static bool loaded;
    static string loadedPath;
    static bool wasGrounded;
    static float lastFall;
    static float nextPrune;

    public static IEnumerable<HSPortalGelSplat> All
    {
        get { return splats; }
    }

    public static int Count { get { return splats.Count; } }

    public static byte Get(Vector3i pos, BlockFace face)
    {
        var fc = HSPortalMath.FaceCenter(pos, face);
        var fn = HSPortalMath.FaceNormal(face);
        byte best = None;
        float bestD = 999f;
        for (int i = 0; i < splats.Count; i++)
        {
            var s = splats[i];
            if (s == null || s.Color == None) continue;
            if (Vector3.Dot(s.Normal, fn) < 0.72f) continue;
            float along = Mathf.Abs(Vector3.Dot(s.Center - fc, fn));
            if (along > 0.6f) continue;
            float d = Vector3.ProjectOnPlane(s.Center - fc, fn).magnitude;
            if (d > s.Radius + 0.52f) continue;
            if (d < bestD)
            {
                bestD = d;
                best = s.Color;
            }
        }
        return best;
    }

    public static bool AllowsPortal(Vector3i pos, BlockFace face)
    {
        return Get(pos, face) == White;
    }

    public static bool PaintLook(World world, EntityPlayer player, byte color, out string fail)
    {
        fail = Localization.Get("hsportalGelDenied");
        if (world == null || player == null) return false;
        Ray ray;
        try { ray = player.GetLookRay(); }
        catch { return false; }
        return PaintRay(world, ray, color, out fail);
    }

    public static bool PaintRay(World world, Ray ray, byte color, out string fail)
    {
        fail = Localization.Get("hsportalGelDenied");
        if (world == null || color == None || ray.direction.sqrMagnitude < 0.0001f) return false;
        Vector3i cell;
        BlockFace face;
        Vector3 pos;
        if (!HSPortalPlacement.TryAimSurface(world, ray, PaintRange, out cell, out face, out pos))
            return false;
        if (color == Cleanse)
        {
            if (CleanseAt(pos, SplatRadius + 0.35f) <= 0) return false;
            fail = null;
            return true;
        }
        if (!CanCoat(world, cell)) return false;
        AddSplat(pos, HSPortalMath.FaceNormal(face), cell, face, color);
        fail = null;
        return true;
    }

    static bool CanCoat(World world, Vector3i cell)
    {
        if (world == null) return false;
        if (world.GetChunkFromWorldPos(cell) == null) return false;
        var bv = world.GetBlock(cell);
        if (bv.isair || bv.Block == null) return false;
        return bv.Block.IsCollideMovement;
    }

    static void AddSplat(Vector3 center, Vector3 normal, Vector3i cell, BlockFace face, byte color)
    {
        if (normal.sqrMagnitude < 0.0001f) normal = HSPortalMath.FaceNormal(face);
        normal.Normalize();
        center += normal * 0.03f;
        for (int i = splats.Count - 1; i >= 0; i--)
        {
            var s = splats[i];
            if (s == null) continue;
            if (Vector3.Dot(s.Normal, normal) < 0.72f) continue;
            if ((s.Center - center).sqrMagnitude > (s.Radius * 0.7f) * (s.Radius * 0.7f)) continue;
            splats.RemoveAt(i);
        }
        var splat = new HSPortalGelSplat();
        splat.Center = center;
        splat.Normal = normal;
        splat.Cell = cell;
        splat.Face = face;
        splat.Color = color;
        splat.Radius = SplatRadius;
        splat.Seed = (cell.x * 73856093) ^ (cell.y * 19349663) ^ (cell.z * 83492791) ^ ((int)face * 17) ^ (color * 31) ^ Time.frameCount;
        splats.Add(splat);
        HSPortalGelVisual.RebuildAll();
    }

    public static int CleanseAt(Vector3 worldPos, float radius)
    {
        int n = 0;
        float r2 = radius * radius;
        for (int i = splats.Count - 1; i >= 0; i--)
        {
            var s = splats[i];
            if (s == null) continue;
            if ((s.Center - worldPos).sqrMagnitude > r2) continue;
            splats.RemoveAt(i);
            n++;
        }
        if (n > 0) HSPortalGelVisual.RebuildAll();
        return n;
    }

    public static void ClearAll()
    {
        splats.Clear();
        HSPortalGelVisual.DestroyAll();
    }

    public static void ReplaceAll(List<Vector3i> cells, List<byte> faces, List<byte> colors, List<Vector3> centers, List<float> radii, List<int> seeds)
    {
        splats.Clear();
        if (cells == null)
        {
            HSPortalGelVisual.RebuildAll();
            return;
        }
        int n = cells.Count;
        if (faces != null) n = Math.Min(n, faces.Count);
        if (colors != null) n = Math.Min(n, colors.Count);
        for (int i = 0; i < n; i++)
        {
            var s = new HSPortalGelSplat();
            s.Cell = cells[i];
            s.Face = (BlockFace)faces[i];
            s.Color = colors[i];
            s.Normal = HSPortalMath.FaceNormal(s.Face);
            s.Center = centers != null && i < centers.Count ? centers[i] : HSPortalMath.FaceCenter(s.Cell, s.Face) + s.Normal * 0.03f;
            s.Radius = radii != null && i < radii.Count && radii[i] > 0.1f ? radii[i] : SplatRadius;
            s.Seed = seeds != null && i < seeds.Count ? seeds[i] : s.Cell.GetHashCode() ^ (int)s.Face;
            if (s.Color != None) splats.Add(s);
        }
        HSPortalGelVisual.RebuildAll();
    }

    public static void Snapshot(out List<Vector3i> cells, out List<byte> faces, out List<byte> colors, out List<Vector3> centers, out List<float> radii, out List<int> seeds)
    {
        cells = new List<Vector3i>(splats.Count);
        faces = new List<byte>(splats.Count);
        colors = new List<byte>(splats.Count);
        centers = new List<Vector3>(splats.Count);
        radii = new List<float>(splats.Count);
        seeds = new List<int>(splats.Count);
        for (int i = 0; i < splats.Count; i++)
        {
            var s = splats[i];
            if (s == null) continue;
            cells.Add(s.Cell);
            faces.Add((byte)s.Face);
            colors.Add(s.Color);
            centers.Add(s.Center);
            radii.Add(s.Radius);
            seeds.Add(s.Seed);
        }
    }

    public static void Tick()
    {
        EnsureLoaded();
        if (Time.unscaledTime < nextPrune) { PhysicsLocal(); return; }
        nextPrune = Time.unscaledTime + 0.35f;
        if (!HSPortalNet.IsAuthority) { PhysicsLocal(); return; }
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) { PhysicsLocal(); return; }
        bool changed = false;
        for (int i = splats.Count - 1; i >= 0; i--)
        {
            var s = splats[i];
            if (s == null) { splats.RemoveAt(i); changed = true; continue; }
            if (world.GetChunkFromWorldPos(s.Cell) == null) continue;
            if (!CanCoat(world, s.Cell))
            {
                splats.RemoveAt(i);
                changed = true;
                continue;
            }
            if (WaterTouches(world, s))
            {
                splats.RemoveAt(i);
                changed = true;
            }
        }
        if (changed)
        {
            HSPortalGelVisual.RebuildAll();
            HSPortalNet.BroadcastGels();
        }
        PhysicsLocal();
    }

    static bool WaterTouches(World world, HSPortalGelSplat s)
    {
        if (HasWater(world, s.Cell)) return true;
        var step = HSPortalMath.FaceStep(s.Face);
        if (HasWater(world, new Vector3i(s.Cell.x + step.x, s.Cell.y + step.y, s.Cell.z + step.z))) return true;
        var around = HSPortalMath.WorldToCell(s.Center);
        if (HasWater(world, around)) return true;
        if (HasWater(world, new Vector3i(around.x + step.x, around.y + step.y, around.z + step.z))) return true;
        return false;
    }

    static bool HasWater(World world, Vector3i pos)
    {
        if (world == null) return false;
        try
        {
            var w = world.GetWater(pos);
            return w.HasMass();
        }
        catch
        {
            return false;
        }
    }

    public static void IgnoreUntil(float unscaled)
    {
        if (unscaled > ignoreUntil) ignoreUntil = unscaled;
    }

    public static void BeforeMove(vp_FPController fp)
    {
        if (fp == null) return;
        var player = LocalFrom(fp);
        if (player == null) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        bool grounded = fp.m_CharacterController != null && fp.m_CharacterController.isGrounded;
        float fall = fp.m_FallSpeed;
        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        if (Time.unscaledTime < ignoreUntil)
        {
            wasGrounded = grounded;
            lastFall = fall;
            return;
        }
        var cell = FootCell(player.position);
        byte top = Get(cell, BlockFace.Top);
        if (top == None) top = Get(new Vector3i(cell.x, cell.y - 1, cell.z), BlockFace.Top);
        bool crouched = player.IsCrouching;

        if (top == Orange && grounded)
        {
            orangeBoost = Mathf.MoveTowards(orangeBoost, 1f, dt * 1.35f);
            var t = fp.m_MotorThrottle;
            t.x *= 1f + (SpeedMul - 1f) * orangeBoost;
            t.z *= 1f + (SpeedMul - 1f) * orangeBoost;
            float mag = Mathf.Sqrt(t.x * t.x + t.z * t.z);
            if (mag > OrangeCap)
            {
                float sc = OrangeCap / mag;
                t.x *= sc;
                t.z *= sc;
            }
            fp.m_MotorThrottle = t;
            orangeRetain = new Vector3(t.x, 0f, t.z);
        }
        else
        {
            orangeBoost = Mathf.MoveTowards(orangeBoost, 0f, dt * 0.28f);
            if (orangeBoost > 0.04f && orangeRetain.sqrMagnitude > 0.0001f)
            {
                var keep = orangeRetain * orangeBoost * 0.85f;
                var ext = fp.m_ExternalForce;
                ext.x += keep.x;
                ext.z += keep.z;
                fp.m_ExternalForce = ext;
                orangeRetain *= 0.985f;
            }
        }

        bool onBlue = top == Blue;
        if (onBlue && !crouched && Time.unscaledTime > bounceLock)
        {
            bool landed = grounded && !wasGrounded;
            bool stepped = grounded && !wasOnBlue;
            if (landed || stepped)
            {
                float impact = Mathf.Max(0f, -lastFall);
                fp.m_FallSpeed = Mathf.Clamp(BounceMin + impact * BounceImpact, BounceMin, BounceMax);
                bounceLock = Time.unscaledTime + 0.14f;
            }
        }

        if (!crouched && Time.unscaledTime > wallLock)
            TryWallBounce(fp, player, world);

        wasOnBlue = onBlue && grounded;
        wasGrounded = grounded;
        lastFall = fp.m_FallSpeed;
    }

    static void TryWallBounce(vp_FPController fp, EntityPlayerLocal player, World world)
    {
        var feet = player.position;
        var mid = feet + Vector3.up * 0.9f;
        var faces = new[] { BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West };
        for (int i = 0; i < faces.Length; i++)
        {
            var face = faces[i];
            var n = HSPortalMath.FaceNormal(face);
            var probe = HSPortalMath.WorldToCell(mid + n * 0.35f);
            if (Get(probe, face) != Blue) continue;
            var into = fp.m_MotorThrottle + fp.m_ExternalForce;
            if (Vector3.Dot(into, n) > -0.008f) continue;
            fp.m_ExternalForce = new Vector3(n.x * WallPush, 0f, n.z * WallPush);
            if (fp.m_FallSpeed < 0.55f) fp.m_FallSpeed = 0.55f;
            wallLock = Time.unscaledTime + 0.2f;
            return;
        }
    }

    static Vector3i FootCell(Vector3 pos)
    {
        return new Vector3i(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y + 0.01f), Mathf.FloorToInt(pos.z));
    }

    static EntityPlayerLocal LocalFrom(vp_FPController fp)
    {
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var p = world != null ? world.GetPrimaryPlayer() : null;
            if (p != null && p.vp_FPController == fp) return p;
        }
        catch { }
        return null;
    }

    static void PhysicsLocal() { }

    public static string ColorName(byte color)
    {
        if (color == Orange) return "orange";
        if (color == White) return "white";
        if (color == Blue) return "blue";
        if (color == Cleanse) return "cleanse";
        return "gel";
    }

    public static void Save()
    {
        try
        {
            var path = SavePath();
            if (string.IsNullOrEmpty(path)) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(0x4853474C);
                bw.Write(2);
                bw.Write(splats.Count);
                for (int i = 0; i < splats.Count; i++)
                {
                    var s = splats[i];
                    bw.Write(s.Cell.x);
                    bw.Write(s.Cell.y);
                    bw.Write(s.Cell.z);
                    bw.Write((byte)s.Face);
                    bw.Write(s.Color);
                    bw.Write(s.Center.x);
                    bw.Write(s.Center.y);
                    bw.Write(s.Center.z);
                    bw.Write(s.Radius);
                    bw.Write(s.Seed);
                }
            }
            loadedPath = path;
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("Gel save failed: " + e.Message);
        }
    }

    public static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            var path = SavePath();
            loadedPath = path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                if (br.ReadInt32() != 0x4853474C) return;
                int ver = br.ReadInt32();
                int n = br.ReadInt32();
                splats.Clear();
                for (int i = 0; i < n && i < 8192; i++)
                {
                    var cell = new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                    var face = (BlockFace)br.ReadByte();
                    byte color = br.ReadByte();
                    var s = new HSPortalGelSplat();
                    s.Cell = cell;
                    s.Face = face;
                    s.Color = color;
                    s.Normal = HSPortalMath.FaceNormal(face);
                    if (ver >= 2)
                    {
                        s.Center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        s.Radius = br.ReadSingle();
                        s.Seed = br.ReadInt32();
                    }
                    else
                    {
                        s.Center = HSPortalMath.FaceCenter(cell, face) + s.Normal * 0.03f;
                        s.Radius = SplatRadius;
                        s.Seed = cell.GetHashCode() ^ (int)face;
                    }
                    if (s.Color != None) splats.Add(s);
                }
            }
            HSPortalGelVisual.RebuildAll();
            HSPortalDebug.Info("Loaded " + splats.Count + " gel splats");
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("Gel load failed: " + e.Message);
        }
    }

    static string SavePath()
    {
        try
        {
            var save = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(save)) return null;
            return Path.Combine(save, "hsportal_gels.bin");
        }
        catch
        {
            return null;
        }
    }
}
