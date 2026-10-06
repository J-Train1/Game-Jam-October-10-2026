using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// One-shot: builds a muddy dirt ground material from the Flooded Grounds textures
// and applies it to the Ground object. Re-run via Tools > Jam > Setup Dirt Ground.
//  - GR_Dirt1_AS : dirt color, smoothness in alpha (damp sheen)
//  - GR_Dirt1_N  : normal map (surface bumps)
//  - GR_Breakup1_A : large-scale grunge used as a detail map to break up visible tiling
public static class JamGroundSetup
{
    const string ReportPath = "Assets/Scripts/Editor/GroundReport.cs";
    const string MatPath = "Assets/Materials/Ground_Dirt.mat";
    const string Tex = "Assets/Flooded_Grounds/Content/Textures/";

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

    [MenuItem("Tools/Jam/Setup Dirt Ground")]
    public static void ApplyFromMenu() => Apply(new StringBuilder());

    static void Apply(StringBuilder sb)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "GR_Dirt1_AS.tif");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "GR_Dirt1_N.tif");
        var breakup = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "GR_Breakup1_A.tif");
        sb.AppendLine($"albedo={albedo != null} normal={normal != null} breakup={breakup != null}");

        // Make sure the normal map is imported as one.
        var ni = AssetImporter.GetAtPath(Tex + "GR_Dirt1_N.tif") as TextureImporter;
        if (ni != null && ni.textureType != TextureImporterType.NormalMap)
        {
            ni.textureType = TextureImporterType.NormalMap;
            ni.SaveAndReimport();
            sb.AppendLine("normal map import type fixed");
        }
        // Breakup is used as a detail albedo (multiplies around mid-grey); keep it linear-ish and mipmapped.
        var bi = AssetImporter.GetAtPath(Tex + "GR_Breakup1_A.tif") as TextureImporter;
        if (bi != null && bi.wrapMode != TextureWrapMode.Repeat) { bi.wrapMode = TextureWrapMode.Repeat; bi.SaveAndReimport(); }

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.shader = lit;
        mat.shaderKeywords = new string[0];

        if (albedo != null) mat.SetTexture("_BaseMap", albedo);
        mat.SetColor("_BaseColor", new Color(0.62f, 0.55f, 0.47f)); // darker, cooler mud
        // Tiling is set per-maze at runtime by MazeGenerator (real-world meters); this is the editor default.
        mat.SetTextureScale("_BaseMap", new Vector2(30f, 30f));

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1.2f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // Smoothness from albedo alpha = damp patches catch the moon/flashlight a little.
        mat.SetFloat("_SmoothnessTextureChannel", 1f);
        mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        mat.SetFloat("_Smoothness", 0.45f);
        mat.SetFloat("_Metallic", 0f);

        // Large-scale grunge to hide the repeating tile pattern.
        if (breakup != null)
        {
            mat.SetTexture("_DetailAlbedoMap", breakup);
            mat.SetFloat("_DetailAlbedoMapScale", 0.8f);
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(0.13f, 0.13f)); // relative to base tiling: repeats ~every 8 dirt tiles
            mat.EnableKeyword("_DETAIL_MULX2");
        }

        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Back);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = -1;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        sb.AppendLine("material saved");

        var ground = GameObject.Find("Ground");
        if (ground != null && ground.TryGetComponent(out MeshRenderer mr))
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            EditorUtility.SetDirty(mr);
            EditorSceneManager.MarkSceneDirty(ground.scene);
            EditorSceneManager.SaveScene(ground.scene);
            sb.AppendLine("applied to Ground, scene saved");
        }
        else sb.AppendLine("Ground object or renderer not found");
    }
}
