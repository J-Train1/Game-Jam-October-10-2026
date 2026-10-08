using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Inspects everything under Assets/pumpkin monster and writes a plain report to
// Assets/Scripts/Editor/PumpkinReport.cs (as a comment, so it can be read like a script).
// Read-only: changes no assets. Runs once automatically after compile (if the report is missing),
// or from Tools/Jam/Inspect Pumpkin Monster. Delete the report to run it again.
public static class JamPumpkinInspect
{
    const string Folder = "Assets/pumpkin monster";
    const string ReportPath = "Assets/Scripts/Editor/PumpkinReport.cs";

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

    [MenuItem("Tools/Jam/Inspect Pumpkin Monster")]
    public static void Run()
    {
        var sb = new StringBuilder();
        try { Inspect(sb); } catch (Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
        Debug.Log("[Jam] Pumpkin report written to " + ReportPath);
    }

    static void Inspect(StringBuilder sb)
    {
        sb.AppendLine("Pumpkin monster report, generated " + DateTime.Now);

        // ---- Models (FBX import settings + clips) ----
        sb.AppendLine("\n== MODELS ==");
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            sb.AppendLine($"\n{path}");
            if (imp == null) { sb.AppendLine("  (no ModelImporter)"); continue; }
            sb.AppendLine($"  animationType={imp.animationType} avatarSetup={imp.avatarSetup} sourceAvatar={(imp.sourceAvatar ? AssetDatabase.GetAssetPath(imp.sourceAvatar) : "none")}");
            sb.AppendLine($"  globalScale={imp.globalScale} useFileScale={imp.useFileScale} importAnimation={imp.importAnimation} materials={imp.materialImportMode}");
            var clipsCfg = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            foreach (var c in clipsCfg)
                sb.AppendLine($"  clipCfg '{c.name}' frames {c.firstFrame}-{c.lastFrame} loopTime={c.loopTime} lockRootRot={c.lockRootRotation} lockRootY={c.lockRootHeightY} lockRootXZ={c.lockRootPositionXZ}");
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    sb.AppendLine($"  clip '{clip.name}' length={clip.length:F2}s loop={clip.isLooping} humanMotion={clip.humanMotion} avgSpeed={clip.averageSpeed}");
                else if (obj is Avatar av)
                    sb.AppendLine($"  avatar '{av.name}' human={av.isHuman} valid={av.isValid}");
                else if (obj is Mesh m)
                    sb.AppendLine($"  mesh '{m.name}' verts={m.vertexCount} submeshes={m.subMeshCount} bounds={m.bounds.size}");
            }
        }

        // ---- Prefabs (instantiated in a preview scene for real bounds) ----
        sb.AppendLine("\n== PREFABS ==");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
                if (inst == null) { sb.AppendLine($"\n{path}  (could not instantiate)"); continue; }
                sb.AppendLine($"\n{path}  root scale={inst.transform.localScale}  transforms={inst.GetComponentsInChildren<Transform>(true).Length}");
                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                    sb.AppendLine($"  world bounds size={b.size} center={b.center}");
                }
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).ToArray();
                    int missing = t.GetComponents<Component>().Count(c => c == null);
                    if (comps.Length == 0 && missing == 0) continue;
                    sb.AppendLine($"  [{GetPath(t, inst.transform)}] " + string.Join(", ", comps.Select(c => c.GetType().Name)) + (missing > 0 ? $"  (+{missing} MISSING SCRIPT)" : ""));
                    foreach (var c in comps)
                    {
                        if (c is Animator a)
                            sb.AppendLine($"     Animator controller={(a.runtimeAnimatorController ? AssetDatabase.GetAssetPath(a.runtimeAnimatorController) : "none")} avatar={(a.avatar ? a.avatar.name + " human=" + a.avatar.isHuman : "none")} rootMotion={a.applyRootMotion}");
                        if (c is Renderer r)
                            sb.AppendLine("     materials: " + string.Join(", ", r.sharedMaterials.Select(m => m ? $"{m.name} ({m.shader.name})" : "null")));
                        if (c is Collider col)
                            sb.AppendLine($"     collider {col.GetType().Name} trigger={col.isTrigger}");
                    }
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        // ---- Animator controllers ----
        sb.AppendLine("\n== ANIMATOR CONTROLLERS ==");
        foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) continue;
            sb.AppendLine($"\n{path}");
            sb.AppendLine("  params: " + string.Join(", ", ac.parameters.Select(p => $"{p.name}:{p.type}")));
            foreach (var layer in ac.layers)
            {
                sb.AppendLine($"  layer '{layer.name}' default={(layer.stateMachine.defaultState ? layer.stateMachine.defaultState.name : "none")}");
                foreach (var s in layer.stateMachine.states)
                    sb.AppendLine($"    state '{s.state.name}' motion={(s.state.motion ? s.state.motion.name : "none")} transitions={s.state.transitions.Length}");
            }
        }

        // ---- Materials ----
        sb.AppendLine("\n== MATERIALS ==");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) sb.AppendLine($"{path}  shader={m.shader.name}");
        }

        // ---- Everything in the folder ----
        sb.AppendLine("\n== ALL FILES ==");
        foreach (var guid in AssetDatabase.FindAssets("", new[] { Folder }).Distinct())
            sb.AppendLine(AssetDatabase.GUIDToAssetPath(guid));
        sb.AppendLine("done");
    }

    static string GetPath(Transform t, Transform root) => t == root ? t.name : GetPath(t.parent, root) + "/" + t.name;
}
