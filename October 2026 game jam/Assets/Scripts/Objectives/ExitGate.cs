using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The exit gate. Built at runtime in the maze's exit gap (wherever the maze put it this run).
//  - Stone posts either side, one iron double gate sized to fill the gap and CUT DOWN THE MIDDLE into a left
//    and right half (by splitting its mesh), each hung on a hinge at its post. One padlock per key hangs on
//    the seam, lanterns sit on top of the posts.
//  - Walk up to it: as many locks as you have keys (not yet used) wiggle and fall off, one by one.
//    With no spare keys, the locks just rattle (it's locked).
//  - When the last lock falls, the gate shudders and both halves swing open (outward). The blocking collider goes.
// Models with missing shaders (pink) get a dark iron material automatically.
// Needs the gate mesh to be readable in builds (Tools/Jam/Make Gate Mesh Readable does it; the editor is fine).
// Win/lose handling hooks onto OnOpened later.
[DefaultExecutionOrder(-40)]
public class ExitGate : MonoBehaviour
{
    public static ExitGate Instance { get; private set; }
    public event System.Action OnOpened;
    public event System.Action<int, int> OnLockRemoved; // (removed, total)
    public event System.Action<int> OnRattled; // (locks still on) player reached the gate without enough keys

    [Header("Models")]
    [Tooltip("A full double gate. It gets split down the middle into two swinging halves.")]
    public GameObject gatePrefab;
    [Tooltip("Optional post/column placed at each side of the gap.")]
    public GameObject postPrefab;
    public GameObject lockPrefab;
    [Tooltip("Optional lantern on top of each post.")]
    public GameObject lanternPrefab;

    [Header("Fit (meters)")]
    public float gateHeight = 2.8f;
    public float postHeight = 3.2f;
    [Tooltip("Posts are slimmed to this width.")]
    public float postWidth = 0.4f;
    [Tooltip("Gap between the gate and each post.")]
    public float clearance = 0.03f;
    [Tooltip("How much the gate may be squeezed / stretched sideways to fill the gap.")]
    public Vector2 widthScaleRange = new Vector2(0.7f, 1.5f);
    [Tooltip("Extra yaw for the gate model if it faces the wrong way (it's auto-detected first).")]
    public float gateYawOffset = 0f;

    [Header("Locks")]
    public float lockSize = 0.22f;
    public float lockSpacing = 0.28f;
    public float lockBaseHeight = 0.85f;
    [Tooltip("How far the locks hang in front of the gate (toward the maze).")]
    public float lockForward = 0.05f;
    public float lockYawOffset = 0f;

    [Header("Unlock")]
    public float unlockDistance = 2.0f;
    public float wiggleTime = 0.45f;
    public float wiggleAngle = 22f;
    public float betweenLocks = 0.2f;

    [Header("Open")]
    public float openAngle = 100f;
    public float openTime = 2.4f;
    public float shudderTime = 0.35f;

    [Header("Lanterns")]
    public Color lanternColor = new Color(1f, 0.62f, 0.3f);
    public float lanternIntensity = 1.6f;
    public float lanternRange = 7f;
    public float lanternSize = 0.4f;

    [Header("Fallback material (for models with missing shaders)")]
    public Color ironColor = new Color(0.22f, 0.2f, 0.19f);

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 0.9f;

    public int TotalLocks => locks.Count + Removed;
    public int Removed { get; private set; }
    public bool IsOpen { get; private set; }

    Transform root, hingeL, hingeR;
    bool singleLeaf;
    BoxCollider blocker;
    readonly List<Transform> locks = new List<Transform>();
    readonly List<Light> lanternLights = new List<Light>();
    readonly List<Vector3> postTops = new List<Vector3>();
    AudioSource src;
    AudioClip clinkClip, clankClip, creakClip, clunkClip;
    Material ironMat;
    bool busy, rattledThisVisit;
    float tileSize = 2.5f;

    void Awake() => Instance = this;

