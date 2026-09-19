using UnityEngine;

[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class RallyVehicleDynamics : MonoBehaviour
{
    [Header("Vehicle references")]
    [SerializeField] private JrsVehicleController controller;
    [SerializeField] private Rigidbody vehicleBody;
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider rearLeft;
    [SerializeField] private WheelCollider rearRight;

    [Header("Arcade handling")]
    [SerializeField] private float vehicleMass = 1450f;
    [SerializeField] private float vehicleAngularDamping = 3.0f;
    [SerializeField] private float maximumSteerAngle = 36f;
    [SerializeField] private float steeringResponse = 6f;
    [SerializeField] private float motorForce = 480f;
    [SerializeField] private float firstGearRatio = 6.0f;
    [SerializeField] private float secondGearRatio = 3.75f;

    [Header("Rally suspension")]
    [SerializeField] private float suspensionDistance = 0.18f;
    [SerializeField] private float springForce = 45000f;
    [SerializeField] private float springDamper = 4500f;
    [SerializeField, Range(0f, 1f)] private float springTargetPosition = 0.45f;
    [SerializeField] private float centerOfMassHeight = -0.45f;
    [SerializeField] private float centerOfMassLongitudinalOffset = -0.10f;

    [Header("Gravel forward friction - all wheels")]
    [SerializeField] private float forwardExtremumSlip = 0.50f;
    [SerializeField] private float forwardExtremumValue = 1.00f;
    [SerializeField] private float forwardAsymptoteSlip = 0.95f;
    [SerializeField] private float forwardAsymptoteValue = 0.65f;
    [SerializeField] private float forwardStiffness = 1.25f;
    [SerializeField] private float rearForwardExtremumValue = 1.05f;
    [SerializeField] private float rearForwardAsymptoteValue = 0.76f;

    [Header("Gravel sideways friction - front")]
    [SerializeField] private float frontSideExtremumSlip = 0.32f;
    [SerializeField] private float frontSideExtremumValue = 1.00f;
    [SerializeField] private float frontSideAsymptoteSlip = 0.70f;
    [SerializeField] private float frontSideAsymptoteValue = 0.72f;
    [SerializeField] private float frontSideStiffness = 0.92f;

    [Header("Rally drift sideways friction - rear")]
    [SerializeField] private float rearSideExtremumSlip = 0.06f;
    [SerializeField] private float rearSideExtremumValue = 0.405f;
    [SerializeField] private float rearSideAsymptoteSlip = 0.72f;
    [SerializeField] private float rearSideAsymptoteValue = 0.135f;
    [SerializeField] private float rearSideStiffness = 0.50f;

    [Header("High-speed rear stability")]
    [SerializeField] private float rearGripIncreaseStartKph = 90f;
    [SerializeField] private float rearGripIncreaseFullKph = 150f;
    [SerializeField, Min(1f)] private float rearGripMultiplierAtFullSpeed = 1.40f;

    [Header("Service brake and handbrake")]
    [SerializeField] private float frontServiceBrakeTorque = 4200f;
    [SerializeField] private float rearServiceBrakeTorque = 1800f;
    [SerializeField] private float serviceBrakeMinimumForwardSpeedKph = 3f;
    [SerializeField] private float absSlipStart = 0.38f;
    [SerializeField] private float absSlipFull = 0.85f;
    [SerializeField, Range(0f, 1f)] private float absMinimumTorqueMultiplier = 0.35f;
    [SerializeField] private float rearHandbrakeTorque = 3500f;

    [Header("High-speed downforce")]
    [SerializeField] private float minimumDownforceSpeedKph = 65f;
    [SerializeField] private float downforceCoefficient = 3.2f;
    [SerializeField] private float maximumDownforce = 4500f;

    [Header("Rear drift smoke")]
    [SerializeField] private ParticleSystem rearLeftDriftSmoke;
    [SerializeField] private ParticleSystem rearRightDriftSmoke;
    [SerializeField] private float smokeStartSlip = 0.18f;
    [SerializeField] private float smokeStopSlip = 0.12f;
    [SerializeField] private float smokeMinimumSpeedKph = 25f;
    [SerializeField] private float smokeMaximumSlip = 0.75f;
    [SerializeField] private float smokeMinimumEmission = 12f;
    [SerializeField] private float smokeMaximumEmission = 42f;

    private bool rearLeftSmokeActive;
    private bool rearRightSmokeActive;
    private JrsInputController inputController;

    public bool HasConfiguredHighSpeedEffects =>
        rearLeftDriftSmoke != null &&
        rearRightDriftSmoke != null &&
        minimumDownforceSpeedKph <= 65f &&
        downforceCoefficient >= 3.2f &&
        maximumDownforce >= 4500f;

    public void Configure(
        JrsVehicleController vehicleController,
        Rigidbody body,
        WheelCollider frontLeftWheel,
        WheelCollider frontRightWheel,
        WheelCollider rearLeftWheel,
        WheelCollider rearRightWheel)
    {
        controller = vehicleController;
        vehicleBody = body;
        frontLeft = frontLeftWheel;
        frontRight = frontRightWheel;
        rearLeft = rearLeftWheel;
        rearRight = rearRightWheel;
        rearLeftDriftSmoke = controller != null ? controller.rearLeftDustParticleSystem : null;
        rearRightDriftSmoke = controller != null ? controller.rearRightDustParticleSystem : null;
        vehicleAngularDamping = 3.0f;
        rearForwardExtremumValue = 1.05f;
        rearForwardAsymptoteValue = 0.76f;
        rearSideAsymptoteSlip = 0.72f;
        frontServiceBrakeTorque = 4200f;
        rearServiceBrakeTorque = 1800f;
        serviceBrakeMinimumForwardSpeedKph = 3f;
        absSlipStart = 0.38f;
        absSlipFull = 0.85f;
        absMinimumTorqueMultiplier = 0.35f;
        minimumDownforceSpeedKph = 65f;
        downforceCoefficient = 3.2f;
        maximumDownforce = 4500f;
        smokeStartSlip = 0.18f;
        smokeStopSlip = 0.12f;
        smokeMinimumSpeedKph = 25f;
        smokeMaximumSlip = 0.75f;
        smokeMinimumEmission = 12f;
        smokeMaximumEmission = 42f;
        ApplySetup();
    }

    private void Awake()
    {
        ApplySetup();
    }

    private void OnEnable()
    {
        ApplySetup();
    }

    private void FixedUpdate()
    {
        if (vehicleBody == null)
            return;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(vehicleBody.linearVelocity, Vector3.up);
        UpdateRearHighSpeedGrip(planarVelocity.magnitude * 3.6f);
        UpdateBrakes();

        // Apply a strong but capped, torque-free aerodynamic load only while
        // both axles have contact. Vertical/bounce velocity must not amplify it.
        bool frontGrounded = IsGrounded(frontLeft) || IsGrounded(frontRight);
        bool rearGrounded = IsGrounded(rearLeft) || IsGrounded(rearRight);
        if (!frontGrounded || !rearGrounded)
            return;

        float minimumDownforceSpeed = minimumDownforceSpeedKph / 3.6f;
        float speedSquaredAboveThreshold = Mathf.Max(
            0f,
            planarVelocity.sqrMagnitude - minimumDownforceSpeed * minimumDownforceSpeed);
        float force = Mathf.Min(maximumDownforce, downforceCoefficient * speedSquaredAboveThreshold);
        if (force > 0f)
            vehicleBody.AddForceAtPosition(Vector3.down * force, vehicleBody.worldCenterOfMass, ForceMode.Force);
    }

    private void LateUpdate()
    {
        if (vehicleBody == null)
            return;

        float speedKph = Vector3.ProjectOnPlane(vehicleBody.linearVelocity, Vector3.up).magnitude * 3.6f;
        UpdateDriftSmoke(rearLeft, rearLeftDriftSmoke, speedKph, ref rearLeftSmokeActive);
        UpdateDriftSmoke(rearRight, rearRightDriftSmoke, speedKph, ref rearRightSmokeActive);
    }

    private void OnDisable()
    {
        SetRearSidewaysStiffness(rearSideStiffness);
        StopSmoke(rearLeftDriftSmoke);
        StopSmoke(rearRightDriftSmoke);
        rearLeftSmokeActive = false;
        rearRightSmokeActive = false;
    }

    private void UpdateDriftSmoke(
        WheelCollider wheel,
        ParticleSystem smoke,
        float speedKph,
        ref bool smokeActive)
    {
        if (wheel == null || smoke == null)
            return;

        bool hasGroundContact = wheel.GetGroundHit(out WheelHit hit);
        float lateralSlip = hasGroundContact ? Mathf.Abs(hit.sidewaysSlip) : 0f;
        float threshold = smokeActive ? smokeStopSlip : smokeStartSlip;
        bool shouldEmit = hasGroundContact && speedKph >= smokeMinimumSpeedKph && lateralSlip >= threshold;

        if (hasGroundContact)
        {
            smoke.transform.SetPositionAndRotation(
                hit.point + Vector3.up * 0.04f,
                Quaternion.LookRotation(Vector3.up, vehicleBody.transform.forward));
        }

        ParticleSystem.EmissionModule emission = smoke.emission;
        if (shouldEmit)
        {
            float intensity = Mathf.InverseLerp(smokeStartSlip, smokeMaximumSlip, lateralSlip);
            emission.rateOverTime = Mathf.Lerp(smokeMinimumEmission, smokeMaximumEmission, intensity);
            if (!smoke.isPlaying)
                smoke.Play(true);
            smokeActive = true;
        }
        else
        {
            emission.rateOverTime = 0f;
            StopSmoke(smoke);
            smokeActive = false;
        }
    }

    private static void StopSmoke(ParticleSystem smoke)
    {
        if (smoke != null && smoke.isPlaying)
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void UpdateBrakes()
    {
        if (inputController == null)
        {
            GameObject inputObject = GameObject.Find("Circuit Keyboard Input");
            inputController = inputObject != null ? inputObject.GetComponent<JrsInputController>() : null;
        }

        float forwardSpeedKph = Vector3.Dot(vehicleBody.linearVelocity, vehicleBody.transform.forward) * 3.6f;
        bool reverseInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ||
                            (inputController != null && inputController.GetVerticalInput() < -0.1f);
        bool serviceBrake = reverseInput && forwardSpeedKph > serviceBrakeMinimumForwardSpeedKph;
        bool handbrake = Input.GetKey(KeyCode.Space) ||
                         (inputController != null && inputController.brakeButton != null &&
                          inputController.brakeButton.IsButtonPressed());

        float frontTorque = serviceBrake ? frontServiceBrakeTorque : 0f;
        float rearTorque = serviceBrake ? rearServiceBrakeTorque : 0f;
        if (serviceBrake)
        {
            // The original controller treats S as reverse torque. Cancel that
            // torque while still moving forward so braking cannot lock the
            // wheels by combining full brakes with reverse engine torque.
            frontLeft.motorTorque = 0f;
            frontRight.motorTorque = 0f;
            rearLeft.motorTorque = 0f;
            rearRight.motorTorque = 0f;
        }

        if (handbrake)
            rearTorque = Mathf.Max(rearTorque, rearHandbrakeTorque);

        SetAbsBrakeTorque(frontLeft, frontTorque);
        SetAbsBrakeTorque(frontRight, frontTorque);
        SetAbsBrakeTorque(rearLeft, rearTorque);
        SetAbsBrakeTorque(rearRight, rearTorque);
    }

    private void SetAbsBrakeTorque(WheelCollider wheel, float requestedTorque)
    {
        if (wheel == null || requestedTorque <= 0f)
        {
            if (wheel != null)
                wheel.brakeTorque = 0f;
            return;
        }

        float torqueMultiplier = 1f;
        if (wheel.GetGroundHit(out WheelHit hit))
        {
            float slip = Mathf.Abs(hit.forwardSlip);
            float absBlend = Mathf.InverseLerp(absSlipStart, absSlipFull, slip);
            torqueMultiplier = Mathf.Lerp(1f, absMinimumTorqueMultiplier, absBlend);
        }

        wheel.brakeTorque = requestedTorque * torqueMultiplier;
    }

    private void UpdateRearHighSpeedGrip(float speedKph)
    {
        // Keep the established arcade drift exactly as tuned through low and
        // medium speeds. Only the high-speed tail of the curve gains grip.
        float speedRange = Mathf.Max(1f, rearGripIncreaseFullKph - rearGripIncreaseStartKph);
        float normalizedSpeed = Mathf.Clamp01((speedKph - rearGripIncreaseStartKph) / speedRange);
        float blend = Mathf.SmoothStep(0f, 1f, normalizedSpeed);
        float stiffness = rearSideStiffness * Mathf.Lerp(1f, rearGripMultiplierAtFullSpeed, blend);
        SetRearSidewaysStiffness(stiffness);
    }

    private void SetRearSidewaysStiffness(float stiffness)
    {
        SetSidewaysStiffness(rearLeft, stiffness);
        SetSidewaysStiffness(rearRight, stiffness);
    }

    private static void SetSidewaysStiffness(WheelCollider wheel, float stiffness)
    {
        if (wheel == null)
            return;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = stiffness;
        wheel.sidewaysFriction = sideways;
    }

    private static bool IsGrounded(WheelCollider wheel)
    {
        return wheel != null && wheel.enabled && wheel.isGrounded;
    }

    public void ApplySetup()
    {
        if (controller == null || frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            return;

        MigratePreviousTuningValues();

        if (rearLeftDriftSmoke == null)
            rearLeftDriftSmoke = controller.rearLeftDustParticleSystem;
        if (rearRightDriftSmoke == null)
            rearRightDriftSmoke = controller.rearRightDustParticleSystem;

        ConfigureWheel(frontLeft, false);
        ConfigureWheel(frontRight, false);
        ConfigureWheel(rearLeft, true);
        ConfigureWheel(rearRight, true);

        ConfigureArcadeHandling();
        ConfigureCenterOfMass();
        ConfigureDriftSmoke(rearLeftDriftSmoke);
        ConfigureDriftSmoke(rearRightDriftSmoke);

        // JrsVehicleController already reads Space every frame. Restricting this
        // array to the rear axle turns that existing input into a handbrake.
        controller.wheelCollidersBrake = new[] { rearLeft, rearRight };
        controller.brakeForce = rearHandbrakeTorque;
    }

    private void MigratePreviousTuningValues()
    {
        // Existing scenes serialize these fields, so new code defaults alone do
        // not replace the previously approved values. Migrate only the exact
        // preceding tune; later manual edits remain untouched.
        if (Mathf.Approximately(vehicleAngularDamping, 2.1f))
            vehicleAngularDamping = 3.0f;
        if (Mathf.Approximately(rearSideAsymptoteSlip, 0.585f))
            rearSideAsymptoteSlip = 0.72f;
        if (Mathf.Approximately(rearForwardExtremumValue, 0.99f))
            rearForwardExtremumValue = 1.05f;
        if (Mathf.Approximately(rearForwardAsymptoteValue, 0.702f))
            rearForwardAsymptoteValue = 0.76f;
    }

    private static void ConfigureDriftSmoke(ParticleSystem smoke)
    {
        if (smoke == null)
            return;

        ParticleSystem.MainModule main = smoke.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.52f, 0.47f, 0.18f),
            new Color(0.78f, 0.74f, 0.66f, 0.34f));

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.12f;

        if (smoke.isPlaying)
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ConfigureArcadeHandling()
    {
        if (vehicleBody != null)
        {
            vehicleBody.mass = vehicleMass;
            vehicleBody.angularDamping = vehicleAngularDamping;
        }

        controller.maxSteerAngle = maximumSteerAngle;
        controller.motorForce = motorForce;
        if (controller.gearRatios != null)
        {
            if (controller.gearRatios.Length > 0)
                controller.gearRatios[0] = firstGearRatio;
            if (controller.gearRatios.Length > 1)
                controller.gearRatios[1] = secondGearRatio;
        }

        GameObject inputObject = GameObject.Find("Circuit Keyboard Input");
        inputController = inputObject != null ? inputObject.GetComponent<JrsInputController>() : null;
        if (inputController != null)
            inputController.steerSpeed = steeringResponse;
    }

    private void ConfigureCenterOfMass()
    {
        if (vehicleBody == null || controller.centerOfMassObject == null)
            return;

        Vector3 center = new Vector3(0f, centerOfMassHeight, centerOfMassLongitudinalOffset);
        controller.centerOfMassObject.transform.localPosition = center;
        vehicleBody.centerOfMass = center;
    }

    private void ConfigureWheel(WheelCollider wheel, bool rear)
    {
        wheel.suspensionDistance = suspensionDistance;

        JointSpring spring = wheel.suspensionSpring;
        spring.spring = springForce;
        spring.damper = springDamper;
        spring.targetPosition = springTargetPosition;
        wheel.suspensionSpring = spring;

        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.extremumSlip = forwardExtremumSlip;
        forward.extremumValue = rear ? rearForwardExtremumValue : forwardExtremumValue;
        forward.asymptoteSlip = forwardAsymptoteSlip;
        forward.asymptoteValue = rear ? rearForwardAsymptoteValue : forwardAsymptoteValue;
        forward.stiffness = forwardStiffness;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.extremumSlip = rear ? rearSideExtremumSlip : frontSideExtremumSlip;
        sideways.extremumValue = rear ? rearSideExtremumValue : frontSideExtremumValue;
        sideways.asymptoteSlip = rear ? rearSideAsymptoteSlip : frontSideAsymptoteSlip;
        sideways.asymptoteValue = rear ? rearSideAsymptoteValue : frontSideAsymptoteValue;
        sideways.stiffness = rear ? rearSideStiffness : frontSideStiffness;
        wheel.sidewaysFriction = sideways;
    }
}
