using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot: configures URP for drawing thousands of corn stalks.
// - Forward+ rendering path (required by the GPU Resident Drawer)
// - GPU Resident Drawer (instanced drawing) + GPU occlusion culling: corn hidden behind corn is skipped
// - Keep BatchRendererGroup shader variants so this works in builds too
// - Shorter shadow distance (fog hides far shadows anyway)
public static class JamPerformanceSetup
{
    const string ReportPath = "Assets/Scripts/Editor/PerformanceReport.cs";

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (File.Exists(ReportPath)) return;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Run; return; }
        if (File.Exists(ReportPath)) return;
        var sb = new StringBuilder();
        try { Apply(sb); } catch (Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Jam/Apply Performance Settings")]
    public static void ApplyFromMenu() => Apply(new StringBuilder());

    static void Apply(StringBuilder sb)
    {
        // 1) BatchRendererGroup variants must be kept or the resident drawer fails in builds.
        var egs = Type.GetType("UnityEditor.Rendering.EditorGraphicsSettings, UnityEditor");
        var brgProp = egs?.GetProperty("batchRendererGroupShaderStrippingMode", BindingFlags.Public | BindingFlags.Static);
        if (brgProp != null)
        {
            brgProp.SetValue(null, Enum.Parse(brgProp.PropertyType, "KeepAll"));
            sb.AppendLine("BRG variants: KeepAll");
        }
        else sb.AppendLine("BRG stripping setting not found (set Project Settings > Graphics > BatchRendererGroup Variants = Keep All manually)");

        // 2) Every URP asset in the project (one per quality level).
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (urp == null) continue;
            sb.AppendLine("URP asset: " + path);

            urp.shadowDistance = 30f;
            sb.AppendLine("  shadowDistance=30");

            bool grd = SetByPropertyOrField(urp, "gpuResidentDrawerMode", "m_GPUResidentDrawerMode", "InstancedDrawing", 1, sb);
            bool occ = SetByPropertyOrField(urp, "gpuResidentDrawerEnableOcclusionCullingInCameras", "m_GPUResidentDrawerEnableOcclusionCullingInCameras", true, 1, sb);
            sb.AppendLine($"  residentDrawer={grd} occlusion={occ}");

            // Renderers -> Forward+
            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            if (list != null)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData rd)
                    {
                        rd.renderingMode = RenderingMode.ForwardPlus;
                        EditorUtility.SetDirty(rd);
                        sb.AppendLine("  renderer " + rd.name + " -> Forward+");
                    }
                }
            }
            EditorUtility.SetDirty(urp);
        }

        AssetDatabase.SaveAssets();
        sb.AppendLine("done");
    }

    // Tries a public property first (enum by name or bool), then the serialized field.
    static bool SetByPropertyOrField(UnityEngine.Object target, string propName, string fieldName, object value, int fallbackInt, StringBuilder sb)
    {
        var prop = target.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            try
            {
                object v = value;
                if (prop.PropertyType.IsEnum && value is string s) v = Enum.Parse(prop.PropertyType, s);
                prop.SetValue(target, v);
                return true;
            }
            catch (Exception e) { sb.AppendLine($"  property {propName} failed: {e.Message}"); }
        }
        var so = new SerializedObject(target);
        var sp = so.FindProperty(fieldName);
        if (sp == null) { sb.AppendLine($"  {propName}: not found"); return false; }
        if (sp.propertyType == SerializedPropertyType.Boolean) sp.boolValue = value is bool b ? b : fallbackInt != 0;
        else if (sp.propertyType == SerializedPropertyType.Enum) sp.enumValueIndex = fallbackInt;
        else sp.intValue = fallbackInt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
