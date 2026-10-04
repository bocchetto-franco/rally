using System.Collections.Generic;
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
    [SerializeField, Min(2f)] private float waypointReachDistance = 3.5f;
    [SerializeField, Min(0.5f)] private float safeCorridorHalfWidth = 1.5f;
    [SerializeField, Range(-1.2f, 1.2f)] private float laneOffsetMeters;

    [Header("Difficulty")]
    [SerializeField, Min(10f)] private float maxSpeedKph = 118f;
    [SerializeField, Range(0f, 1f)] private float cornerBrakingAggressiveness = 0.88f;

    private float raceMaxSpeedKph;
    private float raceCornerBrakingAggressiveness;

    private Transform[] waypoints;
    private Vector3[] waypointPositions;
    private Vector3[] planarLegs;
    private float[] planarLegLengths;
    private int targetIndex;
    private bool routeReady;
    private static readonly List<RallyBotController> activeBots = new List<RallyBotController>();

    public float VerticalInput { get; private set; }
    public float HorizontalInput { get; private set; }
    public bool Braking => VerticalInput < -0.1f;
    public float BrakeInput => Mathf.Clamp01(-VerticalInput);
    public int CurrentWaypointIndex => targetIndex;
    public int WaypointCount => waypoints != null ? waypoints.Length : 0;
    public bool HasRoute => routeReady;
    public float CrossTrackDistance { get; private set; }
    public float RaceMaxSpeedKph => raceMaxSpeedKph;
    public float RaceCornerBrakingAggressiveness => raceCornerBrakingAggressiveness;

    public void Configure(JrsVehicleController controller, Rigidbody body, Transform route = null)
    {
        vehicle = controller;
        vehicleBody = body;
        waypointRoot = route;
        routeReady = false;
        waypoints = null;
        waypointPositions = null;
        planarLegs = null;
        planarLegLengths = null;
    }

    public void SetDifficulty(float speedKph, float brakingAggressiveness)
    {
        maxSpeedKph = Mathf.Max(10f, speedKph);
        cornerBrakingAggressiveness = Mathf.Clamp01(brakingAggressiveness);
        if (Application.isPlaying)
            ApplySelectedDifficulty();
    }

    private void ApplySelectedDifficulty()
    {
        // Retain each bot's serialized speed difference (110/118/126 km/h on the
        // circuits) while applying one selection consistently to every bot.
        switch (RallyGameSession.SelectedBotDifficulty)
        {
            case RallyBotDifficulty.Easy:
                raceMaxSpeedKph = maxSpeedKph * 0.85f;
                raceCornerBrakingAggressiveness = Mathf.Clamp01(cornerBrakingAggressiveness + 0.05f);
                break;
            case RallyBotDifficulty.Hard:
                raceMaxSpeedKph = maxSpeedKph * 1.15f;
                raceCornerBrakingAggressiveness = Mathf.Clamp01(cornerBrakingAggressiveness - 0.12f);
                break;
            default:
                raceMaxSpeedKph = maxSpeedKph;
                raceCornerBrakingAggressiveness = cornerBrakingAggressiveness;
                break;
        }
    }

    public void SetLaneOffset(float offsetMeters) =>
        laneOffsetMeters = Mathf.Clamp(offsetMeters, -1.2f, 1.2f);

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        for (int i = activeBots.Count - 1; i >= 0; i--)
        {
            RallyBotController other = activeBots[i];
            if (other == null || other == this)
            {
                activeBots.RemoveAt(i);
                continue;
            }
            IgnoreBodyCollisions(other);
        }
        activeBots.Add(this);
    }

    private void IgnoreBodyCollisions(RallyBotController other)
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        Collider[] otherColliders = other.GetComponentsInChildren<Collider>();
        foreach (Collider own in ownColliders)
        {
            foreach (Collider obstacle in otherColliders)
                Physics.IgnoreCollision(own, obstacle, true);
        }
    }

    private void Awake()
    {
        RallyGameSession.RestoreSavedState();
        ApplySelectedDifficulty();
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

    private void Start()
    {
        // Execute before JrsVehicleController.Start (default execution order), so
        // its wheel visuals are already bound to the selected model.
        RallyBotVisualSelection.ApplyToScene(gameObject.scene);
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
        AdvanceTarget(position);

        Vector3 toTarget = Vector3.ProjectOnPlane(waypointPositions[targetIndex] - position, Vector3.up);
        int previous = (targetIndex - 1 + waypoints.Length) % waypoints.Length;
        Vector3 segmentStart = waypointPositions[previous];
        Vector3 pathDirection = planarLegs[previous].normalized;
        if (pathDirection.sqrMagnitude < 0.01f)
            pathDirection = forward;
        Vector3 nextDirection = planarLegs[targetIndex].normalized;
        float distanceToWaypoint = toTarget.magnitude;
        float turnBlend = Mathf.Clamp01((20f - distanceToWaypoint) / 20f) * 0.45f;
        Vector3 desiredDirection = nextDirection.sqrMagnitude > 0.01f
            ? Vector3.Slerp(pathDirection, nextDirection, turnBlend).normalized : pathDirection;
        float headingError = Vector3.SignedAngle(forward, desiredDirection, Vector3.up);

        // Stanley-style correction: steer back toward the centerline instead of
        // aiming straight at a waypoint and cutting across the inside of bends.
        Vector3 planarPosition = Vector3.ProjectOnPlane(position, Vector3.up);
        Vector3 planarStart = Vector3.ProjectOnPlane(segmentStart, Vector3.up);
        Vector3 closest = planarStart + pathDirection * Mathf.Clamp(
            Vector3.Dot(planarPosition - planarStart, pathDirection), 0f,
            planarLegLengths[previous]);
        float signedOffset = Vector3.Cross(pathDirection, planarPosition - closest).y;
        float laneError = signedOffset - laneOffsetMeters;
        CrossTrackDistance = Mathf.Abs(laneError);
        float speedKph = Vector3.ProjectOnPlane(vehicleBody.linearVelocity, Vector3.up).magnitude * 3.6f;
        float correctionAngle = Mathf.Atan2(5f * laneError,
            12f + speedKph / 3.6f) * Mathf.Rad2Deg;
        float steeringDenominator = Mathf.Lerp(36f, 55f, Mathf.Clamp01(speedKph / raceMaxSpeedKph));
        float requestedSteering = Mathf.Clamp((headingError - correctionAngle) / steeringDenominator, -1f, 1f);
        HorizontalInput = Mathf.MoveTowards(HorizontalInput, requestedSteering, 3f * Time.fixedDeltaTime);

        // Consider nearby curvature, not the sum of every bend a long way ahead.
        // Hairpins remain visible across several short waypoint legs.
        float lookAhead = Mathf.Lerp(40f, 75f, Mathf.Clamp01(speedKph / raceMaxSpeedKph));
        float upcomingTurn = 0f;
        float distanceAhead = toTarget.magnitude;
        Vector3 previousLeg = pathDirection;
        for (int step = 0; step < waypoints.Length - 1 && distanceAhead < lookAhead; step++)
        {
            int from = (targetIndex + step) % waypoints.Length;
            Vector3 leg = planarLegs[from];
            if (leg.sqrMagnitude < 0.01f)
                continue;
            if (previousLeg.sqrMagnitude > 0.01f)
                upcomingTurn += Vector3.Angle(previousLeg, leg) * Mathf.Lerp(1f, 0.65f, distanceAhead / lookAhead);
            distanceAhead += planarLegLengths[from];
            previousLeg = leg;
        }
        float cornerSeverity = Mathf.Max(
            Mathf.InverseLerp(15f, 70f, Mathf.Abs(headingError)),
            Mathf.InverseLerp(18f, 125f, upcomingTurn));
        float minimumCornerSpeed = Mathf.Min(42f, raceMaxSpeedKph);
        float targetSpeed = Mathf.Lerp(raceMaxSpeedKph, minimumCornerSpeed,
            cornerSeverity * raceCornerBrakingAggressiveness);
        float outsideCorridor = Mathf.Max(0f, CrossTrackDistance - safeCorridorHalfWidth);
        float recoverySpeed = Mathf.Lerp(raceMaxSpeedKph, 45f, Mathf.Clamp01(outsideCorridor / 4f));
        targetSpeed = Mathf.Min(targetSpeed, recoverySpeed);

        // Proportional braking avoids full-brake oscillation around the target.
        VerticalInput = speedKph > targetSpeed + 2f
            ? -Mathf.Clamp01((speedKph - targetSpeed - 2f) / 18f)
            : speedKph < targetSpeed - 1f ? 1f : 0f;
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
        waypointPositions = new Vector3[count];
        planarLegs = new Vector3[count];
        planarLegLengths = new float[count];
        for (int i = 0; i < count; i++)
        {
            waypoints[i] = waypointRoot.GetChild(i);
            waypointPositions[i] = waypoints[i].position;
        }
        for (int i = 0; i < count; i++)
        {
            planarLegs[i] = Vector3.ProjectOnPlane(waypointPositions[(i + 1) % count] - waypointPositions[i], Vector3.up);
            planarLegLengths[i] = planarLegs[i].magnitude;
        }

        Vector3 position = vehicleBody.position;
        Vector3 forward = Vector3.ProjectOnPlane(vehicleBody.transform.forward, Vector3.up).normalized;
        float bestScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Vector3 delta = Vector3.ProjectOnPlane(waypointPositions[i] - position, Vector3.up);
            float score = delta.magnitude + (Vector3.Dot(delta, forward) < -1f ? 1000f : 0f);
            if (score >= bestScore)
                continue;
            bestScore = score;
            targetIndex = i;
        }
        routeReady = true;
        return true;
    }

    private void AdvanceTarget(Vector3 position)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 delta = Vector3.ProjectOnPlane(waypointPositions[targetIndex] - position, Vector3.up);
            bool reached = delta.sqrMagnitude <= waypointReachDistance * waypointReachDistance;
            int previous = (targetIndex - 1 + waypoints.Length) % waypoints.Length;
            Vector3 pathDirection = planarLegs[previous];
            bool justPassed = delta.magnitude < Mathf.Min(25f, planarLegLengths[previous] + 8f) &&
                Vector3.Dot(-delta, pathDirection) > 0f;
            if (!reached && !justPassed)
                break;
            targetIndex = (targetIndex + 1) % waypoints.Length;
        }
    }

    private void OnDisable()
    {
        activeBots.Remove(this);
        VerticalInput = 0f;
        HorizontalInput = 0f;
    }
}
