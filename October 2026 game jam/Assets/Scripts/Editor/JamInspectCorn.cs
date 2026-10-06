using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// One-shot: inspects the corn FBX and writes a report as a comment into CornReport.cs
public static class JamInspectCorn
{
    const string ModelPath = "Assets/fbx/fbx.FBX";
    const string OutPath = "Assets/Scripts/Editor/CornReport.cs";

    [InitializeOnLoadMethod]
    static void Run()
    {
        if (File.Exists(OutPath)) return;
        var sb = new StringBuilder();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (root == null) { sb.AppendLine("model not found"); Write(sb); return; }

        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null)
            sb.AppendLine($"importer: globalScale={importer.globalScale} useFileScale={importer.useFileScale} fileScale={importer.fileScale} materialImportMode={importer.materialImportMode} materialLocation={importer.materialLocation}");

        sb.AppendLine("hierarchy:");
        Dump(root.transform, 0, sb);

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds total = new Bounds();
        bool first = true;
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
            if (mesh == null) continue;
            // bounds in root space
            var m = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var b = TransformBounds(mesh.bounds, m);
            if (first) { total = b; first = false; } else total.Encapsulate(b);
        }
        sb.AppendLine($"total bounds (root space): center={total.center} size={total.size}");

        sb.AppendLine("meshes:");
        foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>())
            sb.AppendLine($"  {mesh.name}: verts={mesh.vertexCount} tris={mesh.triangles.Length / 3} submeshes={mesh.subMeshCount} bounds={mesh.bounds.size} uv={(mesh.uv != null && mesh.uv.Length > 0)} colors={(mesh.colors != null && mesh.colors.Length > 0)}");

        sb.AppendLine("materials:");
        foreach (var r in renderers)
            sb.AppendLine($"  {r.name}: " + string.Join(", ", r.sharedMaterials.Select(mat => mat == null ? "null" : $"{mat.name} [{mat.shader.name}] tex={(mat.mainTexture ? mat.mainTexture.name : "none")}")));

        sb.AppendLine("embedded textures / other assets:");
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ModelPath).Where(a => a != null && !(a is Mesh) && !(a is GameObject) && !(a is Transform) && !(a is MeshFilter) && !(a is MeshRenderer)))
            sb.AppendLine($"  {a.GetType().Name}: {a.name}");

        Write(sb);
    }

    static void Dump(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 3) return;
        string comps = string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name));
        sb.AppendLine($"{new string(' ', depth * 2)}- {t.name} pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale} [{comps}] children={t.childCount}");
        int shown = 0;
        foreach (Transform c in t)
        {
            if (shown++ >= 12) { sb.AppendLine($"{new string(' ', depth * 2 + 2)}... ({t.childCount - 12} more)"); break; }
            Dump(c, depth + 1, sb);
        }
    }

    static Bounds TransformBounds(Bounds b, Matrix4x4 m)
    {
        var c = m.MultiplyPoint3x4(b.center);
        var e = b.extents;
        var ax = m.MultiplyVector(new Vector3(e.x, 0, 0));
        var ay = m.MultiplyVector(new Vector3(0, e.y, 0));
        var az = m.MultiplyVector(new Vector3(0, 0, e.z));
        var ext = new Vector3(
            Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
            Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
            Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
        return new Bounds(c, ext * 2f);
    }

    static void Write(StringBuilder sb)
    {
        File.WriteAllText(OutPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
    }
}
