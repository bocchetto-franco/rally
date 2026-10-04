using UnityEngine;

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
    Vector3 resetPosition;
    Quaternion resetRotation;
    bool hasResetPose;

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
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            float seconds = elapsedTime - minutes * 60f;
            return $"{minutes:00}:{seconds:00.000}";
        }
    }

    void Awake()
    {
        ResetTimer();
        CacheInitialVehiclePose();
    }

    void Update()
    {
        if (Time.timeScale == 0f)
            return;
        RallyLocalPlayerInput localInput = vehicleBody != null ? vehicleBody.GetComponent<RallyLocalPlayerInput>() : null;
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
        if (finished || checkpointIndex != nextCheckpoint || vehicleCollider == null)
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
        resetPosition = new Vector3(checkpointTransform.position.x, vehicleBody.position.y, checkpointTransform.position.z);
        resetRotation = Quaternion.Euler(0f, checkpointTransform.eulerAngles.y, 0f);
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
