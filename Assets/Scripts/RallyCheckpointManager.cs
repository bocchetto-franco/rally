using UnityEngine;
using UnityEngine.UI;

public sealed class RallyCheckpointManager : MonoBehaviour
{
    [SerializeField] RallyCheckpointTrigger[] checkpoints;
    [SerializeField] Text timeText;

    int nextCheckpoint;
    float elapsedTime;
    bool running;
    bool finished;

    public int CheckpointCount => checkpoints == null ? 0 : checkpoints.Length;
    public int NextCheckpoint => nextCheckpoint;
    public float ElapsedTime => elapsedTime;
    public bool IsRunning => running;
    public bool IsFinished => finished;

    void Awake()
    {
        ResetTimer();
    }

    void Update()
    {
        if (running)
            elapsedTime += Time.deltaTime;
        UpdateDisplay();
    }

    public void Configure(RallyCheckpointTrigger[] orderedCheckpoints, Text display)
    {
        checkpoints = orderedCheckpoints;
        timeText = display;
        ResetTimer();
    }

    public void TryPass(int checkpointIndex, Collider vehicleCollider)
    {
        if (finished || checkpointIndex != nextCheckpoint || vehicleCollider == null)
            return;

        JrsVehicleController vehicle = vehicleCollider.GetComponentInParent<JrsVehicleController>();
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
        UpdateDisplay();
    }

    public void ResetTimer()
    {
        nextCheckpoint = 0;
        elapsedTime = 0f;
        running = false;
        finished = false;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (timeText == null)
            return;
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        float seconds = elapsedTime - minutes * 60f;
        string prefix = finished ? "Meta  " : "Tiempo  ";
        timeText.text = $"{prefix}{minutes:00}:{seconds:00.000}";
    }
}
