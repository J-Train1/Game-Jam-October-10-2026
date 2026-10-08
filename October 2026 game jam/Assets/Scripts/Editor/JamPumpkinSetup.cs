using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot setup for the pumpkin monster:
// 1) Mixamo clips (Walk, Run, Jump Attack) -> Humanoid, looping, root motion baked into the pose.
// 2) Creepy freeze pose clip: legs from a held Walk frame (feet planted), upper body from the table below.
// 3) Animator controller: Walk / Run / Pose / JumpAttack, IK pass on (for the look-at).
// 4) Prefab Assets/Monster/PumpkinMonster.prefab (your "pumpkin 1" model + PumpkinMonster script).
// Runs once automatically after compile if the report is missing; or Tools/Jam/Setup Pumpkin Monster.
// To tweak the pose: edit PoseOverrides, then run the menu item again.
public static class JamPumpkinSetup
{
    const string ReportPath = "Assets/Scripts/Editor/PumpkinSetupReport.cs";
    const string Src = "Assets/pumpkin monster/Prefab/";
    const string ModelPrefab = Src + "pumpkin 1.prefab";
    const string OutDir = "Assets/Monster";
    const string AnimDir = OutDir + "/Animations";
    const string PosePath = AnimDir + "/Pumpkin_CreepyPose.anim";
    const string ControllerPath = AnimDir + "/Pumpkin.controller";
    const string PrefabPath = OutDir + "/PumpkinMonster.prefab";
    const float PoseSampleTime = 0f; // which Walk frame the frozen legs hold

    // Humanoid muscle values (-1..1) for the frozen upper body. Hunched spine, shrugged shoulders,
    // arms raised forward (one higher), wrists bent, fingers curled into claws and splayed, head cocked.
    static readonly (string muscle, float value)[] PoseOverrides =
    {
        ("Spine Front-Back", 0.35f), ("Spine Left-Right", 0.1f), ("Spine Twist Left-Right", 0f),
        ("Chest Front-Back", 0.3f), ("Chest Left-Right", 0f), ("Chest Twist Left-Right", -0.1f),
        ("UpperChest Front-Back", 0.2f), ("UpperChest Left-Right", 0f), ("UpperChest Twist Left-Right", 0f),
        ("Neck Nod Down-Up", 0.25f), ("Neck Tilt Left-Right", 0.15f), ("Neck Turn Left-Right", 0f),
        ("Head Nod Down-Up", -0.25f), ("Head Tilt Left-Right", 0.45f), ("Head Turn Left-Right", 0f),

        ("Left Shoulder Down-Up", 0.6f), ("Left Shoulder Front-Back", 0.3f),
        ("Left Arm Down-Up", 0.25f), ("Left Arm Front-Back", 0.6f), ("Left Arm Twist In-Out", 0f),
        ("Left Forearm Stretch", 0.2f), ("Left Forearm Twist In-Out", 0f),
        ("Left Hand Down-Up", -0.4f), ("Left Hand In-Out", 0.2f),

        ("Right Shoulder Down-Up", 0.6f), ("Right Shoulder Front-Back", 0.3f),
        ("Right Arm Down-Up", 0.45f), ("Right Arm Front-Back", 0.4f), ("Right Arm Twist In-Out", 0f),
        ("Right Forearm Stretch", -0.2f), ("Right Forearm Twist In-Out", 0f),
        ("Right Hand Down-Up", -0.4f), ("Right Hand In-Out", 0.2f),
    };
    const float FingerCurl = -0.6f;  // all finger "Stretched" muscles (negative = curled)
    const float ThumbCurl = -0.2f;
    const float FingerSpread = 0.6f; // splayed claws

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

    [MenuItem("Tools/Jam/Setup Pumpkin Monster")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Pumpkin setup, " + DateTime.Now);
        try { Setup(sb); } catch (Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
        Debug.Log("[Jam] Pumpkin setup finished, see " + ReportPath);
    }

    static void Setup(StringBuilder sb)
    {
        EnsureFolder("Assets", "Monster");
        EnsureFolder(OutDir, "Animations");

        var walk = ImportClip(Src + "Mutant Walking.fbx", "Walk", true, sb);
        var run = ImportClip(Src + "Mutant Run.fbx", "Run", true, sb);
        var jump = ImportClip(Src + "Mutant Jump Attack.fbx", "JumpAttack", false, sb);
        if (walk == null || run == null) { sb.AppendLine("STOP: walk/run clip missing"); return; }

        var pose = BuildPose(walk, sb);
        var ctrl = BuildController(walk, run, pose, jump, sb);
        BuildPrefab(ctrl, sb);
        AssetDatabase.SaveAssets();
        sb.AppendLine("done");
    }

    static AnimationClip ImportClip(string path, string clipName, bool loop, StringBuilder sb)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) { sb.AppendLine($"{clipName}: no importer at {path}"); return null; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.SaveAndReimport(); // build the avatar first so the clip list is valid

        var src = imp.defaultClipAnimations;
        if (src.Length == 0) { sb.AppendLine($"{clipName}: no clips in {path}"); return null; }
        var c = src[0];
        c.name = clipName;
        c.loopTime = loop;
        c.loopPose = loop;
        // Bake root motion into the pose: the script moves the monster, the clip plays in place.
        c.lockRootRotation = true;  c.keepOriginalOrientation = false;
        c.lockRootHeightY = true;   c.keepOriginalPositionY = false; c.heightFromFeet = true;
        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = false;
        imp.clipAnimations = new[] { c };
        imp.SaveAndReimport();

        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var clip = assets.OfType<AnimationClip>().FirstOrDefault(x => x.name == clipName);
        var avatar = assets.OfType<Avatar>().FirstOrDefault();
        sb.AppendLine($"{clipName}: avatar={(avatar ? $"valid={avatar.isValid} human={avatar.isHuman}" : "NONE")} clip={(clip ? $"{clip.length:F2}s humanMotion={clip.humanMotion} loop={clip.isLooping}" : "MISSING")}");
        return clip;
    }

