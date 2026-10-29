using UnityEngine;
using System.Collections.Generic;

public sealed class RallyCheckpointManager : MonoBehaviour
{
    [SerializeField] RallyCheckpointTrigger[] checkpoints;
    [SerializeField, Min(1)] int totalLaps = 1;

    int nextCheckpoint;
    int completedLaps;
    float elapsedTime;
    bool running;
    bool finished;
    Rigidbody vehicleBody;
    RallyLocalPlayerInput localInput;
    int formattedMilliseconds = -1;
    string cachedFormattedTime;
    Vector3 resetPosition;
    Quaternion resetRotation;
    bool hasResetPose;
    Collider[] resetRoads;

    public int CheckpointCount => checkpoints == null ? 0 : checkpoints.Length;
    public int NextCheckpoint => nextCheckpoint;
    public int TotalLaps => Mathf.Max(1, totalLaps);
    public int CurrentLap => Mathf.Clamp(completedLaps + 1, 1, TotalLaps);
    public float ElapsedTime => elapsedTime;
    public bool IsRunning => running;
    public bool IsFinished => finished;
    public Rigidbody VehicleBody => vehicleBody;
    public RallyCheckpointTrigger[] OrderedCheckpoints => checkpoints;
    public void BindVehicle(Rigidbody body)
    {
        vehicleBody = body;
        localInput = body != null ? body.GetComponent<RallyLocalPlayerInput>() : null;
        if(body == null) { hasResetPose = false; return; }
        resetPosition = body.position;
        resetRotation = body.rotation;
        hasResetPose = true;
    }
    public void ConfigureLocalPlayer(RallyCheckpointManager source, Rigidbody body)
    {
        totalLaps = source.totalLaps;
        Configure(source.checkpoints);
        BindVehicle(body);
    }
    public int RaceCheckpointCount => Mathf.Max(0, CheckpointCount - 1);
    public int CompletedRaceCheckpoints => Mathf.Clamp(nextCheckpoint, 0, RaceCheckpointCount);
    public string FormattedTime
    {
        get
        {
            int milliseconds = Mathf.Max(0, Mathf.RoundToInt(elapsedTime * 1000f));
            if (milliseconds != formattedMilliseconds)
            {
                cachedFormattedTime = $"{milliseconds / 60000:00}:{milliseconds / 1000 % 60:00}.{milliseconds % 1000:000}";
                formattedMilliseconds = milliseconds;
            }
            return cachedFormattedTime;
        }
    }

    void Awake()
    {
        ResetTimer();
        CacheInitialVehiclePose();
    }

    void Start()
    {
        // Split-screen adds/rebinds local inputs during its earlier Start.
        localInput = vehicleBody != null ? vehicleBody.GetComponent<RallyLocalPlayerInput>() : null;
    }

    void Update()
    {
        if (Time.timeScale == 0f)
            return;
        bool reset = localInput != null ? localInput.ResetPressed :
            (Input.GetKeyDown(KeyCode.R) || RallyGamepadInput.ResetPressedThisFrame);
        if (!finished && reset)
            ResetVehicleToLastCheckpoint();

        if (running)
            elapsedTime += Time.deltaTime;
    }

    public void Configure(RallyCheckpointTrigger[] orderedCheckpoints)
    {
        checkpoints = orderedCheckpoints;
        ResetTimer();
    }

    public void TryPass(int checkpointIndex, Collider vehicleCollider)
    {
        if (RallyRaceStartLights.IsBlocking || finished || checkpointIndex != nextCheckpoint || vehicleCollider == null)
            return;

        Rigidbody attachedBody = vehicleCollider.attachedRigidbody;
        JrsVehicleController vehicle = attachedBody != null
            ? attachedBody.GetComponent<JrsVehicleController>()
            : vehicleCollider.GetComponentInParent<JrsVehicleController>();
        if (vehicle == null || vehicle.GetComponentInParent<RallyBotController>() != null)
            return;

        if (attachedBody == null)
            attachedBody = vehicle.GetComponent<Rigidbody>();
        if (attachedBody == null)
            return;
        if (vehicleBody != null && attachedBody != vehicleBody)
            return;

        if (vehicleBody != attachedBody)
            localInput = attachedBody.GetComponent<RallyLocalPlayerInput>();
        vehicleBody = attachedBody;
        StoreCheckpointPose(checkpointIndex);

        if (checkpointIndex == 0)
        {
            if (completedLaps == 0)
                elapsedTime = 0f;
            running = true;
        }

        nextCheckpoint++;
        if (nextCheckpoint >= CheckpointCount)
        {
            completedLaps++;
            if (completedLaps >= TotalLaps)
            {
                running = false;
                finished = true;
            }
            else
            {
                nextCheckpoint = 0;
            }
        }
    }

