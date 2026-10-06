using System.Collections.Generic;
using UnityEngine;

public class HSPortalController : MonoBehaviour
{
    static HSPortalController instance;

    class PendingShot
    {
        public EntityPlayerLocal player;
        public bool orange;
        public float at;
        public bool gel;
    }

    readonly List<PendingShot> pending = new List<PendingShot>();

    public static void EnsureCreated()
    {
        if (instance != null) return;
        var go = new GameObject("HSPortalController");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<HSPortalController>();
    }

    public static void QueueShot(EntityPlayerLocal player, bool orange, float delay)
    {
        EnsureCreated();
        instance.pending.Add(new PendingShot
        {
            player = player,
            orange = orange,
            at = Time.time + delay
        });
    }

    public static void QueueGel(EntityPlayerLocal player, bool orange, float delay)
    {
        EnsureCreated();
        instance.pending.Add(new PendingShot
        {
            player = player,
            orange = orange,
            at = Time.time + delay,
            gel = true
        });
    }

    public static void OnWorldShuttingDown()
    {
        if (instance != null) instance.pending.Clear();
        HSPortalGel.Save();
        HSPortalVisual.DestroyAll();
        HSPortalGelVisual.DestroyAll();
    }

    void Update()
    {
        try
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (Time.time < p.at) continue;
                pending.RemoveAt(i);
                try
                {
                    if (p.gel) ItemActionHSPortalGelGun.Fire(p.player, p.orange);
                    else ItemActionHSPortalGun.Fire(p.player, p.orange);
                }
                catch (System.Exception e) { HSPortalDebug.Error("Queued fire failed", e); }
            }
            HSPortalWorld.Tick();
            HSPortalGel.Tick();
            HSPortalTeleporter.Tick();
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Error("Tick failed", e);
        }
    }

    void LateUpdate()
    {
        try
        {
            HSPortalVisual.SyncAll();
            HSPortalGelVisual.SyncAll();
        }
        catch (System.Exception e) { HSPortalDebug.Error("Visual sync failed", e); }
    }
}
