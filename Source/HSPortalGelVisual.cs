using System.Collections.Generic;
using UnityEngine;

public static class HSPortalGelVisual
{
    class Patch
    {
        public GameObject go;
        public HSPortalGelSplat splat;
    }

    static readonly List<Patch> patches = new List<Patch>();
    static Shader colorShader;
    static Material blueMat;
    static Material orangeMat;
    static Material whiteMat;
    static readonly Dictionary<int, Mesh> blobs = new Dictionary<int, Mesh>();

    public static void DestroyAll()
    {
        for (int i = 0; i < patches.Count; i++)
        {
            if (patches[i] != null && patches[i].go != null)
                UnityEngine.Object.Destroy(patches[i].go);
        }
        patches.Clear();
    }

    public static void RebuildAll()
    {
        DestroyAll();
        if (GameManager.IsDedicatedServer) return;
        foreach (var s in HSPortalGel.All)
        {
            if (s == null || s.Color == HSPortalGel.None) continue;
            var p = new Patch();
            p.splat = s;
            p.go = new GameObject("HSGelSplat");
            UnityEngine.Object.DontDestroyOnLoad(p.go);
            var mf = p.go.AddComponent<MeshFilter>();
            mf.sharedMesh = Blob(s.Seed);
            var mr = p.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ColorMat(s.Color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            patches.Add(p);
            Pose(p);
        }
    }

    public static void SyncAll()
    {
        for (int i = 0; i < patches.Count; i++)
            Pose(patches[i]);
    }

    static void Pose(Patch p)
    {
        if (p == null || p.go == null || p.splat == null) return;
        var n = p.splat.Normal.sqrMagnitude > 0.0001f ? p.splat.Normal.normalized : HSPortalMath.FaceNormal(p.splat.Face);
        p.go.transform.position = p.splat.Center - Origin.position;
        var up = Mathf.Abs(n.y) > 0.85f ? Vector3.forward : Vector3.up;
        p.go.transform.rotation = Quaternion.LookRotation(n, up);
        float s = p.splat.Radius * 2f;
        p.go.transform.localScale = new Vector3(s, s, 1f);
    }

    static Material ColorMat(byte color)
    {
        if (color == HSPortalGel.Orange)
        {
            if (orangeMat == null) orangeMat = Make(new Color(1f, 0.42f, 0.06f, 0.94f));
            return orangeMat;
        }
        if (color == HSPortalGel.White)
        {
            if (whiteMat == null) whiteMat = Make(new Color(0.93f, 0.95f, 0.98f, 0.96f));
            return whiteMat;
        }
        if (blueMat == null) blueMat = Make(new Color(0.18f, 0.55f, 1f, 0.94f));
        return blueMat;
    }

    static Material Make(Color c)
    {
        if (colorShader == null)
        {
            colorShader = Shader.Find("Sprites/Default");
            if (colorShader == null) colorShader = Shader.Find("Unlit/Color");
            if (colorShader == null) colorShader = Shader.Find("Unlit/Transparent");
        }
        var m = new Material(colorShader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c;
        m.renderQueue = 3001;
        return m;
    }

    static Mesh Blob(int seed)
    {
        Mesh cached;
        if (blobs.TryGetValue(seed, out cached) && cached != null) return cached;
        var rng = new System.Random(seed);
        int segs = 26;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        verts.Add(new Vector3(0f, 0f, 0.04f));
        norms.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segs; i++)
        {
            float a = (i / (float)segs) * Mathf.PI * 2f;
            float wobble = 0.58f + 0.42f * Hash01(rng) + 0.12f * Mathf.Sin(a * 5f + seed * 0.017f);
            float x = Mathf.Cos(a) * wobble * 0.5f;
            float y = Mathf.Sin(a) * wobble * 0.5f;
            verts.Add(new Vector3(x, y, 0.008f));
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(x + 0.5f, y + 0.5f));
        }
        for (int i = 1; i <= segs; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }
        int dome = verts.Count;
        verts.Add(new Vector3(0.02f * (Hash01(rng) - 0.5f), 0.02f * (Hash01(rng) - 0.5f), 0.38f));
        norms.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 0.5f));
        int domeSegs = 18;
        for (int i = 0; i <= domeSegs; i++)
        {
            float a = (i / (float)domeSegs) * Mathf.PI * 2f;
            float r = 0.18f + 0.07f * Hash01(rng);
            verts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.12f));
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.2f, 0.5f + Mathf.Sin(a) * 0.2f));
        }
        for (int i = 1; i <= domeSegs; i++)
        {
            tris.Add(dome);
            tris.Add(dome + i);
            tris.Add(dome + i + 1);
        }
        var mesh = new Mesh();
        mesh.name = "HSGelBlob";
        mesh.vertices = verts.ToArray();
        mesh.normals = norms.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        blobs[seed] = mesh;
        return mesh;
    }

    static float Hash01(System.Random rng)
    {
        return (float)rng.NextDouble();
    }
}
