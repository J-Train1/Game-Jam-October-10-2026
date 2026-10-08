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
// 1) Mixamo clips (Walk, Run, Jump Attack) -> Humanoid. Looping clips play fully in place
//    (root XZ extracted, not baked, so there is no drift-and-snap-back on each loop).
// 2) Creepy freeze pose clip: legs from a held Walk frame (feet planted), upper body from the table below.
// 3) Animator controller: Walk / Run / Pose / JumpAttack, IK pass on (for the look-at).
// 4) Prefab Assets/Monster/PumpkinMonster.prefab ("pumpkin 1" model + PumpkinMonster script).
//    The model gets the humanoid avatar whose bone names actually match its skeleton.
//    An existing prefab is updated in place, so PumpkinMonster tuning values (speeds etc.) are kept.
// Runs once automatically after compile if the report is missing; or Tools/Jam/Setup Pumpkin Monster.
// To tweak the pose: edit PoseOverrides, then run the menu item again.
public static class JamPumpkinSetup
{
    const string ReportPath = "Assets/Scripts/Editor/PumpkinSetupReport.cs";
    const string Src = "Assets/pumpkin monster/Prefab/";
    const string ModelPrefab = Src + "pumpkin 1.prefab";
    const string WalkFbx = Src + "Mutant Walking.fbx";
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

        var walk = ImportClip(WalkFbx, "Walk", true, sb);
        var run = ImportClip(Src + "Mutant Run.fbx", "Run", true, sb);
        var jump = ImportClip(Src + "Mutant Jump Attack.fbx", "JumpAttack", false, sb);
        if (walk == null || run == null) { sb.AppendLine("STOP: walk/run clip missing"); return; }

