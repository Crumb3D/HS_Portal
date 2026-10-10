using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class HSPortalGel
{
    public const byte Blue = 1;
    public const byte Orange = 2;
    public const float PaintRange = 12f;
    public const float SpeedMul = 2.55f;
    public const float BounceMul = 1.12f;
    static float ignoreUntil;

    struct Key : IEquatable<Key>
    {
        public int X, Y, Z;
        public byte Face;
        public Key(Vector3i p, BlockFace f)
        {
            X = p.x; Y = p.y; Z = p.z; Face = (byte)f;
        }
        public Vector3i Pos { get { return new Vector3i(X, Y, Z); } }
        public BlockFace BlockFace { get { return (BlockFace)Face; } }
        public bool Equals(Key other) { return X == other.X && Y == other.Y && Z == other.Z && Face == other.Face; }
        public override bool Equals(object obj) { return obj is Key && Equals((Key)obj); }
        public override int GetHashCode() { return X * 73856093 ^ Y * 19349663 ^ Z * 83492791 ^ (Face * 17); }
    }

    static readonly Dictionary<Key, byte> painted = new Dictionary<Key, byte>();
    static bool loaded;
    static string loadedPath;
    static bool wasGrounded;
    static float lastFall;
    static float nextPrune;

    public static IEnumerable<KeyValuePair<Vector3i, KeyValuePair<BlockFace, byte>>> All
    {
        get
        {
            foreach (var kv in painted)
                yield return new KeyValuePair<Vector3i, KeyValuePair<BlockFace, byte>>(
                    kv.Key.Pos, new KeyValuePair<BlockFace, byte>(kv.Key.BlockFace, kv.Value));
        }
    }

    public static int Count { get { return painted.Count; } }

    public static byte Get(Vector3i pos, BlockFace face)
    {
        byte c;
        return painted.TryGetValue(new Key(pos, face), out c) ? c : (byte)0;
    }

    public static bool Paint(World world, Vector3i pos, BlockFace face, byte color)
    {
        if (world == null || color == 0) return false;
        if (!HSPortalPlacement.IsLegalFace(world, pos, face)) return false;
        var key = new Key(pos, face);
        painted[key] = color;
        HSPortalGelVisual.Upsert(key.Pos, key.BlockFace, color);
        return true;
    }

    public static bool PaintLook(World world, EntityPlayer player, bool orange, out string fail)
    {
        fail = Localization.Get("hsportalGelDenied");
        if (world == null || player == null) return false;
        Ray ray;
        try { ray = player.GetLookRay(); }
        catch { return false; }
        return PaintRay(world, ray, orange ? Orange : Blue, out fail);
    }

    public static bool PaintRay(World world, Ray ray, byte color, out string fail)
    {
        fail = Localization.Get("hsportalGelDenied");
        if (world == null || ray.direction.sqrMagnitude < 0.0001f) return false;
        if (!Voxel.Raycast(world, ray, PaintRange, false, false)) return false;
        var hit = Voxel.voxelRayHitInfo;
        if (hit == null || !hit.bHitValid) return false;
        var face = hit.hit.blockFace;
        var seed = hit.hit.blockPos;
        int n = 0;
        if (Paint(world, seed, face, color)) n++;
        var right = HSPortalMath.FaceRight(face);
        var up = HSPortalMath.FaceUp(face);
        var around = new Vector3i[]
        {
            seed + right, seed - right, seed + up, seed - up
        };
        for (int i = 0; i < around.Length; i++)
        {
            if (Paint(world, around[i], face, color)) n++;
        }
        if (n == 0) return false;
        fail = null;
        return true;
    }

    public static void ClearAll()
    {
        painted.Clear();
        HSPortalGelVisual.DestroyAll();
    }

    public static void ReplaceAll(List<Vector3i> cells, List<byte> faces, List<byte> colors)
    {
        painted.Clear();
        if (cells == null) { HSPortalGelVisual.RebuildAll(); return; }
        int n = Math.Min(cells.Count, Math.Min(faces.Count, colors.Count));
        for (int i = 0; i < n; i++)
            painted[new Key(cells[i], (BlockFace)faces[i])] = colors[i];
        HSPortalGelVisual.RebuildAll();
    }

    public static void Snapshot(out List<Vector3i> cells, out List<byte> faces, out List<byte> colors)
    {
        cells = new List<Vector3i>(painted.Count);
        faces = new List<byte>(painted.Count);
        colors = new List<byte>(painted.Count);
        foreach (var kv in painted)
        {
            cells.Add(kv.Key.Pos);
            faces.Add(kv.Key.Face);
            colors.Add(kv.Value);
        }
    }

    public static void Tick()
    {
        EnsureLoaded();
        if (Time.unscaledTime < nextPrune) { PhysicsLocal(); return; }
        nextPrune = Time.unscaledTime + 0.45f;
        if (!HSPortalNet.IsAuthority) { PhysicsLocal(); return; }
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) { PhysicsLocal(); return; }
        var drop = new List<Key>();
        foreach (var kv in painted)
        {
            var pos = kv.Key.Pos;
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            if (!HSPortalPlacement.IsLegalFace(world, pos, kv.Key.BlockFace)) drop.Add(kv.Key);
        }
        for (int i = 0; i < drop.Count; i++)
        {
            painted.Remove(drop[i]);
            HSPortalGelVisual.Remove(drop[i].Pos, drop[i].BlockFace);
        }
        if (drop.Count > 0) HSPortalNet.BroadcastGels();
        PhysicsLocal();
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
        if (Time.unscaledTime < ignoreUntil)
        {
            wasGrounded = grounded;
            lastFall = fall;
            return;
        }
        var cell = FootCell(player.position);
        byte top = Get(cell, BlockFace.Top);
        if (top == 0) top = Get(new Vector3i(cell.x, cell.y - 1, cell.z), BlockFace.Top);

        if (top == Orange && grounded)
        {
            var t = fp.m_MotorThrottle;
            t.x *= SpeedMul;
            t.z *= SpeedMul;
            float mag = Mathf.Sqrt(t.x * t.x + t.z * t.z);
            if (mag > 3.2f)
            {
                float s = 3.2f / mag;
                t.x *= s;
                t.z *= s;
            }
            fp.m_MotorThrottle = t;
        }

        if (!grounded && fall < -0.85f)
        {
            var below = new Vector3i(cell.x, cell.y - 1, cell.z);
            byte under = Get(below, BlockFace.Top);
            if (under == 0) under = Get(cell, BlockFace.Top);
            if (under == Blue)
            {
                float bounce = Mathf.Clamp(-fall * BounceMul, 0f, 10f);
                if (bounce > 0.8f) fp.m_FallSpeed = bounce;
            }
        }

        wasGrounded = grounded;
        lastFall = fp.m_FallSpeed;
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
                bw.Write(1);
                bw.Write(painted.Count);
                foreach (var kv in painted)
                {
                    bw.Write(kv.Key.X);
                    bw.Write(kv.Key.Y);
                    bw.Write(kv.Key.Z);
                    bw.Write(kv.Key.Face);
                    bw.Write(kv.Value);
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
                br.ReadInt32();
                int n = br.ReadInt32();
                painted.Clear();
                for (int i = 0; i < n && i < 8192; i++)
                {
                    var k = new Key(new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()), (BlockFace)br.ReadByte());
                    painted[k] = br.ReadByte();
                }
            }
            HSPortalGelVisual.RebuildAll();
            HSPortalDebug.Info("Loaded " + painted.Count + " gel faces");
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
