using UnityEngine;

/// <summary>A short, mass-independent push. Never rewrites the vehicle's tuned physics.</summary>
[DisallowMultipleComponent]
public sealed class RallyVehicleTurbo : MonoBehaviour
{
    Rigidbody body;
    JrsVehicleController car;
    RallyBotController bot;
    RallyLocalPlayerInput localInput;
    [SerializeField] bool hasStoredTurbo;
    float storedSeconds, storedAcceleration, storedMaximum, storedBonus;
    float remaining, acceleration, maximumSpeed, speedBonus;
    Vector3 previousPosition;
    public bool IsBoosting => remaining > 0f;
    public float RemainingSeconds => remaining;
    public int ActivationCount { get; private set; }
    public bool HasStoredTurbo => hasStoredTurbo;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        car = GetComponent<JrsVehicleController>();
        bot = GetComponentInParent<RallyBotController>();
        previousPosition = transform.position;
    }

    System.Collections.IEnumerator Start()
    {
        // Split-screen assigns devices during roster creation, possibly after this
        // receiver's first Start. Bind once the owning local input actually exists.
        while (!RallyVehiclePresentationCache.IsRosterReady(gameObject.scene)) yield return null;
        localInput = GetComponent<RallyLocalPlayerInput>();
    }

    public bool Collect(float seconds, float push, float maximumKph, float bonusKph)
    {
        if (hasStoredTurbo || !CanUseVehicle()) return false;
        hasStoredTurbo = true;
        storedSeconds = seconds; storedAcceleration = push; storedMaximum = maximumKph; storedBonus = bonusKph;
        return true;
    }

    bool CanUseVehicle() => body != null && !body.isKinematic && car != null && car.isActiveAndEnabled &&
        !RallyRaceLoading.IsPreparing && Time.timeScale > 0f;

    public bool UseStoredTurbo()
    {
        if (!hasStoredTurbo || !CanUseVehicle() || IsBoosting || Vector3.Dot(transform.up, Vector3.up) < .5f) return false;
        int grounded = 0;
        if (car.frontLeftWheel != null && car.frontLeftWheel.isGrounded) grounded++;
        if (car.frontRightWheel != null && car.frontRightWheel.isGrounded) grounded++;
        if (car.rearLeftWheel != null && car.rearLeftWheel.isGrounded) grounded++;
        if (car.rearRightWheel != null && car.rearRightWheel.isGrounded) grounded++;
        if (grounded < 2 || Vector3.Dot(body.linearVelocity, transform.forward) < 1f) return false;
        if (!Activate(storedSeconds, storedAcceleration, storedMaximum, storedBonus)) return false;
        hasStoredTurbo = false;
        return true;
    }

    void Update()
    {
        if (bot != null)
        {
            if (Mathf.Abs(bot.HorizontalInput) < .2f) UseStoredTurbo();
            return;
        }
        if (localInput == null && RallyGameSession.LocalPlayerCount == 2) return;
        bool pressed = localInput != null ? localInput.TurboPressed :
            (Input.GetKeyDown(KeyCode.LeftShift) || RallyGamepadInput.TurboPressedThisFrame);
        if (pressed) UseStoredTurbo();
    }

    public bool Activate(float seconds, float accelerationMps2, float maximumKph, float botBonusKph)
    {
        if (IsBoosting || body == null || body.isKinematic || car == null || !car.enabled ||
            RallyRaceLoading.IsPreparing || Time.timeScale <= 0f) return false;
        remaining = seconds;
        acceleration = accelerationMps2;
        maximumSpeed = maximumKph / 3.6f;
        speedBonus = botBonusKph;
        previousPosition = body.position;
        if (bot != null) bot.TurboSpeedBonusKph = speedBonus;
        ActivationCount++;
        return true;
    }

    void FixedUpdate()
    {
        if (!IsBoosting) return;
        // Checkpoint recovery/teleport must not carry a boost to a different location.
        if (body == null || body.isKinematic || car == null || !car.enabled ||
            RallyRaceLoading.IsPreparing || (body.position - previousPosition).sqrMagnitude > 144f)
        { CancelBoost(); return; }
        previousPosition = body.position;
        remaining = Mathf.Max(0f, remaining - Time.fixedDeltaTime);
        if (remaining <= 0f) { CancelBoost(); return; }
        int grounded = 0;
        if (car.frontLeftWheel != null && car.frontLeftWheel.isGrounded) grounded++;
        if (car.frontRightWheel != null && car.frontRightWheel.isGrounded) grounded++;
        if (car.rearLeftWheel != null && car.rearLeftWheel.isGrounded) grounded++;
        if (car.rearRightWheel != null && car.rearRightWheel.isGrounded) grounded++;
        if (grounded < 2 || Vector3.Dot(transform.up, Vector3.up) < .5f) return;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
        if (speed >= maximumSpeed || Vector3.Dot(body.linearVelocity, forward) < 1f) return;
        float push = Mathf.Min(acceleration, (maximumSpeed - speed) / Time.fixedDeltaTime);
        body.AddForce(forward * push, ForceMode.Acceleration);
    }

    public void CancelBoost()
    {
        remaining = 0f;
        if (bot != null) bot.TurboSpeedBonusKph = 0f;
    }

    void OnDisable() => CancelBoost();
}