    void Start()
    {
        var gen = MazeGenerator.Instance;
        if (gen == null || gen.Grid == null) { Debug.LogWarning("[Gate] No maze."); return; }
        var g = gen.Grid;
        tileSize = g.tileSize;

        root = new GameObject("ExitGate").transform;
        root.SetParent(transform, false);
        // In the gap in the outer wall, a little toward the maze side; +Z points out of the maze.
        root.position = g.TileToWorld(g.exitTile) - Vector3.forward * (tileSize * 0.25f);
        root.rotation = Quaternion.identity;

        float postW = BuildPosts();
        BuildGate(postW);
        int total = KeyManager.Instance != null ? KeyManager.Instance.Total : 3;
        BuildLocks(total);
        BuildLanterns();

        blocker = root.gameObject.AddComponent<BoxCollider>();
        blocker.center = new Vector3(0f, 1.5f, 0f);
        blocker.size = new Vector3(tileSize, 3f, 0.3f);

        src = root.gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.minDistance = 2f;
        src.maxDistance = 35f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.playOnAwake = false;
        clinkClip = MakeClink();
        clankClip = MakeClank();
        creakClip = GameAudio.Get("Gate_Open");
        if (creakClip == null) creakClip = MakeCreak();
        clunkClip = MakeClunk();

        Debug.Log($"[Gate] built at tile {g.exitTile}: {(singleLeaf ? "single leaf (mesh not readable, couldn't split)" : "split into two halves")}, {locks.Count} locks");
    }

    // ---------------- Build ----------------

    float BuildPosts()
    {
        if (postPrefab == null) return 0f;
        float w = 0f;
        for (int s = -1; s <= 1; s += 2)
        {
            var p = Spawn(postPrefab, root, "Post" + (s < 0 ? "L" : "R"));
            FitHeight(p.transform, postHeight);
            var b = Bounds(p.transform);
            float wide = Mathf.Max(b.size.x, b.size.z);
            if (wide > postWidth)
            {
                // Slim it (keep the height): squeeze the horizontal axes.
                var ls = p.transform.localScale;
                float k = postWidth / wide;
                p.transform.localScale = new Vector3(ls.x * k, ls.y, ls.z * k);
                b = Bounds(p.transform);
            }
            w = Mathf.Max(w, b.size.x);
            PlaceBottomCenter(p.transform, root.position + root.right * s * (tileSize * 0.5f - b.size.x * 0.5f));
            b = Bounds(p.transform);
            postTops.Add(new Vector3(b.center.x, b.max.y, b.center.z));
        }
        return w;
    }

    void BuildGate(float postW)
    {
        if (gatePrefab == null) return;
        float targetW = tileSize - 2f * postW - 2f * clearance;

        // Stretcher: scales along the gap (world X) so the rotated model stretches the right way.
        var stretcher = new GameObject("GateFit").transform;
        stretcher.SetParent(root, false);
        var gate = Spawn(gatePrefab, stretcher, "Gate");
        FixMaterials(gate);
        foreach (var c in gate.GetComponentsInChildren<Collider>(true)) Destroy(c);

        var b0 = Bounds(gate.transform);
        if (b0.size.z > b0.size.x * 1.3f) gate.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * gate.transform.rotation;
        if (Mathf.Abs(gateYawOffset) > 0.01f) gate.transform.rotation = Quaternion.Euler(0f, gateYawOffset, 0f) * gate.transform.rotation;

        FitHeight(gate.transform, gateHeight);
        var b = Bounds(gate.transform);
        float k = Mathf.Clamp(targetW / Mathf.Max(0.01f, b.size.x), widthScaleRange.x, widthScaleRange.y);
        stretcher.localScale = new Vector3(k, 1f, 1f);
        b = Bounds(gate.transform);
        gate.transform.position += new Vector3(root.position.x - b.center.x, root.position.y - b.min.y, root.position.z - b.center.z);
        b = Bounds(gate.transform);

        hingeL = new GameObject("HingeL").transform; hingeL.SetParent(root, false);
        hingeR = new GameObject("HingeR").transform; hingeR.SetParent(root, false);
        hingeL.position = new Vector3(b.min.x, root.position.y, b.center.z);
        hingeR.position = new Vector3(b.max.x, root.position.y, b.center.z);

        // Cut every mesh in the gate down the middle (by triangle position) into left and right halves.
        bool ok = true;
        var filters = gate.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in filters)
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) { ok = false; break; }

        if (!ok || filters.Length == 0)
        {
            Debug.LogWarning("[Gate] Gate mesh isn't readable; run Tools/Jam/Make Gate Mesh Readable. Using one swinging piece for now.");
            singleLeaf = true;
            stretcher.SetParent(hingeL, true);
            return;
        }

        float seamX = b.center.x;
        foreach (var mf in filters)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            SplitInto(mf, mr, seamX, hingeL, hingeR);
        }
        Destroy(stretcher.gameObject);
    }

    // Splits one mesh into the triangles left / right of the seam and parents each half to its hinge.
    static void SplitInto(MeshFilter mf, MeshRenderer mr, float seamX, Transform left, Transform right)
    {
        var mesh = mf.sharedMesh;
        var t = mf.transform;
        var verts = mesh.vertices;
        var normals = mesh.normals;
        var tangents = mesh.tangents;
        var uv = mesh.uv;
        var uv2 = mesh.uv2;
        var colors = mesh.colors;

        for (int side = 0; side < 2; side++)
        {
            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>();
            var nu = new List<Vector2>(); var nu2 = new List<Vector2>(); var nc = new List<Color>();
            var subTris = new List<int>[mesh.subMeshCount];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                subTris[s] = new List<int>();
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 c = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;
                    bool isLeft = t.TransformPoint(c).x < seamX;
                    if (isLeft != (side == 0)) continue;
                    for (int j = 0; j < 3; j++)
                    {
                        int old = tris[i + j];
                        if (!map.TryGetValue(old, out int ni))
                        {
                            ni = nv.Count; map[old] = ni;
                            nv.Add(verts[old]);
                            if (normals.Length > 0) nn.Add(normals[old]);
                            if (tangents.Length > 0) nt.Add(tangents[old]);
                            if (uv.Length > 0) nu.Add(uv[old]);
                            if (uv2.Length > 0) nu2.Add(uv2[old]);
                            if (colors.Length > 0) nc.Add(colors[old]);
                        }
                        subTris[s].Add(ni);
                    }
                }
            }
            if (nv.Count == 0) continue;

            var half = new Mesh { name = mesh.name + (side == 0 ? "_L" : "_R") };
            if (nv.Count > 65000) half.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            half.SetVertices(nv);
            if (nn.Count == nv.Count) half.SetNormals(nn);
            if (nt.Count == nv.Count) half.SetTangents(nt);
            if (nu.Count == nv.Count) half.SetUVs(0, nu);
            if (nu2.Count == nv.Count) half.SetUVs(1, nu2);
            if (nc.Count == nv.Count) half.SetColors(nc);
            half.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++) half.SetTriangles(subTris[s], s);
            half.RecalculateBounds();
            if (nn.Count != nv.Count) half.RecalculateNormals();

            var go = new GameObject(side == 0 ? "GateHalfL" : "GateHalfR");
            go.transform.SetParent(t.parent, false);
            go.transform.localPosition = t.localPosition;
            go.transform.localRotation = t.localRotation;
            go.transform.localScale = t.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = half;
            var r = go.AddComponent<MeshRenderer>();
            if (mr != null) { r.sharedMaterials = mr.sharedMaterials; r.shadowCastingMode = mr.shadowCastingMode; }
            go.transform.SetParent(side == 0 ? left : right, true);
        }
    }

    void BuildLocks(int count)
    {
        if (lockPrefab == null || count <= 0) return;
        float x = 0f;
        if (singleLeaf && hingeR != null) x = (hingeR.position - root.position).x - 0.18f;
        for (int i = 0; i < count; i++)
        {
            var l = Spawn(lockPrefab, root, "Lock" + (i + 1));
            FixMaterials(l);
            foreach (var c in l.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var rb in l.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            // Stand it up if it's lying flat (thinnest along Y): +Z (shackle) goes up, flat face toward the maze.
            var b0 = Bounds(l.transform);
            if (b0.size.y < Mathf.Min(b0.size.x, b0.size.z)) l.transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * l.transform.rotation;
            if (Mathf.Abs(lockYawOffset) > 0.01f) l.transform.rotation = Quaternion.Euler(0f, lockYawOffset, 0f) * l.transform.rotation;
            FitLongest(l.transform, lockSize);

            // Pivot at the top of the lock (the shackle) so the wiggle swings like a hanging padlock.
            var holder = new GameObject("LockHolder" + (i + 1)).transform;
            holder.SetParent(root, false);
            float y = lockBaseHeight + i * lockSpacing;
            var b = Bounds(l.transform);
            holder.position = root.position + root.right * x + Vector3.up * y - root.forward * (lockForward + b.size.z * 0.5f + 0.03f);
            l.transform.position += holder.position - new Vector3(b.center.x, b.max.y, b.center.z);
            l.transform.SetParent(holder, true);
            locks.Add(holder);
        }
    }

    void BuildLanterns()
    {
        for (int s = 0; s < 2; s++)
        {
            Vector3 pos = postTops.Count == 2
                ? postTops[s] + Vector3.up * (lanternSize * 0.5f)
                : root.position + root.right * (s == 0 ? -1f : 1f) * (tileSize * 0.5f) + Vector3.up * 2.2f - root.forward * 0.3f;
            if (lanternPrefab != null)
            {
                var l = Spawn(lanternPrefab, root, "Lantern" + (s == 0 ? "L" : "R"));
                FixMaterials(l);
                foreach (var c in l.GetComponentsInChildren<Collider>(true)) Destroy(c);
                FitHeight(l.transform, lanternSize);
                var b = Bounds(l.transform);
                l.transform.position += pos - b.center;
            }
            var lg = new GameObject("LanternLight" + (s == 0 ? "L" : "R"));
            lg.transform.SetParent(root, false);
            lg.transform.position = pos - root.forward * 0.15f;
            var light = lg.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lanternColor;
            light.intensity = lanternIntensity;
            light.range = lanternRange;
            light.shadows = LightShadows.None;
            lanternLights.Add(light);
        }
    }

    // Replace missing/broken shaders (renders pink) with a dark iron URP material.
    void FixMaterials(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m != null && m.shader != null && m.shader.isSupported && !m.shader.name.Contains("InternalError")) continue;
                mats[i] = IronMaterial();
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    Material IronMaterial()
    {
        if (ironMat != null) return ironMat;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        ironMat = new Material(sh) { name = "GateIron (runtime)" };
        ironMat.SetColor("_BaseColor", ironColor);
        ironMat.SetFloat("_Metallic", 0.75f);
        ironMat.SetFloat("_Smoothness", 0.35f);
        return ironMat;
    }

    // ---------------- Play ----------------

    void Update()
    {
        if (root == null) return;
        for (int i = 0; i < lanternLights.Count; i++)
        {
            float f = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 4f, i * 7.3f);
            lanternLights[i].intensity = lanternIntensity * f;
        }

        if (IsOpen || busy || PumpkinMonster.PlayerIsCaught) return;
        var pc = PlayerController.Instance;
        if (pc == null) return;
        Vector3 d = pc.transform.position - root.position; d.y = 0f;
        bool near = d.magnitude <= unlockDistance && Vector3.Dot(d, root.forward) < 0.5f; // on the maze side
        if (!near) { rattledThisVisit = false; return; }

        int keys = KeyManager.Instance != null ? KeyManager.Instance.Collected : 0;
        int canRemove = Mathf.Min(keys - Removed, locks.Count);
        if (canRemove > 0) StartCoroutine(Unlock(canRemove));
        else if (!rattledThisVisit && locks.Count > 0) { rattledThisVisit = true; StartCoroutine(RattleAll()); }
    }

    IEnumerator Unlock(int count)
    {
        busy = true;
        for (int n = 0; n < count && locks.Count > 0; n++)
        {
            var l = locks[0]; // bottom one first
            locks.RemoveAt(0);
            yield return Wiggle(l, wiggleTime, wiggleAngle, true);
            Play(clunkClip, 0.9f);
            Drop(l);
            Removed++;
            OnLockRemoved?.Invoke(Removed, TotalLocks);
            Debug.Log($"[Gate] lock {Removed}/{TotalLocks} off");
            yield return new WaitForSeconds(betweenLocks);
        }
        if (locks.Count == 0)
        {
            yield return new WaitForSeconds(0.4f);
            yield return Open();
        }
        busy = false;
    }

    IEnumerator RattleAll()
    {
        busy = true;
        OnRattled?.Invoke(locks.Count);
        float t = 0f, dur = 0.35f;
        Play(clinkClip, 0.7f);
        var start = new List<Quaternion>();
        foreach (var l in locks) start.Add(l.localRotation);
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin(t * 50f) * 10f * (1f - t / dur);
            for (int i = 0; i < locks.Count; i++) locks[i].localRotation = start[i] * Quaternion.Euler(0f, 0f, a * (i % 2 == 0 ? 1f : -1f));
            float g = Mathf.Sin(t * 45f) * 0.8f * (1f - t / dur);
            if (hingeL != null) hingeL.localRotation = Quaternion.Euler(0f, -g, 0f);
            if (hingeR != null) hingeR.localRotation = Quaternion.Euler(0f, g, 0f);
            yield return null;
        }
        for (int i = 0; i < locks.Count; i++) locks[i].localRotation = start[i];
        if (hingeL != null) hingeL.localRotation = Quaternion.identity;
        if (hingeR != null) hingeR.localRotation = Quaternion.identity;
        busy = false;
    }

    IEnumerator Wiggle(Transform l, float dur, float angle, bool clinks)
    {
        var start = l.localRotation;
        float t = 0f, nextClink = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            float a = Mathf.Sin(t * 38f) * angle * Mathf.Lerp(0.4f, 1f, k);           // builds up, then pops
            float b = Mathf.Sin(t * 27f + 1.3f) * angle * 0.35f;
            l.localRotation = start * Quaternion.Euler(b, 0f, a);
            if (clinks && t >= nextClink) { Play(clinkClip, 0.5f); nextClink = t + 0.11f; }
            yield return null;
        }
        l.localRotation = start;
    }

    void Drop(Transform l)
    {
        l.SetParent(root, true);
        var b = Bounds(l);
        var box = l.gameObject.AddComponent<BoxCollider>();
        box.center = l.InverseTransformPoint(b.center);
        var ls = l.lossyScale;
        box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), b.size.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), b.size.z / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
        var rb = l.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.6f;
        rb.linearVelocity = -root.forward * 0.6f + Vector3.up * 0.4f + root.right * Random.Range(-0.3f, 0.3f);
        rb.angularVelocity = Random.insideUnitSphere * 8f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        StartCoroutine(PlayLater(clankClip, 0.45f, 1f));
    }

    IEnumerator Open()
    {
        Play(clunkClip, 1f);
        // Shudder: the halves jolt as the last lock lets go.
        float t = 0f;
        while (t < shudderTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin(t * 60f) * 1.5f * (1f - t / shudderTime);
            if (hingeL != null) hingeL.localRotation = Quaternion.Euler(0f, -a, 0f);
            if (hingeR != null) hingeR.localRotation = Quaternion.Euler(0f, a, 0f);
            yield return null;
        }
        Play(creakClip, 1f);
        if (blocker != null) blocker.enabled = false;
        // Both halves swing outward (+Z, away from the player): slow start, heavy, easing to a stop.
        t = 0f;
        while (t < openTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / openTime);
            float e = k * k * (3f - 2f * k); // smooth in and out
            float a = openAngle * e;
            if (hingeL != null) hingeL.localRotation = Quaternion.Euler(0f, -a, 0f);
            if (hingeR != null && !singleLeaf) hingeR.localRotation = Quaternion.Euler(0f, a, 0f);
            yield return null;
        }
        IsOpen = true;
        Debug.Log("[Gate] open");
        OnOpened?.Invoke();
    }

    // ---------------- Helpers ----------------

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

    static void FitLongest(Transform t, float size)
    {
        var b = Bounds(t);
        float m = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (m > 0.0001f) t.localScale *= size / m;
    }

    static void PlaceBottomCenter(Transform t, Vector3 groundPoint)
    {
        var b = Bounds(t);
        t.position += new Vector3(groundPoint.x - b.center.x, groundPoint.y - b.min.y, groundPoint.z - b.center.z);
    }

    void Play(AudioClip c, float v) { if (src != null && c != null) src.PlayOneShot(c, v * volume * GameSettings.Fx); }
    IEnumerator PlayLater(AudioClip c, float delay, float v) { yield return new WaitForSeconds(delay); Play(c, v); }

    // ---------------- Synth sounds (placeholders until real recordings) ----------------

    static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * seconds);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            data[i] = Mathf.Clamp(f(t, (float)(rng.NextDouble() * 2 - 1)), -1f, 1f);
        }
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Small metal tick (lock rattling against bars).
    static AudioClip MakeClink() => Make("Clink", 0.18f, (t, n) =>
        (Mathf.Sin(2f * Mathf.PI * 2900f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 4300f * t) * 0.3f) * Mathf.Exp(-t * 35f) + n * Mathf.Exp(-t * 500f) * 0.4f);

    // Heavy padlock hitting the ground.
    static AudioClip MakeClank() => Make("Clank", 0.6f, (t, n) =>
        (Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 1370f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 2210f * t) * 0.2f) * Mathf.Exp(-t * 9f)
        + Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 25f) * 0.6f + n * Mathf.Exp(-t * 120f) * 0.5f);

    // Shackle popping open.
    static AudioClip MakeClunk() => Make("Clunk", 0.35f, (t, n) =>
        Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 22f) * 0.8f + Mathf.Sin(2f * Mathf.PI * 1600f * t) * Mathf.Exp(-t * 40f) * 0.35f + n * Mathf.Exp(-t * 200f) * 0.5f);

    // Placeholder hinge creak (to be replaced with a recorded sound).
    static AudioClip MakeCreak() => Make("Creak", 2.6f, (t, n) =>
    {
        float f = 260f + 110f * Mathf.Sin(t * 1.7f) + 40f * Mathf.Sin(t * 5.3f);
        float phase = f * t;
        float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
        float stick = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 23f + Mathf.Sin(t * 3f) * 2f));
        float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((2.6f - t) / 0.6f);
        return (saw * 0.35f + n * 0.12f) * stick * env;
    });
}
