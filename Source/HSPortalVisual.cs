using System.Collections.Generic;
using UnityEngine;

public static class HSPortalVisual
{
    static readonly Dictionary<string, GameObject> roots = new Dictionary<string, GameObject>();
    static Mesh disc;
    static Mesh ring;
    static Shader shader;

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
        var keys = new List<string>(roots.Keys);
        for (int i = 0; i < keys.Count; i++) DestroyKey(keys[i]);
    }

    public static void SyncAll()
    {
        if (roots.Count == 0) return;
        foreach (var kv in HSPortalWorld.All)
        {
            var pair = kv.Value;
            if (pair == null) continue;
            Sync(pair.Blue);
            Sync(pair.Orange);
        }
    }

    static void Sync(HSPortal portal)
    {
        if (portal == null) return;
        GameObject go;
        if (!roots.TryGetValue(Key(portal.OwnerId, portal.Orange), out go) || go == null) return;
        go.transform.position = portal.Center - Origin.position;
        go.transform.rotation = portal.Rotation;
    }

    static void DestroyKey(string key)
    {
        GameObject go;
        if (!roots.TryGetValue(key, out go)) return;
        roots.Remove(key);
        if (go != null) Object.Destroy(go);
    }

    static void Build(HSPortal portal, bool linked)
    {
        EnsureMesh();
        var go = new GameObject(portal.Orange ? "HSPortalOrange" : "HSPortalBlue");
        Object.DontDestroyOnLoad(go);
        go.transform.position = portal.Center - Origin.position;
        go.transform.rotation = portal.Rotation;
        go.transform.localScale = new Vector3(portal.HalfWidth * 2f, portal.HalfHeight * 2f, 1f);

        var rim = new GameObject("rim");
        rim.transform.SetParent(go.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0f, 0.005f);
        AddMesh(rim, ring, Tint(portal.Orange, linked, true));

        var fill = new GameObject("fill");
        fill.transform.SetParent(go.transform, false);
        AddMesh(fill, disc, Tint(portal.Orange, linked, false));

        roots[Key(portal.OwnerId, portal.Orange)] = go;
    }

    static Color Tint(bool orange, bool linked, bool rim)
    {
        Color c = orange ? new Color(1f, 0.42f, 0.06f, 1f) : new Color(0.18f, 0.55f, 1f, 1f);
        if (rim) return Color.Lerp(c, Color.white, 0.25f);
        if (!linked) return Color.Lerp(c, new Color(0.08f, 0.08f, 0.1f, 1f), 0.55f);
        return Color.Lerp(c, Color.white, 0.08f) * 0.85f;
    }

    static void AddMesh(GameObject go, Mesh mesh, Color color)
    {
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mat(color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static Material Mat(Color color)
    {
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Standard");
        }
        var m = new Material(shader != null ? shader : Shader.Find("Standard"));
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        else m.color = color;
        m.renderQueue = 3000;
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
