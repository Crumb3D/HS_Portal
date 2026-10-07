using System;
using System.Collections.Generic;
using UnityEngine;

public static class HSPortalVisual
{
    class View
    {
        public GameObject root;
        public GameObject fill;
        public GameObject rim;
        public Camera cam;
        public RenderTexture rt;
        public Material viewMat;
        public Material solidMat;
        public bool linked;
    }

    static readonly Dictionary<string, View> views = new Dictionary<string, View>();
    static Mesh disc;
    static Mesh ring;
    static Shader colorShader;
    static Shader texShader;
    const int RtSize = 512;

    static string Key(int owner, bool orange)
    {
        return owner + (orange ? ":o" : ":b");
    }

    public static void RebuildOwner(int ownerId)
    {
        UnparentCam(Key(ownerId, false));
        UnparentCam(Key(ownerId, true));
        DestroyKey(Key(ownerId, false));
        DestroyKey(Key(ownerId, true));
        if (GameManager.IsDedicatedServer) return;
        var pair = HSPortalWorld.GetPair(ownerId, false);
        if (pair == null) return;
        if (pair.Blue != null) Build(pair.Blue, pair.Linked);
        if (pair.Orange != null) Build(pair.Orange, pair.Linked);
    }

    public static void DestroyAll()
    {
        var keys = new List<string>(views.Keys);
        for (int i = 0; i < keys.Count; i++) UnparentCam(keys[i]);
        for (int i = 0; i < keys.Count; i++) DestroyKey(keys[i]);
    }

