using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Splits the scattered corn-patch FBX into individual stalks, saves a set of varied
// stalks as standalone Y-up meshes + prefabs, and creates a dried/dark corn palette.
public static class JamSplitCorn
{
    const string ModelPath = "Assets/fbx/fbx.FBX";
    const string Root = "Assets/Corn";
    const string ReportPath = "Assets/Scripts/Editor/CornSplitReport.cs";
    const int VariantCount = 8;
    const int StemSub = 2;
    const float SeedMinHeight = 1.0f;
    static readonly string[] SlotNames = { "Leaves1", "Leaves2", "Stem", "StemCap", "Ear", "Husk", "Tassel" };
    static readonly bool[] TwoSided = { true, true, false, false, false, true, true };

    // Dried late-October corn. Muted tans/olives that read as dead under cold moonlight.
    static readonly Color[] PaletteDry =
    {
        new Color(0.42f, 0.38f, 0.22f), new Color(0.34f, 0.33f, 0.19f), new Color(0.40f, 0.36f, 0.21f),
        new Color(0.30f, 0.26f, 0.16f), new Color(0.55f, 0.45f, 0.24f), new Color(0.58f, 0.50f, 0.33f), new Color(0.52f, 0.44f, 0.27f)
    };
    // Rotting variant: darker, browner, for mixing in.
    static readonly Color[] PaletteRot =
    {
        new Color(0.30f, 0.26f, 0.15f), new Color(0.24f, 0.23f, 0.14f), new Color(0.29f, 0.25f, 0.15f),
        new Color(0.21f, 0.18f, 0.11f), new Color(0.38f, 0.30f, 0.17f), new Color(0.41f, 0.35f, 0.23f), new Color(0.37f, 0.31f, 0.19f)
    };

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (File.Exists(ReportPath)) return;
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Jam/Split Corn Into Stalks")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Run; return; }
        if (File.Exists(ReportPath)) return;
        var sb = new StringBuilder();
        try { Split(sb); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText(ReportPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
        AssetDatabase.Refresh();
    }

    static int[] parent;
    static int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
    static void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }

    class Comp
    {
        public List<int> tris = new List<int>(); // (submesh << 24) | triangleNumber
        public Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        public bool hasStem;
        public int stalk = -1;
    }