    static AnimationClip BuildPose(AnimationClip walk, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PosePath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, PosePath); }
        clip.ClearCurves();

        // Start from a held Walk frame (root + legs + everything), then override the upper body.
        var values = new Dictionary<string, float>();
        var bindings = AnimationUtility.GetCurveBindings(walk);
        foreach (var b in bindings)
        {
            if (b.type != typeof(Animator) || !string.IsNullOrEmpty(b.path)) continue;
            var curve = AnimationUtility.GetEditorCurve(walk, b);
            if (curve != null) values[b.propertyName] = curve.Evaluate(PoseSampleTime);
        }
        sb.AppendLine($"pose: copied {values.Count} curves from Walk at t={PoseSampleTime}");

        var known = new HashSet<string>(HumanTrait.MuscleName.Select(ToCurveName));
        int overrides = 0;
        foreach (var (muscle, v) in PoseOverrides)
        {
            if (!known.Contains(muscle)) { sb.AppendLine("  unknown muscle: " + muscle); continue; }
            values[muscle] = v; overrides++;
        }
        foreach (var side in new[] { "Left", "Right" })
        foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
        {
            for (int j = 1; j <= 3; j++) { values[$"{side}Hand.{f}.{j} Stretched"] = f == "Thumb" ? ThumbCurl : FingerCurl; overrides++; }
            if (f != "Thumb") { values[$"{side}Hand.{f}.Spread"] = FingerSpread; overrides++; }
        }
        sb.AppendLine($"pose: {overrides} upper-body overrides");

        var outBindings = new List<EditorCurveBinding>();
        var outCurves = new List<AnimationCurve>();
        foreach (var kv in values)
        {
            outBindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key));
            outCurves.Add(AnimationCurve.Constant(0f, 1f, kv.Value));
        }
        AnimationUtility.SetEditorCurves(clip, outBindings.ToArray(), outCurves.ToArray());

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        sb.AppendLine($"pose: {outBindings.Count} curves written, humanMotion={clip.humanMotion}");
        return clip;
    }

    // HumanTrait finger names ("Left Index 1 Stretched") differ from clip curve names ("LeftHand.Index.1 Stretched").
    static string ToCurveName(string muscle)
    {
        foreach (var side in new[] { "Left", "Right" })
        foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
        {
            string pre = side + " " + f + " ";
            if (muscle.StartsWith(pre)) return $"{side}Hand.{f}.{muscle.Substring(pre.Length)}";
        }
        return muscle;
    }

    static AnimatorController BuildController(AnimationClip walk, AnimationClip run, AnimationClip pose, AnimationClip jump, StringBuilder sb)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var layers = ctrl.layers;
        layers[0].iKPass = true; // needed for look-at
        ctrl.layers = layers;

        var sm = ctrl.layers[0].stateMachine;
        var w = sm.AddState("Walk"); w.motion = walk;
        var r = sm.AddState("Run"); r.motion = run;
        var p = sm.AddState("Pose"); p.motion = pose;
        if (jump != null) { var j = sm.AddState("JumpAttack"); j.motion = jump; }
        sm.defaultState = w;
        EditorUtility.SetDirty(ctrl);
        sb.AppendLine($"controller: {ControllerPath} states={string.Join(", ", sm.states.Select(s => s.state.name))} ikPass={ctrl.layers[0].iKPass}");
        return ctrl;
    }

    static void BuildPrefab(AnimatorController ctrl, StringBuilder sb)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
        if (model == null) { sb.AppendLine("prefab: model missing at " + ModelPrefab); return; }

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("PumpkinMonster");
            EditorSceneManager.MoveGameObjectToScene(root, scene);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            inst.transform.SetParent(root.transform, false);
            inst.name = "Model";
            var a = inst.GetComponent<Animator>();
            if (a == null) a = inst.GetComponentInChildren<Animator>();
            a.runtimeAnimatorController = ctrl;
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            root.AddComponent<PumpkinMonster>();
            if (a.GetComponent<PumpkinIK>() == null) a.gameObject.AddComponent<PumpkinIK>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            sb.AppendLine($"prefab: {PrefabPath} (animator on '{a.name}', avatar={(a.avatar ? a.avatar.name : "none")})");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
