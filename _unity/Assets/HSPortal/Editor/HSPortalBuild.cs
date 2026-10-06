using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class HSPortalModelImport : AssetPostprocessor
{
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
            var mats = EnsureMaterials();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            AnimationClip idleClip = null, fireClip = null, rejectClip = null;
            GameObject src = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;
                var loaded = AssetDatabase.LoadAllAssetsAtPath(path);
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
                break;
            }
            if (src == null) throw new Exception("No PortalGun model in " + Root + "/Models");

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
            ApplyMats(go, mats);
            var anims = go.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
                UnityEngine.Object.DestroyImmediate(anims[i]);
            var animator = go.AddComponent<Animator>();
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
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds();
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static void ApplyMats(GameObject go, Dictionary<string, Material> mats)
    {
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var shared = mr.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                var key = shared[i] != null ? CleanName(shared[i].name) : "";
                Material m;
                if (mats.TryGetValue(key, out m)) shared[i] = m;
                else Debug.LogWarning("[HSPortalBuild] No material for slot '" + key + "' on " + go.name);
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

    static Dictionary<string, Material> EnsureMaterials()
    {
        var d = new Dictionary<string, Material>();
        d["HSGun_Metal"] = Opaque("HSGun_Metal", new Color(0.28f, 0.27f, 0.24f), 0.55f, 0.28f);
        d["HSGun_MetalDark"] = Opaque("HSGun_MetalDark", new Color(0.09f, 0.09f, 0.09f), 0.7f, 0.22f);
        d["HSGun_MetalLight"] = Opaque("HSGun_MetalLight", new Color(0.48f, 0.47f, 0.43f), 0.65f, 0.4f);
        d["HSGun_Grip"] = Opaque("HSGun_Grip", new Color(0.07f, 0.06f, 0.055f), 0f, 0.12f);
        d["HSGun_Copper"] = Opaque("HSGun_Copper", new Color(0.62f, 0.32f, 0.12f), 0.85f, 0.45f);
        d["HSGun_Gauge"] = Opaque("HSGun_Gauge", new Color(0.05f, 0.05f, 0.055f), 0.3f, 0.7f);
        d["HSGun_Plate"] = Opaque("HSGun_Plate", new Color(0.38f, 0.34f, 0.2f), 0.2f, 0.25f);
        d["HSGun_Hazard"] = Opaque("HSGun_Hazard", new Color(0.82f, 0.66f, 0.08f), 0.15f, 0.3f);
        d["HSGun_GaugeFace"] = Opaque("HSGun_GaugeFace", new Color(0.85f, 0.75f, 0.52f), 0.15f, 0.65f);
        d["HSGun_Glass"] = Glass("HSGun_Glass", new Color(0.55f, 0.78f, 0.95f, 0.28f));
        d["HSGun_Blue"] = Emissive("HSGun_Blue", new Color(0.12f, 0.38f, 0.95f), new Color(0.25f, 0.7f, 1.8f));
        d["HSGun_Orange"] = Emissive("HSGun_Orange", new Color(0.95f, 0.32f, 0.05f), new Color(1.8f, 0.45f, 0.05f));
        d["HSGun_Glow"] = Emissive("HSGun_Glow", new Color(0.55f, 0.85f, 1f), new Color(0.8f, 1.4f, 2.2f));
        AssetDatabase.SaveAssets();
        return d;
    }

    static Material Load(string name)
    {
        var path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        return m;
    }

    static Material Opaque(string name, Color c, float metallic, float smooth)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 0f);
        m.SetOverrideTag("RenderType", "");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = -1;
        m.color = c;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color c, Color emit)
    {
        var m = Opaque(name, c, 0.2f, 0.55f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emit);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Glass(string name, Color c)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 3f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.color = c;
        m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Glossiness", 0.85f);
        EditorUtility.SetDirty(m);
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
