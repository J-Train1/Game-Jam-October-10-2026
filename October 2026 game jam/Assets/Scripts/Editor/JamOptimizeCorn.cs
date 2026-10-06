using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot optimization pass for the corn:
// 1. Creates a "Corn" layer (for distance culling + hiding in Scene view during play)
// 2. Merges each stalk's 7 color submeshes into ONE submesh; colors come from a tiny palette texture via UVs
//    -> 1 draw per stalk instead of 7, identical look
// 3. Tries Unity's built-in Mesh LOD generation (Unity 6.2+) for cheaper distant stalks
// 4. Rewires the stalk prefabs (single material, Corn layer)
// 5. Cheaper shadows (1 cascade, 15 m), resident drawer off for now
public static class JamOptimizeCorn
{
    const string Root = "Assets/Corn";
    const string ReportPath = "Assets/Scripts/Editor/OptimizeReport.cs";
    const int Slots = 7; // Leaves1, Leaves2, Stem, StemCap, Ear, Husk, Tassel

    static readonly Color[] PaletteDry =
    {
        new Color(0.42f, 0.38f, 0.22f), new Color(0.34f, 0.33f, 0.19f), new Color(0.40f, 0.36f, 0.21f),
        new Color(0.30f, 0.26f, 0.16f), new Color(0.55f, 0.45f, 0.24f), new Color(0.58f, 0.50f, 0.33f), new Color(0.52f, 0.44f, 0.27f)
    };
    static readonly Color[] PaletteRot =
    {
        new Color(0.30f, 0.26f, 0.15f), new Color(0.24f, 0.23f, 0.14f), new Color(0.29f, 0.25f, 0.15f),
        new Color(0.21f, 0.18f, 0.11f), new Color(0.38f, 0.30f, 0.17f), new Color(0.41f, 0.35f, 0.23f), new Color(0.37f, 0.31f, 0.19f)
    };

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (File.Exists(ReportPath)) return;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += Run; return; }
        if (File.Exists(ReportPath)) return;
        var sb = new StringBuilder();
        try { Apply(sb); } catch (Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Jam/Optimize Corn")]
    public static void ApplyFromMenu() => Apply(new StringBuilder());

    static void Apply(StringBuilder sb)
    {
        // ---- 1. Corn layer ----
        int cornLayer = EnsureLayer("Corn");
        sb.AppendLine("Corn layer index: " + cornLayer);

        // ---- 2. Palette textures + single materials ----
        var dryTex = MakePalette("Corn_Palette_Dry", PaletteDry);
        var rotTex = MakePalette("Corn_Palette_Rot", PaletteRot);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var dryMat = MakeMat("Corn_Opt_Dry", dryTex, lit);
        var rotMat = MakeMat("Corn_Opt_Rot", rotTex, lit);
        sb.AppendLine("palette materials ready");

        // Mesh LOD API probe (Unity 6.2+). Looked up by reflection so this compiles either way.
        MethodInfo genLods = FindMeshLodGenerator(sb);

        // ---- 3/4. Merge meshes + rewire prefabs ----
        for (int k = 0; ; k++)
        {
            string srcPath = $"{Root}/Meshes/CornStalk_{k}.asset";
            string prefabPath = $"{Root}/Prefabs/CornStalk_{k}.prefab";
            var src = AssetDatabase.LoadAssetAtPath<Mesh>(srcPath);
            if (src == null) break;

            var opt = MergeToPalette(src);
            opt.name = $"CornStalkOpt_{k}";
            string optPath = $"{Root}/Meshes/CornStalkOpt_{k}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(optPath);
            if (existing != null) { EditorUtility.CopySerialized(opt, existing); opt = existing; }
            else AssetDatabase.CreateAsset(opt, optPath);

            string lodInfo = TryGenerateLods(genLods, opt, sb);
            EditorUtility.SetDirty(opt);

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var mf = root.GetComponent<MeshFilter>();
            var mr = root.GetComponent<MeshRenderer>();
            bool isRot = mr.sharedMaterials.Any(m => m != null && m.name.Contains("Rot"));
            mf.sharedMesh = opt;
            mr.sharedMaterials = new[] { isRot ? rotMat : dryMat };
            mr.shadowCastingMode = ShadowCastingMode.On;
            if (cornLayer >= 0) root.layer = cornLayer;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            sb.AppendLine($"stalk {k}: tris={opt.triangles.Length / 3} submeshes {src.subMeshCount}->{opt.subMeshCount} palette={(isRot ? "rot" : "dry")} {lodInfo}");
        }

        // ---- 5. Cheaper shadows, resident drawer off ----
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (urp == null) continue;
            urp.shadowDistance = 15f;
            urp.shadowCascadeCount = 1;
            var p = urp.GetType().GetProperty("gpuResidentDrawerMode", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(urp, Enum.Parse(p.PropertyType, "Disabled"));
            EditorUtility.SetDirty(urp);
            sb.AppendLine($"URP {urp.name}: shadowDistance=15 cascades=1 residentDrawer=off");
        }

        AssetDatabase.SaveAssets();
        sb.AppendLine("done");
    }

    // Duplicates vertices per submesh and points each at its palette slot.
    static Mesh MergeToPalette(Mesh src)
    {
        var v = src.vertices; var n = src.normals;
        bool hasN = n != null && n.Length == v.Length;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var tris = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s);
            if (t.Length == 0) continue;
            var remap = new Dictionary<int, int>();
            var uv = new Vector2((Mathf.Min(s, Slots - 1) + 0.5f) / 8f, 0.5f);
            foreach (int oi in t)
            {
                if (!remap.TryGetValue(oi, out int ni))
                {
                    ni = nv.Count; remap[oi] = ni;
                    nv.Add(v[oi]); if (hasN) nn.Add(n[oi]); nuv.Add(uv);
                }
                tris.Add(ni);
            }
        }
        var m = new Mesh { indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(nv);
        if (hasN) m.SetNormals(nn);
        m.SetUVs(0, nuv);
        m.SetTriangles(tris, 0, true);
        if (!hasN) m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static MethodInfo FindMeshLodGenerator(StringBuilder sb)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType("UnityEditor.MeshLodUtility"); } catch { return null; } })
            .FirstOrDefault(t => t != null);
        if (type == null) { sb.AppendLine("Mesh LOD API: not available in this Unity version"); return null; }
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        sb.AppendLine("Mesh LOD API methods: " + string.Join("; ", methods.Select(m => m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")")));
        var gen = methods.FirstOrDefault(m => m.Name == "GenerateMeshLods" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(Mesh));
        if (gen == null) sb.AppendLine("Mesh LOD: GenerateMeshLods(Mesh, ...) not found");
        return gen;
    }

    static string TryGenerateLods(MethodInfo gen, Mesh mesh, StringBuilder sb)
    {
        if (gen == null) return "lods=n/a";
        try
        {
            var ps = gen.GetParameters();
            var args = new object[ps.Length];
            args[0] = mesh;
            for (int i = 1; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
            gen.Invoke(null, args);
            var lodCountProp = typeof(Mesh).GetProperty("lodCount");
            return "lods=" + (lodCountProp != null ? lodCountProp.GetValue(mesh) : "?");
        }
        catch (Exception e) { return "lods failed: " + (e.InnerException?.Message ?? e.Message); }
    }

    static Texture2D MakePalette(string name, Color[] colors)
    {
        string path = $"{Root}/Materials/{name}.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            tex = new Texture2D(8, 1, TextureFormat.RGBA32, false, false);
            AssetDatabase.CreateAsset(tex, path);
        }
        var px = new Color[8];
        for (int i = 0; i < 8; i++) px[i] = colors[Mathf.Min(i, colors.Length - 1)];
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply(false, false);
        EditorUtility.SetDirty(tex);
        return tex;
    }

    static Material MakeMat(string name, Texture2D palette, Shader lit)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", palette);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.08f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Cull", (float)CullMode.Off); // leaves need both sides
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        return -1;
    }
}
