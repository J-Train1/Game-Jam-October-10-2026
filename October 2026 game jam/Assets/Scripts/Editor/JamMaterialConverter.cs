using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Converts the Flooded Grounds (built-in pipeline) materials to URP/Lit.
// Reads saved texture/color/float values straight from the serialized material,
// so it works even when the original shader is missing or unsupported in URP.
public static class JamMaterialConverter
{
    const string Root = "Assets/Flooded_Grounds";
    const string TreeFolder = "Assets/Flooded_Grounds/Content/Trees";

    static readonly string[] AlbedoNames = { "_MainTex", "_BaseMap", "_Albedo", "_AlbedoTex", "_Diffuse", "_DiffuseTex", "_MainTexture", "_Tex" };
    static readonly string[] NormalNames = { "_BumpMap", "_NormalMap", "_Normal", "_NormalTex", "_BumpTex" };
    static readonly string[] MetalNames = { "_MetallicGlossMap", "_MetallicMap", "_SpecGlossMap" };
    static readonly string[] OcclusionNames = { "_OcclusionMap", "_AOMap", "_AO" };
    static readonly string[] EmissionNames = { "_EmissionMap", "_Emission" };
    static readonly string[] FoliageWords = { "leaf", "leaves", "foliage", "grass", "bush", "fern", "reed", "plant", "cutout", "branch", "ivy", "weed" };
    static readonly string[] TransparentWords = { "transparent", "glass", "water", "fade", "window" };
    // Shader families that already work in URP and should be left alone.
    static readonly string[] KeepPrefixes = { "Universal Render Pipeline", "Skybox/", "Particles/", "Legacy Shaders/Particles", "Mobile/Particles", "Unlit/", "Sprites/", "UI/", "Shader Graphs/" };

