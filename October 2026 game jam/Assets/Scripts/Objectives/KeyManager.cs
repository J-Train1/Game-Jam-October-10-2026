using System.Collections.Generic;
using UnityEngine;

// Spawns the keys in the maze's dead ends and handles pickup.
// Placement: first key in the dead end farthest (on foot) from the start; each next key in the dead end
// whose closest distance to the start AND to every key already placed is largest, so keys end up spread
// across the whole maze. Never the exit dead end.
// Pickup: automatic when the player runs into a key. Each pickup spawns one more pumpkin.
[DefaultExecutionOrder(-50)] // after the maze (-100), before the gate and HUD
public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance { get; private set; }
    public event System.Action<int, int> OnKeyCollected; // (collected, total)

    [Header("Keys")]
    public GameObject keyPrefab;
    [Range(1, 5)] public int keyCount = 3;
    [Tooltip("Size of the key in meters (longest side).")]
    public float keySize = 0.32f;
    public float hoverHeight = 1.0f;
    public float bobAmount = 0.06f;
    public float spinSpeed = 70f;
    [Tooltip("Horizontal distance at which you grab a key by running into it.")]
    public float pickupRadius = 1.0f;

    [Header("Glint (so you can spot it at the end of a corridor)")]
    public Color glintColor = new Color(0.85f, 0.9f, 1f);
    public float glintIntensity = 0.6f;
    public float glintRange = 2.5f;

    [Header("Escalation")]
    [Tooltip("Spawn one more pumpkin each time a key is picked up.")]
    public bool spawnPumpkinPerKey = true;

    [Header("Audio")]
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    public int Total => keys.Count + Collected;
    public int Collected { get; private set; }

    readonly List<Transform> keys = new List<Transform>();
    readonly List<float> phases = new List<float>();
    readonly List<Light> glints = new List<Light>();
    AudioSource audioSrc;
    AudioClip pickupClip;

    void Awake() => Instance = this;

    void Start()
    {
        var gen = MazeGenerator.Instance;
        if (gen == null || gen.Grid == null || keyPrefab == null)
        {
            Debug.LogWarning("[Keys] Missing maze or key prefab; no keys spawned.");
            return;
        }
        var g = gen.Grid;
        var tiles = PickTiles(g, keyCount);
        var root = new GameObject("Keys").transform;
        root.SetParent(transform, false);
        foreach (var t in tiles) SpawnKey(g, t, root);

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;
        audioSrc.playOnAwake = false;
        pickupClip = GameAudio.Get("Key_Bell");
        if (pickupClip == null) pickupClip = MakePickupClip();

        Debug.Log($"[Keys] placed {keys.Count} keys at {string.Join(", ", tiles)} ({g.deadEnds.Count} dead ends available)");
        OnKeyCollected?.Invoke(Collected, Total);
    }

    // ---------------- Placement ----------------

    static List<Vector2Int> PickTiles(MazeGrid g, int count)
    {
        var candidates = new List<Vector2Int>(g.deadEnds);
        candidates.Remove(g.exitInsideTile);
        candidates.Remove(g.startTile);
        if (candidates.Count < count)
        {
            // Not enough dead ends: top up with the cell tiles farthest from the start.
            var fromStart = g.DistanceField(g.startTile);
            var cells = new List<Vector2Int>();
            for (int cx = 0; cx < g.cellsX; cx++)
            for (int cy = 0; cy < g.cellsY; cy++)
            {
                var t = MazeGrid.CellToTile(cx, cy);
                if (t != g.startTile && t != g.exitInsideTile && !candidates.Contains(t) && fromStart[t.x, t.y] > 0) cells.Add(t);
            }
            cells.Sort((a, b) => fromStart[b.x, b.y].CompareTo(fromStart[a.x, a.y]));
            for (int i = 0; i < cells.Count && candidates.Count < count; i++) candidates.Add(cells[i]);
        }

        var picked = new List<Vector2Int>();
        var fields = new List<int[,]> { g.DistanceField(g.startTile) }; // start counts as "taken"
        while (picked.Count < count && candidates.Count > 0)
        {
            Vector2Int best = candidates[0];
            int bestScore = -1;
            foreach (var c in candidates)
            {
                int score = int.MaxValue;
                foreach (var f in fields)
                {
                    int d = f[c.x, c.y];
                    if (d < 0) { score = -1; break; } // unreachable
                    score = Mathf.Min(score, d);
                }
                score = score * 4 + Random.Range(0, 4); // light randomness on ties
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (bestScore < 0) break;
            picked.Add(best);
            candidates.Remove(best);
            fields.Add(g.DistanceField(best));
        }
        return picked;
    }

    void SpawnKey(MazeGrid g, Vector2Int tile, Transform root)
    {
        var holder = new GameObject("Key_" + (keys.Count + 1)).transform;
        holder.SetParent(root, false);
        holder.position = g.TileToWorld(tile) + Vector3.up * hoverHeight;

        var k = Instantiate(keyPrefab, holder);
        k.transform.localPosition = Vector3.zero;
        k.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // stand it up
        foreach (var c in k.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var rb in k.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        FitSize(k.transform, keySize);
        CenterOn(k.transform, holder.position);

        var lg = new GameObject("Glint");
        lg.transform.SetParent(holder, false);
        lg.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = glintColor;
        l.intensity = glintIntensity;
        l.range = glintRange;
        l.shadows = LightShadows.None;

        keys.Add(holder);
        phases.Add(Random.value * 10f);
        glints.Add(l);
    }

    static void FitSize(Transform t, float size)
    {
        var rends = t.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) t.localScale *= size / longest;
    }

    static void CenterOn(Transform t, Vector3 point)
    {
        var rends = t.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
        t.position += point - b.center;
    }

    // ---------------- Per frame ----------------

    void Update()
    {
        if (keys.Count == 0) return;
        var pc = PlayerController.Instance;
        Vector3 p = pc != null ? pc.transform.position : Vector3.positiveInfinity;
        bool canPick = pc != null && !PumpkinMonster.PlayerIsCaught;

        for (int i = keys.Count - 1; i >= 0; i--)
        {
            var k = keys[i];
            if (k == null) { RemoveAt(i); continue; }
            float t = Time.time + phases[i];
            if (k.childCount > 0)
            {
                var model = k.GetChild(0);
                model.RotateAround(k.position, Vector3.up, spinSpeed * Time.deltaTime);
            }
            k.position = new Vector3(k.position.x, k.parent.position.y + BaseY(k) + Mathf.Sin(t * 2f) * bobAmount, k.position.z);
            if (glints[i] != null) glints[i].intensity = glintIntensity * (0.75f + 0.25f * Mathf.PerlinNoise(t * 3f, 0.5f));

            if (canPick)
            {
                Vector3 d = k.position - p; d.y = 0f;
                if (d.magnitude <= pickupRadius) Collect(i);
            }
        }
    }

    readonly Dictionary<Transform, float> baseY = new Dictionary<Transform, float>();
    float BaseY(Transform k)
    {
        if (!baseY.TryGetValue(k, out var y)) { y = k.position.y - k.parent.position.y; baseY[k] = y; }
        return y;
    }

    void Collect(int i)
    {
        var k = keys[i];
        RemoveAt(i);
        if (k != null) Destroy(k.gameObject);
        Collected++;
        if (audioSrc != null && pickupClip != null) audioSrc.PlayOneShot(pickupClip, pickupVolume * GameSettings.Fx);
        Debug.Log($"[Keys] picked up key {Collected}/{Total}");
        if (spawnPumpkinPerKey && PumpkinSpawner.Instance != null) PumpkinSpawner.Instance.SpawnOne();
        OnKeyCollected?.Invoke(Collected, Total);
    }

    void RemoveAt(int i)
    {
        keys.RemoveAt(i);
        phases.RemoveAt(i);
        glints.RemoveAt(i);
    }

    // Sharp metallic sting: two inharmonic bell partials + a click, quick decay.
    static AudioClip MakePickupClip()
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * 0.9f);
        var data = new float[n];
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float bell = Mathf.Sin(2f * Mathf.PI * 1318f * t) * Mathf.Exp(-t * 5f) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * 1975f * t) * Mathf.Exp(-t * 7f) * 0.3f
                       + Mathf.Sin(2f * Mathf.PI * 3120f * t) * Mathf.Exp(-t * 11f) * 0.2f
                       + Mathf.Sin(2f * Mathf.PI * 659f * t) * Mathf.Exp(-t * 3f) * 0.25f;
            float click = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 400f) * 0.6f;
            data[i] = Mathf.Clamp((bell + click) * 0.8f, -1f, 1f);
        }
        var clip = AudioClip.Create("KeyPickup", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
