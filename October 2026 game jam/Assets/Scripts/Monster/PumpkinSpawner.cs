using System.Collections.Generic;
using UnityEngine;

// Spawns pumpkin monsters in the maze: startCount at the beginning, then one more each time
// SpawnOne() is called (the key pickups call it). Spawns far from the player by walking
// distance and never somewhere the player can currently see.
// After the player loses a heart, RelocateAll() moves every pumpkin far from the respawn point.
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

        var player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        var tile = PickFarTile(player != null ? player.position : gen.Grid.TileToWorld(gen.Grid.startTile), null, out int distTiles, out int valid);

        var rot = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
        var go = Instantiate(monsterPrefab, gen.Grid.TileToWorld(tile), rot, transform);
        go.name = "Pumpkin_" + (++spawned);
        Debug.Log($"[Pumpkin] spawned {go.name} at tile {tile}, {distTiles} tiles from the player ({valid} valid spots)");
        return go.GetComponent<PumpkinMonster>();
    }

    /// <summary>Move every pumpkin far away from 'from' (and out of sight of it), wandering again.</summary>
    public void RelocateAll(Vector3 from)
    {
        var gen = MazeGenerator.Instance;
        if (gen == null || gen.Grid == null) return;
        var used = new HashSet<Vector2Int>();
        foreach (var m in PumpkinMonster.All.ToArray())
        {
            if (m == null) continue;
            var tile = PickFarTile(from, used, out _, out _);
            used.Add(tile);
            m.ResetTo(gen.Grid.TileToWorld(tile));
        }
        Debug.Log($"[Pumpkin] relocated {used.Count} pumpkins away from the respawn point");
    }

    /// <summary>A random cell tile far (on foot) from 'from' and not visible from eye height there.</summary>
    Vector2Int PickFarTile(Vector3 from, HashSet<Vector2Int> avoid, out int distTiles, out int validCount)
    {
        var g = MazeGenerator.Instance.Grid;
        Vector3 eye = from + Vector3.up * 1.65f;
        var fromTile = g.WorldToTile(from);
        if (!g.IsOpen(fromTile)) fromTile = g.startTile;

        var dist = g.DistanceField(fromTile);
        var options = new List<Vector2Int>();
        Vector2Int farthest = fromTile;
        int farD = -1;
        for (int cx = 0; cx < g.cellsX; cx++)
        for (int cy = 0; cy < g.cellsY; cy++)
        {
            var t = MazeGrid.CellToTile(cx, cy);
            int d = dist[t.x, t.y];
            if (d < 0) continue;
            if (avoid != null && avoid.Contains(t)) continue;
            if (d > farD) { farD = d; farthest = t; }
            if (d < minTilesFromPlayer) continue;
            if (VisibleFrom(eye, g.TileToWorld(t) + Vector3.up * 1.2f)) continue;
            options.Add(t);
        }
        var tile = options.Count > 0 ? options[Random.Range(0, options.Count)] : farthest;
        distTiles = dist[tile.x, tile.y];
        validCount = options.Count;
        return tile;
    }

    bool VisibleFrom(Vector3 eye, Vector3 point)
    {
        if ((point - eye).sqrMagnitude > visibleCheckRange * visibleCheckRange) return false;
        return PumpkinMonster.ClearLine(eye, point);
    }
}