    public static void SyncAll()
    {
        if (views.Count == 0) return;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null) continue;
            SyncPose(pair.Blue);
            SyncPose(pair.Orange);
        }
        RenderViews();
    }

    static void SyncPose(HSPortal portal)
    {
        if (portal == null) return;
        View v;
        if (!views.TryGetValue(Key(portal.OwnerId, portal.Orange), out v) || v == null || v.root == null) return;
        v.root.transform.position = portal.Center - Origin.position;
        v.root.transform.rotation = portal.Rotation;
    }

    static void RenderViews()
    {
        if (GameManager.IsDedicatedServer) return;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null || !pair.Linked || pair.Blue == null || pair.Orange == null) continue;
            RenderThrough(pair.Blue, pair.Orange);
            RenderThrough(pair.Orange, pair.Blue);
        }
    }

    static void RenderThrough(HSPortal lookingAt, HSPortal dest)
    {
        View v;
        if (!views.TryGetValue(Key(lookingAt.OwnerId, lookingAt.Orange), out v) || v == null || v.cam == null) return;

        var camT = v.cam.transform;
        camT.SetParent(null, true);
        camT.position = dest.Center - Origin.position + dest.Normal * 0.15f;
        var up = dest.Up.sqrMagnitude > 0.0001f ? dest.Up : Vector3.up;
        if (Vector3.Dot(up, dest.Normal) > 0.95f) up = Vector3.up;
        camT.rotation = Quaternion.LookRotation(dest.Normal, up);
        camT.localScale = Vector3.one;

        v.cam.enabled = false;
        v.cam.ResetProjectionMatrix();
        v.cam.orthographic = false;
        v.cam.fieldOfView = 75f;
        v.cam.nearClipPlane = 0.08f;
        v.cam.farClipPlane = 250f;
        v.cam.aspect = 1f;
        v.cam.cullingMask = WorldMask();
        v.cam.useOcclusionCulling = false;
        v.cam.clearFlags = CameraClearFlags.Skybox;
        v.cam.backgroundColor = new Color(0.45f, 0.62f, 0.85f, 1f);
        v.cam.targetTexture = v.rt;
        v.cam.depth = -20;

        View destView;
        views.TryGetValue(Key(dest.OwnerId, dest.Orange), out destView);
        bool destWasOn = false;
        if (destView != null && destView.fill != null)
        {
            destWasOn = destView.fill.activeSelf;
            destView.fill.SetActive(false);
        }
        if (v.fill != null) v.fill.SetActive(false);
        var hidden = HideViewmodel();
        var body = ShowLocalBody(true);
        v.cam.Render();
        ShowLocalBody(false, body);
        RestoreViewmodel(hidden);
        if (v.fill != null) v.fill.SetActive(true);
        if (destView != null && destView.fill != null) destView.fill.SetActive(destWasOn);
        if (v.viewMat != null) v.viewMat.mainTexture = v.rt;
    }

    static int WorldMask()
    {
        int mask = ~0;
        mask &= ~(1 << 5);
        var names = new[] { "UI", "ScreenSpace" };
        for (int i = 0; i < names.Length; i++)
        {
            int layer = LayerMask.NameToLayer(names[i]);
            if (layer >= 0) mask &= ~(1 << layer);
        }
        return mask;
    }

    static List<Renderer> HideViewmodel()
    {
        var hidden = new List<Renderer>();
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var p = world != null ? world.GetPrimaryPlayer() : null;
            if (p == null) return hidden;
            CollectEnabled(p.playerCamera != null ? p.playerCamera.transform : null, hidden);
            if (p.vp_FPWeapon != null)
                CollectEnabled(p.vp_FPWeapon.transform, hidden);
        }
        catch { }
        return hidden;
    }

    static void CollectEnabled(Transform root, List<Renderer> hidden)
    {
        if (root == null) return;
        var rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] == null || !rs[i].enabled) continue;
            rs[i].enabled = false;
            hidden.Add(rs[i]);
        }
    }

    static void RestoreViewmodel(List<Renderer> hidden)
    {
        if (hidden == null) return;
        for (int i = 0; i < hidden.Count; i++)
            if (hidden[i] != null) hidden[i].enabled = true;
    }

    class BodyShow
    {
        public readonly List<GameObject> activated = new List<GameObject>();
        public readonly List<Renderer> enabled = new List<Renderer>();
    }

    static BodyShow ShowLocalBody(bool show, BodyShow previous = null)
    {
        if (!show)
        {
            if (previous == null) return null;
            for (int i = 0; i < previous.enabled.Count; i++)
                if (previous.enabled[i] != null) previous.enabled[i].enabled = false;
            for (int i = 0; i < previous.activated.Count; i++)
                if (previous.activated[i] != null) previous.activated[i].SetActive(false);
            return null;
        }
        var state = new BodyShow();
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var p = world != null ? world.GetPrimaryPlayer() : null;
            if (p == null || p.emodel == null) return state;
            var sdcs = p.emodel as EModelSDCS;
            ForceShow(sdcs != null ? sdcs.baseRig : null, state);
            ForceShow(p.emodel.meshTransform != null ? p.emodel.meshTransform.gameObject : null, state);
            if (p.emodel.transform != null) EnableRenderers(p.emodel.transform, state);
        }
        catch { }
        return state;
    }

    static void ForceShow(GameObject go, BodyShow state)
    {
        if (go == null) return;
        if (!go.activeSelf)
        {
            go.SetActive(true);
            state.activated.Add(go);
        }
        EnableRenderers(go.transform, state);
    }

    static void EnableRenderers(Transform root, BodyShow state)
    {
        if (root == null) return;
        var rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null || r.enabled) continue;
            if (IsViewmodelRenderer(r)) continue;
            r.enabled = true;
            state.enabled.Add(r);
        }
    }

    static bool IsViewmodelRenderer(Renderer r)
    {
        var t = r.transform;
        while (t != null)
        {
            var n = t.name;
            if (n.IndexOf("FPV", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Viewmodel", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("vpcam", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            t = t.parent;
        }
        return false;
    }

    static void UnparentCam(string key)
    {
        View v;
        if (!views.TryGetValue(key, out v) || v == null || v.cam == null) return;
        v.cam.transform.SetParent(null, true);
    }

    static void DestroyKey(string key)
    {
        View v;
        if (!views.TryGetValue(key, out v)) return;
        views.Remove(key);
        if (v == null) return;
        if (v.cam != null)
        {
            v.cam.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(v.cam.gameObject);
        }
        if (v.rt != null) v.rt.Release();
        if (v.root != null) UnityEngine.Object.Destroy(v.root);
    }

    static void Build(HSPortal portal, bool linked)
    {
        EnsureMesh();
        var v = new View();
        v.linked = linked;
        v.root = new GameObject(portal.Orange ? "HSPortalOrange" : "HSPortalBlue");
        UnityEngine.Object.DontDestroyOnLoad(v.root);
        v.root.transform.position = portal.Center - Origin.position;
        v.root.transform.rotation = portal.Rotation;
        v.root.transform.localScale = new Vector3(portal.HalfWidth * 2f, portal.HalfHeight * 2f, 1f);

        v.rim = new GameObject("rim");
        v.rim.transform.SetParent(v.root.transform, false);
        v.rim.transform.localPosition = new Vector3(0f, 0f, 0.008f);
        AddMesh(v.rim, ring, Tint(portal.Orange, linked, true));

        v.fill = new GameObject("fill");
        v.fill.transform.SetParent(v.root.transform, false);
        var fillMr = AddMesh(v.fill, disc, Tint(portal.Orange, linked, false));
        v.solidMat = fillMr.sharedMaterial;

        if (linked)
        {
            v.rt = new RenderTexture(RtSize, RtSize, 16);
            v.rt.name = "HSPortalRT_" + Key(portal.OwnerId, portal.Orange);
            var camGo = new GameObject("HSPortalCam_" + (portal.Orange ? "O" : "B"));
            UnityEngine.Object.DontDestroyOnLoad(camGo);
            v.cam = camGo.AddComponent<Camera>();
            v.cam.enabled = false;
            v.cam.targetTexture = v.rt;
            v.cam.depth = -20;
            v.cam.clearFlags = CameraClearFlags.Skybox;
            v.cam.allowHDR = false;
            v.cam.allowMSAA = false;
            v.cam.useOcclusionCulling = false;
            var al = camGo.GetComponent<AudioListener>();
            if (al != null) UnityEngine.Object.Destroy(al);
            v.viewMat = ViewMat(Tint(portal.Orange, true, false));
            v.viewMat.mainTexture = v.rt;
            fillMr.sharedMaterial = v.viewMat;
        }

        views[Key(portal.OwnerId, portal.Orange)] = v;
    }

    static Color Tint(bool orange, bool linked, bool rim)
    {
        Color c = orange ? new Color(1f, 0.42f, 0.06f, 1f) : new Color(0.18f, 0.55f, 1f, 1f);
        if (rim) return Color.Lerp(c, Color.white, 0.25f);
        if (!linked) return Color.Lerp(c, new Color(0.08f, 0.08f, 0.1f, 1f), 0.55f);
        return Color.white;
    }

    static MeshRenderer AddMesh(GameObject go, Mesh mesh, Color color)
    {
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ColorMat(color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    static Material ColorMat(Color color)
    {
        if (colorShader == null)
        {
            colorShader = FindShader("HSPortal/UnlitColor");
            if (colorShader == null) colorShader = Shader.Find("Unlit/Color");
            if (colorShader == null) colorShader = Shader.Find("Sprites/Default");
            if (colorShader == null) colorShader = Shader.Find("Hidden/Internal-Colored");
        }
        var m = new Material(colorShader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        else m.color = color;
        m.renderQueue = 3000;
        return m;
    }

    static Material ViewMat(Color tint)
    {
        if (texShader == null)
        {
            var names = new[]
            {
                "HSPortal/View",
                "Unlit/Texture",
                "Unlit/Transparent",
                "Sprites/Default",
                "UI/Default",
                "Legacy Shaders/Diffuse"
            };
            texShader = FindShader("HSPortal/View");
            if (texShader == null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var s = Shader.Find(names[i]);
                    if (s != null && s.isSupported) { texShader = s; break; }
                }
            }
            if (texShader == null) texShader = colorShader;
        }
        var m = new Material(texShader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        m.renderQueue = 2999;
        return m;
    }

    static Shader FindShader(string name)
    {
        var found = Shader.Find(name);
        if (found != null && found.isSupported) return found;
        try
        {
            foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (b == null) continue;
                var shaders = b.LoadAllAssets<Shader>();
                if (shaders == null) continue;
                for (int i = 0; i < shaders.Length; i++)
                    if (shaders[i] != null && shaders[i].name == name) return shaders[i];
            }
        }
        catch { }
        return found;
    }

    static void EnsureMesh()
    {
        if (disc != null && ring != null) return;
        disc = Ellipse(1f, 28, 0f, 1f);
        ring = Ellipse(1f, 28, 0.82f, 1.02f);
    }

    static Mesh Ellipse(float radius, int segs, float inner, float outer)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        if (inner <= 0.001f)
        {
            verts.Add(Vector3.zero);
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius * outer, Mathf.Sin(a) * radius * outer, 0f));
                norms.Add(Vector3.forward);
                uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 1; i <= segs; i++)
            {
                tris.Add(0);
                tris.Add(i);
                tris.Add(i + 1);
            }
        }
        else
        {
            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts.Add(new Vector3(c * radius * inner, s * radius * inner, 0f));
                verts.Add(new Vector3(c * radius * outer, s * radius * outer, 0f));
                norms.Add(Vector3.forward);
                norms.Add(Vector3.forward);
                uvs.Add(new Vector2(c * 0.4f + 0.5f, s * 0.4f + 0.5f));
                uvs.Add(new Vector2(c * 0.5f + 0.5f, s * 0.5f + 0.5f));
            }
            for (int i = 0; i < segs; i++)
            {
                int i0 = i * 2;
                tris.Add(i0);
                tris.Add(i0 + 1);
                tris.Add(i0 + 3);
                tris.Add(i0);
                tris.Add(i0 + 3);
                tris.Add(i0 + 2);
            }
        }
        var mesh = new Mesh();
        mesh.name = inner <= 0.001f ? "HSPortalDisc" : "HSPortalRing";
        mesh.vertices = verts.ToArray();
        mesh.normals = norms.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }
}
