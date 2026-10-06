using System.Collections.Generic;
using UnityEngine;

// The maze as a tile grid. Tiles are either open (walkable) or corn wall.
// Cells of the logical maze live on odd tile coordinates; even tiles are walls/passages.
// Used by the generator, scarecrow pathfinding, and key spawning.
public class MazeGrid
{
    public readonly int cellsX, cellsY;
    public readonly int width, height;      // in tiles
    public readonly float tileSize;
    public readonly Vector3 origin;          // world position of tile (0,0) center
    public readonly bool[,] open;

    public Vector2Int startTile;
    public Vector2Int exitTile;              // the opening in the outer wall where the gate goes
    public Vector2Int exitInsideTile;        // the open tile just inside the exit
    public readonly List<Vector2Int> deadEnds = new List<Vector2Int>();

    public MazeGrid(int cellsX, int cellsY, float tileSize, Vector3 center)
    {
        this.cellsX = cellsX; this.cellsY = cellsY; this.tileSize = tileSize;
        width = cellsX * 2 + 1; height = cellsY * 2 + 1;
        open = new bool[width, height];
        origin = center - new Vector3((width - 1) * 0.5f * tileSize, 0f, (height - 1) * 0.5f * tileSize);
    }

    public static Vector2Int CellToTile(int cx, int cy) => new Vector2Int(cx * 2 + 1, cy * 2 + 1);

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
    public bool InBounds(Vector2Int t) => InBounds(t.x, t.y);
    public bool IsOpen(int x, int y) => InBounds(x, y) && open[x, y];
    public bool IsOpen(Vector2Int t) => IsOpen(t.x, t.y);

    public Vector3 TileToWorld(Vector2Int t) => origin + new Vector3(t.x * tileSize, 0f, t.y * tileSize);
    public Vector3 TileToWorld(int x, int y) => origin + new Vector3(x * tileSize, 0f, y * tileSize);

    public Vector2Int WorldToTile(Vector3 p)
    {
        var local = p - origin;
        return new Vector2Int(Mathf.RoundToInt(local.x / tileSize), Mathf.RoundToInt(local.z / tileSize));
    }

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public IEnumerable<Vector2Int> OpenNeighbors(Vector2Int t)
    {
        foreach (var d in Dirs)
        {
            var n = t + d;
            if (IsOpen(n)) yield return n;
        }
    }

    public int OpenNeighborCount(Vector2Int t)
    {
        int c = 0;
        foreach (var d in Dirs) if (IsOpen(t + d)) c++;
        return c;
    }

    /// <summary>Breadth-first distance (in tiles) from a tile to every open tile. -1 = unreachable.</summary>
    public int[,] DistanceField(Vector2Int from)
    {
        var dist = new int[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) dist[x, y] = -1;
        if (!IsOpen(from)) return dist;
        var q = new Queue<Vector2Int>();
        dist[from.x, from.y] = 0; q.Enqueue(from);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in OpenNeighbors(c))
                if (dist[n.x, n.y] < 0) { dist[n.x, n.y] = dist[c.x, c.y] + 1; q.Enqueue(n); }
        }
        return dist;
    }

    /// <summary>Shortest path between two open tiles (inclusive), or null if none.</summary>
    public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
    {
        if (!IsOpen(from) || !IsOpen(to)) return null;
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var q = new Queue<Vector2Int>();
        q.Enqueue(from); prev[from] = from;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == to) break;
            foreach (var n in OpenNeighbors(c))
                if (!prev.ContainsKey(n)) { prev[n] = c; q.Enqueue(n); }
        }
        if (!prev.ContainsKey(to)) return null;
        var path = new List<Vector2Int>();
        for (var c = to; c != from; c = prev[c]) path.Add(c);
        path.Add(from);
        path.Reverse();
        return path;
    }
}