    public void ResetTimer()
    {
        nextCheckpoint = 0;
        completedLaps = 0;
        elapsedTime = 0f;
        running = false;
        finished = false;
    }

    void CacheInitialVehiclePose()
    {
        JrsVehicleController vehicle = null;
        foreach (JrsVehicleController candidate in FindObjectsByType<JrsVehicleController>())
        {
            if (candidate.GetComponentInParent<RallyBotController>() != null)
                continue;
            vehicle = candidate;
            break;
        }
        vehicleBody = vehicle != null ? vehicle.GetComponent<Rigidbody>() : null;
        localInput = vehicleBody != null ? vehicleBody.GetComponent<RallyLocalPlayerInput>() : null;
        if (vehicleBody == null)
            return;

        resetPosition = vehicleBody.position;
        resetRotation = vehicleBody.rotation;
        hasResetPose = true;
    }

    void StoreCheckpointPose(int checkpointIndex)
    {
        if (vehicleBody == null || checkpoints == null || checkpointIndex < 0 || checkpointIndex >= checkpoints.Length)
            return;

        RallyCheckpointTrigger checkpoint = checkpoints[checkpointIndex];
        if (checkpoint == null)
            return;

        Transform checkpointTransform = checkpoint.transform;
        Quaternion upright = Quaternion.Euler(0f, checkpointTransform.eulerAngles.y, 0f);
        if (resetRoads == null)
        {
            var roads = new List<Collider>();
            foreach (var root in vehicleBody.gameObject.scene.GetRootGameObjects())
                foreach (var surface in root.GetComponentsInChildren<Collider>(true))
                    if (!surface.isTrigger && (surface.name == "Rally_Road_Start_to_Finish" ||
                        surface.name == "Circuit02_Road" || surface.name == "Circuit03_Road" ||
                        surface.name == "Continuous smooth deck collider")) roads.Add(surface);
            resetRoads = roads.ToArray();
        }
        // Gate centre X/Z must use the road's height, never the passing car's
        // airborne/bank height. Only known drivable meshes qualify (not trees/walls).
        var gateBox = checkpoint.GetComponent<BoxCollider>();
        Vector3 origin = checkpointTransform.position;
        origin.y = (gateBox != null ? gateBox.bounds.max.y : origin.y + 10f) + 2f;
        Ray ray = new Ray(origin, Vector3.down);
        float nearest = float.PositiveInfinity, roadHeight = 0f;
        foreach (var road in resetRoads)
            if (road != null && road.enabled && road.gameObject.activeInHierarchy &&
                road.Raycast(ray, out RaycastHit hit, 200f) && hit.normal.y > .5f && hit.distance < nearest)
            { nearest = hit.distance; roadHeight = hit.point.y; }
        if (float.IsPositiveInfinity(nearest)) return; // Retain the previous safe pose.
        float clearance = .5f;
        var controller = vehicleBody.GetComponent<JrsVehicleController>();
        if (controller != null)
        {
            clearance = .25f;
            foreach (var wheel in new[] { controller.frontLeftWheel, controller.frontRightWheel,
                controller.rearLeftWheel, controller.rearRightWheel })
            {
                if (wheel == null) continue;
                Vector3 offset = Quaternion.Inverse(vehicleBody.rotation) *
                    (wheel.transform.TransformPoint(wheel.center) - vehicleBody.position);
                float radius = wheel.radius * Mathf.Abs(wheel.transform.lossyScale.y);
                float suspension = wheel.suspensionDistance * (1f - wheel.suspensionSpring.targetPosition) * Mathf.Abs(wheel.transform.lossyScale.y);
                clearance = Mathf.Max(clearance, radius + suspension - (upright * offset).y + .03f);
            }
        }
        resetPosition = new Vector3(checkpointTransform.position.x, roadHeight + clearance, checkpointTransform.position.z);
        resetRotation = upright;
        hasResetPose = true;
    }

    public void ResetVehicleToLastCheckpoint()
    {
        if (vehicleBody == null)
            CacheInitialVehiclePose();
        if (vehicleBody == null || !hasResetPose)
            return;

        // Preserve race progress: this recovery only changes the vehicle pose and motion.
        // Keep the interpolated Transform in sync before SyncTransforms; otherwise
        // it can push the pre-teleport pose back into physics for a cloned local car.
        vehicleBody.transform.SetPositionAndRotation(resetPosition, resetRotation);
        vehicleBody.position = resetPosition;
        vehicleBody.rotation = resetRotation;
        vehicleBody.linearVelocity = Vector3.zero;
        vehicleBody.angularVelocity = Vector3.zero;
        vehicleBody.WakeUp();
        Physics.SyncTransforms();
    }
}
