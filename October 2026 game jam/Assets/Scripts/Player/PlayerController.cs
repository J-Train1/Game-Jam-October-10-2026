using UnityEngine;
using UnityEngine.InputSystem;

// First-person controller for the corn maze.
// Walk / sprint with stamina, mouse look, gravity and a light head bob.
// Sprint works in any direction (forward, sideways, backwards).
// Exposes IsSprinting + NoiseRadius so the monsters can "hear" the player.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("References")]
    [SerializeField] Transform cameraRoot;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 3f;
    [SerializeField] float sprintSpeed = 5.5f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float gravity = -20f;

    [Header("Stamina")]
    [SerializeField] float maxStamina = 5f;          // seconds of sprint
    [SerializeField] float staminaRegenRate = 1f;    // stamina per second
    [SerializeField] float regenDelay = 1.2f;        // pause after sprinting before regen
    [SerializeField] float minStaminaToSprint = 1f;  // must recover this much after running dry

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 0.08f;
    [SerializeField] float maxPitch = 85f;

    [Header("Head Bob")]
    [SerializeField] float bobFrequencyWalk = 1.8f;
    [SerializeField] float bobFrequencySprint = 2.6f;
    [SerializeField] float bobAmplitude = 0.04f;

    [Header("Footsteps & breath")]
    // Steps land on the low point of each head bob, so sound and camera move together.
    [SerializeField, Range(0f, 1f)] float walkStepVolume = 0.13f;   // quiet: sneaking is silent to the pumpkins
    [SerializeField, Range(0f, 1f)] float sprintStepVolume = 0.42f;
    [SerializeField, Range(0f, 1f)] float breathVolume = 0.6f;

    [Header("Noise (for monster hearing)")]
    [SerializeField] float walkNoiseRadius = 0f;     // walking is silent to the monsters; only sprinting carries
    [SerializeField] float sprintNoiseRadius = 14f;

    public bool IsSprinting { get; private set; }
    public bool IsMoving { get; private set; }
    public float Stamina01 => stamina / maxStamina;
    /// <summary>True after running stamina dry, until enough has recovered to sprint again.</summary>
    public bool IsExhausted => exhausted;
    public float NoiseRadius => IsSprinting ? sprintNoiseRadius : IsMoving ? walkNoiseRadius : 0f;
    public bool InputEnabled { get; set; } = true;
    public Transform CameraRoot => cameraRoot;

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float pitch;
    float stamina;
    float regenTimer;
    bool exhausted;
    float bobTimer;
    Vector3 cameraBasePos;
    AudioSource stepSrc, breathSrc;
    float stepPhase, breathLevel;
    bool wasExhausted;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        stamina = maxStamina;
        if (cameraRoot == null && Camera.main != null) cameraRoot = Camera.main.transform;
        if (cameraRoot != null) cameraBasePos = cameraRoot.localPosition;

        stepSrc = gameObject.AddComponent<AudioSource>();
        stepSrc.playOnAwake = false; stepSrc.spatialBlend = 0f;
        breathSrc = gameObject.AddComponent<AudioSource>();
        breathSrc.playOnAwake = false; breathSrc.spatialBlend = 0f; breathSrc.loop = true;
        breathSrc.clip = GameAudio.Get("Pant");
        breathSrc.volume = 0f;
    }

    void OnEnable()
    {
        LockCursor(true);
        GameSettings.Changed += ApplyFov;
        ApplyFov();
    }

    void OnDisable()
    {
        GameSettings.Changed -= ApplyFov;
        breathLevel = 0f;                                 // no panting through a jump scare / cutscene
        if (breathSrc != null) { breathSrc.volume = 0f; breathSrc.Stop(); }
    }

    // Field of view from the settings (not while a scare/cutscene owns the camera: this script is off then).
    void ApplyFov()
    {
        var cam = Camera.main;
        if (cam != null) cam.fieldOfView = GameSettings.Fov;
    }

    /// <summary>Put the player back at a spawn point (after losing a heart): full stamina, camera level, no momentum.</summary>
    public void Respawn(Vector3 position, Quaternion rotation)
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        stamina = maxStamina;
        exhausted = false;
        regenTimer = 0f;
        pitch = 0f;
        bobTimer = 0f;
        IsSprinting = false;
        IsMoving = false;
        if (cameraRoot != null)
        {
            cameraRoot.localPosition = cameraBasePos;
            cameraRoot.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        if (PauseMenu.IsPaused) return; // the pause menu owns the mouse and keyboard

        // Escape frees the cursor (or opens the pause menu, which does it), clicking recaptures it.
        if (kb.escapeKey.wasPressedThisFrame && PauseMenu.Instance == null) LockCursor(false);
        else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        bool canControl = InputEnabled && Cursor.lockState == CursorLockMode.Locked;

        // --- Look ---
        if (canControl)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity * GameSettings.Sensitivity;
            if (GameSettings.InvertY) delta.y = -delta.y;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
            if (cameraRoot != null) cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // --- Move input ---
        Vector2 input = Vector2.zero;
        if (canControl)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
            input = Vector2.ClampMagnitude(input, 1f);
        }
        IsMoving = input.sqrMagnitude > 0.01f;

        // --- Sprint + stamina (any direction) ---
        bool wantsSprint = canControl && kb.leftShiftKey.isPressed && IsMoving;
        if (exhausted && stamina >= minStaminaToSprint) exhausted = false;
        IsSprinting = wantsSprint && !exhausted && stamina > 0f;

        if (IsSprinting)
        {
            stamina -= Time.deltaTime;
            regenTimer = regenDelay;
            if (stamina <= 0f) { stamina = 0f; exhausted = true; }
        }
        else
        {
            regenTimer -= Time.deltaTime;
            if (regenTimer <= 0f) stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * Time.deltaTime);
        }

        // --- Apply movement ---
        float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * targetSpeed, acceleration * Time.deltaTime);

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        UpdateHeadBob();
        UpdateSounds();
    }

    // Footsteps in the dry leaves (heavier and quicker when sprinting) and panting when out of breath.
    void UpdateSounds()
    {
        float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        if (controller.isGrounded && speed > 0.2f)
        {
            // Same rhythm as the head bob (one step per bob cycle), landing at the bottom of the bob.
            float freq = IsSprinting ? bobFrequencySprint : bobFrequencyWalk;
            float prev = stepPhase;
            stepPhase += Time.deltaTime * freq;
            if (Mathf.Floor(stepPhase - 0.8f) > Mathf.Floor(prev - 0.8f)) // bottom of the bob (camera eases in a touch late)
            {
                stepSrc.pitch = Random.Range(0.93f, 1.05f) * (IsSprinting ? 1.04f : 1f);
                float v = (IsSprinting ? sprintStepVolume : walkStepVolume) * Mathf.Clamp01(speed / (IsSprinting ? sprintSpeed : walkSpeed));
                stepSrc.PlayOneShot(GameAudio.Pick("Step", 10), v * Random.Range(0.85f, 1f) * GameSettings.Fx);
            }
        }
        else stepPhase = 0f;

        if (exhausted && !wasExhausted) stepSrc.PlayOneShot(GameAudio.Get("Gasp"), breathVolume * GameSettings.Fx);
        wasExhausted = exhausted;
        // Keep panting a little after you can sprint again, then fade out.
        float target = exhausted ? 1f : (stamina < maxStamina * 0.45f && regenTimer <= 0f ? 0.5f : 0f);
        breathLevel = Mathf.MoveTowards(breathLevel, target, Time.deltaTime * (target > breathLevel ? 1.5f : 0.4f));
        if (breathSrc.clip != null)
        {
            breathSrc.volume = breathLevel * breathVolume * GameSettings.Fx;
            if (breathLevel > 0.01f && !breathSrc.isPlaying) breathSrc.Play();
            else if (breathLevel <= 0.01f && breathSrc.isPlaying) breathSrc.Stop();
        }
    }

    void UpdateHeadBob()
    {
        if (cameraRoot == null) return;
        float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        if (controller.isGrounded && speed > 0.2f && GameSettings.HeadBob)
        {
            bobTimer = stepPhase * Mathf.PI * 2f; // shares the footstep rhythm
            float amp = bobAmplitude * (IsSprinting ? 1.6f : 1f);
            Vector3 offset = new Vector3(Mathf.Cos(bobTimer * 0.5f) * amp * 0.5f, Mathf.Sin(bobTimer) * amp, 0f);
            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, cameraBasePos + offset, 12f * Time.deltaTime);
        }
        else
        {
            bobTimer = 0f;
            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, cameraBasePos, 8f * Time.deltaTime);
        }
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? NoiseRadius : sprintNoiseRadius);
    }
}
