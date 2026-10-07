using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public static class HSPortalNet
{
    public const byte Place = 1;
    public const byte Clear = 2;
    public const byte State = 3;
    public const byte Teleport = 4;
    public const byte Tip = 5;
    public const byte GelPaint = 6;
    public const byte GelState = 7;

    public static bool IsAuthority
    {
        get
        {
            try
            {
                if (GameManager.IsDedicatedServer) return true;
                var cm = ConnectionManager.Instance;
                if (cm != null) return cm.IsServer;
            }
            catch { }
            return true;
        }
    }

    public static bool IsRemoteClient
    {
        get
        {
            try
            {
                var cm = ConnectionManager.Instance;
                return cm != null && cm.IsClient && !cm.IsServer;
            }
            catch { }
            return false;
        }
    }

    static Type pkgType;

    static Type PackageType32()
    {
        return typeof(NetPackageHSPortal);
    }

    public static void RegisterPackage()
    {
        try
        {
            var t = HSGameVersion.Is33
                ? HSGameApi.NetPackageType33("NetPackageHSPortal", typeof(NetPackageHSPortalCore))
                : PackageType32();
            pkgType = t;
            var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            var dict = f.GetValue(null) as IDictionary;
            if (dict == null) return;
            var args = f.FieldType.GetGenericArguments();
            if (args != null && args.Length >= 1 && args[0] == typeof(string))
            {
                if (!dict.Contains(t.Name)) dict[t.Name] = t;
            }
            else if (args != null && args.Length >= 1 && args[0] == typeof(Type))
            {
                if (!dict.Contains(t)) dict[t] = t.Name;
            }
            else if (!dict.Contains(t.Name)) dict[t.Name] = t;
            HSPortalDebug.Verbose("Registered NetPackageHSPortal");
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Net package register failed", e);
        }
    }

    static NetPackageHSPortalCore Pkg()
    {
        if (pkgType == null) RegisterPackage();
        return (NetPackageHSPortalCore)HSGameApi.GetNetPackage(pkgType);
    }

    static void ToServer(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null) return;
        cm.SendToClientsOrServer(pkg);
    }

    static void ToClients(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer) return;
        cm.SendPackage(pkg);
    }

    public static void SendPlace(EntityPlayerLocal player, bool orange)
    {
        if (player == null) return;
        var ray = player.GetLookRay();
        ToServer(Pkg().OfPlace(player.entityId, orange, ray.origin, ray.direction));
    }

    public static void SendClear(int ownerId)
    {
        if (IsRemoteClient) ToServer(Pkg().OfClear(ownerId));
        else
        {
            HSPortalWorld.ClearOwner(ownerId);
            BroadcastState(ownerId);
        }
    }

    public static void BroadcastState(int ownerId)
    {
        if (!IsAuthority) return;
        var pair = HSPortalWorld.GetPair(ownerId, false);
        ToClients(Pkg().OfState(ownerId, pair != null ? pair.Blue : null, pair != null ? pair.Orange : null));
    }

    public static void SendTeleport(int entityId, Vector3 pos, float yaw, float pitch, Vector3 vel)
    {
        if (IsRemoteClient)
            ToServer(Pkg().OfTeleport(entityId, pos, yaw, pitch, vel));
    }

    public static void TellLocal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var locals = world != null ? world.GetLocalPlayers() : null;
            if (locals == null) return;
            for (int i = 0; i < locals.Count; i++)
            {
                var p = locals[i] as EntityPlayerLocal;
                if (p != null) GameManager.ShowTooltip(p, msg);
            }
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("Local tip failed: " + e.Message);
        }
    }

    public static void TellOwner(int ownerId, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var locals = world != null ? world.GetLocalPlayers() : null;
            if (locals != null)
            {
                for (int i = 0; i < locals.Count; i++)
                {
                    var p = locals[i] as EntityPlayerLocal;
                    if (p != null && p.entityId == ownerId)
                    {
                        GameManager.ShowTooltip(p, msg);
                        return;
                    }
                }
            }
            if (IsAuthority)
            {
                var cm = ConnectionManager.Instance;
                if (cm != null && cm.IsServer)
                    cm.SendPackage(Pkg().OfTip(msg), false, ownerId);
            }
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("TellOwner failed: " + e.Message);
        }
    }

    public static void SendGelPaint(EntityPlayerLocal player, bool orange)
    {
        if (player == null) return;
        var ray = player.GetLookRay();
        ToServer(Pkg().OfGelPaint(player.entityId, orange, ray.origin, ray.direction));
    }

    public static void BroadcastGels()
    {
        if (!IsAuthority) return;
        List<Vector3i> cells;
        List<byte> faces;
        List<byte> colors;
        HSPortalGel.Snapshot(out cells, out faces, out colors);
        ToClients(Pkg().OfGelState(cells, faces, colors));
    }

    public static void HandleGelPaint(int ownerId, bool orange, Vector3 origin, Vector3 dir)
    {
        if (!IsAuthority) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        string fail;
        if (!HSPortalGel.PaintRay(world, new Ray(origin, dir), orange ? HSPortalGel.Orange : HSPortalGel.Blue, out fail))
        {
            TellOwner(ownerId, fail);
            return;
        }
        BroadcastGels();
        TellOwner(ownerId, Localization.Get(orange ? "hsportalGelOrange" : "hsportalGelBlue"));
    }

    public static void HandleGelState(List<Vector3i> cells, List<byte> faces, List<byte> colors)
    {
        if (IsAuthority) return;
        HSPortalGel.ReplaceAll(cells, faces, colors);
    }

    public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!IsAuthority) return;
        foreach (var kv in HSPortalWorld.All)
            BroadcastState(kv.Key);
        BroadcastGels();
    }

    public static void HandlePlace(int ownerId, bool orange, Vector3 origin, Vector3 dir)
    {
        if (!IsAuthority) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        string fail;
        HSPortal portal;
        if (!HSPortalPlacement.TryBuildFromRay(world, ownerId, new Ray(origin, dir), orange, out portal, out fail))
        {
            TellOwner(ownerId, fail);
            return;
        }
        HSPortalWorld.Put(portal);
        BroadcastState(ownerId);
        TellOwner(ownerId, Localization.Get(orange ? "hsportalPlacedOrange" : "hsportalPlacedBlue"));
    }

    public static void HandleState(int ownerId, HSPortal blue, HSPortal orange)
    {
        if (IsAuthority) return;
        HSPortalWorld.ReplacePair(ownerId, blue, orange);
    }

    public static void HandleTeleport(World world, int entityId, Vector3 pos, float yaw, float pitch, Vector3 vel)
    {
        if (world == null) return;
        var e = world.GetEntity(entityId);
        if (e == null) return;
        if (IsAuthority && IsRemoteClient) return;
        HSPortalTeleporter.ApplyRemote(e, pos, yaw, pitch, vel);
    }
}

