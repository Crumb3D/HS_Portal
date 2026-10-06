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
            "hsportal give                 - portal gun\n" +
            "hsportal room                 - concrete chamber around you, then give the gun\n" +
            "hsportal blue | orange        - place that colour on the aimed surface\n" +
            "hsportal clear                - remove your portals\n" +
            "hsportal status | debug";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            var sub = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            var world = GameManager.Instance.World;
            EntityPlayerLocal player = null;
            if (world != null)
            {
                player = world.GetPrimaryPlayer();
                if (player == null)
                {
                    var locals = world.GetLocalPlayers();
                    if (locals != null && locals.Count > 0) player = locals[0] as EntityPlayerLocal;
                }
            }
            Out(Run(sub, world, player));
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Command failed", e);
            Out("HSPortal command failed: " + e.Message);
        }
    }

    static string Run(string sub, World world, EntityPlayerLocal player)
    {
        switch (sub)
        {
            case "give":
                return Give(player);
            case "room":
                return BuildRoom(world, player);
            case "blue":
                return Place(world, player, false);
            case "orange":
                return Place(world, player, true);
            case "clear":
                if (player == null) return "No local player.";
                HSPortalNet.SendClear(player.entityId);
                return Localization.Get("hsportalCleared");
            case "debug":
                HSPortalDebug.Enabled = !HSPortalDebug.Enabled;
                return "Debug " + (HSPortalDebug.Enabled ? "on" : "off");
            default:
                return Status(player);
        }
    }

    static string Status(EntityPlayerLocal player)
    {
        if (player == null) return "No local player.";
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

    static string Place(World world, EntityPlayerLocal player, bool orange)
    {
        if (player == null || world == null) return "No local player.";
        if (HSPortalNet.IsRemoteClient)
        {
            HSPortalNet.SendPlace(player, orange);
            return "Requested " + (orange ? "orange" : "blue") + " portal.";
        }
        string fail;
        if (!HSPortalPlacement.TryPlace(world, player, orange, out fail)) return fail ?? Localization.Get("hsportalDenied");
        HSPortalNet.BroadcastState(player.entityId);
        return Localization.Get(orange ? "hsportalPlacedOrange" : "hsportalPlacedBlue");
    }

    static string Give(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null) return "No local player.";
        var item = ItemClass.GetItem("hsportalGun", true);
        if (item == null || item.ItemClass == null) return "hsportalGun is not loaded.";
        var stack = new ItemStack(new ItemValue(item.type, true), 1);
        if (!player.inventory.AddItem(stack)) return "Inventory full.";
        return "Gave Portal Gun.";
    }

    static string BuildRoom(World world, EntityPlayerLocal player)
    {
        if (world == null || player == null) return "No local player.";
        if (!HSPortalNet.IsAuthority) return "Host only.";
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
        var fp = player.vp_FPController;
        if (fp != null) fp.SetPosition(stand - Origin.position);
        Give(player);
        HSPortalDebug.Info("Test room at " + new Vector3i(cx, fy, cz) + " block=" + block.GetBlockName() + " changes=" + changes.Count);
        return "Built concrete test room (" + changes.Count + " blocks) and gave the Portal Gun. Left click blue, right click orange.";
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
