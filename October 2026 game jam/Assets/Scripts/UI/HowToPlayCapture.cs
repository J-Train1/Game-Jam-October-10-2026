#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// EDITOR ONLY. Takes the photographs for the HOW TO PLAY pages straight from the game camera.
// In Play Mode in the Maze scene: Tools > How To Play > Capture Screenshots (or add this component to anything).
// The player (and a pumpkin, for two of the shots) is posed around the maze, the camera renders each view with the
// game's look, and the pictures are saved to Assets/Resources/HowToPlay/Shot_*.jpg. Stop Play Mode afterwards.
public class HowToPlayCapture : MonoBehaviour
{
    public const string Folder = "Assets/Resources/HowToPlay";
    const int W = 1600, H = 900;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    void Awake()
    {
        CaptureAll();
        Destroy(this);
    }

    public static void CaptureAll()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HowToPlay] Enter Play Mode in the Maze scene first."); return; }
        var gen = MazeGenerator.Instance;
        var pc = PlayerController.Instance;
        var cam = Camera.main;
        if (gen == null || gen.Grid == null || pc == null || cam == null) { Debug.LogWarning("[HowToPlay] No maze / player / camera found."); return; }
        var g = gen.Grid;
        Directory.CreateDirectory(Folder);

        var fl = Flashlight.Instance;
        var pumpkin = GetPumpkin();
        var hidden = new List<Renderer>();
        int saved = 0;

        // 1. The goal: standing at the start, looking down the longest row.
        var (startDir, _) = LongestRun(g, g.startTile);
        HidePumpkins(hidden);
        Pose(pc, cam, fl, Floor(g, g.startTile), Floor(g, g.startTile + startDir * 4) + Vector3.up * 1.2f);
        SetLight(fl, true);
        saved += Save(cam, "Shot_Maze");

        // 2. A key at the end of its row.
        var km = KeyManager.Instance;
        if (km != null && km.Keys.Count > 0)
        {
            var key = km.Keys[0];
            var kt = g.WorldToTile(key.position);
            Vector2Int d = Vector2Int.zero;
            foreach (var dd in Dirs) if (g.IsOpen(kt + dd)) { d = dd; break; }
            // Close enough to see it glint: one tile back from the dead end.
            Vector3 from = Floor(g, kt) + new Vector3(d.x, 0f, d.y) * g.tileSize * 1.0f;
            Pose(pc, cam, fl, from, key.position + Vector3.down * 0.1f);
            FaceCamera(key, cam.transform);
            Debug.Log($"[HowToPlay] key at {key.position} (tile {kt}), camera from {from}");
            SetLight(fl, true, 0.4f);
            saved += Save(cam, "Shot_Key", 40f);
        }
        else Debug.LogWarning("[HowToPlay] No keys in the maze for Shot_Key.");

        // 3 + 4. A pumpkin caught in the beam up close, and one further down a row.
        var rows = LongRows(g, 5);
        if (pumpkin != null && rows.Count > 0)
        {
            var (t0, d0) = rows[0];
            ShowPumpkins(hidden);
            HideOtherPumpkins(pumpkin, hidden);
            Vector3 pp = g.TileToWorld(t0) + new Vector3(d0.x, 0f, d0.y) * g.tileSize * 1.6f;
            PlacePumpkin(pumpkin, pp, Floor(g, t0));
            Pose(pc, cam, fl, Floor(g, t0), pp + Vector3.up * 1.45f);
            SetLight(fl, true);
            saved += Save(cam, "Shot_Freeze", 52f);
            Icon(cam, fl, pumpkin, Floor(g, t0));

            var (t1, d1) = rows.Count > 1 ? rows[Mathf.Min(rows.Count - 1, rows.Count / 2)] : rows[0];
            int far = Mathf.Min(3, Run(g, t1, d1));
            PlacePumpkin(pumpkin, g.TileToWorld(t1 + d1 * far), Floor(g, t1));
            Pose(pc, cam, fl, Floor(g, t1), Floor(g, t1 + d1 * far) + Vector3.up * 1.4f);
            SetLight(fl, true);
            saved += Save(cam, "Shot_Hunt", 50f);
        }
        else Debug.LogWarning("[HowToPlay] No pumpkin (or no long row) for Shot_Freeze / Shot_Hunt.");

        // 5. The gate, from a few steps inside.
        HidePumpkins(hidden);
        {
            Vector2Int d = g.exitTile - g.exitInsideTile;
            int n = Mathf.Clamp(Run(g, g.exitInsideTile, -d), 0, 1);
            Pose(pc, cam, fl, Floor(g, g.exitInsideTile - d * n), Floor(g, g.exitTile) + Vector3.up * 1.3f);
            SetLight(fl, true, n == 0 ? 0.3f : 0.6f); // the gate's own lanterns do most of the work up close
            saved += Save(cam, "Shot_Gate");
        }

        // 6. In the dark, light off.
        {
            var (t2, d2) = rows.Count > 2 ? rows[rows.Count - 1] : (g.startTile, -startDir);
            if (!g.IsOpen(t2 + d2)) d2 = startDir;
            Pose(pc, cam, fl, Floor(g, t2), Floor(g, t2 + d2 * 3) + Vector3.up * 1.4f);
            SetLight(fl, false);
            saved += Save(cam, "Shot_Dark");
        }

        // Put things back roughly where they were.
        ShowPumpkins(hidden);
        SetLight(fl, true);
        pc.Respawn(Floor(g, g.startTile), Quaternion.identity);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[HowToPlay] saved {saved} screenshots to {Folder}. Stop Play Mode now.");
    }

    // ---------------- maze helpers ----------------

    static Vector3 Floor(MazeGrid g, Vector2Int t) => g.TileToWorld(t) + Vector3.up * 0.05f;

    static int Run(MazeGrid g, Vector2Int t, Vector2Int d)
    {
        int n = 0;
        while (g.IsOpen(t + d * (n + 1)) && n < 50) n++;
        return n;
    }

    static (Vector2Int, int) LongestRun(MazeGrid g, Vector2Int t)
    {
        Vector2Int best = Vector2Int.up; int bn = -1;
        foreach (var d in Dirs) { int n = Run(g, t, d); if (n > bn) { bn = n; best = d; } }
        return (best, bn);
    }

    // Straight rows at least minLen tiles long (start tile + direction), longest first, spread out.
    static List<(Vector2Int, Vector2Int)> LongRows(MazeGrid g, int minLen)
    {
        var all = new List<(Vector2Int t, Vector2Int d, int n)>();
        for (int x = 0; x < g.width; x++)
        for (int y = 0; y < g.height; y++)
        {
            var t = new Vector2Int(x, y);
            if (!g.IsOpen(t)) continue;
            if (x == 0 || y == 0 || x == g.width - 1 || y == g.height - 1) continue; // the gate's opening in the outer wall
            foreach (var d in Dirs)
            {
                if (g.IsOpen(t - d)) continue; // start at the beginning of the row
                int n = Run(g, t, d);
                if (n >= minLen) all.Add((t, d, n));
            }
        }
        all.Sort((a, b) => b.n.CompareTo(a.n));
        var res = new List<(Vector2Int, Vector2Int)>();
        foreach (var r in all)
        {
            bool near = false;
            foreach (var q in res) if ((q.Item1 - r.t).sqrMagnitude < 36) near = true;
            if (!near) res.Add((r.t, r.d));
        }
        return res;
    }

    // ---------------- posing ----------------

    static void Pose(PlayerController pc, Camera cam, Flashlight fl, Vector3 feet, Vector3 lookAt)
    {
        Vector3 flat = lookAt - feet; flat.y = 0f;
        if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
        pc.Respawn(feet, Quaternion.LookRotation(flat.normalized));
        var root = pc.CameraRoot != null ? pc.CameraRoot : cam.transform;
        Vector3 to = lookAt - root.position;
        float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        root.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        if (fl != null)
        {
            var ct = cam.transform;
            fl.transform.SetPositionAndRotation(ct.position + ct.rotation * fl.offset, ct.rotation);
        }
        Physics.SyncTransforms();
    }

    static void SetLight(Flashlight fl, bool on, float power = 1f)
    {
        if (fl == null) return;
        fl.AddBattery(fl.maxBattery);
        if (fl.IsOn != on) fl.Toggle();
        var l = fl.GetComponent<Light>();
        if (l != null) { l.enabled = on; if (on) l.intensity = fl.intensity * power; }
    }

    // The key spins; turn it so its flat side (not its edge) faces the camera.
    static void FaceCamera(Transform holder, Transform cam)
    {
        if (holder.childCount == 0) return;
        var model = holder.GetChild(0);
        var rends = model.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        float best = -1f, bestAngle = 0f;
        for (int a = 0; a < 180; a += 6)
        {
            float w = Width(rends, cam.right);
            if (w > best) { best = w; bestAngle = a; }
            model.RotateAround(holder.position, Vector3.up, 6f);
        }
        // back where it started (180 degrees turned), then on to the widest angle
        model.RotateAround(holder.position, Vector3.up, 180f + bestAngle);
    }

    static float Width(Renderer[] rends, Vector3 axis)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var r in rends)
        {
            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float v = Vector3.Dot(r.transform.TransformPoint(c), axis);
                    lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
                }
            }
        }
        return hi - lo;
    }

    static PumpkinMonster GetPumpkin()
    {
        foreach (var m in PumpkinMonster.All) if (m != null && m.gameObject.activeInHierarchy) return m;
        var sp = Object.FindFirstObjectByType<PumpkinSpawner>(FindObjectsInactive.Include);
        return sp != null ? sp.SpawnOne() : null;
    }

    static void PlacePumpkin(PumpkinMonster m, Vector3 p, Vector3 facing)
    {
        m.ResetTo(p);
        Vector3 d = facing - p; d.y = 0f;
        m.transform.rotation = Quaternion.LookRotation(d.normalized);
        foreach (var a in m.GetComponentsInChildren<Animator>()) a.Update(0f);
    }

    static void HidePumpkins(List<Renderer> hidden)
    {
        foreach (var m in PumpkinMonster.All)
            if (m != null)
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
    }

    static void HideOtherPumpkins(PumpkinMonster keep, List<Renderer> hidden)
    {
        foreach (var m in PumpkinMonster.All)
            if (m != null && m != keep)
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
    }

    static void ShowPumpkins(List<Renderer> hidden)
    {
        foreach (var r in hidden) if (r != null) r.enabled = true;
        hidden.Clear();
    }

    // ---------------- rendering ----------------

    // The game's icon: the pumpkin's carved face up close, caught in the flashlight. Saved to Assets/Icon/GameIcon.png.
    public const string IconPath = "Assets/Icon/GameIcon.png";
    static void Icon(Camera cam, Flashlight fl, PumpkinMonster m, Vector3 from)
    {
        Transform head = null;
        foreach (var a in m.GetComponentsInChildren<Animator>()) if (a.isHuman) head = a.GetBoneTransform(HumanBodyBones.Head);
        Vector3 hp;
        if (head != null) hp = head.position + Vector3.up * 0.12f;
        else
        {
            var rs = m.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            hp = new Vector3(b.center.x, b.max.y - 0.3f, b.center.z);
        }
        Vector3 toCam = from - hp; toCam.y = 0f; toCam.Normalize();
        var ct = cam.transform;
        Vector3 oldPos = ct.position; Quaternion oldRot = ct.rotation;
        Vector3 camPos = hp + toCam * 1.25f + Vector3.down * 0.18f;      // a little below: it looms
        ct.SetPositionAndRotation(camPos, Quaternion.LookRotation(hp - camPos));
        if (fl != null) fl.transform.SetPositionAndRotation(ct.position + ct.rotation * fl.offset, ct.rotation);
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
        Render(cam, 1024, 1024, 30f, IconPath, true);
        ct.SetPositionAndRotation(oldPos, oldRot);
    }

    static int Save(Camera cam, string name, float fov = 0f)
    {
        Render(cam, W, H, fov, Path.Combine(Folder, name + ".jpg"), false);
        return 1;
    }

    static void Render(Camera cam, int W, int H, float fov, string path, bool png)
    {
        float oldFov = cam.fieldOfView;
        if (fov > 0f) cam.fieldOfView = fov;
        var desc = new RenderTextureDescriptor(W, H, GraphicsFormat.R8G8B8A8_SRGB, 24) { msaaSamples = 1 };
        var rt = RenderTexture.GetTemporary(desc);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prevTarget;
        cam.fieldOfView = oldFov;

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        // The game is very dark; lift the shadows a little so the pictures read at menu size.
        var lut = new byte[256];
        for (int i = 0; i < 256; i++) lut[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(i / 255f, 0.72f) * 1.25f * 255f), 0, 255);
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++) { var c = px[i]; px[i] = new Color32(lut[c.r], lut[c.g], lut[c.b], 255); }
        tex.SetPixels32(px);
        tex.Apply();
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        File.WriteAllBytes(path, png ? tex.EncodeToPNG() : tex.EncodeToJPG(90));
        Object.DestroyImmediate(tex);
    }

}
#endif
