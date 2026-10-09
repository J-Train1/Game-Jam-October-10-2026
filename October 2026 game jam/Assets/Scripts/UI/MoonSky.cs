using UnityEngine;

// A big moon in the sky with a soft halo. Put it on the moonlight (directional light): the moon sits exactly
// where the light comes from, so the light "points at the field" from the moon. It stays at the same spot in
// the sky wherever you walk (it follows the camera at a fixed distance, like it's infinitely far away), and it
// isn't swallowed by the fog. Corn and walls in front of it still hide it.
// Drawn with Assets/Resources/Shaders/JamMoon.shader (unlit, no fog, HDR so the bloom makes it glow).
public class MoonSky : MonoBehaviour
{
    [Tooltip("How far from the camera it's drawn (keep it under the camera's far clip).")]
    public float distance = 60f;
    [Tooltip("Moon diameter at that distance (meters). 60 m away, 7 m is about 6.7 degrees across.")]
    public float size = 7f;
    [Tooltip("HDR: above 1 makes it bloom.")]
    [ColorUsage(false, true)] public Color moonColor = new Color(1.5f, 1.47f, 1.38f);
    public float haloScale = 4.2f;
    public Color haloColor = new Color(0.62f, 0.7f, 0.9f, 0.32f);
    [Tooltip("Optional: point the moon in this direction (from the viewer). Leave at zero to use the light's direction.")]
    public Vector3 direction;
    [Tooltip("When a direction is given, also turn this light so it shines from the moon.")]
    public bool alignLight = true;

    Transform disc, halo;
    static Texture2D moonTex, haloTex;

    void Start()
    {
        var shader = Resources.Load<Shader>("Shaders/JamMoon");
        if (shader == null) shader = Shader.Find("Jam/Moon");
        if (shader == null) { Debug.LogWarning("[Moon] JamMoon shader missing"); enabled = false; return; }

        if (moonTex == null) moonTex = MakeMoon();
        if (haloTex == null) haloTex = MakeHalo();
        halo = MakeQuad("MoonHalo", shader, haloTex, haloColor);
        disc = MakeQuad("Moon", shader, moonTex, moonColor);

        if (direction.sqrMagnitude > 0.0001f && alignLight)
            transform.rotation = Quaternion.LookRotation(-direction.normalized);
        LateUpdate();
    }

    Transform MakeQuad(string name, Shader shader, Texture2D tex, Color color)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        var mr = q.GetComponent<MeshRenderer>();
        var mat = new Material(shader);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", color);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return q.transform;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || disc == null) return;
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;
        Vector3 c = cam.transform.position;
        var rot = Quaternion.LookRotation(dir);                 // quad faces back toward the camera
        disc.SetPositionAndRotation(c + dir * distance, rot);
        disc.localScale = Vector3.one * size;
        halo.SetPositionAndRotation(c + dir * (distance + 0.5f), rot);
        halo.localScale = Vector3.one * size * haloScale;
    }

    void OnDestroy()
    {
        if (disc != null) Destroy(disc.gameObject);
        if (halo != null) Destroy(halo.gameObject);
    }

    static Texture2D MakeMoon()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[N * N];
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            float u = (i + 0.5f) / N * 2f - 1f, v = (j + 0.5f) / N * 2f - 1f;
            float d = Mathf.Sqrt(u * u + v * v);
            float maria = Mathf.PerlinNoise(u * 2.2f + 5f, v * 2.2f + 3f) * 0.6f + Mathf.PerlinNoise(u * 7f, v * 7f) * 0.4f;
            float shade = Mathf.Lerp(1f, 0.6f, Mathf.SmoothStep(0.45f, 0.75f, maria)) * Mathf.Lerp(1f, 0.78f, d * d);
            float a = Mathf.Clamp01((1f - d) * N * 0.25f);           // crisp, anti-aliased edge
            px[j * N + i] = new Color(shade, shade * 0.97f, shade * 0.9f, a);
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D MakeHalo()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[N * N];
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            float u = (i + 0.5f) / N * 2f - 1f, v = (j + 0.5f) / N * 2f - 1f;
            float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
            float a = Mathf.Pow(1f - d, 2.6f);
            px[j * N + i] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}
