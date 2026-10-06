using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Sets up the night-time horror atmosphere in the Maze scene.
// Moonlight, dark trilight ambient, fog, and a post-processing volume.
// Runs once per ReportPath version; re-run any time from Tools > Jam > Apply Night Atmosphere.
// v3: brighter. Playable without the flashlight, but still murky at distance.
// v4: heavier film grain.
public static class JamAtmosphere
{
    const string ProfilePath = "Assets/Settings/Jam_NightVolume.asset";
    const string ReportPath = "Assets/Scripts/Editor/AtmosphereReport4.cs";

    public static readonly Color FogColor = new Color(0.10f, 0.115f, 0.14f);
    const float FogDensity = 0.04f;
    const float MoonIntensity = 0.75f;
    const float PostExposure = 0.5f;
    const float VignetteIntensity = 0.32f;
    const float GrainIntensity = 0.65f;   // 0-1
    const float GrainResponse = 0.45f;    // lower = grain also visible in brighter areas

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
        try { Apply(sb); } catch (System.Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Jam/Apply Night Atmosphere")]
    public static void ApplyFromMenu() => Apply(new StringBuilder());

    static void Apply(StringBuilder sb)
    {
        var scene = EditorSceneManager.GetActiveScene();
        sb.AppendLine("scene: " + scene.name);

        // --- Moonlight: cold, low angle so the corn throws long shadows. ---
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null)
        {
            sun.name = "Moonlight";
            sun.color = new Color(0.65f, 0.74f, 0.95f);
            sun.intensity = MoonIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(28f, 215f, 0f);
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(sun.transform);
            sb.AppendLine("moonlight configured");
        }
        else sb.AppendLine("no directional light found");

        // --- Ambient + fog ---
        RenderSettings.skybox = null;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.20f, 0.23f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.13f, 0.14f, 0.17f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.065f, 0.06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = FogDensity;
        RenderSettings.reflectionIntensity = 0.25f;
        sb.AppendLine("ambient + fog configured");

        // --- Camera: fog-colored background, short far plane (fog hides it anyway). ---
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.farClipPlane = 70f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(data);
            sb.AppendLine("camera configured");
        }
        else sb.AppendLine("no main camera found");

        // --- Post-processing profile ---
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        profile.components.RemoveAll(c => c == null);

        var tone = Get<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        var ca = Get<ColorAdjustments>(profile);
        ca.postExposure.Override(PostExposure);
        ca.contrast.Override(14f);
        ca.saturation.Override(-30f);
        ca.colorFilter.Override(new Color(0.88f, 0.93f, 1f));

        var vig = Get<Vignette>(profile);
        vig.color.Override(Color.black);
        vig.intensity.Override(VignetteIntensity);
        vig.smoothness.Override(0.45f);

        var grain = Get<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium4);
        grain.intensity.Override(GrainIntensity);
        grain.response.Override(GrainResponse);

        var bloom = Get<Bloom>(profile);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.6f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        sb.AppendLine("volume profile saved with " + profile.components.Count + " effects");

        var volGo = GameObject.Find("Global Volume");
        if (volGo == null) volGo = new GameObject("Global Volume");
        if (!volGo.TryGetComponent(out Volume vol)) vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1;
        vol.sharedProfile = profile;
        EditorUtility.SetDirty(vol);
        sb.AppendLine("global volume ready");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        sb.AppendLine("scene saved");
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet(out T c))
        {
            c = p.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        }
        if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, p); // effects must live inside the profile asset to persist
        c.active = true;
        EditorUtility.SetDirty(c);
        return c;
    }
}