public abstract class NetPackageHSPortalCore : NetPackage
{
    protected byte kind;
    protected int ownerId;
    protected bool orange;
    protected Vector3 a;
    protected Vector3 b;
    protected Vector3 c;
    protected float yaw;
    protected float pitch;
    protected HSPortal blue;
    protected HSPortal orangePortal;
    protected List<Vector3i> gelCells;
    protected List<byte> gelFaces;
    protected List<byte> gelColors;
    protected string text = "";

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.Both; } }

    public NetPackageHSPortalCore OfPlace(int owner, bool isOrange, Vector3 origin, Vector3 dir)
    {
        kind = HSPortalNet.Place;
        ownerId = owner;
        orange = isOrange;
        a = origin;
        b = dir;
        return this;
    }

    public NetPackageHSPortalCore OfClear(int owner)
    {
        kind = HSPortalNet.Clear;
        ownerId = owner;
        return this;
    }

    public NetPackageHSPortalCore OfState(int owner, HSPortal bPortal, HSPortal oPortal)
    {
        kind = HSPortalNet.State;
        ownerId = owner;
        blue = bPortal;
        orangePortal = oPortal;
        return this;
    }

    public NetPackageHSPortalCore OfTeleport(int entityId, Vector3 pos, float y, float p, Vector3 vel)
    {
        kind = HSPortalNet.Teleport;
        ownerId = entityId;
        a = pos;
        b = vel;
        yaw = y;
        pitch = p;
        return this;
    }

    public NetPackageHSPortalCore OfGelPaint(int owner, bool isOrange, Vector3 origin, Vector3 dir)
    {
        kind = HSPortalNet.GelPaint;
        ownerId = owner;
        orange = isOrange;
        a = origin;
        b = dir;
        return this;
    }

    public NetPackageHSPortalCore OfGelState(List<Vector3i> cells, List<byte> faces, List<byte> colors)
    {
        kind = HSPortalNet.GelState;
        gelCells = cells;
        gelFaces = faces;
        gelColors = colors;
        return this;
    }

    public NetPackageHSPortalCore OfTip(string msg)
    {
        kind = HSPortalNet.Tip;
        text = msg ?? "";
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        kind = br.ReadByte();
        ownerId = br.ReadInt32();
        orange = br.ReadBoolean();
        a = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        b = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        c = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        yaw = br.ReadSingle();
        pitch = br.ReadSingle();
        text = br.ReadString();
        blue = ReadPortal(br);
        orangePortal = ReadPortal(br);
        int gn = br.ReadInt32();
        gelCells = new List<Vector3i>(gn);
        gelFaces = new List<byte>(gn);
        gelColors = new List<byte>(gn);
        for (int i = 0; i < gn; i++)
        {
            gelCells.Add(new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()));
            gelFaces.Add(br.ReadByte());
            gelColors.Add(br.ReadByte());
        }
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(kind);
        bw.Write(ownerId);
        bw.Write(orange);
        bw.Write(a.x); bw.Write(a.y); bw.Write(a.z);
        bw.Write(b.x); bw.Write(b.y); bw.Write(b.z);
        bw.Write(c.x); bw.Write(c.y); bw.Write(c.z);
        bw.Write(yaw);
        bw.Write(pitch);
        bw.Write(text ?? "");
        WritePortal(bw, blue);
        WritePortal(bw, orangePortal);
        int gn = gelCells != null ? gelCells.Count : 0;
        bw.Write(gn);
        for (int i = 0; i < gn; i++)
        {
            bw.Write(gelCells[i].x);
            bw.Write(gelCells[i].y);
            bw.Write(gelCells[i].z);
            bw.Write(gelFaces[i]);
            bw.Write(gelColors[i]);
        }
    }

    static void WritePortal(PooledBinaryWriter bw, HSPortal p)
    {
        bw.Write(p != null);
        if (p == null) return;
        bw.Write(p.OwnerId);
        bw.Write(p.Orange);
        bw.Write(p.Center.x); bw.Write(p.Center.y); bw.Write(p.Center.z);
        bw.Write(p.Normal.x); bw.Write(p.Normal.y); bw.Write(p.Normal.z);
        bw.Write(p.Up.x); bw.Write(p.Up.y); bw.Write(p.Up.z);
        bw.Write(p.HalfWidth);
        bw.Write(p.HalfHeight);
        bw.Write((byte)p.Face);
        int n = p.Cells != null ? p.Cells.Length : 0;
        bw.Write(n);
        for (int i = 0; i < n; i++)
        {
            bw.Write(p.Cells[i].x);
            bw.Write(p.Cells[i].y);
            bw.Write(p.Cells[i].z);
        }
    }

    static HSPortal ReadPortal(PooledBinaryReader br)
    {
        if (!br.ReadBoolean()) return null;
        var p = new HSPortal();
        p.OwnerId = br.ReadInt32();
        p.Orange = br.ReadBoolean();
        p.Center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        p.Normal = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        p.Up = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        p.HalfWidth = br.ReadSingle();
        p.HalfHeight = br.ReadSingle();
        p.Face = (BlockFace)br.ReadByte();
        int n = br.ReadInt32();
        p.Cells = new Vector3i[Math.Max(0, n)];
        for (int i = 0; i < p.Cells.Length; i++)
            p.Cells[i] = new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
        p.FinishAxes();
        return p;
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        try
        {
            if (world == null) return;
            switch (kind)
            {
                case HSPortalNet.Place:
                    HSPortalNet.HandlePlace(ownerId, orange, a, b);
                    break;
                case HSPortalNet.Clear:
                    if (!HSPortalNet.IsAuthority) return;
                    HSPortalWorld.ClearOwner(ownerId);
                    HSPortalNet.BroadcastState(ownerId);
                    break;
                case HSPortalNet.State:
                    HSPortalNet.HandleState(ownerId, blue, orangePortal);
                    break;
                case HSPortalNet.Teleport:
                    HSPortalNet.HandleTeleport(world, ownerId, a, yaw, pitch, b);
                    break;
                case HSPortalNet.Tip:
                    HSPortalNet.TellLocal(text);
                    break;
                case HSPortalNet.GelPaint:
                    HSPortalNet.HandleGelPaint(ownerId, orange, a, b);
                    break;
                case HSPortalNet.GelState:
                    HSPortalNet.HandleGelState(gelCells, gelFaces, gelColors);
                    break;
            }
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Net package failed (" + kind + ")", e);
        }
    }

}

public sealed class NetPackageHSPortal : NetPackageHSPortalCore
{
    public override int GetLength()
    {
        return 96;
    }
}

[HarmonyPatch(typeof(NetPackageManager), "SetupBaseMapping")]
public static class HSPortalNetRegister
{
    static void Postfix()
    {
        HSPortalNet.RegisterPackage();
    }
}
