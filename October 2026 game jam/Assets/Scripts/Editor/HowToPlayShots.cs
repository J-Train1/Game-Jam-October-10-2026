#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > How To Play > Capture Screenshots (in Play Mode, Maze scene) -> HowToPlayCapture.CaptureAll().
// Also imports the photographs as plain UI images (no mipmaps, no power-of-two squashing).
[InitializeOnLoad]
public static class HowToPlayShots
{
    const string Trigger = "Assets/.howtoplay_capture";
    static HowToPlayShots()
    {
        EditorApplication.update += () =>
        {
            if (!Application.isPlaying || !File.Exists(Trigger)) return;
            File.Delete(Trigger);
            HowToPlayCapture.CaptureAll();
        };
    }

    [MenuItem("Tools/How To Play/Capture Screenshots")]
    static void Capture() => HowToPlayCapture.CaptureAll();

    class Importer : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(HowToPlayCapture.Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
#endif
