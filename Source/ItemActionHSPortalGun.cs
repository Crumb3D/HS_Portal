using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class ItemActionHSPortalGun : ItemAction
{
    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (_bReleased) return;
        if (_actionData == null) return;
        if (_actionData.lastUseTime > 0f && Time.time - _actionData.lastUseTime < 0.28f) return;
        _actionData.lastUseTime = Time.time;
        try
        {
            var player = _actionData.invData != null ? _actionData.invData.holdingEntity as EntityPlayerLocal : null;
            if (player == null) return;
            bool orange = _actionData.indexInEntityOfAction == 1;
            Fire(player, orange);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Portal gun fire failed", e);
        }
    }

    public static void Fire(EntityPlayerLocal player, bool orange)
    {
        if (player == null) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (HSPortalNet.IsRemoteClient)
        {
            HSPortalNet.SendPlace(player, orange);
            return;
        }
        string fail;
        if (!HSPortalPlacement.TryPlace(world, player, orange, out fail))
        {
            Deny(player, fail);
            return;
        }
        HSPortalNet.BroadcastState(player.entityId);
        var key = orange ? "hsportalPlacedOrange" : "hsportalPlacedBlue";
        GameManager.ShowTooltip(player, Localization.Get(key));
        try { Audio.Manager.Play(player, "place_block"); } catch { }
    }

    public static void Deny(EntityPlayerLocal player, string msg)
    {
        if (player == null) return;
        if (string.IsNullOrEmpty(msg)) msg = Localization.Get("hsportalDenied");
        GameManager.ShowTooltip(player, msg, "", "close");
    }
}
