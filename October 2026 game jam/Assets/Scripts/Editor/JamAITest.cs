#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Test helper (Play Mode, Maze): drop two pumpkins near the player and make them hear a "key", to watch the
// pack-hunting. Start it with Tools > Jam > Test Pack Hunt, or by creating Assets/.jam_aitest.
[InitializeOnLoad]
static class JamAITest
{
    const string Trigger = "Assets/.jam_aitest";
    static JamAITest()
    {
        EditorApplication.update += () =>
        {
            if (Application.isPlaying && File.Exists(Trigger)) { File.Delete(Trigger); Run(); }
        };
    }

    [MenuItem("Tools/Jam/Test Pack Hunt")]
    static void Run()
    {
        if (!Application.isPlaying) return;
        var g = MazeGenerator.Instance != null ? MazeGenerator.Instance.Grid : null;
        var pc = PlayerController.Instance;
        if (g == null || pc == null) return;
        while (PumpkinMonster.All.Count(m => m != null && m.isActiveAndEnabled) < 2 && PumpkinSpawner.Instance != null)
            if (PumpkinSpawner.Instance.SpawnOne() == null) break;
        var mons = PumpkinMonster.All.Where(m => m != null && m.isActiveAndEnabled).Take(2).ToArray();
        var pt = g.WorldToTile(pc.transform.position);
        var dP = g.DistanceField(pt);
        int placed = 0;
        int[] want = { 5, 9 };
        for (int x = 0; x < g.width && placed < mons.Length; x++)
        for (int y = 0; y < g.height && placed < mons.Length; y++)
        {
            if (placed == 0 ? dP[x, y] != 5 : (dP[x, y] < 6 || dP[x, y] > 12)) continue;
            var w = g.TileToWorld(new Vector2Int(x, y));
            if (Vector3.Distance(w, pc.transform.position) > 13.5f) continue;
            if (placed == 1 && Vector3.Distance(w, mons[0].transform.position) < 4f) continue;
            mons[placed].ResetTo(w);
            placed++;
        }
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var T = typeof(PumpkinMonster);
        foreach (var m in mons)
            if (T.GetField("grid", F).GetValue(m) == null) T.GetMethod("Start", F).Invoke(m, null);  // freshly spawned: start it now
        foreach (var m in mons) T.GetMethod("HearNoiseAtPlayer", F).Invoke(m, null);
        Debug.Log($"[AITest] placed {placed} pumpkins, states: {string.Join(", ", mons.Select(m => m.name + "=" + m.CurrentState))}");

        // Step the hunt logic a few times by hand (the game loop may not run while the editor is in the background).
        for (int i = 0; i < 3; i++)
            foreach (var m in mons) T.GetMethod("Hunt", F).Invoke(m, new object[] { 0.02f, true });
        foreach (var m in mons)
        {
            var path = (System.Collections.Generic.List<Vector2Int>)T.GetField("path", F).GetValue(m);
            Debug.Log($"[AITest] {m.name} tile {g.WorldToTile(m.transform.position)} d={dP[g.WorldToTile(m.transform.position).x, g.WorldToTile(m.transform.position).y]} flanking={m.IsFlanking} path={(path == null ? "none" : string.Join(" ", path))}");
        }
        Debug.Log($"[AITest] player tile {pt}");
    }
}
#endif
