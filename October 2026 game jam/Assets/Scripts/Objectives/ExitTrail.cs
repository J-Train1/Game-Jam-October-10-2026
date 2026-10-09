using System.Collections.Generic;
using UnityEngine;

// The trail behind the exit gate: a straight lane walled with real corn on both sides that opens into a small
// clearing, where a stone sarcophagus sits on a slab like an altar (cross behind it, candles, a faint cold light
// so it reads from down the trail). Touch the coffin to win.
// Built at runtime off the maze's exit (it moves with the exit every run). Flat corn cards behind the walls stop
// you seeing empty ground through the stalks. Removes the maze's ExitBlocker so the lane is walkable once the
// gate is open (the gate itself still blocks until then).
[DefaultExecutionOrder(-30)]
public class ExitTrail : MonoBehaviour
{
    public static ExitTrail Instance { get; private set; }

    [Header("Layout (in maze tiles)")]
    [Tooltip("Length of the corn lane before the clearing.")]
    [Min(1)] public int laneTiles = 6;
    [Tooltip("The clearing is this many tiles wide and deep (odd number).")]
    [Min(3)] public int clearingTiles = 3;

    [Header("Coffin altar")]
    public GameObject coffinPrefab;
    public GameObject slabPrefab;
    public GameObject crossPrefab;
    public GameObject candlePrefab;
    [Tooltip("The pumpkin that comes out of the grave in the ending. Empty = the spawner's pumpkin prefab.")]
    public GameObject gravePumpkinPrefab;
    [Tooltip("Coffin length (m). It lies across the clearing, facing you.")]
    public float coffinLength = 2.1f;
    public float slabSize = 2.6f;
    public float slabHeight = 0.22f;
    public float crossHeight = 1.9f;
    public float candleHeight = 0.45f;
    [Tooltip("How close (m, from the coffin's edge to your center) counts as touching it.")]
    public float touchDistance = 1.5f;

    [Header("Lights")]
    public Color candleColor = new Color(1f, 0.6f, 0.28f);
    public float candleIntensity = 0.9f;
    public float candleRange = 4f;
    public Color coffinLightColor = new Color(0.6f, 0.7f, 1f);
    public float coffinLightIntensity = 1.2f;
    public float coffinLightRange = 6f;

    [Header("Backing corn cards")]
    public float cardHeight = 3.8f;

    public Transform Coffin { get; private set; }
    public Bounds CoffinBounds => coffinBounds;
    /// <summary>The whole stone altar (slab + coffin). The ending's pumpkin comes up from the dirt just outside it.</summary>
    public Bounds AltarBounds => altarBounds;
    Bounds altarBounds;
    [Tooltip("How close (m) to the edge of the stone slab starts the ending.")]
    public float finaleDistance = 2.6f;
    [Header("Testing")]
    [Tooltip("TEMPORARY: start the run on the trail just before the coffin, to test the ending. Untick when done.")]
    public bool startAtEnd = true;
    bool startAtEndDone;
    public List<Light> CandleLights => candleLights;

    /// <summary>One candle on the altar: its light, its glowing flame material(s) and where its wick is.</summary>
    public class Candle
    {
        public Transform transform;
        public Light light;
        public Vector3 wick;
        public readonly List<Material> flameMats = new List<Material>();
        public readonly List<Color> flameEmission = new List<Color>();
        public float baseIntensity;
        public bool lit = true;

        /// <summary>0 = out, 1 = normal, above 1 = flaring.</summary>
        public void SetFlame(float k)
        {
            if (light != null) { light.enabled = k > 0.001f; light.intensity = baseIntensity * k; }
            for (int i = 0; i < flameMats.Count; i++)
                if (flameMats[i] != null) flameMats[i].SetColor("_EmissionColor", flameEmission[i] * k);
        }
    }
    public readonly List<Candle> Candles = new List<Candle>();
    public Light CoffinLight => coffinLight;
    /// <summary>While true the trail stops animating its lights (the ending snuffs them out).</summary>
    public bool LightsOverridden { get; set; }
    public bool Won { get; private set; }

    Transform root;
    Bounds coffinBounds;
    readonly List<Light> candleLights = new List<Light>();
    Light coffinLight;
    float T;

    void Awake() => Instance = this;

