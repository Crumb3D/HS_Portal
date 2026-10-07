using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSPortalHud : XUiController
{
    public override void Init()
    {
        base.Init();
        AlwaysUpdate = true;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RefreshBindings();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        bool hold, blue, orange, linked;
        ItemActionHSPortalGun.ReadHud(ItemActionHSPortalGun.LocalPlayer(), out hold, out blue, out orange, out linked);
        bool show = hold || blue || orange;
        switch (_bindingName)
        {
            case "hudvisible":
                _value = show ? "true" : "false";
                return true;
            case "bluecolor":
                _value = blue ? "70,170,255,255" : "35,45,60,70";
                return true;
            case "orangecolor":
                _value = orange ? "255,140,25,255" : "60,42,22,70";
                return true;
            case "linkcolor":
                _value = linked ? "210,255,210,255" : "50,50,50,50";
                return true;
            case "bluetext":
                _value = blue ? Localization.Get("hsportalHudBlueOn") : Localization.Get("hsportalHudBlueOff");
                return true;
            case "orangetext":
                _value = orange ? Localization.Get("hsportalHudOrangeOn") : Localization.Get("hsportalHudOrangeOff");
                return true;
            case "linktext":
                _value = linked ? Localization.Get("hsportalHudLinked") : (show ? Localization.Get("hsportalHudUnlinked") : "");
                return true;
            case "hinttext":
                _value = hold ? Localization.Get("hsportalHudHint") : "";
                return true;
        }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }
}

[HarmonyPatch(typeof(ReflectionHelpers), "GetTypeWithPrefix")]
static class HSPortalXuiTypePatch
{
    static void Postfix(string _prefix, string _name, ref System.Type __result)
    {
        if (__result != null || _prefix != "XUiC_" || string.IsNullOrEmpty(_name)) return;
        if (_name != "HSPortalHud" && !_name.StartsWith("HSPortalHud")) return;
        __result = typeof(XUiC_HSPortalHud);
    }
}
