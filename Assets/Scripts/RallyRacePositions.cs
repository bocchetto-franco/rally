using UnityEngine;

/// <summary>Ranks the player and bots by completed laps plus distance along AI_Waypoints.</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class RallyRacePositions : MonoBehaviour
{
    [SerializeField] private Transform waypointRoot;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private RallyCheckpointManager checkpointManager;

    private sealed class Racer
    {
        public Rigidbody body;
        public RallyBotController bot;
        public int order;
        public int segment;
        public int laps;
        public float lastDistance;
        public float progress;
        public Vector3 lastPosition;
        public bool initialized;
    }

    private struct Projection
    {
        public int segment;
        public float distance;
        public float squareError;
    }

    private Vector3[] points;
    private Vector3[] flatPoints;
    private Vector3[] flatLegs;
    private float[] flatLegSquareLengths;
    private float[] segmentLengths;
    private float[] cumulativeDistances;
    private float routeLength;
    private float startDistance;
    private Racer[] racers;
    private Racer player;

    public int PlayerPosition { get; private set; } = 1;
    public int RacerCount => racers == null ? 1 : racers.Length;

    public void Configure(Transform route, Rigidbody body, RallyCheckpointManager manager)
    {
        waypointRoot = route;
        playerBody = body;
        checkpointManager = manager;
    }

    private void Awake()
    {
        if (waypointRoot == null)
        {
            GameObject route = GameObject.Find("AI_Waypoints");
            waypointRoot = route != null ? route.transform : null;
        }
        if (playerBody == null)
        {
            foreach (JrsVehicleController vehicle in FindObjectsByType<JrsVehicleController>())
            {
                if (vehicle.GetComponentInParent<RallyBotController>() == null)
                {
                    playerBody = vehicle.GetComponent<Rigidbody>();
                    break;
                }
            }
        }
        if (checkpointManager == null)
            checkpointManager = FindAnyObjectByType<RallyCheckpointManager>();
        if (waypointRoot == null || waypointRoot.childCount < 3 || playerBody == null)
        {
            Debug.LogError("Race positions need AI_Waypoints and the player Rigidbody.", this);
            enabled = false;
            return;
        }

        int count = waypointRoot.childCount;
        points = new Vector3[count];
        flatPoints = new Vector3[count];
        flatLegs = new Vector3[count];
        flatLegSquareLengths = new float[count];
        segmentLengths = new float[count];
        cumulativeDistances = new float[count + 1];
        for (int i = 0; i < count; i++)
        {
            points[i] = waypointRoot.GetChild(i).position;
            flatPoints[i] = Vector3.ProjectOnPlane(points[i], Vector3.up);
        }
        for (int i = 0; i < count; i++)
        {
            flatLegs[i] = flatPoints[(i + 1) % count] - flatPoints[i];
            flatLegSquareLengths[i] = flatLegs[i].sqrMagnitude;
            segmentLengths[i] = Vector3.ProjectOnPlane(points[(i + 1) % count] - points[i], Vector3.up).magnitude;
            cumulativeDistances[i + 1] = cumulativeDistances[i] + segmentLengths[i];
        }
        routeLength = cumulativeDistances[count];
        if (routeLength < 10f)
        {
            Debug.LogError("AI_Waypoints has no usable route length.", this);
            enabled = false;
            return;
        }

        if (checkpointManager != null)
        {
            foreach (RallyCheckpointTrigger checkpoint in checkpointManager.GetComponentsInChildren<RallyCheckpointTrigger>(true))
            {
                if (checkpoint.CheckpointIndex == 0)
                {
                    startDistance = FindNearestSegment(checkpoint.transform.position).distance;
                    break;
                }
            }
        }

        RallyBotController[] bots = FindObjectsByType<RallyBotController>();
        int activeBots = 0;
        foreach (RallyBotController bot in bots)
            if (bot.gameObject.scene == gameObject.scene && bot.isActiveAndEnabled &&
                bot.GetComponentInChildren<Rigidbody>() != null)
                activeBots++;
        racers = new Racer[activeBots + 1];
        player = new Racer { body = playerBody, order = 0 };
        racers[0] = player;
        int index = 1;
        foreach (RallyBotController bot in bots)
        {
            if (bot.gameObject.scene != gameObject.scene || !bot.isActiveAndEnabled)
                continue;
            Rigidbody body = bot.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                racers[index] = new Racer { body = body, bot = bot, order = index };
                index++;
            }
        }
    }

    private void Update()
    {
        if (racers == null)
            return;
        foreach (Racer racer in racers)
            UpdateProgress(racer);

        // Only the player's rank is displayed. Counting racers ahead avoids
        // sorting the four racers and allocating a comparison delegate each frame.
        int position = 1;
        foreach (Racer racer in racers)
            if (racer != player && (racer.progress > player.progress ||
                (racer.progress == player.progress && racer.order < player.order)))
                position++;
        PlayerPosition = position;
    }

    private void UpdateProgress(Racer racer)
    {
        if (racer.body == null)
            return;
        Vector3 position = racer.body.position;
        bool teleported = racer.initialized && Vector3.Distance(position, racer.lastPosition) > 25f;
        Projection projection;
        if (racer.bot != null && racer.bot.HasRoute && !teleported)
            projection = ProjectSegment(position,
                (racer.bot.CurrentWaypointIndex - 1 + points.Length) % points.Length);
        else if (racer.initialized && !teleported)
            projection = FindNearbySegment(position, racer.segment);
        else
            projection = FindNearestSegment(position);

        float distance = projection.distance;
        if (!racer.initialized)
        {
            // Grid row two starts on the closing segment, just behind Waypoint_01.
            // All racers there are before this lap, not nearly a lap ahead.
            racer.laps = distance > routeLength * 0.75f && startDistance < routeLength * 0.25f ? -1 : 0;
        }
        else if (!teleported)
        {
            if (racer.lastDistance > routeLength * 0.75f && distance < routeLength * 0.25f)
                racer.laps++;
            else if (racer.lastDistance < routeLength * 0.25f && distance > routeLength * 0.75f)
                racer.laps--;
        }
        else if (racer == player && checkpointManager != null && checkpointManager.NextCheckpoint == 0)
        {
            racer.laps = distance > routeLength * 0.75f ? -1 : 0;
        }

        racer.segment = projection.segment;
        racer.lastDistance = distance;
        racer.lastPosition = position;
        racer.initialized = true;
        racer.progress = racer.laps * routeLength + distance - startDistance;
    }

    private Projection FindNearestSegment(Vector3 position)
    {
        Projection best = new Projection { squareError = float.PositiveInfinity };
        for (int i = 0; i < points.Length; i++)
        {
            Projection candidate = ProjectSegment(position, i);
            if (candidate.squareError < best.squareError)
                best = candidate;
        }
        return best;
    }

    private Projection FindNearbySegment(Vector3 position, int previous)
    {
        Projection best = new Projection { squareError = float.PositiveInfinity };
        for (int offset = -4; offset <= 8; offset++)
        {
            int index = (previous + offset + points.Length) % points.Length;
            Projection candidate = ProjectSegment(position, index);
            if (candidate.squareError < best.squareError)
                best = candidate;
        }
        return best;
    }

    private Projection ProjectSegment(Vector3 position, int index)
    {
        Vector3 start = flatPoints[index];
        Vector3 flatPosition = Vector3.ProjectOnPlane(position, Vector3.up);
        Vector3 leg = flatLegs[index];
        float t = flatLegSquareLengths[index] > 0.001f ?
            Mathf.Clamp01(Vector3.Dot(flatPosition - start, leg) / flatLegSquareLengths[index]) : 0f;
        Vector3 nearest = start + leg * t;
        return new Projection
        {
            segment = index,
            distance = cumulativeDistances[index] + segmentLengths[index] * t,
            squareError = (flatPosition - nearest).sqrMagnitude
        };
    }
}
