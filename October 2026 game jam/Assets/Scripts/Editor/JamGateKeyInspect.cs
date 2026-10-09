using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Read-only: reports structure, sizes and materials of the key, gate, lock and lantern prefabs
// to Assets/Scripts/Editor/GateKeyReport.cs. Runs once automatically (when the report is missing)
// or from Tools/Jam/Inspect Gate And Key Assets. Changes nothing.
public static class JamGateKeyInspect
{
    const string ReportPath = "Assets/Scripts/Editor/GateKeyReport.cs";
    static readonly string[] Folders =
    {
        "Assets/Rust Key",
        "Assets/CemeteryPack/Prefabs/Gate",
    };
    static readonly string[] Extra =
    {
        "Assets/Erbeilo3d_StylizeFreeProps/Content/Stylize_FreeProps/Prefab/lock.prefab",
        "Assets/Erbeilo3d_StylizeFreeProps/Content/Stylize_FreeProps/Prefab/Iron_SarLock.prefab",
        "Assets/Erbeilo3d_StylizeFreeProps/Content/Stylize_FreeProps/Prefab/Lantern.prefab",
        "Assets/Erbeilo3d_StylizeFreeProps/Content/Stylize_FreeProps/Prefab/Wooden_Gate.prefab",
    };

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (File.Exists(ReportPath)) return;
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += AutoRun; return; }
        if (File.Exists(ReportPath)) return;
        Run();
    }

    [MenuItem("Tools/Jam/Inspect Gate And Key Assets")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Gate/key report, " + DateTime.Now);
        try
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", Folders).Select(AssetDatabase.GUIDToAssetPath).ToList();
            paths.AddRange(Extra.Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null));
            foreach (var p in paths.Distinct()) Inspect(p, sb);

            sb.AppendLine("\n== MATERIALS (Rust Key + CemeteryPack) ==");
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Rust Key", "Assets/CemeteryPack" }))
            {
                var mp = AssetDatabase.GUIDToAssetPath(g);
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m != null) sb.AppendLine($"{mp}  shader={m.shader.name}");
            }
        }
        catch (Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        sb.AppendLine("done");
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
        Debug.Log("[Jam] Gate/key report written to " + ReportPath);
    }

    static void Inspect(string path, StringBuilder sb)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            sb.AppendLine($"\n== {path} ==");
            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                sb.AppendLine($"  bounds size={F(b.size)} center={F(b.center)} (root at origin, scale {F(root.transform.localScale)})");
            }
            Dump(root.transform, 1, sb);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Dump(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 6) return;
        string ind = new string(' ', depth * 2);
        var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).ToArray();
        sb.AppendLine($"{ind}- {t.name}  pos={F(t.localPosition)} rot={F(t.localEulerAngles)} scale={F(t.localScale)}  [{string.Join(", ", comps.Select(c => c.GetType().Name))}]");
        foreach (var c in comps)
        {
            if (c is MeshFilter mf && mf.sharedMesh != null)
                sb.AppendLine($"{ind}    mesh '{mf.sharedMesh.name}' bounds center={F(mf.sharedMesh.bounds.center)} size={F(mf.sharedMesh.bounds.size)} verts={mf.sharedMesh.vertexCount}");
            if (c is Renderer r)
                sb.AppendLine($"{ind}    materials: " + string.Join(", ", r.sharedMaterials.Select(m => m ? $"{m.name} ({m.shader.name})" : "null")));
            if (c is BoxCollider bc) sb.AppendLine($"{ind}    box center={F(bc.center)} size={F(bc.size)} trigger={bc.isTrigger}");
            if (c is MeshCollider mc) sb.AppendLine($"{ind}    meshcollider convex={mc.convex}");
            if (c is Light l) sb.AppendLine($"{ind}    light {l.type} range={l.range} intensity={l.intensity}");
        }
        foreach (Transform ch in t) Dump(ch, depth + 1, sb);
    }

    static string F(Vector3 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
}
