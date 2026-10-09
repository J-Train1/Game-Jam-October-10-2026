using UnityEngine;
using UnityEngine.InputSystem;

// Handheld flashlight: a spotlight that trails the camera slightly, with a rechargeable battery.
// Drains while on, recharges while off. Low battery dims and flickers the beam.
// Running it fully dry locks it off until it has recharged a little.
// Scarecrows ask IsPointLit() to decide whether they're frozen in the beam.
[RequireComponent(typeof(Light))]
public class Flashlight : MonoBehaviour
{
    public static Flashlight Instance { get; private set; }

    [Header("Follow")]
    public Transform viewCamera;
    [Tooltip("Where the torch sits relative to the camera (right, up, forward).")]
    public Vector3 offset = new Vector3(0.28f, -0.25f, 0.15f);
    [Tooltip("How quickly the beam catches up with where you look. Lower = more swing.")]
    public float followSharpness = 12f;

    [Header("Beam")]
    public float range = 22f;
    public float spotAngle = 55f;
    public float innerSpotAngle = 25f;
    public float intensity = 25f;
    public Color color = new Color(1f, 0.92f, 0.78f);

    [Header("Battery")]
    public float maxBattery = 100f;
    [Tooltip("Battery % lost per second while on. 4 = about 25 seconds from full.")]
    public float drainPerSecond = 4f;
    [Tooltip("Battery % regained per second while off. 6 = about 17 seconds from empty.")]
    public float rechargePerSecond = 6f;
    [Tooltip("Seconds after switching off before recharging starts.")]
    public float rechargeDelay = 0.75f;
    [Tooltip("After running completely dry, the light can't turn on until it reaches this %.")]
    public float minToRestart = 15f;
    [Tooltip("Below this %, the beam dims and flickers.")]
    public float lowThreshold = 25f;
    [Range(0f, 1f)] public float brightnessAtEmpty = 0.35f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip clickOn, clickOff, clickDead;

    public bool IsOn { get; private set; }
    public float Battery { get; private set; }
    public float Battery01 => maxBattery > 0f ? Battery / maxBattery : 0f;
    public bool IsLow => Battery < lowThreshold;
    /// <summary>True after running dry, until recharged to minToRestart.</summary>
    public bool IsLockedOut { get; private set; }
    /// <summary>True while switched off and actively refilling.</summary>
    public bool IsRecharging => !IsOn && Battery < maxBattery && offTimer >= rechargeDelay;
    /// <summary>True only when light is actually coming out (false during flicker blackouts).</summary>
    public bool IsEmitting => beam != null && beam.enabled && beam.intensity > 0.05f;

    Light beam;
    float offTimer;
    float forcedFlickerUntil;
    float blackoutUntil;

    void Awake()
    {
        Instance = this;
        beam = GetComponent<Light>();
        beam.type = LightType.Spot;
        beam.range = range;
        beam.spotAngle = spotAngle;
        beam.innerSpotAngle = innerSpotAngle;
        beam.color = color;
        beam.shadows = LightShadows.None;
        Battery = maxBattery;
        IsOn = true; // the maze is dark; start with the light on
    }

    void Start()
    {
        if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0f; }
        if (clickOn == null) clickOn = GameAudio.Get("Flashlight_On");
        if (clickOff == null) clickOff = GameAudio.Get("Flashlight_Off");
        if (clickDead == null) clickDead = GameAudio.Get("Flashlight_Dead");
        if (FindFirstObjectByType<FlashlightHUD>() == null)
            new GameObject("FlashlightHUD").AddComponent<FlashlightHUD>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool controlling = Cursor.lockState == CursorLockMode.Locked && !PauseMenu.IsPaused &&
                           (PlayerController.Instance == null || PlayerController.Instance.InputEnabled);
        if (controlling && ((kb != null && kb.fKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)))
            Toggle();

