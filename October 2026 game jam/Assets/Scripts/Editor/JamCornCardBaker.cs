using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot: renders a strip of the real corn stalks into a transparent texture ("corn card"),
// used for cheap rows of background corn outside the maze walls.
// Renders against magenta and keys it out, so it doesn't depend on alpha support in the camera.
public static class JamCornCardBaker
{
    const string ReportPath = "Assets/Scripts/Editor/CornCardReport.cs";
    const string TexPath = "Assets/Corn/Textures/CornCard.png";
    const float StripWidth = 5f;
    const float StripHeight = 3.8f;
    const float StripDepth = 1.4f;
    const int TexWidth = 1024;

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
        try { Bake(sb); } catch (Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Jam/Bake Corn Card Texture")]
    public static void BakeFromMenu() => Bake(new StringBuilder());

    static void Bake(StringBuilder sb)
    {
        int cornLayer = LayerMask.NameToLayer("Corn");
        if (cornLayer < 0) { sb.AppendLine("Corn layer missing"); return; }

        var prefabs = new System.Collections.Generic.List<GameObject>();
        for (int k = 0; k < 16; k++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Corn/Prefabs/CornStalk_{k}.prefab");
            if (p != null) prefabs.Add(p);
        }
        if (prefabs.Count == 0) { sb.AppendLine("no stalk prefabs"); return; }

        Vector3 basePos = new Vector3(0f, -1000f, 0f);
        var root = new GameObject("CardBakeTemp") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.position = basePos;

        // --- Save scene lighting state, set up neutral studio lighting ---
        bool oldFog = RenderSettings.fog;
        var oldAmbientMode = RenderSettings.ambientMode;
        var oldProbe = RenderSettings.ambientProbe;
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var lightStates = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++) { lightStates[i] = lights[i].enabled; lights[i].enabled = false; }

        RenderTexture rt = null;
        try
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.55f, 0.55f, 0.55f));
            RenderSettings.ambientProbe = sh;

            var keyLight = new GameObject("BakeLight") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
            keyLight.transform.SetParent(root.transform, false);
            keyLight.type = LightType.Directional;
            keyLight.color = Color.white;
            keyLight.intensity = 1.0f;
            keyLight.shadows = LightShadows.None;
            keyLight.transform.rotation = Quaternion.Euler(30f, 20f, 0f); // from the front, slightly left/above

            // --- Plant a dense strip of real stalks ---
            var rng = new System.Random(777);
            int planted = 0;
            for (float x = -StripWidth * 0.5f - 0.3f; x <= StripWidth * 0.5f + 0.3f; x += 0.28f)
            for (float z = 0f; z <= StripDepth; z += 0.35f)
            {
                var pos = basePos + new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.2f, 0f, z + (float)(rng.NextDouble() - 0.5) * 0.2f);
                var rot = Quaternion.Euler((float)(rng.NextDouble() * 2 - 1) * 5f, (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 2 - 1) * 5f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(prefabs.Count)]);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(root.transform, true);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.15f, (float)rng.NextDouble());
                go.layer = cornLayer;
                if (go.TryGetComponent(out MeshRenderer mr)) { mr.shadowCastingMode = ShadowCastingMode.Off; }
                planted++;
            }
            sb.AppendLine("planted " + planted + " stalks for bake");

            // --- Orthographic camera looking at the strip from the front ---
            var cam = new GameObject("BakeCam") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            cam.transform.SetParent(root.transform, false);
            cam.orthographic = true;
            cam.orthographicSize = StripHeight * 0.5f;
            cam.transform.position = basePos + new Vector3(0f, StripHeight * 0.5f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 30f;
            cam.cullingMask = 1 << cornLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 0f, 1f, 1f);
            cam.allowHDR = false; cam.allowMSAA = false;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = false;

            int texH = Mathf.RoundToInt(TexWidth * StripHeight / StripWidth);
            cam.aspect = (float)TexWidth / texH;
            rt = new RenderTexture(TexWidth, texH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;

            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) { RenderPipeline.SubmitRenderRequest(cam, req); sb.AppendLine("rendered via render request"); }
            else { cam.Render(); sb.AppendLine("rendered via Camera.Render"); }

            // --- Read back and key out the magenta ---
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(TexWidth, texH, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, TexWidth, texH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var px = tex.GetPixels32();
            int opaque = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bool magenta = c.r > 150 && c.b > 150 && c.g < 90;
                if (magenta) px[i] = new Color32(0, 0, 0, 0);
                else { px[i].a = 255; opaque++; }
            }
            Dilate(px, TexWidth, texH, 6); // bleed colors into transparent area so mipmaps don't fringe
            tex.SetPixels32(px);
            tex.Apply();
            sb.AppendLine($"texture {TexWidth}x{texH}, opaque pixels {opaque * 100f / px.Length:F1}%");

            if (!AssetDatabase.IsValidFolder("Assets/Corn/Textures")) AssetDatabase.CreateFolder("Assets/Corn", "Textures");
            File.WriteAllBytes(TexPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(TexPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.SaveAndReimport();
            }

            // --- Card materials ---
            var cardTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            MakeCardMat("Corn_Card_Dry", cardTex, Color.white, lit);
            MakeCardMat("Corn_Card_Rot", cardTex, new Color(0.72f, 0.68f, 0.62f), lit);
            AssetDatabase.SaveAssets();
            sb.AppendLine("card materials saved");
        }
        finally
        {
            RenderSettings.fog = oldFog;
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientProbe = oldProbe;
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = lightStates[i];
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            UnityEngine.Object.DestroyImmediate(root);
        }
        sb.AppendLine("done");
    }

    static void MakeCardMat(string name, Texture2D tex, Color tint, Shader lit)
    {
        string path = $"Assets/Corn/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", 0.03f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
    }

    // Spread opaque colors outward into transparent pixels (keeps alpha 0).
    static void Dilate(Color32[] px, int w, int h, int passes)
    {
        var filled = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) filled[i] = px[i].a > 0;
        for (int pass = 0; pass < passes; pass++)
        {
            var next = (bool[])filled.Clone();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (filled[i]) continue;
                int r = 0, g = 0, b = 0, n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (!filled[j]) continue;
                    r += px[j].r; g += px[j].g; b += px[j].b; n++;
                }
                if (n > 0) { px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0); next[i] = true; }
            }
            filled = next;
        }
    }
}
