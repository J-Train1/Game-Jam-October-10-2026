using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The exit gate is split down the middle at runtime, which needs its mesh to be CPU-readable in builds.
// This turns on Read/Write for the model behind the gate prefab. Runs once automatically (when the report
// is missing) or from Tools/Jam/Make Gate Mesh Readable.
public static class JamGateReadable
{
    const string ReportPath = "Assets/Scripts/Editor/GateReadableReport.cs";
    const string GatePrefab = "Assets/CemeteryPack/Prefabs/Gate/MetObj_GatesArch.prefab";

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

    [MenuItem("Tools/Jam/Make Gate Mesh Readable")]
    public static void Run()
    {
        string msg;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefab);
        if (prefab == null) msg = "gate prefab not found: " + GatePrefab;
        else
        {
            var paths = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(m => m.sharedMesh != null)
                .Select(m => AssetDatabase.GetAssetPath(m.sharedMesh)).Distinct().ToList();
            msg = "";
            foreach (var p in paths)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { msg += $"{p}: not a model (mesh asset), readable={IsReadable(p)}\n"; continue; }
                if (imp.isReadable) { msg += $"{p}: already readable\n"; continue; }
                imp.isReadable = true;
                imp.SaveAndReimport();
                msg += $"{p}: Read/Write turned ON\n";
            }
        }
        File.WriteAllText(ReportPath, "/*\n" + msg.Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
        Debug.Log("[Jam] Gate mesh readable: " + msg);
    }

    static bool IsReadable(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        return m != null && m.isReadable;
    }
}
