using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class HSPortalModelImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/HSPortal/Textures/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = true;
        ti.sRGBTexture = assetPath.IndexOf("Displacement", StringComparison.OrdinalIgnoreCase) < 0;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureType = TextureImporterType.Default;
        ti.maxTextureSize = 2048;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').Contains("/HSPortal/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.bakeAxisConversion = false;
        mi.preserveHierarchy = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = true;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importBlendShapes = false;
        mi.addCollider = false;
        mi.isReadable = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.animationCompression = ModelImporterAnimationCompression.Off;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.Replace('\\', '/').Contains("/HSPortal/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        if (clips == null) return;
        for (int i = 0; i < clips.Length; i++)
        {
            var n = clips[i].name ?? "";
            clips[i].loopTime = n.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0;
            clips[i].lockRootRotation = true;
            clips[i].lockRootHeightY = true;
            clips[i].lockRootPositionXZ = true;
        }
        mi.clipAnimations = clips;
    }
}

public static class HSPortalBuild
{
    const string Root = "Assets/HSPortal";
    const string BundleName = "hsportal";

    [MenuItem("HS Portal/Build Bundle")]
    public static void BuildMenu()
    {
        Run(false);
    }

    public static void Build()
    {
        Run(true);
    }

    static void Run(bool exit)
    {
        var log = new StringBuilder();
        int code = 0;
        try
        {
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Anim");
            EnsureFolder(Root + "/Textures");
            EnsureFolder(Root + "/Shaders");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root + "/Shaders/HSPortalUnlit.shader", ImportAssetOptions.ForceUpdate);
            foreach (var shPath in new[]
            {
                Root + "/Shaders/HSPortalCube.shader",
                Root + "/Shaders/HSPortalView.shader",
                Root + "/Shaders/HSPortalUnlit.shader"
            })
            {
                if (AssetDatabase.LoadAssetAtPath<Shader>(shPath) != null)
                    AssetImporter.GetAtPath(shPath).SetAssetBundleNameAndVariant(BundleName, "");
            }
            var mats = EnsureMaterials();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            AnimationClip idleClip = null, fireClip = null, rejectClip = null;
            var gunPath = Root + "/Models/PortalGun.fbx";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
            if (src == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("PortalGun t:Model", new[] { Root + "/Models" }))
                {
                    gunPath = AssetDatabase.GUIDToAssetPath(guid);
                    src = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
                    if (src != null) break;
                }
            }
            if (src == null) throw new Exception("No PortalGun model in " + Root + "/Models");
            var loaded = AssetDatabase.LoadAllAssetsAtPath(gunPath);
            for (int i = 0; i < loaded.Length; i++)
            {
                var clip = loaded[i] as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview", StringComparison.Ordinal)) continue;
                var n = clip.name;
                if (MatchClip(n, "Idle") && idleClip == null) idleClip = clip;
                else if (MatchClip(n, "Fire") && fireClip == null) fireClip = clip;
                else if (MatchClip(n, "Reject") && rejectClip == null) rejectClip = clip;
            }
            log.AppendLine("Model " + src.name + " clips idle=" + (idleClip != null) + " fire=" + (fireClip != null) + " reject=" + (rejectClip != null));

            var ctrlPath = Root + "/Anim/PortalGun.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
                AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Reject", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = idleClip;
            var fire = sm.AddState("Fire");
            fire.motion = fireClip;
            var reject = sm.AddState("Reject");
            reject.motion = rejectClip;
            sm.defaultState = idle;
            AddTrig(sm, fire, "Fire");
            AddTrig(sm, reject, "Reject");
            AddExit(fire, idle);
            AddExit(reject, idle);
            EditorUtility.SetDirty(ctrl);

            var go = UnityEngine.Object.Instantiate(src);
            go.name = "PortalGun";
            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            ApplyMats(go, mats);
            var anims = go.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
                UnityEngine.Object.DestroyImmediate(anims[i]);
            var hold = OrientForHold(go, true);
            AttachViewProbe(hold != null ? hold.gameObject : go, mats);
            var animatorHost = hold != null ? hold.gameObject : go;
            var animator = animatorHost.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                UnityEngine.Object.DestroyImmediate(cols[i]);