    void Start()
    {
        var gen = MazeGenerator.Instance;
        if (gen == null || gen.Grid == null) { Debug.LogWarning("[Trail] No maze."); return; }
        var g = gen.Grid;
        T = g.tileSize;

        var built = gen.transform.Find("MazeBuilt");
        root = new GameObject("ExitTrail").transform;
        root.SetParent(built != null ? built : gen.transform, false); // under the maze so walls block sight and the beam

        // The maze's blocker beyond the exit would block the lane; the gate does the blocking now.
        var blocker = built != null ? built.Find("ExitBlocker") : null;
        if (blocker != null) Destroy(blocker.gameObject);

        Vector3 exitTile = g.TileToWorld(g.exitTile);
        Vector3 origin = new Vector3(exitTile.x, exitTile.y, exitTile.z + T); // lane row 0 center (just outside the maze)

        BuildCorn(gen, origin);
        BuildBackingCards(gen, origin);
        BuildAltar(origin);
        Debug.Log($"[Trail] built: {laneTiles}-tile lane, {clearingTiles}x{clearingTiles} clearing, coffin at {coffinBounds.center}");
    }

    // ---------------- Corn walls ----------------

    // Grid in tile units relative to the lane start: column c (0 = lane center, + = east), row r (0 = first lane row).
    bool IsOpen(int c, int r)
    {
        int h = clearingTiles / 2;
        if (r < 0) return c == 0;                               // the maze's exit gap behind us
        if (r < laneTiles) return c == 0;                       // lane
        return r < laneTiles + clearingTiles && Mathf.Abs(c) <= h; // clearing
    }

    void BuildCorn(MazeGenerator gen, Vector3 origin)
    {
        var stalks = new GameObject("Stalks").transform; stalks.SetParent(root, false);
        var cols = new GameObject("WallColliders").transform; cols.SetParent(root, false);
        var rng = new System.Random(gen.LastSeed ^ 0x7a11);
        int h = clearingTiles / 2;
        int count = 0, tiles = 0;
        for (int r = 0; r <= laneTiles + clearingTiles; r++)
        for (int c = -(h + 1); c <= h + 1; c++)
        {
            if (IsOpen(c, r)) continue;
            bool openN = IsOpen(c, r + 1), openS = IsOpen(c, r - 1), openE = IsOpen(c + 1, r), openW = IsOpen(c - 1, r);
            bool diag = IsOpen(c + 1, r + 1) || IsOpen(c - 1, r + 1) || IsOpen(c + 1, r - 1) || IsOpen(c - 1, r - 1);
            if (!openN && !openS && !openE && !openW && !diag) continue; // nowhere near the path
            Vector3 center = origin + new Vector3(c * T, 0f, r * T);
            count += CornPlanter.FillTile(stalks, gen.stalkPrefabs, center, T, openN, openE, openS, openW, rng, gen.cornSettings);
            CornPlanter.AddTileCollider(cols, center, T, gen.wallColliderHeight);
            tiles++;
        }
        // Seal the clearing's far side and the lane edges against the outside.
        Debug.Log($"[Trail] planted {count} stalks in {tiles} tiles");
    }

    // Two staggered rows of corn cards just outside the walls, so gaps between stalks show more corn.
    void BuildBackingCards(MazeGenerator gen, Vector3 origin)
    {
        var backdrop = gen.GetComponent<OuterCornBackdrop>();
        if (backdrop == null || backdrop.cardMaterials == null || backdrop.cardMaterials.Length == 0) return;
        var mats = backdrop.cardMaterials;
        var cards = new GameObject("BackingCards").transform; cards.SetParent(root, false);
        var rng = new System.Random(gen.LastSeed ^ 0x3c4d);
        int h = clearingTiles / 2;

        float laneOuter = 1.5f * T;                        // outer face of the lane's wall tiles (from lane center)
        float clearOuter = (h + 1.5f) * T;                 // outer face of the clearing's side wall tiles
        float zStart = origin.z - T * 0.5f;                // where the lane leaves the maze
        float zClear = origin.z + (laneTiles - 0.5f) * T;  // clearing starts
        float zBack = origin.z + (laneTiles + clearingTiles + 0.5f) * T; // outer face of the back wall

        foreach (float off in new[] { 0.3f, 1.4f })
        {
            for (int s = -1; s <= 1; s += 2)
            {
                // Lane sides (facing in), then clearing sides.
                Line(cards, mats, rng, new Vector3(origin.x + s * (laneOuter + off), origin.y, zStart), new Vector3(origin.x + s * (laneOuter + off), origin.y, zClear), s);
                Line(cards, mats, rng, new Vector3(origin.x + s * (clearOuter + off), origin.y, zClear - T), new Vector3(origin.x + s * (clearOuter + off), origin.y, zBack + off), s);
            }
            // Back.
            Line(cards, mats, rng, new Vector3(origin.x - clearOuter - off, origin.y, zBack + off), new Vector3(origin.x + clearOuter + off, origin.y, zBack + off), 0);
        }
    }

