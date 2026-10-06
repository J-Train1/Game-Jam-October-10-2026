using System.Collections.Generic;
using UnityEngine;

// Scatters spare batteries through each new maze: away from the start, spread apart,
// and never in dead ends (those are reserved for keys).
[RequireComponent(typeof(MazeGenerator))]
public class BatterySpawner : MonoBehaviour
{
    public int count = 5;
    [Tooltip("Minimum walking distance (in tiles) from the start.")]
    public int minTilesFromStart = 8;
    [Tooltip("Minimum straight-line distance (m) between two batteries.")]
    public float minSpacing = 10f;
    public float restoreAmount = 40f;
    [Tooltip("Optional custom model. Must have a BatteryPickup component. Leave empty for the placeholder.")]
    public GameObject batteryPrefab;

    MazeGenerator gen;
    Transform root;

    void Start()
    {
        gen = GetComponent<MazeGenerator>();
        gen.OnMazeBuilt += Spawn;
        if (gen.Grid != null) Spawn(gen.Grid);
    }

    void OnDestroy()
    {
        if (gen != null) gen.OnMazeBuilt -= Spawn;
    }

    public void Spawn(MazeGrid g)
    {
        if (root != null) Destroy(root.gameObject);
        root = new GameObject("Batteries").transform;
        root.SetParent(transform, false);

        var rng = new System.Random(gen.LastSeed ^ 0xBA77);
        var dist = g.DistanceField(g.startTile);
        var deadEnds = new HashSet<Vector2Int>(g.deadEnds);

        // Candidates: open cell tiles (odd coords), far enough from start, not dead ends, not the exit.
        var candidates = new List<Vector2Int>();
        for (int x = 1; x < g.width; x += 2)
        for (int y = 1; y < g.height; y += 2)
        {
            var t = new Vector2Int(x, y);
            if (!g.IsOpen(t) || deadEnds.Contains(t) || t == g.exitInsideTile) continue;
            if (dist[x, y] < minTilesFromStart) continue;
            candidates.Add(t);
        }
        // Shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var placed = new List<Vector3>();
        foreach (var t in candidates)
        {
            if (placed.Count >= count) break;
            Vector3 p = g.TileToWorld(t);
            bool tooClose = false;
            foreach (var q in placed) if ((q - p).sqrMagnitude < minSpacing * minSpacing) { tooClose = true; break; }
            if (tooClose) continue;

            // Small random offset within the tile so they don't sit dead-center.
            p += new Vector3((float)(rng.NextDouble() - 0.5) * g.tileSize * 0.4f, 0.12f, (float)(rng.NextDouble() - 0.5) * g.tileSize * 0.4f);
            GameObject go = batteryPrefab != null
                ? Instantiate(batteryPrefab, p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), root)
                : BatteryPickup.CreatePlaceholder(root, p);
            if (go.TryGetComponent(out BatteryPickup pickup)) pickup.restoreAmount = restoreAmount;
            placed.Add(p);
        }
        Debug.Log($"[Batteries] placed {placed.Count} of {count}");
    }
}
