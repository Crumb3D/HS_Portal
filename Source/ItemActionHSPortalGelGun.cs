using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class ItemActionHSPortalGelGun : ItemActionRanged
{
    public const float FireDelay = 0.11f;

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);
        Delay = FireDelay;
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (_bReleased) return;
        if (_actionData == null) return;
        var ranged = _actionData as ItemActionDataRanged;
        if (ranged != null && Reloading(ranged)) return;
        if (_actionData.lastUseTime > 0f && Time.time - _actionData.lastUseTime < 0.22f) return;
        try
        {
            var player = _actionData.invData != null ? _actionData.invData.holdingEntity as EntityPlayerLocal : null;
            if (player == null) return;
            if (!checkAmmo(_actionData))
            {
                try { Audio.Manager.Play(player, string.IsNullOrEmpty(soundEmpty) ? "dryfire" : soundEmpty); } catch { }
                ItemActionHSPortalGun.Deny(player, Localization.Get("hsportalGooEmpty"));
                return;
            }
            _actionData.lastUseTime = Time.time;
            ConsumeAmmo(_actionData);
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
