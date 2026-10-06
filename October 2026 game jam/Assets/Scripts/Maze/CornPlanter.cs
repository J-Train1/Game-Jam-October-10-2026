using UnityEngine;
using UnityEngine.Rendering;

// Plants corn stalks to fill rectangular wall tiles. Used by the maze generator.
// Walls are dense along faces that border open ground (what the player sees)
// and a bit sparser in the hidden interior to save performance.
public static class CornPlanter
{
    [System.Serializable]
    public struct Settings
    {
        [Tooltip("Distance between stalks on the jittered grid. Smaller = denser.")]
        public float spacing;
        [Tooltip("Random offset as a fraction of spacing (0-1).")]
        public float jitter;
        [Tooltip("How deep (m) the fully dense band is along faces the player can see.")]
        public float edgeDepth;
        [Tooltip("Chance (0-1) to keep a stalk in the hidden interior of a wall.")]
        public float interiorKeep;
        [Tooltip("How far (m) stalks may poke past the tile edge into the path.")]
        public float overhang;
        public Vector2 scaleRange;
        public float maxLeanDegrees;

        public static Settings Default => new Settings
        {
            spacing = 0.35f, jitter = 0.6f, edgeDepth = 1.25f, interiorKeep = 0.65f,
            overhang = 0.12f, scaleRange = new Vector2(0.9f, 1.15f), maxLeanDegrees = 5f
        };
    }

    /// <summary>
    /// Fills one wall tile centered at 'center' (world XZ) with corn.
    /// openN/E/S/W = true if that side borders an open (walkable) tile.
    /// </summary>
    public static int FillTile(Transform parent, GameObject[] prefabs, Vector3 center, float tileSize,
        bool openN, bool openE, bool openS, bool openW, System.Random rng, Settings s)
    {
        if (prefabs == null || prefabs.Length == 0) return 0;
        float half = tileSize * 0.5f;
        int steps = Mathf.Max(1, Mathf.RoundToInt(tileSize / Mathf.Max(0.1f, s.spacing)));
        float step = tileSize / steps;
        int planted = 0;

        for (int ix = 0; ix < steps; ix++)
        for (int iz = 0; iz < steps; iz++)
        {
            float lx = -half + (ix + 0.5f) * step + (float)(rng.NextDouble() - 0.5) * step * s.jitter;
            float lz = -half + (iz + 0.5f) * step + (float)(rng.NextDouble() - 0.5) * step * s.jitter;

            // Let stalks on open faces creep slightly into the path so edges look natural, not ruler-straight.
            float maxX = half + (openE ? s.overhang : 0f), minX = -half - (openW ? s.overhang : 0f);
            float maxZ = half + (openN ? s.overhang : 0f), minZ = -half - (openS ? s.overhang : 0f);
            lx = Mathf.Clamp(lx, minX, maxX);
            lz = Mathf.Clamp(lz, minZ, maxZ);

            // Distance to the nearest visible face decides density.
            float dist = float.MaxValue;
            if (openN) dist = Mathf.Min(dist, half - lz);
            if (openS) dist = Mathf.Min(dist, lz + half);
            if (openE) dist = Mathf.Min(dist, half - lx);
            if (openW) dist = Mathf.Min(dist, lx + half);
            bool visible = dist <= s.edgeDepth;
            if (!visible && rng.NextDouble() > s.interiorKeep) continue;

            var prefab = prefabs[rng.Next(prefabs.Length)];
            var pos = center + new Vector3(lx, 0f, lz);
            float yaw = (float)rng.NextDouble() * 360f;
            float leanX = ((float)rng.NextDouble() * 2f - 1f) * s.maxLeanDegrees;
            float leanZ = ((float)rng.NextDouble() * 2f - 1f) * s.maxLeanDegrees;
            var rot = Quaternion.Euler(leanX, 0f, leanZ) * Quaternion.Euler(0f, yaw, 0f);
            float scale = Mathf.Lerp(s.scaleRange.x, s.scaleRange.y, (float)rng.NextDouble());

            var go = Object.Instantiate(prefab, pos, rot, parent);
            go.transform.localScale = Vector3.one * scale;
            // Hidden interior stalks don't need to cast shadows; big performance saving.
            if (!visible && go.TryGetComponent(out MeshRenderer mr)) mr.shadowCastingMode = ShadowCastingMode.Off;
            planted++;
        }
        return planted;
    }

    /// <summary>Invisible collider so the player can't walk through a wall tile.</summary>
    public static void AddTileCollider(Transform parent, Vector3 center, float tileSize, float height = 3f)
    {
        var col = new GameObject("WallCollider");
        col.transform.SetParent(parent, false);
        col.transform.position = center + Vector3.up * height * 0.5f;
        var box = col.AddComponent<BoxCollider>();
        box.size = new Vector3(tileSize, height, tileSize);
    }
}
