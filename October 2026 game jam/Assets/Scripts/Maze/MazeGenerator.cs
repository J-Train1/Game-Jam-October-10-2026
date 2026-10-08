using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Generates a new corn maze every time the game starts.
// 1) Recursive backtracker: long winding corridors.
// 2) Braiding: most dead ends get one wall knocked out, joining them to the farthest-away
//    neighbor, which creates big loops you can circle around scarecrows with.
// 3) A few random extra openings for variety.
// Start on the south edge, exit gap on the north edge.
[DefaultExecutionOrder(-100)] // build the maze before anything else asks about it
public class MazeGenerator : MonoBehaviour
{
    public static MazeGenerator Instance { get; private set; }
    public MazeGrid Grid { get; private set; }
    public event System.Action<MazeGrid> OnMazeBuilt;

    [Header("Size")]
    [Min(3)] public int cellsX = 10;
    [Min(3)] public int cellsY = 10;
    public float tileSize = 2.5f;

    [Header("Layout")]
    [Tooltip("Fraction of dead ends to KEEP. The rest are braided into loops. 0 = no dead ends, 1 = keep all.")]
    [Range(0f, 1f)] public float deadEndKeep = 0.35f;
    [Tooltip("Never braid below this many dead ends (keys hide in dead ends).")]
    [Min(0)] public int minDeadEnds = 6;
    [Tooltip("Extra chance to knock out any interior wall, for variety.")]
    [Range(0f, 0.4f)] public float loopChance = 0.06f;

    [Header("Seed")]
    public bool randomSeedEachRun = true;
    public int seed = 1031;
    [SerializeField] int lastSeed;

    [Header("Corn")]
    public GameObject[] stalkPrefabs;
    public CornPlanter.Settings cornSettings = CornPlanter.Settings.Default;
    public float wallColliderHeight = 3f;

    [Header("Performance")]
    [Tooltip("Corn farther than this from the camera isn't drawn. Fog hides it anyway.")]
    public float cornDrawDistance = 32f;
    [Tooltip("Safety cap: stop planting past this many stalks.")]
    public int maxStalks = 9000;

    [Header("Scene")]
    public Transform player;
    public Transform ground;
    public float groundMargin = 30f;

    [Header("Debug")]
    public bool drawGizmos = true;

    Transform root;
    public int LastSeed => lastSeed;
    public int StalkCount { get; private set; }
    public int BraidedCount { get; private set; }

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    void Awake()
    {
        Instance = this;
        ApplyCameraCulling();
        Generate();
    }

    void ApplyCameraCulling()
    {
        int layer = LayerMask.NameToLayer("Corn");
        var cam = Camera.main;
        if (layer < 0 || cam == null) { Debug.LogWarning("[Maze] Corn layer or main camera missing; no corn distance culling."); return; }
        var distances = new float[32];
        distances[layer] = cornDrawDistance;
        cam.layerCullDistances = distances;
        cam.layerCullSpherical = true;
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        var sw = Stopwatch.StartNew();
        lastSeed = randomSeedEachRun ? System.Environment.TickCount : seed;
        var rng = new System.Random(lastSeed);

        Grid = BuildLayout(rng);
        BuildCorn(rng);
        PlacePlayer();
        SizeGround();

        sw.Stop();
        Debug.Log($"[Maze] seed {lastSeed}: {Grid.width}x{Grid.height} tiles, {StalkCount} stalks, {Grid.deadEnds.Count} dead ends ({BraidedCount} braided), built in {sw.ElapsedMilliseconds} ms");
        OnMazeBuilt?.Invoke(Grid);
    }

    // ---------------- Layout ----------------

