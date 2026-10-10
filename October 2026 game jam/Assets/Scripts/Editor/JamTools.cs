#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Small editor helpers for getting the jam build out.
//   Tools > Jam > Write Build Report    -> Assets/.jam_report.txt: build scenes, what actually goes into the build
//                                          (grouped by folder, with sizes) and the current Player Settings.
//   Tools > Jam > Apply Player Settings -> name, version, icon, window, splash, build scene list for the release.
//   After every build: CREDITS.txt and the font licenses (Assets/Licenses) are copied next to the game.
// Each can also be started by creating the matching empty file in Assets (".jam_report"), so outside tools can run it.
[InitializeOnLoad]
public static class JamTools
{
    const string ApplyTrigger = "Assets/.jam_apply";
    const string LicenseFolder = "Assets/Licenses";
    const string ReportTrigger = "Assets/.jam_report";
    const string ReportFile = "Assets/.jam_report.txt";

    static JamTools()
    {
        EditorApplication.update += () =>
        {
            if (File.Exists(ReportTrigger)) { File.Delete(ReportTrigger); WriteReport(); }
            if (File.Exists(ApplyTrigger)) { File.Delete(ApplyTrigger); ApplyPlayerSettings(); WriteReport(); }
        };
    }

    [MenuItem("Tools/Jam/Write Build Report")]
    public static void WriteReport()
    {
        var sb = new StringBuilder();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        sb.AppendLine("== Build scenes");
        foreach (var s in EditorBuildSettings.scenes) sb.AppendLine($"  {(s.enabled ? "x" : " ")} {s.path}");

        // Everything the build will contain: the scenes' dependencies plus every Resources folder.
        var roots = new List<string>(scenes);
        foreach (var p in AssetDatabase.GetAllAssetPaths())
            if (p.StartsWith("Assets/") && p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)) roots.Add(p);
        var deps = AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true)
                                .Where(p => p.StartsWith("Assets/") && !p.EndsWith(".cs")).Distinct().OrderBy(p => p).ToList();

        sb.AppendLine();
        sb.AppendLine("== Assets in the build, by top folder (files, source MB)");
        foreach (var g in deps.GroupBy(p => p.Split('/').Length > 2 ? p.Split('/')[1] : "(root)").OrderBy(g => g.Key))
        {
            long bytes = g.Sum(p => File.Exists(p) ? new FileInfo(p).Length : 0);
            sb.AppendLine($"  {g.Key,-40} {g.Count(),5}  {bytes / 1048576f,8:0.0}");
        }
        sb.AppendLine();
        sb.AppendLine("== Asset list");
        foreach (var p in deps) sb.AppendLine("  " + p);

        sb.AppendLine();
        sb.AppendLine("== Player Settings");
        sb.AppendLine($"  target            {EditorUserBuildSettings.activeBuildTarget}");
        sb.AppendLine($"  companyName       {PlayerSettings.companyName}");
        sb.AppendLine($"  productName       {PlayerSettings.productName}");
        sb.AppendLine($"  bundleVersion     {PlayerSettings.bundleVersion}");
        sb.AppendLine($"  colorSpace        {PlayerSettings.colorSpace}");
        sb.AppendLine($"  fullScreenMode    {PlayerSettings.fullScreenMode}");
        sb.AppendLine($"  defaultScreen     {PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight}");
        sb.AppendLine($"  resizableWindow   {PlayerSettings.resizableWindow}");
        sb.AppendLine($"  runInBackground   {PlayerSettings.runInBackground}");
        sb.AppendLine($"  visibleInBg       {PlayerSettings.visibleInBackground}");
        sb.AppendLine($"  usePlayerLog      {PlayerSettings.usePlayerLog}");
        sb.AppendLine($"  resolutionDialog  (removed in Unity 6)");
        sb.AppendLine($"  splash            show={PlayerSettings.SplashScreen.show} logo={PlayerSettings.SplashScreen.showUnityLogo} style={PlayerSettings.SplashScreen.unityLogoStyle} bg={PlayerSettings.SplashScreen.backgroundColor}");
        sb.AppendLine($"  icons(standalone) {string.Join(", ", PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.Standalone, IconKind.Any).Select(i => i != null ? AssetDatabase.GetAssetPath(i) : "-"))}");
        sb.AppendLine($"  defaultIcon       {string.Join(", ", PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, IconKind.Any).Select(i => i != null ? AssetDatabase.GetAssetPath(i) : "-"))}");
        sb.AppendLine($"  cursor            {(PlayerSettings.defaultCursor != null ? AssetDatabase.GetAssetPath(PlayerSettings.defaultCursor) : "-")}");
        var nbt = UnityEditor.Build.NamedBuildTarget.Standalone;
        sb.AppendLine($"  scriptingBackend  {PlayerSettings.GetScriptingBackend(nbt)}");
        sb.AppendLine($"  apiCompat         {PlayerSettings.GetApiCompatibilityLevel(nbt)}");
        sb.AppendLine($"  il2cppConfig      {PlayerSettings.GetIl2CppCompilerConfiguration(nbt)}");
        sb.AppendLine($"  stripping         {PlayerSettings.GetManagedStrippingLevel(nbt)}");
        sb.AppendLine($"  defines           {PlayerSettings.GetScriptingDefineSymbols(nbt)}");
        sb.AppendLine($"  activeInput       {GetActiveInputHandling()}");
        sb.AppendLine($"  gfxAPIs(win)      {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64))} auto={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64)}");
        sb.AppendLine($"  vsync(quality)    {QualitySettings.vSyncCount}  level={QualitySettings.names[QualitySettings.GetQualityLevel()]}  levels={string.Join("/", QualitySettings.names)}");
        sb.AppendLine($"  development       {EditorUserBuildSettings.development}");
        sb.AppendLine($"  standaloneSubtarget {EditorUserBuildSettings.standaloneBuildSubtarget}");
        sb.AppendLine($"  installedModules  win={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)} webgl={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)} mac={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX)}");

        File.WriteAllText(ReportFile, sb.ToString());
        Debug.Log("[Jam] report written to " + ReportFile);
    }

    [MenuItem("Tools/Jam/Apply Player Settings")]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = "Creepers in the Corn";
        PlayerSettings.companyName = CreditsPanel.Company;
        PlayerSettings.bundleVersion = "1.0";

        // Window: borderless fullscreen at the desktop's resolution; 1920x1080 if someone switches to windowed.
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;      // alt-tab: the game stops instead of hunting you in the background
        PlayerSettings.visibleInBackground = true;

        // Straight to the menu: no Unity splash (allowed from Unity 6 on every plan), black behind anything that shows.
        PlayerSettings.SplashScreen.backgroundColor = Color.black;
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
        try { PlayerSettings.SplashScreen.show = false; } catch (System.Exception ex) { Debug.LogWarning("[Jam] splash: " + ex.Message); }

        // Icon (made by the How To Play capture: the pumpkin's face up close).
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(HowToPlayCapture.IconPath);
        if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        else Debug.LogWarning("[Jam] no icon at " + HowToPlayCapture.IconPath);

        // Only the scenes the game uses: the menu and the maze (the old SampleScene stays out of the build).
        EditorBuildSettings.scenes = EditorBuildSettings.scenes
            .Select(s => new EditorBuildSettingsScene(s.path, s.path.EndsWith("/MainMenu.unity") || s.path.EndsWith("/Maze.unity")))
            .ToArray();

        EditorUserBuildSettings.development = false;
        AssetDatabase.SaveAssets();
        Debug.Log("[Jam] player settings applied");
    }

    // After a build: CREDITS.txt and the font licenses go next to the game.
    class AfterBuild : UnityEditor.Build.IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            string outPath = report.summary.outputPath;
            string dir = Directory.Exists(outPath) ? outPath : Path.GetDirectoryName(outPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            File.WriteAllText(Path.Combine(dir, "CREDITS.txt"), CreditsPanel.AsText());
            if (Directory.Exists(LicenseFolder))
            {
                string lic = Path.Combine(dir, "Licenses");
                Directory.CreateDirectory(lic);
                foreach (var f in Directory.GetFiles(LicenseFolder, "*.txt"))
                    File.Copy(f, Path.Combine(lic, Path.GetFileName(f)), true);
            }
            Debug.Log("[Jam] wrote CREDITS.txt and Licenses next to the build");
        }
    }

    // The icon and the company logo import as plain, uncompressed images (the logo keeps its transparency).
    class IconImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.EndsWith("DollHeadLogo.png"))
            {
                var li = (TextureImporter)assetImporter;
                li.textureType = TextureImporterType.Default;
                li.alphaIsTransparency = true;
                li.mipmapEnabled = true;
                li.npotScale = TextureImporterNPOTScale.None;
                li.wrapMode = TextureWrapMode.Clamp;
                li.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (assetPath != HowToPlayCapture.IconPath) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 1024;
        }
    }

    static string GetActiveInputHandling()
    {
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault());
        var p = so.FindProperty("activeInputHandler");
        return p == null ? "?" : p.intValue switch { 0 => "Old", 1 => "New Input System", 2 => "Both", _ => p.intValue.ToString() };
    }
}
#endif