    static void Split(StringBuilder sb)
    {
        var mesh = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().FirstOrDefault();
        if (mesh == null) { sb.AppendLine("mesh not found"); return; }
        var v = mesh.vertices; var nrm = mesh.normals; var uv = mesh.uv; var col = mesh.colors;
        int vc = v.Length, subs = mesh.subMeshCount;
        var tris = new int[subs][];
        for (int s = 0; s < subs; s++) tris[s] = mesh.GetTriangles(s);
        sb.AppendLine($"source: verts={vc} submeshes={subs}");

        // 1) Weld coincident vertices + union triangles -> connected pieces.
        parent = new int[vc];
        for (int i = 0; i < vc; i++) parent[i] = i;
        var weld = new Dictionary<(int, int, int), int>(vc);
        for (int i = 0; i < vc; i++)
        {
            var key = (Mathf.RoundToInt(v[i].x * 10000f), Mathf.RoundToInt(v[i].y * 10000f), Mathf.RoundToInt(v[i].z * 10000f));
            if (weld.TryGetValue(key, out int j)) Union(j, i); else weld[key] = i;
        }
        weld = null;
        for (int s = 0; s < subs; s++)
            for (int t = 0; t < tris[s].Length; t += 3) { Union(tris[s][t], tris[s][t + 1]); Union(tris[s][t], tris[s][t + 2]); }

        var compIndex = new Dictionary<int, int>();
        var comps = new List<Comp>();
        for (int s = 0; s < subs; s++)
        {
            var tr = tris[s];
            for (int t = 0; t < tr.Length; t += 3)
            {
                int r = Find(tr[t]);
                if (!compIndex.TryGetValue(r, out int ci)) { ci = comps.Count; compIndex[r] = ci; comps.Add(new Comp()); }
                var c = comps[ci];
                c.tris.Add((s << 24) | (t / 3));
                if (s == StemSub) c.hasStem = true;
                for (int k = 0; k < 3; k++) { var p = v[tr[t + k]]; c.min = Vector3.Min(c.min, p); c.max = Vector3.Max(c.max, p); }
            }
        }
        sb.AppendLine($"connected pieces: {comps.Count}");

        // 2) Seeds = tall stem pieces. Each becomes one stalk. (Mesh is Z-up.)
        var seeds = comps.Where(c => c.hasStem && (c.max.z - c.min.z) >= SeedMinHeight).ToList();
        for (int i = 0; i < seeds.Count; i++) seeds[i].stalk = i;
        sb.AppendLine($"stalks found: {seeds.Count}");
        if (seeds.Count == 0) return;

        // Spatial hash of seed stem vertices for nearest-stem lookups.
        const float cell = 0.1f;
        var grid = new Dictionary<(int, int, int), List<(Vector3 p, int stalk)>>();
        foreach (var sd in seeds)
            foreach (var code in sd.tris)
            {
                int s = code >> 24, tn = code & 0xFFFFFF;
                if (s != StemSub) continue;
                for (int k = 0; k < 3; k++)
                {
                    var p = v[tris[s][tn * 3 + k]];
                    var key = (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
                    if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<(Vector3, int)>();
                    if (list.Count < 24) list.Add((p, sd.stalk));
                }
            }

        // 3) Attach every other piece (leaves, ears, husks, short stem bits) to the stalk whose stem it touches.
        int orphans = 0;
        var seedCenters = seeds.Select(sd => new Vector2((sd.min.x + sd.max.x) * 0.5f, (sd.min.y + sd.max.y) * 0.5f)).ToArray();
        foreach (var c in comps)
        {
            if (c.stalk >= 0) continue;
            float best = float.MaxValue; int bestStalk = -1;
            int step = Mathf.Max(1, c.tris.Count / 150);
            for (int i = 0; i < c.tris.Count; i += step)
            {
                int s = c.tris[i] >> 24, tn = c.tris[i] & 0xFFFFFF;
                var p = v[tris[s][tn * 3]];
                int gx = Mathf.FloorToInt(p.x / cell), gy = Mathf.FloorToInt(p.y / cell), gz = Mathf.FloorToInt(p.z / cell);
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) for (int dz = -2; dz <= 2; dz++)
                    if (grid.TryGetValue((gx + dx, gy + dy, gz + dz), out var list))
                        foreach (var e in list)
                        {
                            float d = (e.p - p).sqrMagnitude;
                            if (d < best) { best = d; bestStalk = e.stalk; }
                        }
            }
            if (bestStalk < 0)
            {
                orphans++;
                var cc = new Vector2((c.min.x + c.max.x) * 0.5f, (c.min.y + c.max.y) * 0.5f);
                float bd = float.MaxValue;
                for (int i = 0; i < seedCenters.Length; i++) { float d = (seedCenters[i] - cc).sqrMagnitude; if (d < bd) { bd = d; bestStalk = i; } }
            }
            c.stalk = bestStalk;
        }
        sb.AppendLine($"pieces attached by fallback (no nearby stem): {orphans}");

        // 4) Gather stalk stats.
        var stalkPieces = new List<Comp>[seeds.Count];
        for (int i = 0; i < seeds.Count; i++) stalkPieces[i] = new List<Comp>();
        foreach (var c in comps) stalkPieces[c.stalk].Add(c);
        var stats = Enumerable.Range(0, seeds.Count).Select(i =>
        {
            var pcs = stalkPieces[i];
            int triCount = pcs.Sum(p => p.tris.Count);
            float minZ = pcs.Min(p => p.min.z), maxZ = pcs.Max(p => p.max.z);
            bool hasEar = pcs.Any(p => p.tris.Any(code => (code >> 24) == 4));
            return (index: i, tris: triCount, height: maxZ - minZ, hasEar);
        }).OrderBy(x => x.tris).ToList();
        var triList = stats.Select(x => x.tris).OrderBy(x => x).ToList();
        sb.AppendLine($"tris per stalk: min={triList.First()} median={triList[triList.Count / 2]} max={triList.Last()}");
        sb.AppendLine($"heights: min={stats.Min(x => x.height):F2} max={stats.Max(x => x.height):F2}");

        int median = triList[triList.Count / 2];
        var candidates = stats.Where(x => x.height >= 1.8f && x.tris >= median * 0.6f && x.tris <= median * 2.5f).ToList();
        if (candidates.Count < VariantCount) candidates = stats.Where(x => x.height >= 1.5f).ToList();
        if (candidates.Count < VariantCount) candidates = stats;
        var chosen = new List<(int index, int tris, float height, bool hasEar)>();
        for (int k = 0; k < VariantCount && k < candidates.Count; k++)
            chosen.Add(candidates[(int)((k + 0.5f) * candidates.Count / VariantCount)]);

        // 5) Folders + materials.
        EnsureFolder("Assets", "Corn"); EnsureFolder(Root, "Meshes"); EnsureFolder(Root, "Materials"); EnsureFolder(Root, "Prefabs");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var dryMats = new Material[subs];
        var rotMats = new Material[subs];
        for (int s = 0; s < subs; s++)
        {
            string slot = s < SlotNames.Length ? SlotNames[s] : "Slot" + s;
            bool twoSided = s < TwoSided.Length && TwoSided[s];
            dryMats[s] = MakeMat($"Corn_Dry_{slot}", s < PaletteDry.Length ? PaletteDry[s] : PaletteDry[0], twoSided, lit);
            rotMats[s] = MakeMat($"Corn_Rot_{slot}", s < PaletteRot.Length ? PaletteRot[s] : PaletteRot[0], twoSided, lit);
        }

        // 6) Build Y-up meshes, base at origin, + prefabs.
        sb.AppendLine("variants:");
        for (int k = 0; k < chosen.Count; k++)
        {
            var st = chosen[k];
            var pcs = stalkPieces[st.index];
            var seed = seeds[st.index];
            float minZ = pcs.Min(p => p.min.z);
            // Base center = average XY of the lowest stem vertices.
            Vector2 baseXY = Vector2.zero; int baseN = 0;
            foreach (var code in seed.tris)
            {
                int s = code >> 24, tn = code & 0xFFFFFF;
                if (s != StemSub) continue;
                for (int q = 0; q < 3; q++) { var p = v[tris[s][tn * 3 + q]]; if (p.z < seed.min.z + 0.15f) { baseXY += new Vector2(p.x, p.y); baseN++; } }
            }
            if (baseN > 0) baseXY /= baseN; else baseXY = new Vector2((seed.min.x + seed.max.x) * 0.5f, (seed.min.y + seed.max.y) * 0.5f);

            var remap = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var ncol = new List<Color>();
            var subTris = new List<int>[subs];
            for (int s = 0; s < subs; s++) subTris[s] = new List<int>();
            foreach (var pc in pcs)
                foreach (var code in pc.tris)
                {
                    int s = code >> 24, tn = code & 0xFFFFFF;
                    for (int q = 0; q < 3; q++)
                    {
                        int oi = tris[s][tn * 3 + q];
                        if (!remap.TryGetValue(oi, out int ni))
                        {
                            ni = nv.Count; remap[oi] = ni;
                            var p = v[oi];
                            nv.Add(new Vector3(p.x - baseXY.x, p.z - minZ, -(p.y - baseXY.y)));
                            if (nrm != null && nrm.Length == vc) { var n = nrm[oi]; nn.Add(new Vector3(n.x, n.z, -n.y)); }
                            if (uv != null && uv.Length == vc) nuv.Add(uv[oi]);
                            if (col != null && col.Length == vc) ncol.Add(col[oi]);
                        }
                        subTris[s].Add(ni);
                    }
                }

            var m = new Mesh { name = $"CornStalk_{k}", indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(nv);
            if (nn.Count == nv.Count) m.SetNormals(nn);
            if (nuv.Count == nv.Count) m.SetUVs(0, nuv);
            if (ncol.Count == nv.Count) m.SetColors(ncol);
            m.subMeshCount = subs;
            for (int s = 0; s < subs; s++) m.SetTriangles(subTris[s], s, false);
            if (nn.Count != nv.Count) m.RecalculateNormals();
            m.RecalculateBounds();
            string meshPath = $"{Root}/Meshes/CornStalk_{k}.asset";
            AssetDatabase.CreateAsset(m, meshPath);

            var go = new GameObject($"CornStalk_{k}");
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = (k % 3 == 2) ? rotMats : dryMats; // mix in some rotting stalks
            mr.shadowCastingMode = ShadowCastingMode.On;
            PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/CornStalk_{k}.prefab");
            Object.DestroyImmediate(go);

            sb.AppendLine($"  CornStalk_{k}: tris={st.tris} verts={nv.Count} height={st.height:F2} ear={st.hasEar} bounds={m.bounds.size} palette={((k % 3 == 2) ? "rot" : "dry")}");
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine("done");
    }

    static Material MakeMat(string name, Color c, bool twoSided, Shader lit)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.08f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string parentPath, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parentPath}/{name}")) AssetDatabase.CreateFolder(parentPath, name);
    }
}
