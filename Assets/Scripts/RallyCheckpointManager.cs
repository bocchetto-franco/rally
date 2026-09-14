using UnityEngine;

public sealed class RallyCheckpointManager : MonoBehaviour
{
    [SerializeField] RallyCheckpointTrigger[] checkpoints;

    int nextCheckpoint;
    float elapsedTime;
    bool running;
    bool finished;

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
    }

    void Update()
    {
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
}
