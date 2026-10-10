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

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        FinishReloadIfDue(_actionData);
    }

    public override bool CanExecute(ItemActionData _actionData)
    {
        return _actionData != null;
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (_bReleased) return;
        if (_actionData == null || _actionData.invData == null) return;
        if (_actionData.lastUseTime > 0f && Time.time - _actionData.lastUseTime < 0.22f) return;
        FinishReloadIfDue(_actionData);
        var ranged = _actionData as ItemActionDataRanged;
        if (ranged != null && Reloading(ranged)) return;
        try
        {
            var holding = _actionData.invData.holdingEntity as EntityPlayer;
            if (holding == null) return;
            if (!EnsureAmmo(_actionData))
            {
                try { Audio.Manager.Play(holding, string.IsNullOrEmpty(soundEmpty) ? "dryfire" : soundEmpty); } catch { }
                var localDeny = holding as EntityPlayerLocal ?? ItemActionHSPortalGun.LocalPlayer();
                if (localDeny != null) ItemActionHSPortalGun.Deny(localDeny, Localization.Get("hsportalGooEmpty"));
                return;
            }
            _actionData.lastUseTime = Time.time;
            ConsumeAmmo(_actionData);
            byte color = _actionData.indexInEntityOfAction == 1 ? HSPortalGel.Orange : HSPortalGel.Blue;
            ItemActionHSPortalGun.PlayGunAnim(_actionData, "Fire");
            ItemActionHSPortalGun.PlayAvatarFire(holding);
            try { Audio.Manager.Play(holding, "paint_spray"); } catch { try { Audio.Manager.Play(holding, "pistol_fire"); } catch { } }
            var local = holding as EntityPlayerLocal ?? ItemActionHSPortalGun.LocalPlayer();
            if (local != null) HSPortalController.QueueGel(local, color, FireDelay);
            else Fire(holding, color);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Gel gun fire failed", e);
        }
    }

    public static void Fire(EntityPlayer player, byte color)
    {
        if (player == null) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (color == 0) color = HSPortalGel.Blue;
        if (HSPortalNet.IsRemoteClient)
        {
            var local = player as EntityPlayerLocal;
            if (local != null) HSPortalNet.SendGelPaint(local, color);
            return;
        }
        string fail;
        if (!HSPortalGel.PaintLook(world, player, color, out fail))
        {
            var local = player as EntityPlayerLocal;
            if (local != null) ItemActionHSPortalGun.Deny(local, fail);
            return;
        }
        HSPortalNet.BroadcastGels();
        var tell = player as EntityPlayerLocal;
        if (tell != null) GameManager.ShowTooltip(tell, GelTip(color));
    }

    public static bool IsHolding(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null) return false;
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return false;
        foreach (var a in holding.Actions)
            if (a is ItemActionHSPortalGelGun) return true;
        return false;
    }

    static float lastWhiteTime;

    public static void PollWhite()
    {
        try
        {
            if (GameManager.IsDedicatedServer) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
            if (!Input.GetMouseButtonDown(2)) return;
            var player = ItemActionHSPortalGun.LocalPlayer();
            if (player == null || player.IsDead() || !IsHolding(player)) return;
            var ui = player.playerUI;
            if (ui != null && ui.windowManager != null)
            {
                if (ui.windowManager.IsModalWindowOpen()) return;
                if (ui.windowManager.cursorWindowOpen) return;
                if (ui.windowManager.IsInputActive()) return;
            }
            if (lastWhiteTime > 0f && Time.time - lastWhiteTime < 0.22f) return;
            if (!SpendWhiteShot(player))
            {
                ItemActionHSPortalGun.Deny(player, Localization.Get("hsportalGooEmpty"));
                return;
            }
            lastWhiteTime = Time.time;
            ItemActionHSPortalGun.PlayGunAnim(player.inventory.holdingItemData, "Fire");
            ItemActionHSPortalGun.PlayAvatarFire(player);
            try { Audio.Manager.Play(player, "paint_spray"); } catch { }
            HSPortalController.QueueGel(player, HSPortalGel.White, FireDelay);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("White gel poll failed", e);
        }
    }

    public static string GelTip(byte color)
    {
        if (color == HSPortalGel.Orange) return Localization.Get("hsportalGelOrange");
        if (color == HSPortalGel.White) return Localization.Get("hsportalGelWhite");
        if (color == HSPortalGel.Cleanse) return Localization.Get("hsportalGelCleansed");
        return Localization.Get("hsportalGelBlue");
    }

    static bool SpendWhiteShot(EntityPlayerLocal player)
    {
        try
        {
            if (player == null || player.inventory == null) return false;
            var iv = player.inventory.holdingItemItemValue;
            if (iv == null) return false;
            if (iv.Meta > 0)
            {
                iv.Meta--;
                return true;
            }
            var ammo = ItemClass.GetItem("hsportalGoo", true);
            if (ammo == null || ammo.ItemClass == null) return false;
            if (player.bag != null && player.bag.DecItem(ammo, 1) > 0) return true;
            if (player.inventory != null && player.inventory.DecItem(ammo, 1) > 0) return true;
        }
        catch { }
        return false;
    }

    bool EnsureAmmo(ItemActionData data)
    {
        if (HasInfiniteAmmo(data)) return true;
        if (checkAmmo(data)) return true;
        try
        {
            if (CanReload(data))
            {
                var ranged = data as ItemActionDataRanged;
                ReloadGun(data);
                if (ranged != null) CompleteReload(ranged);
            }
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("Gel reload failed: " + e.Message);
        }
        if (checkAmmo(data)) return true;
        return FillFromGoo(data);
    }

    bool FillFromGoo(ItemActionData data)
    {
        try
        {
            if (data == null || data.invData == null) return false;
            var iv = data.invData.itemValue;
            var entity = data.invData.holdingEntity;
            if (iv == null || entity == null) return false;
            if (iv.Meta > 0) return true;
            var ammo = ItemClass.GetItem("hsportalGoo", true);
            if (ammo == null || ammo.ItemClass == null) return false;
            int mag = 8;
            try { mag = Mathf.Max(1, GetMaxAmmoCount(data)); } catch { }
            int taken = 0;
            if (entity.bag != null) taken += entity.bag.DecItem(ammo, mag);
            if (taken < mag && entity.inventory != null) taken += entity.inventory.DecItem(ammo, mag - taken);
            if (taken <= 0) return false;
            iv.Meta = taken;
            return true;
        }
        catch
        {
            return false;
        }
    }

    void FinishReloadIfDue(ItemActionData data)
    {
        var ranged = data as ItemActionDataRanged;
        if (ranged == null || !Reloading(ranged)) return;
        float dur = reloadingTime > 0.15f ? reloadingTime : 1.4f;
        if (data.lastUseTime > 0f && Time.time - data.lastUseTime < dur) return;
        try { CompleteReload(ranged); }
        catch (Exception e) { HSPortalDebug.Warn("Gel CompleteReload failed: " + e.Message); }
    }
}
