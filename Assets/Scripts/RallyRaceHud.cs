using TMPro;
using UnityEngine;

public sealed class RallyRaceHud : MonoBehaviour
{
    [SerializeField] RallyCheckpointManager checkpointManager;
    [SerializeField] Rigidbody vehicleBody;
    [SerializeField] TMP_Text speedText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text checkpointText;

    public void Configure(RallyCheckpointManager manager, Rigidbody body, TMP_Text speed, TMP_Text time, TMP_Text checkpoint)
    {
        checkpointManager = manager;
        vehicleBody = body;
        speedText = speed;
        timeText = time;
        checkpointText = checkpoint;
        Refresh();
    }

    void Update() => Refresh();

    void Refresh()
    {
        if (speedText != null)
        {
            float speedKph = vehicleBody == null ? 0f : vehicleBody.linearVelocity.magnitude * 3.6f;
            speedText.text = $"{Mathf.RoundToInt(speedKph):000} <size=45%>km/h</size>";
        }

        if (checkpointManager == null)
            return;

        if (timeText != null)
            timeText.text = $"{(checkpointManager.IsFinished ? "META" : "TIEMPO")}  {checkpointManager.FormattedTime}";
        if (checkpointText != null)
            checkpointText.text = $"Checkpoint {checkpointManager.CompletedRaceCheckpoints}/{checkpointManager.RaceCheckpointCount}";
    }
}
