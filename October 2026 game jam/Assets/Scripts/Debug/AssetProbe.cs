using System.Linq;
using System.Text;
using UnityEngine;

// Temporary debug helper: in Play mode, spawns each prefab far below the map, logs its hierarchy,
// sizes and materials to the console, then destroys it. Safe to delete.
public class AssetProbe : MonoBehaviour
{
    public GameObject[] prefabs;

    void Start()
    {
        if (prefabs == null) return;
        foreach (var p in prefabs)
        {
            if (p == null) continue;
            var go = Instantiate(p, new Vector3(0f, -500f, 0f), Quaternion.identity);
            var sb = new StringBuilder();
            sb.Append($"[Probe] {p.name}");
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                sb.Append($" | bounds size={F(b.size)} centerOffset={F(b.center - go.transform.position)} minY={b.min.y - go.transform.position.y:0.###}");
            }
            sb.Append(" ||");
            Dump(go.transform, go.transform, 0, sb);
            Debug.Log(sb.ToString());
            Destroy(go);
        }
    }

    static void Dump(Transform t, Transform root, int depth, StringBuilder sb)
    {
        if (depth > 5) return;
        var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name);
        sb.Append($" {new string('>', depth)}{t.name} lp={F(t.localPosition)} lr={F(t.localEulerAngles)} ls={F(t.localScale)} [{string.Join(",", comps)}]");
        var r = t.GetComponent<Renderer>();
        if (r != null)
        {
            var b = r.bounds;
            sb.Append($" rb={F(b.size)}@{F(b.center - root.position)} mats=" + string.Join("+", r.sharedMaterials.Select(m => m ? m.name + "(" + m.shader.name + ")" : "null")));
        }
        sb.Append(";");
        foreach (Transform c in t) Dump(c, root, depth + 1, sb);
    }

    static string F(Vector3 v) => $"({v.x:0.##},{v.y:0.##},{v.z:0.##})";
}
