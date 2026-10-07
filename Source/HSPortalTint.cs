using UnityEngine;

public class HSPortalTinted : MonoBehaviour { }

public static class HSPortalTint
{
    static Shader shader;

    public static void TickHeld()
    {
        if (GameManager.IsDedicatedServer) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var p = world != null ? world.GetPrimaryPlayer() : null;
            if (p == null || p.inventory == null) return;
            var item = p.inventory.holdingItem;
            if (item == null) return;
            var n = item.Name;
            if (n != "hsportalGun" && n != "hsportalGelGun" && n != "hsportalCube") return;
            var t = p.inventory.GetHoldingItemTransform();
            if (t == null) return;
            Paint(t.gameObject);
            if (n == "hsportalGun" || n == "hsportalGelGun") AimGun(t, n == "hsportalGun", p);
        }
        catch { }
    }

    public static void Paint(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<HSPortalTinted>() != null) return;
        var sh = Shader();
        if (sh == null) return;
        go.AddComponent<HSPortalTinted>();
        var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < mrs.Length; i++)
        {
            var mr = mrs[i];
            if (mr == null) continue;
            var m = new Material(sh);
            var c = ColorOf(mr.gameObject.name);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            else m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.25f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
            mr.sharedMaterial = m;
            mr.material = m;
        }
    }

    // Portal gun Idle writes Blender identity (barrel +Y, grip -Z). HoldType 1 aims +Z with grip down.
    // Rx(-90) puts the barrel on -Z and the grip on -Y; yaw 180 aims +Z. Same call on 3.2 and 3.3.
    static readonly Quaternion PortalHold = Quaternion.Euler(-90f, 180f, 0f);
    static readonly Quaternion GelHold = Quaternion.Euler(0f, 180f, 0f);
    static readonly Vector3 PortalHoldPosFp = new Vector3(0.05f, -0.1f, 0.22f);
    static readonly Vector3 PortalHoldPosTp = new Vector3(0f, 0.06f, 0.08f);
    static readonly Quaternion HubFbx = Quaternion.Euler(90f, 0f, 0f);

    static void AimGun(Transform root, bool portal, EntityPlayerLocal p)
    {
        if (root == null) return;
        var hold = root.Find("Hold");
        if (hold == null)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c != null && c.name == "Hold") { hold = c; break; }
            }
        }
        if (hold == null) return;
        bool fpv = p == null || p.emodel == null || p.emodel.IsFPV;
        SeatInHand(root, p, fpv);
        hold.localRotation = portal ? PortalHold : GelHold;
        hold.localPosition = fpv ? PortalHoldPosFp : PortalHoldPosTp;
        // Idle keys claw spin around Blender +Y. Hub keeps FBX Rx(90), so remap that
        // spin onto Hub +Z (the barrel) after the clip writes. Same on 3.2 and 3.3.
        if (portal) AimClaws(hold);
    }

    static void SeatInHand(Transform root, EntityPlayerLocal p, bool fpv)
    {
        if (p == null || p.emodel == null) return;
        var hand = p.emodel.GetRightHandTransform();
        if (hand == null) return;
        if (root.parent != hand)
            root.SetParent(hand, false);
        if (fpv)
        {
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            return;
        }
        var item = p.inventory != null ? p.inventory.holdingItem : null;
        int ht = item != null && item.HoldType != null ? item.HoldType.Value : 1;
        var offs = AnimationGunjointOffsetData.AnimationGunjointOffset;
        if (offs != null && ht >= 0 && ht < offs.Length)
        {
            root.localPosition = offs[ht].position;
            root.localRotation = Quaternion.Euler(offs[ht].rotation);
        }
        else
        {
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
        }
    }

    static void AimClaws(Transform hold)
    {
        var hub = FindNamed(hold, "EmitterHub");
        if (hub == null) return;
        hub.localRotation = HubFbx;
        for (int i = 0; i < hub.childCount; i++)
        {
            var c = hub.GetChild(i);
            if (c == null) continue;
            var n = c.name;
            if (n.Length < 6 || n.IndexOf("Claw_", System.StringComparison.Ordinal) != 0) continue;
            if (n.IndexOf('_', 5) >= 0) continue;
            c.localRotation = HubFbx * c.localRotation;
        }
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindNamed(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static Shader Shader()
    {
        if (shader != null) return shader;
        shader = UnityEngine.Shader.Find("Standard");
        if (shader == null) shader = UnityEngine.Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null) shader = UnityEngine.Shader.Find("Sprites/Default");
        if (shader == null) shader = UnityEngine.Shader.Find("Hidden/Internal-Colored");
        return shader;
    }

    static Color ColorOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return new Color(0.32f, 0.3f, 0.27f);
        if (Contains(name, "Blue")) return new Color(0.2f, 0.55f, 1f);
        if (Contains(name, "Orange") || Contains(name, "Shin")) return new Color(1f, 0.4f, 0.06f);
        if (Contains(name, "Copper") || Contains(name, "Hazard")) return new Color(0.85f, 0.55f, 0.1f);
        if (Contains(name, "Grip") || Contains(name, "Sole")) return new Color(0.08f, 0.07f, 0.06f);
        if (Contains(name, "Dark")) return new Color(0.12f, 0.12f, 0.12f);
        if (Contains(name, "Light") || Contains(name, "GaugeFace")) return new Color(0.55f, 0.53f, 0.48f);
        if (Contains(name, "Heart")) return new Color(0.78f, 0.16f, 0.22f);
        if (Contains(name, "Stripe")) return new Color(0.85f, 0.68f, 0.08f);
        if (Contains(name, "Leather")) return new Color(0.22f, 0.13f, 0.08f);
        if (Contains(name, "Glow") || Contains(name, "Glass")) return new Color(0.45f, 0.7f, 0.95f);
        if (Contains(name, "Body") || Contains(name, "Cube")) return new Color(0.55f, 0.55f, 0.56f);
        return new Color(0.35f, 0.34f, 0.31f);
    }

    static bool Contains(string name, string token)
    {
        return name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
