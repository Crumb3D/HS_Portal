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
            mf.sharedMesh = Puddle(s.Seed);
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
        var plane = HSPortalMath.FaceCenter(p.splat.Cell, p.splat.Face) + n * 0.012f;
        var along = Vector3.ProjectOnPlane(p.splat.Center - plane, n);
        p.go.transform.position = plane + along - Origin.position;
        var up = Mathf.Abs(n.y) > 0.85f ? Vector3.forward : Vector3.up;
        p.go.transform.rotation = Quaternion.LookRotation(n, up);
        float w = p.splat.Radius * 1.7f;
        float h = Mathf.Abs(n.y) > 0.85f ? w : w * 1.15f;
        p.go.transform.localScale = new Vector3(w, h, 0.045f);
    }

    static Material ColorMat(byte color)
    {
        if (color == HSPortalGel.Orange)
        {
            if (orangeMat == null) orangeMat = Make(new Color(1f, 0.45f, 0.08f, 0.82f));
            return orangeMat;
        }
        if (color == HSPortalGel.White)
        {
            if (whiteMat == null) whiteMat = Make(new Color(0.94f, 0.96f, 0.99f, 0.8f));
            return whiteMat;
        }
        if (blueMat == null) blueMat = Make(new Color(0.2f, 0.58f, 1f, 0.82f));
        return blueMat;
    }

    static Material Make(Color c)
    {
        if (colorShader == null)
        {
            colorShader = Shader.Find("Unlit/Transparent");
            if (colorShader == null) colorShader = Shader.Find("Sprites/Default");
            if (colorShader == null) colorShader = Shader.Find("Unlit/Color");
        }
        var m = new Material(colorShader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c;
        m.renderQueue = 3001;
        return m;
    }

    static Mesh Puddle(int seed)
    {
        Mesh cached;
        if (blobs.TryGetValue(seed, out cached) && cached != null) return cached;
        var rng = new System.Random(seed);
        int segs = 36;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        verts.Add(new Vector3(0f, 0f, 0.55f));
        norms.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segs; i++)
        {
            float a = (i / (float)segs) * Mathf.PI * 2f;
            float lobe = 0.78f + 0.16f * Mathf.Sin(a * 3f + seed * 0.11f) + 0.1f * Hash01(rng);
            if (Mathf.Sin(a + 1.2f) < -0.55f) lobe += 0.18f * Hash01(rng);
            float x = Mathf.Cos(a) * lobe * 0.5f;
            float y = Mathf.Sin(a) * lobe * 0.5f;
            if (y < 0f) y *= 1.12f + 0.12f * Hash01(rng);
            verts.Add(new Vector3(x, y, 0.02f));
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(x + 0.5f, y + 0.5f));
        }
        for (int i = 1; i <= segs; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }
        var mesh = new Mesh();
        mesh.name = "HSGelPuddle";
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