    void Line(Transform parent, Material[] mats, System.Random rng, Vector3 a, Vector3 b, int side)
    {
        Vector3 dir = b - a; float len = dir.magnitude; if (len < 0.5f) return; dir /= len;
        // Face the trail: cards' front is -Z local; rotate so local X runs along the line.
        Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up) * (side >= 0 ? 1f : -1f), Vector3.up);
        float pos = 0f;
        while (pos < len - 0.5f)
        {
            float w = Mathf.Lerp(2.5f, 5f, (float)rng.NextDouble());
            if (pos + w > len) w = len - pos;
            if (w < 1f) break;
            var go = new GameObject("CornCard");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(a + dir * (pos + w * 0.5f), rot);
            float s = Mathf.Lerp(0.95f, 1.15f, (float)rng.NextDouble());
            go.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -s : s, s, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = CardMesh(w / s, (float)rng.NextDouble());
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats[rng.Next(mats.Length)];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pos += w * Mathf.Lerp(0.55f, 0.75f, (float)rng.NextDouble());
        }
    }

    Mesh CardMesh(float w, float u)
    {
        const float texW = 5f; // matches the baked corn card texture (5 m wide)
        float uw = Mathf.Min(1f, w / texW), u0 = (1f - uw) * u;
        var m = new Mesh { name = "TrailCornCard" };
        m.vertices = new[] { new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, cardHeight, 0f), new Vector3(-w * 0.5f, cardHeight, 0f) };
        m.uv = new[] { new Vector2(u0, 0f), new Vector2(u0 + uw, 0f), new Vector2(u0 + uw, 1f), new Vector2(u0, 1f) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    // ---------------- The coffin altar ----------------

    void BuildAltar(Vector3 origin)
    {
        int h = clearingTiles / 2;
        // Toward the back of the clearing, centered.
        Vector3 c = origin + new Vector3(0f, 0f, (laneTiles + clearingTiles - 1) * T - 0.3f);
        var altar = new GameObject("CoffinAltar").transform;
        altar.SetParent(root, false);
        altar.position = c;

        // Slab.
        float top = c.y;
        if (slabPrefab != null)
        {
            var slab = Spawn(slabPrefab, altar, "Slab");
            var b = Bounds(slab.transform);
            var ls = slab.transform.localScale;
            slab.transform.localScale = new Vector3(ls.x * slabSize / b.size.x, ls.y * slabHeight / b.size.y, ls.z * slabSize / b.size.z);
            PlaceBottomCenter(slab.transform, c);
            altarBounds = Bounds(slab.transform);
            b = Bounds(slab.transform);
            top = b.max.y;
            AddBox(slab.transform, b);
        }

        // Coffin: long axis across the clearing (X), on top of the slab.
        if (coffinPrefab != null)
        {
            var coffin = Spawn(coffinPrefab, altar, "Coffin");
            var b0 = Bounds(coffin.transform);
            if (b0.size.z > b0.size.x) coffin.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * coffin.transform.rotation;
            var b = Bounds(coffin.transform);
            coffin.transform.localScale *= coffinLength / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            PlaceBottomCenter(coffin.transform, new Vector3(c.x, top, c.z));
            coffinBounds = Bounds(coffin.transform);
            AddBox(coffin.transform, coffinBounds);
            Coffin = coffin.transform;
        }
        else coffinBounds = new Bounds(c + Vector3.up * 0.5f, new Vector3(coffinLength, 1f, 0.8f));
        if (altarBounds.size == Vector3.zero) altarBounds = coffinBounds; else altarBounds.Encapsulate(coffinBounds);

        // Cross at the head (behind the coffin, away from the player).
        if (crossPrefab != null)
        {
            var cross = Spawn(crossPrefab, altar, "Cross");
            var b0 = Bounds(cross.transform);
            if (b0.size.z > b0.size.x) cross.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * cross.transform.rotation;
            FitHeight(cross.transform, crossHeight);
            PlaceBottomCenter(cross.transform, new Vector3(c.x, top, coffinBounds.max.z + 0.25f));
            AddBox(cross.transform, Bounds(cross.transform));
        }

        // Candles at the slab corners + a couple on the ground in front.
        float e = slabSize * 0.5f - 0.2f;
        var spots = new List<Vector3>
        {
            new Vector3(c.x - e, top, c.z - e), new Vector3(c.x + e, top, c.z - e),
            new Vector3(c.x - e, top, c.z + e), new Vector3(c.x + e, top, c.z + e),
            new Vector3(c.x - slabSize * 0.5f - 0.5f, c.y, c.z - slabSize * 0.5f - 0.3f),
            new Vector3(c.x + slabSize * 0.5f + 0.4f, c.y, c.z - slabSize * 0.5f - 0.6f),
        };
        for (int i = 0; i < spots.Count; i++)
        {
            if (candlePrefab != null)
            {
                var cd = Spawn(candlePrefab, altar, "Candle" + (i + 1));
                foreach (var col in cd.GetComponentsInChildren<Collider>(true)) Destroy(col);
                FitHeight(cd.transform, candleHeight * (i < 4 ? 1f : 1.4f));
                PlaceBottomCenter(cd.transform, spots[i]);
                var b = Bounds(cd.transform);
                // Every candle gets its own small light, so each one going out in the ending is visible.
                var candle = new Candle { transform = cd.transform, wick = new Vector3(b.center.x, b.max.y, b.center.z), baseIntensity = candleIntensity * 0.7f };
                candle.light = MakeLight(altar, candle.wick + Vector3.up * 0.08f, candleColor, candle.baseIntensity, candleRange * 0.85f);
                candleLights.Add(candle.light);
                foreach (var r in cd.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.materials) // instances, so each flame can go out on its own
                        if (m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION"))
                        {
                            candle.flameMats.Add(m);
                            candle.flameEmission.Add(m.GetColor("_EmissionColor"));
                        }
                Candles.Add(candle);
            }
        }

        // Faint cold light above the coffin: a beacon down the trail.
        coffinLight = MakeLight(altar, coffinBounds.center + Vector3.up * 1.6f, coffinLightColor, coffinLightIntensity, coffinLightRange);
    }

    // ---------------- Win ----------------

    void Update()
    {
        if (startAtEnd && !startAtEndDone && root != null && PlayerController.Instance != null)
        {
            startAtEndDone = true;
            // A few steps down the trail from where the ending kicks in, facing the coffin.
            var spawn = new Vector3(altarBounds.center.x, altarBounds.min.y + 0.05f, altarBounds.min.z - finaleDistance - 3f);
            PlayerController.Instance.Respawn(spawn, Quaternion.LookRotation(Vector3.forward));
            Debug.Log("[Trail] TEST: started at the end of the trail (untick Start At End on ExitTrail when done)");
        }
        float t = Time.time;
        if (!LightsOverridden)
        for (int i = 0; i < Candles.Count; i++)
            Candles[i].SetFlame(0.75f + 0.25f * Mathf.PerlinNoise(t * 6f, i * 3.7f));
        if (coffinLight != null && !LightsOverridden) coffinLight.intensity = coffinLightIntensity * (0.85f + 0.15f * Mathf.Sin(t * 0.8f));

        if (Won || root == null || PumpkinMonster.PlayerIsCaught) return;
        var pc = PlayerController.Instance;
        if (pc == null) return;
        Vector3 p = pc.transform.position;
        Vector3 closest = altarBounds.ClosestPoint(new Vector3(p.x, altarBounds.center.y, p.z));
        Vector3 d = closest - p; d.y = 0f;
        if (d.magnitude <= finaleDistance)
        {
            Won = true;
            Debug.Log("[Trail] coffin touched: ending");
            GraveFinale.Begin(); // one last scare at the grave, then YOU ESCAPED
        }
    }

    // ---------------- Helpers ----------------

    Light MakeLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static void AddBox(Transform t, Bounds worldBounds)
    {
        foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        var go = new GameObject("Collider");
        go.transform.SetParent(t.parent, false);
        go.transform.position = worldBounds.center;
        go.AddComponent<BoxCollider>().size = worldBounds.size;
    }

    static GameObject Spawn(GameObject prefab, Transform parent, string name)
    {
        var go = Instantiate(prefab, parent);
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        return go;
    }

    static Bounds Bounds(Transform t)
    {
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(t.position, Vector3.zero);
        var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    static void FitHeight(Transform t, float h)
    {
        var b = Bounds(t);
        if (b.size.y > 0.0001f) t.localScale *= h / b.size.y;
    }

    static void PlaceBottomCenter(Transform t, Vector3 groundPoint)
    {
        var b = Bounds(t);
        t.position += new Vector3(groundPoint.x - b.center.x, groundPoint.y - b.min.y, groundPoint.z - b.center.z);
    }
}
