using UnityEngine;

/// <summary>Produces driving inputs for the existing Porsche physics, without applying forces itself.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class RallyBotController : MonoBehaviour
{
    [Header("Vehicle and route")]
    [SerializeField] private JrsVehicleController vehicle;
    [SerializeField] private Rigidbody vehicleBody;
    [SerializeField] private Transform waypointRoot;
    [SerializeField, Min(2f)] private float waypointReachDistance = 6f;

    [Header("Difficulty")]
    [SerializeField, Min(10f)] private float maxSpeedKph = 95f;
    [SerializeField, Range(0f, 1f)] private float cornerBrakingAggressiveness = 0.7f;

    private Transform[] waypoints;
    private int targetIndex;
    private bool routeReady;

    public float VerticalInput { get; private set; }
    public float HorizontalInput { get; private set; }
    public bool Braking => VerticalInput < -0.1f;

    public void Configure(JrsVehicleController controller, Rigidbody body, Transform route = null)
    {
        vehicle = controller;
        vehicleBody = body;
        waypointRoot = route;
        routeReady = false;
        waypoints = null;
    }

    private void Awake()
    {
        if (vehicle == null)
            vehicle = GetComponentInChildren<JrsVehicleController>();
        if (vehicleBody == null && vehicle != null)
            vehicleBody = vehicle.GetComponent<Rigidbody>();
        if (vehicle != null)
            vehicle.SetBotInput(this);
        RallyVehicleDynamics dynamics = GetComponent<RallyVehicleDynamics>();
        if (dynamics != null)
            dynamics.SetBotInput(this);
    }

    private void FixedUpdate()
    {
        if (vehicle == null || vehicleBody == null || !EnsureRoute())
        {
            VerticalInput = 0f;
            HorizontalInput = 0f;
            return;
        }

        Vector3 position = vehicleBody.position;
        Vector3 forward = Vector3.ProjectOnPlane(vehicleBody.transform.forward, Vector3.up).normalized;
        AdvanceTarget(position, forward);

        Vector3 toTarget = Vector3.ProjectOnPlane(waypoints[targetIndex].position - position, Vector3.up);
        float steeringAngle = toTarget.sqrMagnitude > 0.01f
            ? Vector3.SignedAngle(forward, toTarget, Vector3.up) : 0f;
        HorizontalInput = Mathf.Clamp(steeringAngle / 32f, -1f, 1f);

        // Look one segment ahead so the car brakes before arriving at a hairpin.
        int next = (targetIndex + 1) % waypoints.Length;
        int afterNext = (next + 1) % waypoints.Length;
        Vector3 firstLeg = Vector3.ProjectOnPlane(waypoints[next].position - waypoints[targetIndex].position, Vector3.up);
        Vector3 secondLeg = Vector3.ProjectOnPlane(waypoints[afterNext].position - waypoints[next].position, Vector3.up);
        float upcomingTurn = firstLeg.sqrMagnitude > 0.01f && secondLeg.sqrMagnitude > 0.01f
            ? Vector3.Angle(firstLeg, secondLeg) : 0f;
        float cornerSeverity = Mathf.Clamp01(Mathf.Max(Mathf.Abs(steeringAngle), upcomingTurn * 1.4f) / 75f);
        float minimumCornerSpeed = Mathf.Min(28f, maxSpeedKph);
        float targetSpeed = Mathf.Lerp(maxSpeedKph, minimumCornerSpeed,
            cornerSeverity * cornerBrakingAggressiveness);
        float speedKph = vehicleBody.linearVelocity.magnitude * 3.6f;

        VerticalInput = speedKph > targetSpeed + 4f ? -1f : speedKph < targetSpeed - 2f ? 1f : 0f;
    }

    private bool EnsureRoute()
    {
        if (routeReady)
            return waypoints != null && waypoints.Length >= 3;

        if (waypointRoot == null)
        {
            GameObject routeObject = GameObject.Find("AI_Waypoints");
            if (routeObject == null)
                return false;
            waypointRoot = routeObject.transform;
        }

        int count = waypointRoot.childCount;
        if (count < 3)
            return false;
        waypoints = new Transform[count];
        for (int i = 0; i < count; i++)
            waypoints[i] = waypointRoot.GetChild(i);

        Vector3 position = vehicleBody.position;
        Vector3 forward = Vector3.ProjectOnPlane(vehicleBody.transform.forward, Vector3.up).normalized;
        float bestScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Vector3 delta = Vector3.ProjectOnPlane(waypoints[i].position - position, Vector3.up);
            float score = delta.magnitude + (Vector3.Dot(delta, forward) < -1f ? 1000f : 0f);
            if (score >= bestScore)
                continue;
            bestScore = score;
            targetIndex = i;
        }
        routeReady = true;
        return true;
    }

    private void AdvanceTarget(Vector3 position, Vector3 forward)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 delta = Vector3.ProjectOnPlane(waypoints[targetIndex].position - position, Vector3.up);
            bool reached = delta.sqrMagnitude <= waypointReachDistance * waypointReachDistance;
            bool justPassed = delta.magnitude < waypointReachDistance * 2f && Vector3.Dot(delta, forward) < -1f;
            if (!reached && !justPassed)
                break;
            targetIndex = (targetIndex + 1) % waypoints.Length;
        }
    }

    private void OnDisable()
    {
        VerticalInput = 0f;
        HorizontalInput = 0f;
    }
}
