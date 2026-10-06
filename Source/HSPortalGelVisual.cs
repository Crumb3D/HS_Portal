using System.Collections.Generic;
using UnityEngine;

public static class HSPortalGelVisual
{
    class Patch
    {
        public GameObject go;
        public Vector3i pos;
        public BlockFace face;
        public byte color;
    }

    static readonly Dictionary<string, Patch> patches = new Dictionary<string, Patch>();
    static Mesh quad;
    static Shader colorShader;

    static string Id(Vector3i p, BlockFace f)
    {
        return p.x + ":" + p.y + ":" + p.z + ":" + (int)f;
    }

    public static void DestroyAll()
    {
        var keys = new List<string>(patches.Keys);
        for (int i = 0; i < keys.Count; i++) RemoveKey(keys[i]);
    }

    public static void RebuildAll()
    {
        DestroyAll();
        if (GameManager.IsDedicatedServer) return;
        foreach (var kv in HSPortalGel.All)
            Upsert(kv.Key, kv.Value.Key, kv.Value.Value);
    }

    public static void Upsert(Vector3i pos, BlockFace face, byte color)
    {
        if (GameManager.IsDedicatedServer) return;
        if (color == 0) { Remove(pos, face); return; }
        EnsureMesh();
        var id = Id(pos, face);
        Patch p;
        if (!patches.TryGetValue(id, out p) || p == null || p.go == null)
        {
            p = new Patch();
            p.go = new GameObject("HSGel");
            UnityEngine.Object.DontDestroyOnLoad(p.go);
            var mf = p.go.AddComponent<MeshFilter>();
            mf.sharedMesh = quad;
            p.go.AddComponent<MeshRenderer>();
            patches[id] = p;
        }
        p.pos = pos;
        p.face = face;
        p.color = color;
        Pose(p);
    }

    public static void Remove(Vector3i pos, BlockFace face)
    {
        RemoveKey(Id(pos, face));
    }

    public static void SyncAll()
    {
        if (patches.Count == 0) return;
        foreach (var kv in patches)
        {
            if (kv.Value != null) Pose(kv.Value);
        }
    }

    static void RemoveKey(string id)
    {
        Patch p;
        if (!patches.TryGetValue(id, out p)) return;
        patches.Remove(id);
        if (p != null && p.go != null) UnityEngine.Object.Destroy(p.go);
    }

    static void Pose(Patch p)
    {
        if (p == null || p.go == null) return;
        var n = HSPortalMath.FaceNormal(p.face);
        var center = HSPortalMath.FaceCenter(p.pos, p.face) + n * 0.02f;
        p.go.transform.position = center - Origin.position;
        var up = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
        p.go.transform.rotation = Quaternion.LookRotation(n, up);
        p.go.transform.localScale = new Vector3(0.98f, 0.98f, 1f);
        var mr = p.go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = ColorMat(p.color);
    }

    static Material blueMat;
    static Material orangeMat;

    static Material ColorMat(byte color)
    {
        if (color == HSPortalGel.Orange)
        {
            if (orangeMat == null) orangeMat = Make(new Color(1f, 0.42f, 0.06f, 0.92f));
            return orangeMat;
        }
        if (blueMat == null) blueMat = Make(new Color(0.18f, 0.55f, 1f, 0.92f));
        return blueMat;
    }

    static Material Make(Color c)
    {
        if (colorShader == null)
        {
            colorShader = Shader.Find("Unlit/Color");
            if (colorShader == null) colorShader = Shader.Find("Sprites/Default");
        }
        var m = new Material(colorShader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c;
        m.renderQueue = 3001;
        return m;
    }

    static void EnsureMesh()
    {
        if (quad != null) return;
        quad = new Mesh();
        quad.name = "HSGelQuad";
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        quad.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
    }
}
