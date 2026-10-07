using System.Reflection;
using HarmonyLib;

public class HSPortalMod : IModApi
{
    public static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        if (!HSGameVersion.AllowLoad("[HSPortal]"))
            return;
        ModPath = _modInstance.Path;
        HSPortalDebug.Info("Init v0.3.7; admin: hsportal give | room | blue | orange | clear | gel");
        HSPortalNet.RegisterPackage();
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
        ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(HSPortalNet.OnPlayerSpawned);
        try
        {
            new Harmony("HSPortal").PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Error("Harmony patch failed (velocity carry after teleport will be wrong)", e);
        }
    }

    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        try
        {
            HSPortalController.EnsureCreated();
            HSPortalGel.EnsureLoaded();
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Error("Start failed", e);
        }
    }

    static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            HSPortalController.OnWorldShuttingDown();
            HSPortalWorld.ClearAll();
            HSPortalGel.Save();
            HSPortalGel.ClearAll();
            HSPortalVisual.DestroyAll();
            HSPortalGelVisual.DestroyAll();
        }
        catch (System.Exception e)
        {
            HSPortalDebug.Error("Shutdown handling failed", e);
        }
    }
}
