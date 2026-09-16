using UnityEngine;

public sealed class RallyCheckpointManager : MonoBehaviour
{
    [SerializeField] RallyCheckpointTrigger[] checkpoints;

    int nextCheckpoint;
    float elapsedTime;
    bool running;
    bool finished;
    Rigidbody vehicleBody;
    Vector3 resetPosition;
    Quaternion resetRotation;
    bool hasResetPose;

    public int CheckpointCount => checkpoints == null ? 0 : checkpoints.Length;
    public int NextCheckpoint => nextCheckpoint;
    public float ElapsedTime => elapsedTime;
    public bool IsRunning => running;
    public bool IsFinished => finished;
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
        if (Input.GetKeyDown(KeyCode.R))
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
        if (vehicle == null)
            return;

        if (attachedBody == null)
            attachedBody = vehicle.GetComponent<Rigidbody>();
        if (attachedBody == null)
            return;

        vehicleBody = attachedBody;
        StoreCheckpointPose(checkpointIndex);

        if (checkpointIndex == 0)
        {
            elapsedTime = 0f;
            running = true;
        }

        nextCheckpoint++;
        if (nextCheckpoint >= CheckpointCount)
        {
            running = false;
            finished = true;
        }
    }

    public void ResetTimer()
    {
        nextCheckpoint = 0;
        elapsedTime = 0f;
        running = false;
        finished = false;
    }

    void CacheInitialVehiclePose()
    {
        JrsVehicleController vehicle = FindAnyObjectByType<JrsVehicleController>();
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
        vehicleBody.position = resetPosition;
        vehicleBody.rotation = resetRotation;
        vehicleBody.linearVelocity = Vector3.zero;
        vehicleBody.angularVelocity = Vector3.zero;
        vehicleBody.WakeUp();
        Physics.SyncTransforms();
    }
}
