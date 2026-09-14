using UnityEngine;

[DefaultExecutionOrder(-900)]
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

    [Header("Gravel sideways friction - front")]
    [SerializeField] private float frontSideExtremumSlip = 0.32f;
    [SerializeField] private float frontSideExtremumValue = 1.00f;
    [SerializeField] private float frontSideAsymptoteSlip = 0.70f;
    [SerializeField] private float frontSideAsymptoteValue = 0.72f;
    [SerializeField] private float frontSideStiffness = 0.92f;

    [Header("Rally drift sideways friction - rear")]
    [SerializeField] private float rearSideExtremumSlip = 0.42f;
    [SerializeField] private float rearSideExtremumValue = 0.95f;
    [SerializeField] private float rearSideAsymptoteSlip = 1.00f;
    [SerializeField] private float rearSideAsymptoteValue = 0.52f;
    [SerializeField] private float rearSideStiffness = 0.78f;

    [Header("Handbrake and downforce")]
    [SerializeField] private float rearHandbrakeTorque = 3500f;
    [SerializeField] private float minimumDownforceSpeed = 8f;
    [SerializeField] private float downforceCoefficient = 0.45f;
    [SerializeField] private float maximumDownforce = 600f;

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

        // Apply only a small, torque-free aerodynamic load while both axles
        // have ground contact. Vertical/bounce velocity must not amplify it.
        bool frontGrounded = IsGrounded(frontLeft) || IsGrounded(frontRight);
        bool rearGrounded = IsGrounded(rearLeft) || IsGrounded(rearRight);
        if (!frontGrounded || !rearGrounded)
            return;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(vehicleBody.linearVelocity, Vector3.up);
        float speedSquaredAboveThreshold = Mathf.Max(
            0f,
            planarVelocity.sqrMagnitude - minimumDownforceSpeed * minimumDownforceSpeed);
        float force = Mathf.Min(maximumDownforce, downforceCoefficient * speedSquaredAboveThreshold);
        if (force > 0f)
            vehicleBody.AddForceAtPosition(Vector3.down * force, vehicleBody.worldCenterOfMass, ForceMode.Force);
    }

    private static bool IsGrounded(WheelCollider wheel)
    {
        return wheel != null && wheel.enabled && wheel.isGrounded;
    }

    public void ApplySetup()
    {
        if (controller == null || frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            return;

        ConfigureWheel(frontLeft, false);
        ConfigureWheel(frontRight, false);
        ConfigureWheel(rearLeft, true);
        ConfigureWheel(rearRight, true);

        ConfigureCenterOfMass();

        // JrsVehicleController already reads Space every frame. Restricting this
        // array to the rear axle turns that existing input into a handbrake.
        controller.wheelCollidersBrake = new[] { rearLeft, rearRight };
        controller.brakeForce = rearHandbrakeTorque;
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
        forward.extremumValue = forwardExtremumValue;
        forward.asymptoteSlip = forwardAsymptoteSlip;
        forward.asymptoteValue = forwardAsymptoteValue;
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
