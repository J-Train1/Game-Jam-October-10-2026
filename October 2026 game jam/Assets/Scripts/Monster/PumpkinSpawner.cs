using System.Collections.Generic;
using UnityEngine;

// Spawns pumpkin monsters in the maze: startCount at the beginning, then one more each time
// SpawnOne() is called (the key pickups will call it). Spawns far from the player by walking
// distance and never somewhere the player can currently see.
public class PumpkinSpawner : MonoBehaviour
{
    public static PumpkinSpawner Instance { get; private set; }

    public GameObject monsterPrefab;
    [Min(0)] public int startCount = 1;
    [Tooltip("Safety cap on live pumpkins.")]
    [Min(1)] public int maxMonsters = 6;
    [Tooltip("New pumpkins spawn at least this many tiles away from the player on foot (2 tiles = 1 maze cell).")]
    [Min(0)] public int minTilesFromPlayer = 14;
    [Tooltip("Tiles closer than this (straight line, meters) count as visible if nothing blocks the view.")]
    public float visibleCheckRange = 30f;

    int spawned;

    void Awake() => Instance = this;

    void Start()
    {
        PumpkinMonster.ResetRound();
        for (int i = 0; i < startCount; i++) SpawnOne();
    }

    public PumpkinMonster SpawnOne()
    {
        var gen = MazeGenerator.Instance;
        if (gen == null || gen.Grid == null || monsterPrefab == null)
        {
            Debug.LogWarning("[Pumpkin] Can't spawn: maze or monster prefab missing.");
            return null;
        }
        if (PumpkinMonster.All.Count >= maxMonsters) return null;

        var g = gen.Grid;
        var player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        var eye = Camera.main != null ? Camera.main.transform.position : (player != null ? player.position + Vector3.up * 1.6f : Vector3.zero);
        var from = player != null ? g.WorldToTile(player.position) : g.startTile;
        if (!g.IsOpen(from)) from = g.startTile;

        var dist = g.DistanceField(from);
        var options = new List<Vector2Int>();
        Vector2Int farthest = from;
        int farD = -1;
        for (int cx = 0; cx < g.cellsX; cx++)
        for (int cy = 0; cy < g.cellsY; cy++)
        {
            var t = MazeGrid.CellToTile(cx, cy);
            int d = dist[t.x, t.y];
            if (d < 0) continue;
            if (d > farD) { farD = d; farthest = t; }
            if (d < minTilesFromPlayer) continue;
            if (VisibleFrom(eye, g.TileToWorld(t) + Vector3.up * 1.2f)) continue;
            options.Add(t);
        }
        var tile = options.Count > 0 ? options[Random.Range(0, options.Count)] : farthest;

        var rot = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
        var go = Instantiate(monsterPrefab, g.TileToWorld(tile), rot, transform);
        go.name = "Pumpkin_" + (++spawned);
        Debug.Log($"[Pumpkin] spawned {go.name} at tile {tile}, {dist[tile.x, tile.y]} tiles from the player ({options.Count} valid spots)");
        return go.GetComponent<PumpkinMonster>();
    }

    bool VisibleFrom(Vector3 eye, Vector3 point)
    {
        if ((point - eye).sqrMagnitude > visibleCheckRange * visibleCheckRange) return false;
        return PumpkinMonster.ClearLine(eye, point);
    }
}
