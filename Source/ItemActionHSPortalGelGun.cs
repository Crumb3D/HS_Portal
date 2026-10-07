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
            bool orange = _actionData.indexInEntityOfAction == 1;
            ItemActionHSPortalGun.PlayGunAnim(_actionData, "Fire");
            ItemActionHSPortalGun.PlayAvatarFire(holding);
            try { Audio.Manager.Play(holding, "paint_spray"); } catch { try { Audio.Manager.Play(holding, "pistol_fire"); } catch { } }
            var local = holding as EntityPlayerLocal ?? ItemActionHSPortalGun.LocalPlayer();
            if (local != null) HSPortalController.QueueGel(local, orange, FireDelay);
            else Fire(holding, orange);
        }
        catch (Exception e)
        {
            HSPortalDebug.Error("Gel gun fire failed", e);
        }
    }

    public static void Fire(EntityPlayer player, bool orange)
    {
        if (player == null) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (HSPortalNet.IsRemoteClient)
        {
            var local = player as EntityPlayerLocal;
            if (local != null) HSPortalNet.SendGelPaint(local, orange);
            return;
        }
        string fail;
        if (!HSPortalGel.PaintLook(world, player, orange, out fail))
        {
            var local = player as EntityPlayerLocal;
            if (local != null) ItemActionHSPortalGun.Deny(local, fail);
            return;
        }
        HSPortalNet.BroadcastGels();
        var tell = player as EntityPlayerLocal;
        if (tell != null) GameManager.ShowTooltip(tell, Localization.Get(orange ? "hsportalGelOrange" : "hsportalGelBlue"));
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
