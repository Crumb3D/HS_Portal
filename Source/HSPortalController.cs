using UnityEngine;

public class HSPortalController : MonoBehaviour
{
    static HSPortalController instance;

    public static void EnsureCreated()
    {
        if (instance != null) return;
        var go = new GameObject("HSPortalController");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<HSPortalController>();
    }

    public static void OnWorldShuttingDown()
    {
        HSPortalVisual.DestroyAll();
    }

    void Update()
    {
        try
        {
            HSPortalWorld.Tick();
            HSPortalTeleporter.Tick();
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Error("Tick failed", e);
        }
    }

    void LateUpdate()
    {
        try { HSPortalVisual.SyncAll(); }
        catch (System.Exception e) { HSPortalDebug.Error("Visual sync failed", e); }
    }
}