        var pose = BuildPose(walk, sb);
        var ctrl = BuildController(walk, run, pose, jump, sb);
        var avatar = PickAvatar(sb);
        BuildPrefab(ctrl, avatar, sb);
        AssetDatabase.SaveAssets();
        sb.AppendLine("done");
    }

    // ---------------- Clips ----------------

    static AnimationClip ImportClip(string path, string clipName, bool loop, StringBuilder sb)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) { sb.AppendLine($"{clipName}: no importer at {path}"); return null; }
        if (imp.animationType != ModelImporterAnimationType.Human || imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel || !imp.importAnimation)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport(); // build the avatar first so the clip list is valid
        }

        var src = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        if (src.Length == 0) { sb.AppendLine($"{clipName}: no clips in {path}"); return null; }
        var c = src[0];
        c.name = clipName;
        c.loopTime = loop;
        c.loopPose = loop;
        // Rotation and height stay in the pose. Looping clips: root XZ is extracted as root motion, which is
        // discarded (applyRootMotion is off), so they play fully in place while the script moves the monster.
        // Baking XZ into the pose made the body drift sideways and snap back every loop.
        c.lockRootRotation = true;  c.keepOriginalOrientation = false;
        c.lockRootHeightY = true;   c.keepOriginalPositionY = false; c.heightFromFeet = true;
        c.lockRootPositionXZ = !loop; c.keepOriginalPositionXZ = false;
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
        foreach (var b in AnimationUtility.GetCurveBindings(walk))
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
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        var layers = ctrl.layers;
        layers[0].iKPass = true; // needed for look-at
        ctrl.layers = layers;

        // Rebuild the states in place (keeps the asset and every reference to it).
        var sm = ctrl.layers[0].stateMachine;
        foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
        var w = sm.AddState("Walk"); w.motion = walk;
        var r = sm.AddState("Run"); r.motion = run;
        var p = sm.AddState("Pose"); p.motion = pose;
        if (jump != null) { var j = sm.AddState("JumpAttack"); j.motion = jump; }
        sm.defaultState = w;
        EditorUtility.SetDirty(ctrl);
        sb.AppendLine($"controller: {ControllerPath} states={string.Join(", ", sm.states.Select(s => s.state.name))} ikPass={ctrl.layers[0].iKPass}");
        return ctrl;
    }

    // ---------------- Avatar ----------------

    // The model's Animator needs a humanoid avatar whose bone names exist in the model's own skeleton,
    // or nothing animates. Check every candidate and take the best match.
    static Avatar PickAvatar(StringBuilder sb)
    {
        // Read the model's real bone names and mesh source.
        var names = new HashSet<string>();
        string meshFbx = null;
        Avatar current = null;
        var contents = PrefabUtility.LoadPrefabContents(ModelPrefab);
        try
        {
            foreach (var t in contents.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
            var smr = contents.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null) meshFbx = AssetDatabase.GetAssetPath(smr.sharedMesh);
            var anims = contents.GetComponentsInChildren<Animator>(true);
            var legacy = contents.GetComponentsInChildren<Animation>(true);
            var a = anims.FirstOrDefault();
            current = a != null ? a.avatar : null;
            sb.AppendLine($"model: {names.Count} transforms, {anims.Length} Animator(s), {legacy.Length} legacy Animation(s), animator enabled={(a != null && a.enabled)}, mesh from {meshFbx ?? "?"}");
            var hips = names.Where(n => n.IndexOf("hips", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("spine", StringComparison.OrdinalIgnoreCase) >= 0).Take(4);
            sb.AppendLine("  sample bones: " + string.Join(", ", hips));
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        var candidates = new List<(string label, Avatar av)>();
        if (current != null) candidates.Add(("current (" + current.name + ")", current));
        if (!string.IsNullOrEmpty(meshFbx) && meshFbx.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            var av = EnsureHumanAvatar(meshFbx, sb);
            if (av != null) candidates.Add(("mesh source " + meshFbx, av));
        }
        var walkAv = AssetDatabase.LoadAllAssetsAtPath(WalkFbx).OfType<Avatar>().FirstOrDefault();
        if (walkAv != null) candidates.Add(("walk clip rig", walkAv));

        Avatar best = null;
        int bestMissing = int.MaxValue;
        foreach (var (label, av) in candidates)
        {
            if (!av.isValid || !av.isHuman) { sb.AppendLine($"  avatar {label}: valid={av.isValid} human={av.isHuman} (skipped)"); continue; }
            var bones = av.humanDescription.human;
            var missing = bones.Where(b => !names.Contains(b.boneName)).Select(b => b.boneName).ToList();
            sb.AppendLine($"  avatar {label}: {bones.Length} bones mapped, {missing.Count} missing from the model" +
                          (missing.Count > 0 ? " (e.g. " + string.Join(", ", missing.Take(3)) + ")" : ""));
            if (missing.Count < bestMissing) { bestMissing = missing.Count; best = av; }
        }
        sb.AppendLine(best != null ? $"avatar chosen: {best.name} ({bestMissing} missing bones)" : "avatar chosen: NONE FOUND");
        return best;
    }

    static Avatar EnsureHumanAvatar(string fbxPath, StringBuilder sb)
    {
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) return null;
        if (imp.animationType != ModelImporterAnimationType.Human)
        {
            sb.AppendLine($"  {fbxPath} was {imp.animationType}; switching to Humanoid so its own rig gets an avatar");
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
    }

    // ---------------- Prefab ----------------

    static void BuildPrefab(AnimatorController ctrl, Avatar avatar, StringBuilder sb)
    {
        GameObject root;
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        PreviewSceneHolder holder = null;
        if (existing)
        {
            root = PrefabUtility.LoadPrefabContents(PrefabPath);
        }
        else
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
            if (model == null) { sb.AppendLine("prefab: model missing at " + ModelPrefab); return; }
            holder = new PreviewSceneHolder();
            root = new GameObject("PumpkinMonster");
            EditorSceneManager.MoveGameObjectToScene(root, holder.scene);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, holder.scene);
            inst.transform.SetParent(root.transform, false);
            inst.name = "Model";
        }

        try
        {
            var a = root.GetComponentInChildren<Animator>(true);
            if (a == null) { sb.AppendLine("prefab: no Animator found"); return; }
            a.enabled = true;
            a.runtimeAnimatorController = ctrl;
            if (avatar != null) a.avatar = avatar;
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate; // never skip animating while visible-ish
            if (root.GetComponent<PumpkinMonster>() == null) root.AddComponent<PumpkinMonster>();
            if (a.GetComponent<PumpkinIK>() == null) a.gameObject.AddComponent<PumpkinIK>();
            var pm = root.GetComponent<PumpkinMonster>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            sb.AppendLine($"prefab: {PrefabPath} {(existing ? "updated" : "created")} (animator on '{a.name}', avatar={(a.avatar ? a.avatar.name : "none")}, huntSpeed={pm.huntSpeed}, wanderSpeed={pm.wanderSpeed})");
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            holder?.Dispose();
        }
    }

    sealed class PreviewSceneHolder : IDisposable
    {
        public readonly UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
