using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class ItemActionHSPortalGelGun : ItemAction
{
    public const float FireDelay = 0.11f;

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (_bReleased) return;
        if (_actionData == null) return;
        if (_actionData.lastUseTime > 0f && Time.time - _actionData.lastUseTime < 0.22f) return;
        _actionData.lastUseTime = Time.time;
        try
        {
            var player = _actionData.invData != null ? _actionData.invData.holdingEntity as EntityPlayerLocal : null;
            if (player == null) return;
            bool orange = _actionData.indexInEntityOfAction == 1;
            ItemActionHSPortalGun.PlayGunAnim(_actionData, "Fire");
            ItemActionHSPortalGun.PlayAvatarFire(player);
            try { Audio.Manager.Play(player, "paint_spray"); } catch { try { Audio.Manager.Play(player, "pistol_fire"); } catch { } }
            HSPortalController.QueueGel(player, orange, FireDelay);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Gel gun fire failed", e);
        }
    }

    public static void Fire(EntityPlayerLocal player, bool orange)
    {
        if (player == null) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (HSPortalNet.IsRemoteClient)
        {
            HSPortalNet.SendGelPaint(player, orange);
            return;
        }
        string fail;
        if (!HSPortalGel.PaintLook(world, player, orange, out fail))
        {
            ItemActionHSPortalGun.Deny(player, fail);
            return;
        }
        HSPortalNet.BroadcastGels();
        GameManager.ShowTooltip(player, Localization.Get(orange ? "hsportalGelOrange" : "hsportalGelBlue"));
    }
}
