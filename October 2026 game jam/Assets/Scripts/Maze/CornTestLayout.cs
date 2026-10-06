using UnityEngine;

// Small hand-drawn corn maze section for previewing walls/corners/colors.
// '#' = corn wall, '.' = path. Builds ONLY in Play mode (or via right-click > Rebuild),
// never automatically in the editor: auto-rebuilding thousands of stalks froze the editor.
public class CornTestLayout : MonoBehaviour
{
    public GameObject[] stalkPrefabs;
    public float tileSize = 2.5f;
    public int seed = 1031;
    public CornPlanter.Settings cornSettings = CornPlanter.Settings.Default;

    static readonly string[] Layout =
    {
        "#########",
        "#.......#",
        "#.#####.#",
        "#.#...#.#",
        "#.#.#.#.#",
        "#...#...#",
        "####.####",
    };

    void Start() => Build();

    [ContextMenu("Rebuild")]
    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        if (stalkPrefabs == null || stalkPrefabs.Length == 0) return;

        var rng = new System.Random(seed);
        int rows = Layout.Length, cols = Layout[0].Length;
        var stalks = new GameObject("Stalks").transform; stalks.SetParent(transform, false);
        var colliders = new GameObject("Colliders").transform; colliders.SetParent(transform, false);

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (Layout[r][c] != '#') continue;
            var center = transform.position + new Vector3((c - cols / 2) * tileSize, 0f, (rows - 1 - r) * tileSize + tileSize * 1.5f);
            bool openN = IsOpen(r - 1, c), openS = IsOpen(r + 1, c), openE = IsOpen(r, c + 1), openW = IsOpen(r, c - 1);
            CornPlanter.FillTile(stalks, stalkPrefabs, center, tileSize, openN, openE, openS, openW, rng, cornSettings);
            CornPlanter.AddTileCollider(colliders, center, tileSize);
        }
    }

    static bool IsOpen(int r, int c)
    {
        if (r < 0 || r >= Layout.Length || c < 0 || c >= Layout[0].Length) return true;
        return Layout[r][c] != '#';
    }
}