        if (IsOn)
        {
            offTimer = 0f;
            Battery = Mathf.Max(0f, Battery - drainPerSecond * Time.deltaTime);
            if (Battery <= 0f)
            {
                IsOn = false;
                IsLockedOut = true;
                Play(clickDead);
                ForceFlicker(0.4f); // dying sputter
            }
        }
        else
        {
            offTimer += Time.deltaTime;
            if (offTimer >= rechargeDelay)
                Battery = Mathf.Min(maxBattery, Battery + rechargePerSecond * Time.deltaTime);
            if (IsLockedOut && Battery >= minToRestart) IsLockedOut = false;
        }
        UpdateBeam();
    }

    void LateUpdate()
    {
        if (viewCamera == null) return;
        transform.position = viewCamera.position + viewCamera.rotation * offset;
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, viewCamera.rotation, t);
    }

    public void Toggle()
    {
        if (!IsOn && (IsLockedOut || Battery <= 0f)) { Play(clickDead); ForceFlicker(0.15f); return; }
        IsOn = !IsOn;
        Play(IsOn ? clickOn : clickOff);
    }

    /// <summary>Instantly add charge (not used by default; kept for possible pickups/rewards).</summary>
    public void AddBattery(float amount)
    {
        Battery = Mathf.Min(maxBattery, Battery + amount);
        if (IsLockedOut && Battery >= minToRestart) IsLockedOut = false;
    }

    /// <summary>Make the beam stutter for a while (e.g. when a scarecrow is close).</summary>
    public void ForceFlicker(float seconds) => forcedFlickerUntil = Mathf.Max(forcedFlickerUntil, Time.time + seconds);

    void UpdateBeam()
    {
        bool forced = Time.time < forcedFlickerUntil;
        if (!IsOn)
        {
            if (forced)
            {
                // weak blink: dying sputter or a failed switch-on attempt
                beam.enabled = Random.value > 0.5f;
                beam.intensity = intensity * 0.15f;
            }
            else beam.enabled = false;
            return;
        }

        float lowT = Mathf.Max(0.001f, lowThreshold / maxBattery);
        float b01 = Battery01;
        float brightness = b01 >= lowT ? 1f : Mathf.Lerp(brightnessAtEmpty, 1f, b01 / lowT);

        float flicker = 1f;
        float severity = forced ? 1f : (b01 < lowT ? 1f - b01 / lowT : 0f);
        if (severity > 0f)
        {
            float noise = Mathf.PerlinNoise(Time.time * 14f, 0.37f);
            flicker = Mathf.Lerp(1f, 0.35f + noise * 0.8f, severity * 0.7f);
            if (Time.time > blackoutUntil && Random.value < (0.4f + severity * 2.5f) * Time.deltaTime)
                blackoutUntil = Time.time + Random.Range(0.05f, 0.15f + severity * 0.35f);
            if (Time.time < blackoutUntil) flicker = 0f;
        }

        beam.enabled = flicker > 0.01f;
        beam.intensity = intensity * brightness * flicker;
    }

    /// <summary>Is this world point inside the lit beam right now (cone, range, battery, not behind corn)?</summary>
    public bool IsPointLit(Vector3 point)
    {
        if (!IsEmitting) return false;
        Vector3 from = transform.position;
        Vector3 to = point - from;
        float dist = to.magnitude;
        if (dist > range || dist < 0.01f) return false;
        if (Vector3.Angle(transform.forward, to) > beam.spotAngle * 0.5f) return false;
        // Corn walls block the beam (their colliders live under the MazeGenerator).
        if (Physics.Raycast(from, to / dist, out var hit, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<MazeGenerator>() != null)
            return false;
        return true;
    }

    /// <summary>True if any of the points is lit (e.g. head, chest, feet of a scarecrow).</summary>
    public bool IsAnyPointLit(Vector3[] points)
    {
        foreach (var p in points) if (IsPointLit(p)) return true;
        return false;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, 0.8f * GameSettings.Fx);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.6f, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * range);
    }
}
