using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class ItemActionHSPortalGun : ItemAction
{
    public const float FireDelay = 0.28f;

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (_bReleased) return;
        if (_actionData == null) return;
        if (_actionData.lastUseTime > 0f && Time.time - _actionData.lastUseTime < 0.55f) return;
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
            string previewFail;
            HSPortal preview;
            if (!HSPortalPlacement.TryBuild(world, player, orange, out preview, out previewFail))
                Deny(player, previewFail);
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

    static float lastClearTime;

    public static EntityPlayerLocal LocalPlayer()
    {
        try
        {
            if (GameManager.IsDedicatedServer) return null;
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null) return null;
            var locals = world.GetLocalPlayers();
            if (locals == null || locals.Count == 0) return null;
            return locals[0] as EntityPlayerLocal;
        }
        catch
        {
            return null;
        }
    }

    public static void PollClear()
    {
        try
        {
            if (GameManager.IsDedicatedServer) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
            if (!Input.GetMouseButtonDown(2)) return;
            TryClear(LocalPlayer());
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Portal gun clear poll failed", e);
        }
    }

    public static void TryClear(EntityPlayerLocal player)
    {
        if (player == null || player.IsDead()) return;
        if (!IsHolding(player)) return;
        if (lastClearTime > 0f && Time.time - lastClearTime < 0.4f) return;
        var ui = player.playerUI;
        if (ui != null && ui.windowManager != null)
        {
            if (ui.windowManager.IsModalWindowOpen()) return;
            if (ui.windowManager.cursorWindowOpen) return;
            if (ui.windowManager.IsInputActive()) return;
        }
        lastClearTime = Time.time;
        Clear(player);
    }

    public static void Clear(EntityPlayerLocal player)
    {
        if (player == null) return;
        HSPortalNet.SendClear(player.entityId);
        GameManager.ShowTooltip(player, Localization.Get("hsportalCleared"));
        if (IsHolding(player) && player.inventory != null)
            PlayGunAnim(player.inventory.holdingItemData, "Reject");
        try { Audio.Manager.Play(player, "close"); } catch { }
    }

    public static void ReadHud(EntityPlayerLocal player, out bool holding, out bool blue, out bool orange, out bool linked)
    {
        holding = IsHolding(player);
        blue = false;
        orange = false;
        linked = false;
        if (player == null) return;
        var pair = HSPortalWorld.GetPair(player.entityId, false);
        if (pair == null) return;
        blue = pair.Blue != null;
        orange = pair.Orange != null;
        linked = pair.Linked;
    }
}
