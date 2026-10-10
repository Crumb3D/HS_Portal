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

    public static void RegisterPackage()
    {
        try
        {
            if (HSGameVersion.Is33) Register33();
            else Register32();
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Net package register failed", e);
        }
    }

    static void Register32()
    {
        var t = HSGameApi.NetPackageType32("NetPackageHSPortal", typeof(NetPackageHSPortalCore));
        pkgType = t;
        PutKnown(t, false);
        HSPortalDebug.Verbose("Registered NetPackageHSPortal");
    }

    static void Register33()
    {
        var t = HSGameApi.NetPackageType33("NetPackageHSPortal", typeof(NetPackageHSPortalCore));
        pkgType = t;
        PutKnown(t, true);
        MapEmitted33(t);
        HSPortalDebug.Verbose("Registered emitted NetPackageHSPortal");
    }

    static void PutKnown(Type t, bool overwrite)
    {
        var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return;
        var dict = f.GetValue(null) as IDictionary;
        if (dict == null) return;
        if (!overwrite && dict.Contains(t.Name)) return;
        dict[t.Name] = t;
    }

    static void MapEmitted33(Type t)
    {
        var mapF = typeof(NetPackageManager).GetField("packageClassToPackageId", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (mapF == null) return;
        var map = mapF.GetValue(null) as IDictionary;
        if (map == null || map.Contains(t)) return;
        var arrF = typeof(NetPackageManager).GetField("packageIdToClass", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (arrF == null) return;
        var arr = arrF.GetValue(null) as Type[];
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null || arr[i] == t || arr[i].Name != t.Name) continue;
            map[t] = i;
            arr[i] = t;
            return;
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

    public static void SendGelPaint(EntityPlayerLocal player, byte color)
    {
        if (player == null) return;
        var ray = player.GetLookRay();
        ToServer(Pkg().OfGelPaint(player.entityId, color, ray.origin, ray.direction));
    }

    public static void BroadcastGels()
    {
        if (!IsAuthority) return;
        List<Vector3i> cells;
        List<byte> faces;
        List<byte> colors;
        List<Vector3> centers;
        List<float> radii;
        List<int> seeds;
        HSPortalGel.Snapshot(out cells, out faces, out colors, out centers, out radii, out seeds);
        ToClients(Pkg().OfGelState(cells, faces, colors, centers, radii, seeds));
    }

    public static void HandleGelPaint(int ownerId, byte color, Vector3 origin, Vector3 dir)
    {
        if (!IsAuthority) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        string fail;
        if (color == 0) color = HSPortalGel.Blue;
        if (!HSPortalGel.PaintRay(world, new Ray(origin, dir), color, out fail))
        {
            TellOwner(ownerId, fail);
            return;
        }
        BroadcastGels();
        TellOwner(ownerId, ItemActionHSPortalGelGun.GelTip(color));
    }

    public static void HandleGelState(List<Vector3i> cells, List<byte> faces, List<byte> colors, List<Vector3> centers, List<float> radii, List<int> seeds)
    {
        if (IsAuthority) return;
        HSPortalGel.ReplaceAll(cells, faces, colors, centers, radii, seeds);
    }

    public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!IsAuthority) return;
        RegisterPackage();
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
    protected List<Vector3> gelCenters;
    protected List<float> gelRadii;
    protected List<int> gelSeeds;
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

    public NetPackageHSPortalCore OfGelPaint(int owner, byte color, Vector3 origin, Vector3 dir)
    {
        kind = HSPortalNet.GelPaint;
        ownerId = owner;
        orange = color == HSPortalGel.Orange;
        a = origin;
        b = dir;
        c = new Vector3(color, 0f, 0f);
        return this;
    }

    public NetPackageHSPortalCore OfGelState(List<Vector3i> cells, List<byte> faces, List<byte> colors, List<Vector3> centers, List<float> radii, List<int> seeds)
    {
        kind = HSPortalNet.GelState;
        gelCells = cells;
        gelFaces = faces;
        gelColors = colors;
        gelCenters = centers;
        gelRadii = radii;
        gelSeeds = seeds;
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
        gelCenters = new List<Vector3>(gn);
        gelRadii = new List<float>(gn);
        gelSeeds = new List<int>(gn);
        for (int i = 0; i < gn; i++)
        {
            gelCells.Add(new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()));
            gelFaces.Add(br.ReadByte());
            gelColors.Add(br.ReadByte());
            gelCenters.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
            gelRadii.Add(br.ReadSingle());
            gelSeeds.Add(br.ReadInt32());
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
            var ctr = gelCenters != null && i < gelCenters.Count ? gelCenters[i] : Vector3.zero;
            bw.Write(ctr.x); bw.Write(ctr.y); bw.Write(ctr.z);
            bw.Write(gelRadii != null && i < gelRadii.Count ? gelRadii[i] : HSPortalGel.SplatRadius);
            bw.Write(gelSeeds != null && i < gelSeeds.Count ? gelSeeds[i] : 0);
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
                    {
                        byte color = (byte)Mathf.Round(c.x);
                        if (color == 0) color = orange ? HSPortalGel.Orange : HSPortalGel.Blue;
                        HSPortalNet.HandleGelPaint(ownerId, color, a, b);
                    }
                    break;
                case HSPortalNet.GelState:
                    HSPortalNet.HandleGelState(gelCells, gelFaces, gelColors, gelCenters, gelRadii, gelSeeds);
                    break;
            }
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Net package failed (" + kind + ")", e);
        }
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

[HarmonyPatch(typeof(NetPackageManager), "StartServer")]
public static class HSPortalNetRegisterServer
{
    static void Prefix()
    {
        if (HSGameVersion.Is33) HSPortalNet.RegisterPackage();
    }
}

[HarmonyPatch(typeof(NetPackageManager), "StartClient")]
public static class HSPortalNetRegisterClient
{
    static void Prefix()
    {
        if (HSGameVersion.Is33) HSPortalNet.RegisterPackage();
    }
}
