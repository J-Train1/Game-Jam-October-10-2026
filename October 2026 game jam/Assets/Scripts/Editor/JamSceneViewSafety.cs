using UnityEditor;
using UnityEngine;

// Hides the Corn layer in the Scene view while in Play mode, so the editor
// doesn't render the entire maze from above every frame (that's what can hang the GPU).
// Maze gizmos (start, exit, dead ends, open tiles) still show. Restored when Play stops.
[InitializeOnLoad]
public static class JamSceneViewSafety
{
    static JamSceneViewSafety()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        int layer = LayerMask.NameToLayer("Corn");
        if (layer < 0) return;
        int mask = 1 << layer;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Tools.visibleLayers &= ~mask;
            SceneView.RepaintAll();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Tools.visibleLayers |= mask;
            SceneView.RepaintAll();
        }
    }
}
