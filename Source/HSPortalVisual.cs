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
        var playerCam = PlayerCam();
        if (playerCam == null) return;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null || !pair.Linked || pair.Blue == null || pair.Orange == null) continue;
            RenderThrough(pair.Blue, pair.Orange, playerCam);
            RenderThrough(pair.Orange, pair.Blue, playerCam);
        }
    }

    static void RenderThrough(HSPortal lookingAt, HSPortal dest, Camera playerCam)
    {
        View v;
        if (!views.TryGetValue(Key(lookingAt.OwnerId, lookingAt.Orange), out v) || v == null || v.cam == null) return;
        var camWorld = playerCam.transform.position + Origin.position;
        var outWorld = HSPortalMath.TransformPoint(lookingAt, dest, camWorld);
        v.cam.transform.position = outWorld - Origin.position;
        v.cam.transform.rotation = HSPortalMath.TransformRotation(lookingAt, dest, playerCam.transform.rotation);
        v.cam.ResetProjectionMatrix();
        v.cam.fieldOfView = playerCam.fieldOfView;
        v.cam.nearClipPlane = 0.08f;
        v.cam.farClipPlane = playerCam.farClipPlane > 1f ? playerCam.farClipPlane : 250f;
        v.cam.aspect = playerCam.aspect;
        v.cam.cullingMask = playerCam.cullingMask;
        v.cam.useOcclusionCulling = false;
        v.cam.clearFlags = playerCam.clearFlags;
        v.cam.backgroundColor = playerCam.backgroundColor;
        SetOblique(v.cam, dest.Center - Origin.position, dest.Normal);

        View destView;
        bool destWasOn = false;
        if (views.TryGetValue(Key(dest.OwnerId, dest.Orange), out destView) && destView != null && destView.fill != null)
        {
            destWasOn = destView.fill.activeSelf;
            destView.fill.SetActive(false);
        }
        if (v.fill != null) v.fill.SetActive(false);
        v.cam.Render();
        if (v.fill != null) v.fill.SetActive(true);
        if (destView != null && destView.fill != null) destView.fill.SetActive(destWasOn);
        if (v.viewMat != null) v.viewMat.mainTexture = v.rt;
    }

    static void SetOblique(Camera cam, Vector3 planePos, Vector3 planeNormal)
    {
        var offset = planePos + planeNormal * 0.04f;
        var m = cam.worldToCameraMatrix;
        var cpos = m.MultiplyPoint(offset);
        var cnormal = m.MultiplyVector(planeNormal).normalized;
        if (Mathf.Abs(cnormal.z) < 0.001f) return;
        var clip = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        try { cam.projectionMatrix = cam.CalculateObliqueMatrix(clip); }
        catch { cam.ResetProjectionMatrix(); }
    }

    static Camera PlayerCam()
    {
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var p = world != null ? world.GetPrimaryPlayer() : null;
            if (p != null && p.playerCamera != null) return p.playerCamera;
        }
        catch { }
        return Camera.main;
    }

    static void DestroyKey(string key)
    {
        View v;
        if (!views.TryGetValue(key, out v)) return;
        views.Remove(key);
        if (v == null) return;
        if (v.cam != null) UnityEngine.Object.Destroy(v.cam.gameObject);
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
            colorShader = Shader.Find("Unlit/Color");
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
            for (int i = 0; i < names.Length; i++)
            {
                var s = Shader.Find(names[i]);
                if (s != null && s.isSupported) { texShader = s; break; }
            }
            if (texShader == null) texShader = colorShader;
        }
        var m = new Material(texShader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        m.renderQueue = 2999;
        return m;
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
