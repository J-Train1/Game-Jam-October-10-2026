using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Surrounds the maze with staggered rows of flat "corn cards" (a baked picture of real corn),
// so the field seems to continue past the border walls. ~2 triangles per card: practically free.
// Leaves a clear lane behind the exit so escaping leads somewhere.
[RequireComponent(typeof(MazeGenerator))]
public class OuterCornBackdrop : MonoBehaviour
{
    public Material[] cardMaterials;
    [Tooltip("Distances (m) of each card row from the outer edge of the maze.")]
    public float[] rowOffsets = { 0.1f, 0.9f, 1.9f, 3.1f, 4.6f, 6.4f, 8.6f, 11.5f };
    public Vector2 cardWidthRange = new Vector2(2.5f, 5f);
    public Vector2 cardScaleRange = new Vector2(0.9f, 1.15f);
    [Tooltip("Random forward/back wobble per card so rows don't look like straight fences.")]
    public float depthJitter = 0.35f;
    public float exitLaneHalfWidth = 1.6f;

    // Must match the baked texture (JamCornCardBaker): 5 m wide x 3.8 m tall.
    const float TexWorldWidth = 5f;
    const float TexWorldHeight = 3.8f;

    readonly Dictionary<(int, int), Mesh> meshCache = new Dictionary<(int, int), Mesh>();
    MazeGenerator gen;
    Transform cardsRoot;

    void Start()
    {
        gen = GetComponent<MazeGenerator>();
        gen.OnMazeBuilt += Build;
        if (gen.Grid != null) Build(gen.Grid);
    }

    void OnDestroy()
    {
        if (gen != null) gen.OnMazeBuilt -= Build;
    }

    public void Build(MazeGrid g)
    {
        if (cardsRoot != null) Destroy(cardsRoot.gameObject);
        if (cardMaterials == null || cardMaterials.Length == 0) { Debug.LogWarning("[OuterCorn] No card materials assigned."); return; }

        cardsRoot = new GameObject("OuterCorn").transform;
        cardsRoot.SetParent(transform, false);
        var rng = new System.Random(gen.LastSeed ^ 0x5eed);

        float half = g.tileSize * 0.5f;
        float minX = g.origin.x - half, maxX = g.origin.x + (g.width - 1) * g.tileSize + half;
        float minZ = g.origin.z - half, maxZ = g.origin.z + (g.height - 1) * g.tileSize + half;
        float exitX = g.TileToWorld(g.exitTile).x;
        float y = transform.position.y;
        int count = 0;

        foreach (float off in rowOffsets)
        {
            float ext = off + cardWidthRange.y; // run past the corners so they fill in
            // North: cards beyond +Z, facing the maze (-Z). Split around the exit lane.
            count += FillLine(rng, minX - ext, exitX - exitLaneHalfWidth, x => new Vector3(x, y, maxZ + off), Quaternion.identity);
            count += FillLine(rng, exitX + exitLaneHalfWidth, maxX + ext, x => new Vector3(x, y, maxZ + off), Quaternion.identity);
            // South: beyond -Z, facing +Z.
            count += FillLine(rng, minX - ext, maxX + ext, x => new Vector3(x, y, minZ - off), Quaternion.Euler(0f, 180f, 0f));
            // East: beyond +X, facing -X.
            count += FillLine(rng, minZ - ext, maxZ + ext, z => new Vector3(maxX + off, y, z), Quaternion.Euler(0f, 90f, 0f));
            // West: beyond -X, facing +X.
            count += FillLine(rng, minZ - ext, maxZ + ext, z => new Vector3(minX - off, y, z), Quaternion.Euler(0f, -90f, 0f));
        }
        Debug.Log($"[OuterCorn] placed {count} corn cards");
    }

    // Lays overlapping cards along a line segment [from, to] (in the line's own axis).
    int FillLine(System.Random rng, float from, float to, System.Func<float, Vector3> toWorld, Quaternion facing)
    {
        int placed = 0;
        float pos = from;
        while (pos < to - 0.5f)
        {
            float w = Mathf.Lerp(cardWidthRange.x, cardWidthRange.y, (float)rng.NextDouble());
            if (pos + w > to) w = to - pos;
            if (w < 1f) break;

            float center = pos + w * 0.5f;
            Vector3 p = toWorld(center);
            Vector3 normal = facing * Vector3.back;                          // card faces this way (toward the maze)
            p += normal * (float)((rng.NextDouble() * 2 - 1) * depthJitter);

            var go = new GameObject("CornCard");
            go.transform.SetParent(cardsRoot, false);
            go.transform.SetPositionAndRotation(p, facing);
            float s = Mathf.Lerp(cardScaleRange.x, cardScaleRange.y, (float)rng.NextDouble());
            float flip = rng.NextDouble() < 0.5 ? -1f : 1f;
            go.transform.localScale = new Vector3(s * flip, s, 1f);

            go.AddComponent<MeshFilter>().sharedMesh = GetCardMesh(w / s, rng);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cardMaterials[rng.Next(cardMaterials.Length)];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            placed++;
            pos += w * Mathf.Lerp(0.55f, 0.75f, (float)rng.NextDouble()); // overlap neighbors
        }
        return placed;
    }

    // Quad with bottom-center pivot, showing a true-scale slice of the corn texture.
    // Widths are binned and UV offsets varied so neighboring cards don't look identical.
    Mesh GetCardMesh(float width, System.Random rng)
    {
        int bin = Mathf.Clamp(Mathf.RoundToInt(width * 2f), 1, Mathf.RoundToInt(TexWorldWidth * 2f)); // 0.5 m bins
        int variant = rng.Next(3);
        if (meshCache.TryGetValue((bin, variant), out var cached)) return cached;

        float w = bin * 0.5f;
        float uWidth = w / TexWorldWidth;
        float u0 = (1f - uWidth) * (variant / 2f);
        var m = new Mesh { name = $"CornCard_{bin}_{variant}" };
        m.vertices = new[]
        {
            new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f),
            new Vector3(w * 0.5f, TexWorldHeight, 0f), new Vector3(-w * 0.5f, TexWorldHeight, 0f)
        };
        m.uv = new[] { new Vector2(u0, 0f), new Vector2(u0 + uWidth, 0f), new Vector2(u0 + uWidth, 1f), new Vector2(u0, 1f) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        meshCache[(bin, variant)] = m;
        return m;
    }
}
