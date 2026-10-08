using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot tuning (Oct 8):
// 1) Player: max stamina 4.5 s (was 3). Flashlight: drain 3 %/s (was 4) -> ~33 s per full charge.
// 2) Pumpkin Walk + Run clips: root position XZ NOT baked into the pose, so the clip plays fully in place
//    (fixes the side-step-then-snap-back drift). The script moves the monster; root motion stays off.
// Only touches those fields and the two FBX import settings. Does not touch the monster prefab.
// Runs once automatically (when TuneReport.cs is missing), or Tools/Jam/Tune Player And Walk.
public static class JamTunePlayerAndWalk
{
    const string ReportPath = "Assets/Scripts/Editor/TuneReport.cs";
    const float NewMaxStamina = 4.5f;
    const float NewDrain = 3f;
    static readonly string[] InPlaceClips =
    {
        "Assets/pumpkin monster/Prefab/Mutant Walking.fbx",
        "Assets/pumpkin monster/Prefab/Mutant Run.fbx",
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

    [MenuItem("Tools/Jam/Tune Player And Walk")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Tune, " + DateTime.Now);
        try { TunePlayer(sb); } catch (Exception e) { sb.AppendLine("PLAYER EXCEPTION: " + e); }
        try { FixClips(sb); } catch (Exception e) { sb.AppendLine("CLIP EXCEPTION: " + e); }
        sb.AppendLine("done");
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
        Debug.Log("[Jam] Tuning finished, see " + ReportPath);
    }

    static void TunePlayer(StringBuilder sb)
    {
        if (Application.isPlaying) { sb.AppendLine("player: skipped (in Play mode)"); return; }
        bool changed = false;

        var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (pc == null) sb.AppendLine("player: no PlayerController in the open scene");
        else changed |= SetFloat(pc, "maxStamina", NewMaxStamina, sb);

        var fl = UnityEngine.Object.FindFirstObjectByType<Flashlight>();
        if (fl == null) sb.AppendLine("flashlight: no Flashlight in the open scene");
        else changed |= SetFloat(fl, "drainPerSecond", NewDrain, sb);

        if (changed)
        {
            var scene = pc != null ? pc.gameObject.scene : fl.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            sb.AppendLine($"scene '{scene.name}' saved={saved}");
        }
    }

    static bool SetFloat(UnityEngine.Object target, string field, float value, StringBuilder sb)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { sb.AppendLine($"{target.GetType().Name}.{field}: not found"); return false; }
        float old = p.floatValue;
        p.floatValue = value;
        so.ApplyModifiedProperties();
        sb.AppendLine($"{target.GetType().Name}.{field}: {old} -> {value}");
        return !Mathf.Approximately(old, value);
    }

    static void FixClips(StringBuilder sb)
    {
        foreach (var path in InPlaceClips)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { sb.AppendLine($"{path}: no importer"); continue; }
            var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                sb.AppendLine($"{Path.GetFileName(path)} '{c.name}': bakeXZ {c.lockRootPositionXZ} -> False (rot baked={c.lockRootRotation}, Y baked={c.lockRootHeightY}, loop={c.loopTime})");
                c.lockRootPositionXZ = false;     // extract XZ as root motion (discarded: applyRootMotion is off) = plays in place
                c.keepOriginalPositionXZ = false;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
            sb.AppendLine($"  reimported: {(clip ? $"'{clip.name}' {clip.length:F2}s avgSpeed={clip.averageSpeed}" : "clip missing")}");
        }
    }
}
