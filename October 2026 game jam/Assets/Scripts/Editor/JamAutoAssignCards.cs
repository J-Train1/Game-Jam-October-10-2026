using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Manual helper: assigns the baked corn card materials to OuterCornBackdrop.
// (No automatic hooks; it used to run on every project change.)
public static class JamAutoAssignCards
{
    static readonly string[] MatPaths = { "Assets/Corn/Materials/Corn_Card_Dry.mat", "Assets/Corn/Materials/Corn_Card_Rot.mat" };

    [MenuItem("Tools/Jam/Assign Corn Card Materials")]
    static void Assign()
    {
        var backdrop = Object.FindFirstObjectByType<OuterCornBackdrop>();
        if (backdrop == null) return;
        var mats = MatPaths.Select(p => AssetDatabase.LoadAssetAtPath<Material>(p)).Where(m => m != null).ToArray();
        if (mats.Length == 0) return;
        backdrop.cardMaterials = mats.Length > 1 ? new[] { mats[0], mats[0], mats[1] } : mats;
        EditorUtility.SetDirty(backdrop);
        EditorSceneManager.MarkSceneDirty(backdrop.gameObject.scene);
    }
}
