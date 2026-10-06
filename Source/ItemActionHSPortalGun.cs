using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class ItemActionHSPortalGun : ItemAction
{
    public const float FireDelay = 0.13f;

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
            PlayGunAnim(_actionData, "Fire");
            PlayAvatarFire(player);
            try { Audio.Manager.Play(player, "pistol_fire"); } catch { }
            HSPortalController.QueueShot(player, orange, FireDelay);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Portal gun fire failed", e);
        }
    }

    public static bool IsHolding(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null) return false;
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return false;
        foreach (var a in holding.Actions)
            if (a is ItemActionHSPortalGun) return true;
        return false;
    }

    public static void PlayGunAnim(ItemActionData data, string trigger)
    {
        if (data == null) return;
        PlayGunAnim(data.invData, trigger);
    }

    public static void PlayGunAnim(ItemInventoryData inv, string trigger)
    {
        if (inv == null || inv.model == null) return;
        var anim = inv.model.GetComponentInChildren<Animator>();
        if (anim == null) return;
        anim.ResetTrigger("Fire");
        anim.ResetTrigger("Reject");
        anim.SetTrigger(trigger);
    }

    public static void PlayAvatarFire(EntityAlive player)
    {
        if (player == null || player.emodel == null) return;
        var ac = player.emodel.avatarController;
        if (ac == null) return;
        ac.TriggerEvent(AvatarController.weaponFireHash);
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
        if (IsHolding(player) && player.inventory != null)
            PlayGunAnim(player.inventory.holdingItemData, "Reject");
    }
}
