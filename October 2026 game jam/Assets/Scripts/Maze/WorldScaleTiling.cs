using UnityEngine;

// Keeps a plane's texture at a fixed real-world size no matter how the plane is scaled
// (the maze generator resizes the ground to fit each maze).
[RequireComponent(typeof(Renderer))]
public class WorldScaleTiling : MonoBehaviour
{
    [Tooltip("How many meters one repeat of the texture covers.")]
    public float metersPerTile = 3f;
    [Tooltip("Size of the mesh in local units (Unity's Plane is 10x10).")]
    public Vector2 meshSize = new Vector2(10f, 10f);

    void Start() => Apply(); // runs after MazeGenerator has resized the ground in Awake

    public void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null || r.sharedMaterial == null) return;
        var s = transform.lossyScale;
        var tiling = new Vector2(meshSize.x * s.x / metersPerTile, meshSize.y * s.z / metersPerTile);
        // Instance the material at runtime so the asset itself isn't modified.
        var mat = Application.isPlaying ? r.material : r.sharedMaterial;
        mat.SetTextureScale("_BaseMap", tiling);
    }
}
