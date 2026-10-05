using UnityEngine;
using UnityEngine.InputSystem;

// First-person controller for the corn maze.
// Walk / sprint with stamina, mouse look, gravity and a light head bob.
// Exposes IsSprinting + NoiseRadius so the scarecrows can "hear" the player later.
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

    [Header("Noise (for scarecrow hearing)")]
    [SerializeField] float walkNoiseRadius = 3f;
    [SerializeField] float sprintNoiseRadius = 14f;

    public bool IsSprinting { get; private set; }
    public bool IsMoving { get; private set; }
    public float Stamina01 => stamina / maxStamina;
    public float NoiseRadius => IsSprinting ? sprintNoiseRadius : IsMoving ? walkNoiseRadius : 0f;
    public bool InputEnabled { get; set; } = true;

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float pitch;
    float stamina;
    float regenTimer;
    bool exhausted;
    float bobTimer;
    Vector3 cameraBasePos;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        stamina = maxStamina;
        if (cameraRoot == null && Camera.main != null) cameraRoot = Camera.main.transform;
        if (cameraRoot != null) cameraBasePos = cameraRoot.localPosition;
    }

    void OnEnable() => LockCursor(true);

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // Escape frees the cursor, clicking recaptures it.
        if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
        else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        bool canControl = InputEnabled && Cursor.lockState == CursorLockMode.Locked;

        // --- Look ---
        if (canControl)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
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

        // --- Sprint + stamina ---
        bool wantsSprint = canControl && kb.leftShiftKey.isPressed && IsMoving && input.y > 0.1f;
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
    }

    void UpdateHeadBob()
    {
        if (cameraRoot == null) return;
        float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        if (controller.isGrounded && speed > 0.2f)
        {
            float freq = IsSprinting ? bobFrequencySprint : bobFrequencyWalk;
            bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;
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
