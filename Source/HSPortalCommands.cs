using System;
using System.Collections.Generic;
using UnityEngine;

public class ConsoleCmdHSPortal : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "hsportal" };
    }

    public override string getDescription()
    {
        return "HSPortal admin: give gun, build a test room, place or clear portals.";
    }

    public override int DefaultPermissionLevel { get { return 1000; } }

    public override string getHelp()
    {
        return
            "On a dedicated server, type these in the in-game F1 console after you join.\n" +
            "From the server window, add your player name: hsportal room YourName\n" +
            "hsportal give [name]          - portal gun, gel gun, cube, long-fall boots\n" +
            "hsportal room [name]          - concrete chamber around you, then give the kit\n" +
            "hsportal blue | orange        - place that colour portal on the aimed surface\n" +
            "hsportal gel blue|orange      - spray that gel on the aimed face\n" +
            "hsportal clear                - remove your portals\n" +
            "hsportal status | debug";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            var sub = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            var who = _params != null && _params.Count > 1 ? _params[1] : "";
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (sub == "gel" && _params != null && _params.Count > 1)
            {
                var color = _params[1].ToLowerInvariant();
                who = _params.Count > 2 ? _params[2] : "";
                var gelPlayer = ResolvePlayer(world, _senderInfo, who);
                Out(Gel(world, gelPlayer, color == "orange" || color == "o", who));
                return;
            }
            var player = ResolvePlayer(world, _senderInfo, who);
            Out(Run(sub, world, player, who));
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Command failed", e);
            Out("HSPortal command failed: " + e.Message);
        }
    }

    static EntityPlayer ResolvePlayer(World world, CommandSenderInfo sender, string name)
    {
        if (world == null) return null;
        if (!string.IsNullOrEmpty(name) && name != "debug" && name != "blue" && name != "orange" && name != "clear" && name != "status" && name != "give" && name != "room" && name != "gel")
        {
            var named = FindByName(world, name);
            if (named != null) return named;
        }
        if (sender.RemoteClientInfo != null)
        {
            var fromNet = world.GetEntity(sender.RemoteClientInfo.entityId) as EntityPlayer;
            if (fromNet != null) return fromNet;
        }
        var local = world.GetPrimaryPlayer();
        if (local != null) return local;
        var locals = world.GetLocalPlayers();
        if (locals != null && locals.Count > 0)
        {
            var p = locals[0] as EntityPlayer;
            if (p != null) return p;
        }
        return null;
    }

    static EntityPlayer FindByName(World world, string name)
    {
        if (world == null || string.IsNullOrEmpty(name)) return null;
        var list = world.GetPlayers();
        if (list == null) return null;
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i] as EntityPlayer;
            if (p == null) continue;
            if (string.Equals(p.EntityName, name, StringComparison.OrdinalIgnoreCase)) return p;
        }
        return null;
    }

    static string NeedPlayer(string triedName)
    {
        if (!string.IsNullOrEmpty(triedName))
            return "No player named '" + triedName + "'. They must be in the world. From the server window: hsportal room TheirName";
        return "No player. Dedicated server has no 'local' player — type this in the in-game F1 console after you join, or from the server window: hsportal room YourName";
    }

    static string Run(string sub, World world, EntityPlayer player, string who)
    {
        switch (sub)
        {
            case "give":
                return Give(player, who);
            case "room":
                return BuildRoom(world, player, who);
            case "blue":
                return Place(world, player, false, who);
            case "orange":
                return Place(world, player, true, who);
            case "clear":
                if (player == null) return NeedPlayer(who);
                HSPortalNet.SendClear(player.entityId);
                return Localization.Get("hsportalCleared");
            case "debug":
                HSPortalDebug.Enabled = !HSPortalDebug.Enabled;
                return "Debug " + (HSPortalDebug.Enabled ? "on" : "off");
            default:
                return Status(player, who);
        }
    }

    static string Status(EntityPlayer player, string who)
    {
        if (player == null) return NeedPlayer(who);
        var pair = HSPortalWorld.GetPair(player.entityId, false);
        if (pair == null || (pair.Blue == null && pair.Orange == null)) return "No portals.";
        var b = pair.Blue == null ? "none" : Fmt(pair.Blue);
        var o = pair.Orange == null ? "none" : Fmt(pair.Orange);
        return "Blue " + b + " | Orange " + o + (pair.Linked ? " | linked" : " | static (need both colours)");
    }

    static string Fmt(HSPortal p)
    {
        return p.Center.ToString("F1") + " n=" + p.Normal.ToString("F0") + " face=" + p.Face;
    }

    static string Place(World world, EntityPlayer player, bool orange, string who)
    {
        if (player == null || world == null) return NeedPlayer(who);
        if (HSPortalNet.IsRemoteClient)
        {
            var local = player as EntityPlayerLocal;
            if (local == null) return "Place from your own F1 console.";
            HSPortalNet.SendPlace(local, orange);
            return "Requested " + (orange ? "orange" : "blue") + " portal.";
        }
        string fail;
        if (!HSPortalPlacement.TryPlace(world, player, orange, out fail)) return fail ?? Localization.Get("hsportalDenied");
        HSPortalNet.BroadcastState(player.entityId);
        return Localization.Get(orange ? "hsportalPlacedOrange" : "hsportalPlacedBlue");
    }

    static string Give(EntityPlayer player, string who)
    {
        if (player == null || player.inventory == null) return NeedPlayer(who);
        var names = new[] { "hsportalGun", "hsportalGelGun", "hsportalCube", "hsportalBoots" };
        var got = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            var item = ItemClass.GetItem(names[i], true);
            if (item == null || item.ItemClass == null) continue;
            var stack = new ItemStack(new ItemValue(item.type, true), 1);
            if (player.inventory.AddItem(stack)) got.Add(names[i]);
        }
        if (got.Count == 0) return "Could not give kit (missing items or inventory full).";
        return "Gave " + string.Join(", ", got.ToArray()) + " to " + player.EntityName + ".";
    }

    static string Gel(World world, EntityPlayer player, bool orange, string who)
    {
        if (player == null || world == null) return NeedPlayer(who);
        if (HSPortalNet.IsRemoteClient)
        {
            var local = player as EntityPlayerLocal;
            if (local == null) return "Spray from your own F1 console.";
            HSPortalNet.SendGelPaint(local, orange);
            return "Requested " + (orange ? "orange" : "blue") + " gel.";
        }
        string fail;
        if (!HSPortalGel.PaintLook(world, player, orange, out fail)) return fail ?? Localization.Get("hsportalGelDenied");
        HSPortalNet.BroadcastGels();
        return Localization.Get(orange ? "hsportalGelOrange" : "hsportalGelBlue");
    }

    static string BuildRoom(World world, EntityPlayer player, string who)
    {
        if (world == null || player == null) return NeedPlayer(who);
        if (!HSPortalNet.IsAuthority) return "Host only. On a dedicated server this must run on the server (F1 as admin, or server console with your name).";
        var block = FindCube();
        if (block == null) return "No cube block (tried concreteShapes:Cube / steelShapes:Cube).";
        var bv = block.ToBlockValue();
        int cx = Mathf.FloorToInt(player.position.x);
        int cz = Mathf.FloorToInt(player.position.z);
        int fy = Mathf.FloorToInt(player.position.y) - 1;
        for (int d = 0; d <= 4; d++)
        {
            var probe = new Vector3i(cx, fy - d, cz);
            if (world.GetChunkFromWorldPos(probe) == null) continue;
            var cur = world.GetBlock(probe);
            if (!cur.isair && cur.Block != null && cur.Block.IsCollideMovement)
            {
                fy = fy - d;
                break;
            }
        }
        const int half = 5;
        const int h = 6;
        int x0 = cx - half;
        int x1 = cx + half;
        int z0 = cz - half;
        int z1 = cz + half;
        int y0 = fy;
        int y1 = fy + h + 1;
        var changes = new List<BlockChangeInfo>();
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
        {
            var pos = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(pos) == null) return "Chunk not loaded at " + pos + ". Move somewhere loaded and try again.";
            bool shell = x == x0 || x == x1 || z == z0 || z == z1 || y == y0 || y == y1;
            bool pad = x >= x1 - 3 && x <= x1 - 1 && z >= z0 + 1 && z <= z0 + 3 && y > y0 && y <= y0 + 2;
            if (shell || pad) changes.Add(new BlockChangeInfo(pos, bv, (sbyte)0));
            else changes.Add(new BlockChangeInfo(pos, BlockValue.Air, MarchingCubes.DensityAir));
        }
        world.SetBlocksRPC(changes);
        var stand = new Vector3(cx + 0.5f, fy + 1.1f, cz + 0.5f);
        player.SetPosition(stand, true);
        var local = player as EntityPlayerLocal;
        if (local != null && local.vp_FPController != null)
            local.vp_FPController.SetPosition(stand - Origin.position);
        Give(player, who);
        HSPortalDebug.Info("Test room at " + new Vector3i(cx, fy, cz) + " block=" + block.GetBlockName() + " player=" + player.EntityName + " changes=" + changes.Count);
        return "Built concrete test room for " + player.EntityName + " (" + changes.Count + " blocks) and gave the kit. Portal gun: left blue / right orange. Gel gun: left bounce / right speed.";
    }

    static Block FindCube()
    {
        string[] names = { "concreteShapes:Cube", "steelShapes:Cube", "woodShapes:Cube", "concreteBlock" };
        for (int i = 0; i < names.Length; i++)
        {
            var b = Block.GetBlockByName(names[i], true);
            if (b != null) return b;
        }
        return null;
    }

    static void Out(string s)
    {
        SdtdConsole.Instance.Output(s);
    }
}