    MazeGrid BuildLayout(System.Random rng)
    {
        var g = new MazeGrid(cellsX, cellsY, tileSize, transform.position);
        var visited = new bool[cellsX, cellsY];
        var stack = new Stack<Vector2Int>();

        // 1) Recursive backtracker from a random cell.
        var startCell = new Vector2Int(rng.Next(cellsX), rng.Next(cellsY));
        visited[startCell.x, startCell.y] = true;
        Open(g, MazeGrid.CellToTile(startCell.x, startCell.y));
        stack.Push(startCell);
        var options = new List<Vector2Int>(4);
        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            options.Clear();
            foreach (var d in Dirs)
            {
                var n = cur + d;
                if (InCells(n) && !visited[n.x, n.y]) options.Add(d);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            var dir = options[rng.Next(options.Count)];
            var next = cur + dir;
            Open(g, MazeGrid.CellToTile(cur.x, cur.y) + dir);   // knock down the wall between
            Open(g, MazeGrid.CellToTile(next.x, next.y));
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // Start: middle of the south edge.
        var sCell = new Vector2Int(cellsX / 2, 0);
        g.startTile = MazeGrid.CellToTile(sCell.x, sCell.y);

        // 2) Braid dead ends into big loops.
        Braid(g, rng);

        // 3) A few random extra openings. Only walls between two cells (never corner pillars).
        for (int x = 1; x < g.width - 1; x++)
        for (int y = 1; y < g.height - 1; y++)
        {
            bool betweenCellsH = x % 2 == 0 && y % 2 == 1;
            bool betweenCellsV = x % 2 == 1 && y % 2 == 0;
            if ((betweenCellsH || betweenCellsV) && !g.open[x, y] && rng.NextDouble() < loopChance)
                g.open[x, y] = true;
        }

        // Exit: random cell on the north edge, opening through the outer wall.
        int exitCellX = rng.Next(cellsX);
        g.exitInsideTile = MazeGrid.CellToTile(exitCellX, cellsY - 1);
        g.exitTile = g.exitInsideTile + Vector2Int.up;   // the outer border tile
        g.open[g.exitTile.x, g.exitTile.y] = true;

        // Final dead-end list (for keys): open cell tiles with a single way in, not the start or exit.
        g.deadEnds.Clear();
        for (int cx = 0; cx < cellsX; cx++)
        for (int cy = 0; cy < cellsY; cy++)
        {
            var t = MazeGrid.CellToTile(cx, cy);
            if (t == g.startTile || t == g.exitInsideTile) continue;
            if (g.OpenNeighborCount(t) == 1) g.deadEnds.Add(t);
        }
        return g;
    }

    void Braid(MazeGrid g, System.Random rng)
    {
        BraidedCount = 0;
        var ends = new List<Vector2Int>();
        for (int cx = 0; cx < cellsX; cx++)
        for (int cy = 0; cy < cellsY; cy++)
        {
            var t = MazeGrid.CellToTile(cx, cy);
            if (g.OpenNeighborCount(t) == 1 && t != g.startTile) ends.Add(t);
        }
        // Shuffle, then always handle the start first so the player never spawns in a dead end.
        for (int i = ends.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (ends[i], ends[j]) = (ends[j], ends[i]); }
        bool startIsDeadEnd = g.OpenNeighborCount(g.startTile) == 1;
        if (startIsDeadEnd) ends.Insert(0, g.startTile);

        int total = ends.Count;
        int keep = Mathf.Max(minDeadEnds, Mathf.RoundToInt(total * deadEndKeep));
        int toBraid = Mathf.Max(0, total - keep);
        if (startIsDeadEnd) toBraid = Mathf.Max(toBraid, 1);

        foreach (var end in ends)
        {
            if (BraidedCount >= toBraid) break;
            if (g.OpenNeighborCount(end) != 1) continue; // already fixed by an earlier braid

            // Candidate walls: closed walls from this cell to a neighboring cell inside the maze.
            var dist = g.DistanceField(end);
            Vector2Int bestWall = default;
            int bestScore = -1;
            bool found = false;
            foreach (var d in Dirs)
            {
                var wall = end + d;
                var neighborCell = end + d * 2;
                if (neighborCell.x <= 0 || neighborCell.y <= 0 ||
                    neighborCell.x >= g.width - 1 || neighborCell.y >= g.height - 1) continue;
                if (g.open[wall.x, wall.y]) continue;
                // Prefer the neighbor that's farthest away on foot: that makes the biggest loop.
                int nd = dist[neighborCell.x, neighborCell.y];
                if (nd < 0) nd = 10000;
                int score = nd * 4 + rng.Next(4); // light randomness to break ties
                if (score > bestScore) { bestScore = score; bestWall = wall; found = true; }
            }
            if (!found) continue;
            g.open[bestWall.x, bestWall.y] = true;
            BraidedCount++;
        }
    }

    bool InCells(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < cellsX && c.y < cellsY;
    static void Open(MazeGrid g, Vector2Int t) => g.open[t.x, t.y] = true;

    // ---------------- Corn ----------------

    void BuildCorn(System.Random rng)
    {
        ClearBuilt();
        root = new GameObject("MazeBuilt").transform;
        root.SetParent(transform, false);
        var stalks = new GameObject("Stalks").transform; stalks.SetParent(root, false);
        var colliders = new GameObject("WallColliders").transform; colliders.SetParent(root, false);

        StalkCount = 0;
        if (stalkPrefabs == null || stalkPrefabs.Length == 0) { Debug.LogWarning("[Maze] No stalk prefabs assigned."); return; }

        var g = Grid;
        bool capped = false;
        for (int x = 0; x < g.width; x++)
        for (int y = 0; y < g.height; y++)
        {
            if (g.open[x, y]) continue;
            var center = g.TileToWorld(x, y);
            // Faces that border an open tile are the ones the player sees. Outside the maze counts as closed.
            bool openN = g.IsOpen(x, y + 1), openS = g.IsOpen(x, y - 1), openE = g.IsOpen(x + 1, y), openW = g.IsOpen(x - 1, y);
            if (!capped)
            {
                StalkCount += CornPlanter.FillTile(stalks, stalkPrefabs, center, tileSize, openN, openE, openS, openW, rng, cornSettings);
                if (StalkCount >= maxStalks)
                {
                    capped = true;
                    Debug.LogWarning($"[Maze] Hit the {maxStalks} stalk safety cap. Lower corn density or maze size.");
                }
            }
            CornPlanter.AddTileCollider(colliders, center, tileSize, wallColliderHeight);
        }

        // Exit marker: an empty at the gap in the outer wall, facing out. The gate goes here later.
        var exit = new GameObject("ExitPoint").transform;
        exit.SetParent(root, false);
        exit.position = g.TileToWorld(g.exitTile);
        exit.rotation = Quaternion.identity; // +Z = out of the maze

        // Block the world beyond the exit gap so the player can only leave through the gate later.
        var beyond = new GameObject("ExitBlocker");
        beyond.transform.SetParent(root, false);
        beyond.transform.position = g.TileToWorld(g.exitTile + Vector2Int.up) + Vector3.up * wallColliderHeight * 0.5f;
        beyond.AddComponent<BoxCollider>().size = new Vector3(tileSize * 3f, wallColliderHeight, tileSize);
    }

    void ClearBuilt()
    {
        var old = transform.Find("MazeBuilt");
        if (old == null) return;
        if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
    }

    // ---------------- Scene placement ----------------

    void PlacePlayer()
    {
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.position = Grid.TileToWorld(Grid.startTile) + Vector3.up * 0.05f;
        player.rotation = Quaternion.identity; // face north, toward the exit side
        if (cc) cc.enabled = true;
    }

    void SizeGround()
    {
        if (ground == null) return;
        float w = Grid.width * tileSize + groundMargin * 2f;
        float h = Grid.height * tileSize + groundMargin * 2f;
        ground.position = new Vector3(transform.position.x, ground.position.y, transform.position.z);
        ground.localScale = new Vector3(w / 10f, 1f, h / 10f); // Unity plane is 10x10 units
    }

    // ---------------- Debug ----------------

    void OnDrawGizmos()
    {
        if (!drawGizmos || Grid == null) return;
        var g = Grid;
        float s = tileSize * 0.9f;
        for (int x = 0; x < g.width; x++)
        for (int y = 0; y < g.height; y++)
        {
            if (!g.open[x, y]) continue;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
            Gizmos.DrawCube(g.TileToWorld(x, y) + Vector3.up * 0.05f, new Vector3(s, 0.05f, s));
        }
        Gizmos.color = Color.green;  Gizmos.DrawSphere(g.TileToWorld(g.startTile) + Vector3.up, 0.5f);
        Gizmos.color = Color.red;    Gizmos.DrawSphere(g.TileToWorld(g.exitTile) + Vector3.up, 0.5f);
        Gizmos.color = Color.yellow;
        foreach (var d in g.deadEnds) Gizmos.DrawWireSphere(g.TileToWorld(d) + Vector3.up, 0.4f);
    }
}
