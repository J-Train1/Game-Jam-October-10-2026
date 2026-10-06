using UnityEngine;

// A spare battery lying in the maze. Bobs and slowly spins with a faint glow.
// Built from primitives at runtime; swap in a real model later via BatterySpawner.batteryPrefab.
public class BatteryPickup : MonoBehaviour
{
    public float restoreAmount = 40f;
    public float pickupRadius = 1.1f;
    public AudioClip pickupSound;

    Vector3 basePos;
    float phase;

    void Start()
    {
        basePos = transform.position;
        phase = Random.value * 10f;
    }

    void Update()
    {
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * 0.05f);
        transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);

        var player = PlayerController.Instance;
        if (player == null || Flashlight.Instance == null) return;
        Vector3 d = player.transform.position - basePos;
        d.y = 0f;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius)
        {
            Flashlight.Instance.AddBattery(restoreAmount);
            if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.8f);
            Destroy(gameObject);
        }
    }

    /// <summary>Builds the placeholder battery: dark green body, silver cap, glowing band, tiny light.</summary>
    public static GameObject CreatePlaceholder(Transform parent, Vector3 position)
    {
        var root = new GameObject("Battery");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, 0f, 70f); // lying almost on its side

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.1f, 0.09f), Mat(lit, new Color(0.12f, 0.2f, 0.12f), 0.4f, Color.black));
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0f), new Vector3(0.04f, 0.012f, 0.04f), Mat(lit, new Color(0.75f, 0.75f, 0.72f), 0.8f, Color.black));
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.093f, 0.012f, 0.093f), Mat(lit, new Color(0.6f, 0.9f, 0.4f), 0.3f, new Color(0.6f, 1.2f, 0.4f) * 1.5f));

        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.type = LightType.Point;
        glow.range = 1.4f;
        glow.intensity = 0.5f;
        glow.color = new Color(0.7f, 1f, 0.5f);
        glow.shadows = LightShadows.None;

        root.AddComponent<BatteryPickup>();
        return root;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Mat(Shader lit, Color baseColor, float smoothness, Color emission)
    {
        var m = new Material(lit);
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
        }
        return m;
    }
}