            var prefabPath = Root + "/Prefabs/PortalGun.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            var b = RenderBounds(go);
            UnityEngine.Object.DestroyImmediate(go);
            AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(BundleName, "");
            AssetImporter.GetAtPath(ctrlPath).SetAssetBundleNameAndVariant(BundleName, "");
            log.AppendLine(string.Format("PortalGun bounds min {0} max {1}", V(b.min), V(b.max)));

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                if (model.name.Equals("PortalGun", StringComparison.OrdinalIgnoreCase)) continue;
                MakePropPrefab(model, mats, log);
            }

            AssetDatabase.SaveAssets();

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BundleOut"));
            Directory.CreateDirectory(outDir);
            var manifest = BuildPipeline.BuildAssetBundles(outDir,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("BuildAssetBundles returned null");

            var modResources = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Resources"));
            Directory.CreateDirectory(modResources);
            var dest = Path.Combine(modResources, "HSPortal.unity3d");
            File.Copy(Path.Combine(outDir, BundleName), dest, true);
            log.AppendLine("Bundle: " + dest + " (" + new FileInfo(dest).Length + " bytes)");
            log.AppendLine("DONE");
        }
        catch (Exception e)
        {
            code = 1;
            log.AppendLine("FAILED: " + e);
        }
        var report = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build_report.txt"));
        File.WriteAllText(report, log.ToString());
        Debug.Log("[HSPortalBuild]\n" + log);
        if (exit) EditorApplication.Exit(code);
    }

    static bool MatchClip(string n, string token)
    {
        if (string.IsNullOrEmpty(n)) return false;
        if (n.Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
        if (n.EndsWith("|" + token, StringComparison.OrdinalIgnoreCase)) return true;
        if (n.EndsWith("_" + token, StringComparison.OrdinalIgnoreCase)) return true;
        return n.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void AddTrig(AnimatorStateMachine sm, AnimatorState dest, string param)
    {
        var t = sm.AddAnyStateTransition(dest);
        t.AddCondition(AnimatorConditionMode.If, 0, param);
        t.hasExitTime = false;
        t.duration = 0.04f;
        t.canTransitionToSelf = true;
    }

    static void AddExit(AnimatorState from, AnimatorState idle)
    {
        var t = from.AddTransition(idle);
        t.hasExitTime = true;
        t.exitTime = 0.92f;
        t.duration = 0.06f;
        t.hasFixedDuration = true;
    }

    static string V(Vector3 v)
    {
        return string.Format("({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);
    }

    static Bounds RenderBounds(GameObject go)
    {
        Bounds b = new Bounds();
        bool any = false;
        var filters = go.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            var mf = filters[i];
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.name == "ViewProbe") continue;
            var meshB = mf.sharedMesh.bounds;
            var m = mf.transform.localToWorldMatrix;
            var c = meshB.center;
            var e = meshB.extents;
            var corners = new Vector3[]
            {
                new Vector3(c.x - e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z + e.z)
            };
            for (int k = 0; k < corners.Length; k++)
            {
                var w = m.MultiplyPoint3x4(corners[k]);
                if (!any) { b = new Bounds(w, Vector3.zero); any = true; }
                else b.Encapsulate(w);
            }
        }
        return b;
    }

    static void ApplyMats(GameObject go, Dictionary<string, Material> mats)
    {
        Material fallback;
        mats.TryGetValue("HSGun_Metal", out fallback);
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var shared = mr.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                var key = shared[i] != null ? CleanName(shared[i].name) : "";
                Material m;
                if (mats.TryGetValue(key, out m)) shared[i] = m;
                else if (fallback != null) shared[i] = fallback;
            }
            mr.sharedMaterials = shared;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
    }

    static string CleanName(string n)
    {
        n = n.Replace(" (Instance)", "");
        int dot = n.IndexOf('.');
        if (dot > 0) n = n.Substring(0, dot);
        return n.Trim();
    }

    static Transform OrientForHold(GameObject go, bool barrelAlongY)
    {
        // Portal gun Idle writes Blender identity (barrel +Y) — pitch 90 onto HoldType 1 +Z.
        // Gel gun verts are already Z-forward; only yaw 180.
        var hold = new GameObject("Hold");
        hold.transform.SetParent(go.transform, false);
        var anim = new GameObject("Anim");
        anim.transform.SetParent(hold.transform, false);
        var kids = new List<Transform>();
        foreach (Transform t in go.transform)
        {
            if (t != hold.transform) kids.Add(t);
        }
        for (int i = 0; i < kids.Count; i++)
            kids[i].SetParent(anim.transform, true);
        hold.transform.localRotation = barrelAlongY
            ? Quaternion.Euler(-90f, 180f, 0f)
            : Quaternion.Euler(0f, 180f, 0f);
        hold.transform.localPosition = new Vector3(0.05f, -0.1f, 0.22f);
        hold.transform.localScale = Vector3.one * 0.8f;
        return anim.transform;
    }

    static void AttachViewProbe(GameObject host, Dictionary<string, Material> mats)
    {
        var sh = Shader.Find("HSPortal/View");
        if (sh == null) return;
        var path = Root + "/Materials/HSPortal_View.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(m);
        mats["HSPortal_View"] = m;
        var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        probe.name = "ViewProbe";
        probe.transform.SetParent(host.transform, false);
        probe.transform.localScale = Vector3.one * 0.001f;
        probe.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        var col = probe.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        var mr = probe.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = m;
            mr.enabled = false;
        }
        probe.SetActive(false);
        AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(BundleName, "");
        var shaderPath = Root + "/Shaders/HSPortalView.shader";
        if (AssetDatabase.LoadAssetAtPath<Shader>(shaderPath) != null)
            AssetImporter.GetAtPath(shaderPath).SetAssetBundleNameAndVariant(BundleName, "");
        var cubeShader = Root + "/Shaders/HSPortalCube.shader";
        if (AssetDatabase.LoadAssetAtPath<Shader>(cubeShader) != null)
            AssetImporter.GetAtPath(cubeShader).SetAssetBundleNameAndVariant(BundleName, "");
    }

    static Material CubeLit()
    {
        var path = Root + "/Materials/HSCube_Body.mat";
        var sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("HSPortal/Cube");
        if (sh == null) sh = UnlitShader();
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Cube_Diffuse.jpg");
        var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Cube_Glow.jpg");
        if (m.HasProperty("_MainTex") && diff != null) m.SetTexture("_MainTex", diff);
        if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.82f, 0.82f, 0.84f, 1f));
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.28f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.32f);
        if (glow != null && m.HasProperty("_EmissionMap"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", glow);
            m.SetColor("_EmissionColor", new Color(0.75f, 0.2f, 0.5f) * 0.55f);
        }
        if (m.HasProperty("_EmissionTex") && glow != null) m.SetTexture("_EmissionTex", glow);
        if (m.HasProperty("_EmissionStrength")) m.SetFloat("_EmissionStrength", 0.85f);
        EditorUtility.SetDirty(m);
        AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(BundleName, "");
        foreach (var texPath in new[] { Root + "/Textures/Cube_Diffuse.jpg", Root + "/Textures/Cube_Glow.jpg" })
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) != null)
                AssetImporter.GetAtPath(texPath).SetAssetBundleNameAndVariant(BundleName, "");
        }
        return m;
    }

    static Dictionary<string, Material> EnsureMaterials()
    {
        var unlit = Shader.Find("HSPortal/UnlitColor");
        if (unlit == null) Debug.LogWarning("[HSPortalBuild] HSPortal/UnlitColor missing — gun will be white in-game");
        var d = new Dictionary<string, Material>();
        d["HSGun_Metal"] = Unlit("HSGun_Metal", new Color(0.32f, 0.30f, 0.27f));
        d["HSGun_MetalDark"] = Unlit("HSGun_MetalDark", new Color(0.12f, 0.12f, 0.12f));
        d["HSGun_MetalLight"] = Unlit("HSGun_MetalLight", new Color(0.55f, 0.53f, 0.48f));
        d["HSGun_Grip"] = Unlit("HSGun_Grip", new Color(0.09f, 0.08f, 0.07f));
        d["HSGun_Copper"] = Unlit("HSGun_Copper", new Color(0.70f, 0.36f, 0.12f));
        d["HSGun_Gauge"] = Unlit("HSGun_Gauge", new Color(0.08f, 0.08f, 0.09f));
        d["HSGun_Plate"] = Unlit("HSGun_Plate", new Color(0.42f, 0.38f, 0.22f));
        d["HSGun_Hazard"] = Unlit("HSGun_Hazard", new Color(0.85f, 0.68f, 0.08f));
        d["HSGun_GaugeFace"] = Unlit("HSGun_GaugeFace", new Color(0.88f, 0.78f, 0.52f));
        d["HSGun_Glass"] = Unlit("HSGun_Glass", new Color(0.45f, 0.70f, 0.95f, 0.35f));
        d["HSGun_Blue"] = Unlit("HSGun_Blue", new Color(0.20f, 0.55f, 1f));
        d["HSGun_Orange"] = Unlit("HSGun_Orange", new Color(1f, 0.40f, 0.06f));
        d["HSGun_Glow"] = Unlit("HSGun_Glow", new Color(0.65f, 0.90f, 1f));
        d["HSCube_Metal"] = Unlit("HSCube_Metal", new Color(0.32f, 0.30f, 0.27f));
        d["HSCube_MetalDark"] = Unlit("HSCube_MetalDark", new Color(0.12f, 0.12f, 0.13f));
        d["HSCube_Heart"] = Unlit("HSCube_Heart", new Color(0.78f, 0.16f, 0.22f));
        d["HSCube_Stripe"] = Unlit("HSCube_Stripe", new Color(0.85f, 0.68f, 0.08f));
        d["HSCube_Body"] = CubeLit();
        d["HSBoot_Leather"] = Unlit("HSBoot_Leather", new Color(0.22f, 0.13f, 0.08f));
        d["HSBoot_Metal"] = Unlit("HSBoot_Metal", new Color(0.42f, 0.42f, 0.40f));
        d["HSBoot_Orange"] = Unlit("HSBoot_Orange", new Color(0.95f, 0.45f, 0.08f));
        d["HSBoot_Sole"] = Unlit("HSBoot_Sole", new Color(0.08f, 0.08f, 0.09f));
        AssetDatabase.SaveAssets();
        return d;
    }

    static void MakePropPrefab(GameObject src, Dictionary<string, Material> mats, StringBuilder log)
    {
        var go = UnityEngine.Object.Instantiate(src);
        go.name = src.name;
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        ApplyMats(go, mats);
        var anims = go.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
            UnityEngine.Object.DestroyImmediate(anims[i]);
        bool isGun = src.name.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isCube = src.name.IndexOf("Cube", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isGun)
            OrientForHold(go, false);
        if (isCube)
        {
            EnsureTag("T_Block");
            go.tag = "T_Block";
            go.layer = 16;
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                UnityEngine.Object.DestroyImmediate(cols[i]);
            var kill = new List<GameObject>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 16;
                if (t.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase))
                    kill.Add(t.gameObject);
            }
            for (int i = 0; i < kill.Count; i++)
                UnityEngine.Object.DestroyImmediate(kill[i]);
            // 7DTD ModelEntity sits on the block corner. Mesh is centered, so
            // shift it into the 0..1 cell and keep ModelOffset at 0,0,0.
            foreach (Transform t in go.transform)
                t.localPosition += new Vector3(0.5f, 0.5f, 0.5f);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0.5f, 0.5f, 0.5f);
            bc.size = Vector3.one;
        }
        else
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                UnityEngine.Object.DestroyImmediate(cols[i]);
        }
        var prefabPath = Root + "/Prefabs/" + src.name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        var b = RenderBounds(go);
        if (src.name.IndexOf("Boot", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                log.AppendLine("  boot node " + t.name);
        }
        UnityEngine.Object.DestroyImmediate(go);
        AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(BundleName, "");
        log.AppendLine(string.Format("{0} bounds min {1} max {2}", src.name, V(b.min), V(b.max)));
    }

    static void EnsureTag(string tag)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    static Shader UnlitShader()
    {
        var s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("HSPortal/UnlitColor");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
        return s;
    }

    static Material Unlit(string name, Color c)
    {
        var path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = UnlitShader();
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 ? 0.55f : 0.08f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.28f);
        EditorUtility.SetDirty(m);
        AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(BundleName, "");
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
