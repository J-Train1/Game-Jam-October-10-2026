using System.Linq;
using UnityEditor;

// Keeps the build scene list right: MainMenu first (the game opens on it), then the Maze.
// Runs whenever scripts reload; only touches the list if something is missing or out of order.
[InitializeOnLoad]
static class JamBuildScenes
{
    const string Menu = "Assets/Scenes/MainMenu.unity";
    const string Game = "Assets/Scenes/Maze.unity";

    static JamBuildScenes()
    {
        if (!System.IO.File.Exists(Menu) || !System.IO.File.Exists(Game)) return;
        var list = EditorBuildSettings.scenes.ToList();
        bool changed = false;

        int gi = list.FindIndex(s => s.path == Game);
        if (gi < 0) { list.Add(new EditorBuildSettingsScene(Game, true)); changed = true; }
        else if (!list[gi].enabled) { list[gi] = new EditorBuildSettingsScene(Game, true); changed = true; }

        int mi = list.FindIndex(s => s.path == Menu);
        if (mi != 0 || !list[0].enabled)
        {
            if (mi >= 0) list.RemoveAt(mi);
            list.Insert(0, new EditorBuildSettingsScene(Menu, true));
            changed = true;
        }
        if (changed)
        {
            EditorBuildSettings.scenes = list.ToArray();
            UnityEngine.Debug.Log("[Build] scene list: " + string.Join(", ", list.Select(s => s.path)));
        }
    }
}
