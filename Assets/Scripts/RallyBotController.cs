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
    [SerializeField, Min(10f)] private float maxSpeedKph = 80f;
    [SerializeField, Range(0f, 1f)] private float cornerBrakingAggressiveness = 0.95f;

    private Transform[] waypoints;
    private int targetIndex;
    private bool routeReady;

    public float VerticalInput { get; private set; }
    public float HorizontalInput { get; private set; }
    public bool Braking => VerticalInput < -0.1f;
    public int CurrentWaypointIndex => targetIndex;

    public void Configure(JrsVehicleController controller, Rigidbody body, Transform route = null)
    {
        vehicle = controller;
        vehicleBody = body;
        waypointRoot = route;
        routeReady = false;
        waypoints = null;
    }

    public void SetDifficulty(float speedKph, float brakingAggressiveness)
    {
        maxSpeedKph = Mathf.Max(10f, speedKph);
        cornerBrakingAggressiveness = Mathf.Clamp01(brakingAggressiveness);
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
        float speedKph = vehicleBody.linearVelocity.magnitude * 3.6f;
        float steeringDenominator = Mathf.Lerp(36f, 55f, Mathf.Clamp01(speedKph / maxSpeedKph));
        float requestedSteering = Mathf.Clamp(steeringAngle / steeringDenominator, -1f, 1f);
        HorizontalInput = Mathf.MoveTowards(HorizontalInput, requestedSteering, 3f * Time.fixedDeltaTime);

        // Accumulate heading changes over up to 90 m of road, not just the next
        // waypoint. Closely spaced hairpin points would otherwise look harmless.
        float lookAhead = Mathf.Lerp(45f, 90f, Mathf.Clamp01(speedKph / maxSpeedKph));
        float upcomingTurn = 0f;
        float distanceAhead = toTarget.magnitude;
        Vector3 previousLeg = Vector3.zero;
        for (int step = 0; step < waypoints.Length - 1 && distanceAhead < lookAhead; step++)
        {
            int from = (targetIndex + step) % waypoints.Length;
            int to = (from + 1) % waypoints.Length;
            Vector3 leg = Vector3.ProjectOnPlane(waypoints[to].position - waypoints[from].position, Vector3.up);
            if (leg.sqrMagnitude < 0.01f)
                continue;
            if (previousLeg.sqrMagnitude > 0.01f)
                upcomingTurn += Vector3.Angle(previousLeg, leg) * Mathf.Lerp(1f, 0.55f, distanceAhead / lookAhead);
            distanceAhead += leg.magnitude;
            previousLeg = leg;
        }
        float cornerSeverity = Mathf.Clamp01(Mathf.Max(Mathf.Abs(steeringAngle), upcomingTurn) / 90f);
        float minimumCornerSpeed = Mathf.Min(24f, maxSpeedKph);
        float targetSpeed = Mathf.Lerp(maxSpeedKph, minimumCornerSpeed,
            cornerSeverity * cornerBrakingAggressiveness);

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