    static IEnumerable<(Material mat, string path)> FloodedMaterials()
    {
        return AssetDatabase.FindAssets("t:Material", new[] { Root })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => p.EndsWith(".mat") && !p.StartsWith(TreeFolder))
            .Select(p => (AssetDatabase.LoadAssetAtPath<Material>(p), p))
            .Where(t => t.Item1 != null);
    }

    static bool ShouldConvert(Material m)
    {
        if (m.shader == null) return true;
        string n = m.shader.name;
        if (n == "Hidden/InternalErrorShader" || !m.shader.isSupported) return true;
        return !KeepPrefixes.Any(p => n.StartsWith(p));
    }

    [MenuItem("Tools/Jam/Report Flooded Grounds Shaders")]
    public static void Report()
    {
        var groups = FloodedMaterials()
            .GroupBy(t => (t.mat.shader == null ? "(none)" : t.mat.shader.name) + (ShouldConvert(t.mat) ? "  -> convert" : "  -> keep"))
            .OrderByDescending(g => g.Count());
        var sb = new StringBuilder("[JamConverter] Shader report:\n");
        foreach (var g in groups)
            sb.AppendLine($"{g.Count(),4}  {g.Key}   e.g. {string.Join(", ", g.Take(3).Select(t => t.mat.name))}");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Jam/Convert Flooded Grounds Materials To URP")]
    public static void ConvertAll()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { Debug.LogError("[JamConverter] URP/Lit shader not found."); return; }

        int converted = 0, kept = 0;
        var log = new StringBuilder();
        foreach (var (mat, path) in FloodedMaterials())
        {
            if (!ShouldConvert(mat)) { kept++; log.AppendLine($"kept         {mat.name} ({mat.shader.name})"); continue; }
            string kind = Convert(mat, lit);
            EditorUtility.SetDirty(mat);
            log.AppendLine($"{kind,-12} {mat.name}");
            converted++;
        }
        AssetDatabase.SaveAssets();
        string summary = $"[JamConverter] Converted {converted} materials, kept {kept} that already work in URP.\n{log}";
        Debug.Log(summary);
        System.IO.File.WriteAllText("Assets/Scripts/Editor/JamConverterLog.txt", summary);
        AssetDatabase.Refresh();
    }

    static string Convert(Material mat, Shader lit)
    {
        // Snapshot saved properties before switching shader.
        var so = new SerializedObject(mat);
        var texs = ReadTextures(so);
        var cols = ReadColors(so);
        var floats = ReadFloats(so);
        string oldShader = mat.shader != null ? mat.shader.name.ToLowerInvariant() : "";
        int oldQueue = mat.renderQueue;
        string lname = mat.name.ToLowerInvariant();
        bool hadCutout = oldShader.Contains("cutout") || oldShader.Contains("leaves") || oldShader.Contains("leaf")
                         || oldQueue == 2450 || mat.IsKeywordEnabled("_ALPHATEST_ON") || mat.GetTag("RenderType", false) == "TransparentCutout";
        bool hadTransparent = oldShader.Contains("transparent") || oldShader.Contains("fade") || (oldQueue >= 3000 && oldQueue < 4000)
                              || mat.GetTag("RenderType", false) == "Transparent";
        bool isWater = lname.Contains("water") || oldShader.Contains("water");
        bool isFoliage = !isWater && (hadCutout || FoliageWords.Any(w => lname.Contains(w) || oldShader.Contains(w)));
        bool isTransparent = !isFoliage && (hadTransparent || isWater || TransparentWords.Any(w => lname.Contains(w)));

        mat.shader = lit;
        mat.shaderKeywords = new string[0];

        // Base map + color
        var albedo = First(texs, AlbedoNames);
        if (albedo.tex != null)
        {
            mat.SetTexture("_BaseMap", albedo.tex);
            mat.SetTextureScale("_BaseMap", albedo.scale);
            mat.SetTextureOffset("_BaseMap", albedo.offset);
        }
        Color baseCol = cols.TryGetValue("_Color", out var c) ? c : cols.TryGetValue("_BaseColor", out var c2) ? c2 : Color.white;
        if (isWater) baseCol = new Color(0.07f, 0.09f, 0.08f, 0.8f); // murky dark water
        mat.SetColor("_BaseColor", baseCol);

        // Normal
        var normal = First(texs, NormalNames);
        if (normal.tex != null)
        {
            mat.SetTexture("_BumpMap", normal.tex);
            mat.SetTextureScale("_BumpMap", normal.scale);
            mat.SetFloat("_BumpScale", floats.TryGetValue("_BumpScale", out var bs) ? bs : 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // Metallic / smoothness
        var metal = First(texs, MetalNames);
        if (metal.tex != null)
        {
            mat.SetTexture("_MetallicGlossMap", metal.tex);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        mat.SetFloat("_Metallic", floats.TryGetValue("_Metallic", out var met) ? met : 0f);
        float smooth = floats.TryGetValue("_Glossiness", out var gl) ? gl : floats.TryGetValue("_Smoothness", out var sm) ? sm : 0.2f;
        if (isWater) smooth = 0.95f;
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_GlossMapScale", floats.TryGetValue("_GlossMapScale", out var gms) ? gms : 1f);

        // Occlusion
        var occ = First(texs, OcclusionNames);
        if (occ.tex != null)
        {
            mat.SetTexture("_OcclusionMap", occ.tex);
            mat.SetFloat("_OcclusionStrength", floats.TryGetValue("_OcclusionStrength", out var os) ? os : 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }

        // Emission (only if there's actually emissive color)
        var emis = First(texs, EmissionNames);
        if (cols.TryGetValue("_EmissionColor", out var ec) && ec.maxColorComponent > 0.01f)
        {
            mat.SetColor("_EmissionColor", ec);
            if (emis.tex != null) mat.SetTexture("_EmissionMap", emis.tex);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        if (isFoliage)
        {
            mat.SetFloat("_Surface", 0);
            mat.SetFloat("_AlphaClip", 1);
            mat.SetFloat("_Cutoff", floats.TryGetValue("_Cutoff", out var cut) ? Mathf.Clamp(cut, 0.2f, 0.7f) : 0.5f);
            mat.SetFloat("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", (float)CullMode.Off); // two-sided leaves
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            return "foliage";
        }
        if (isTransparent)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_AlphaClip", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return isWater ? "water" : "transparent";
        }
        mat.SetFloat("_Surface", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetFloat("_ZWrite", 1);
        mat.SetFloat("_Cull", (float)CullMode.Back);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = -1;
        return "opaque";
    }

    struct TexEntry { public Texture tex; public Vector2 scale; public Vector2 offset; }

    static TexEntry First(Dictionary<string, TexEntry> texs, string[] names)
    {
        foreach (var n in names)
            if (texs.TryGetValue(n, out var t) && t.tex != null) return t;
        return new TexEntry { scale = Vector2.one };
    }

    static Dictionary<string, TexEntry> ReadTextures(SerializedObject so)
    {
        var result = new Dictionary<string, TexEntry>();
        var arr = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (arr == null) return result;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            string name = e.FindPropertyRelative("first").stringValue;
            var second = e.FindPropertyRelative("second");
            result[name] = new TexEntry
            {
                tex = second.FindPropertyRelative("m_Texture").objectReferenceValue as Texture,
                scale = second.FindPropertyRelative("m_Scale").vector2Value,
                offset = second.FindPropertyRelative("m_Offset").vector2Value
            };
        }
        return result;
    }

    static Dictionary<string, Color> ReadColors(SerializedObject so)
    {
        var result = new Dictionary<string, Color>();
        var arr = so.FindProperty("m_SavedProperties.m_Colors");
        if (arr == null) return result;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            result[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
        }
        return result;
    }

    static Dictionary<string, float> ReadFloats(SerializedObject so)
    {
        var result = new Dictionary<string, float>();
        var arr = so.FindProperty("m_SavedProperties.m_Floats");
        if (arr == null) return result;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            result[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
        }
        return result;
    }
}
