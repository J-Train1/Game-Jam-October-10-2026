using UnityEditor;

// Runs the Flooded Grounds -> URP material conversion exactly once, after the next script reload.
public static class JamConvertOnce
{
    const string Key = "JamConverter_Ran_v1";

    [InitializeOnLoadMethod]
    static void Run()
    {
        if (EditorPrefs.GetBool(Key, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorPrefs.SetBool(Key, true);
            JamMaterialConverter.ConvertAll();
        };
    }
}
