using TMPro;
using UnityEngine;

public sealed class RallyRaceHud : MonoBehaviour
{
    [SerializeField] RallyCheckpointManager checkpointManager;
    [SerializeField] Rigidbody vehicleBody;
    [SerializeField] TMP_Text speedText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text checkpointText;
    [SerializeField] TMP_Text positionText;
    [SerializeField] RallyRacePositions racePositions;

    void Awake()
    {
        PreparePositionPanel();
        PrepareSpeedPanel();
    }

    void PrepareSpeedPanel()
    {
        if (speedText == null) return;

        speedText.fontSize = 88f;
        speedText.richText = true;
        RectTransform label = speedText.rectTransform;
        label.sizeDelta = new Vector2(380f, 130f);
        if (label.parent is RectTransform panel && panel.name == "Speed Panel")
            panel.sizeDelta = new Vector2(410f, 154f);
    }

    public void Configure(RallyCheckpointManager manager, Rigidbody body, TMP_Text speed, TMP_Text time, TMP_Text checkpoint)
    {
        checkpointManager = manager;
        vehicleBody = body;
        speedText = speed;
        PrepareSpeedPanel();
        timeText = time;
        checkpointText = checkpoint;
        Refresh();
    }

    public void ConfigurePositions(RallyRacePositions positions, TMP_Text position)
    {
        racePositions = positions;
        positionText = position;
        PreparePositionPanel();
        Refresh();
    }

    void PreparePositionPanel()
    {
        if (positionText == null)
            return;

        positionText.richText = true;
        RectTransform label = positionText.rectTransform;
        RectTransform panel = label.parent as RectTransform;
        if (panel != null && panel.name == "Position Panel")
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, 132f));
        label.sizeDelta = new Vector2(label.sizeDelta.x, Mathf.Max(label.sizeDelta.y, 116f));
    }

    void Update() => Refresh();

    void Refresh()
    {
        if (positionText != null)
        {
            string position = racePositions == null ? "—/—" :
                $"{racePositions.PlayerPosition}°/{racePositions.RacerCount}";
            string lap = checkpointManager == null ? "Vuelta —/—" :
                $"Vuelta {checkpointManager.CurrentLap}/{checkpointManager.TotalLaps}";
            positionText.text = $"{position}\n<size=26>{lap}</size>";
        }

        if (speedText != null)
        {
            float speedKph = vehicleBody == null ? 0f : vehicleBody.linearVelocity.magnitude * 3.6f;
            speedText.text = $"{Mathf.RoundToInt(speedKph):000} <size=40%>km/h</size>";
        }

        if (checkpointManager == null)
            return;

        if (timeText != null)
            timeText.text = $"{(checkpointManager.IsFinished ? "META" : "TIEMPO")}  {checkpointManager.FormattedTime}";
        if (checkpointText != null)
            checkpointText.text = $"Checkpoint {checkpointManager.CompletedRaceCheckpoints}/{checkpointManager.RaceCheckpointCount}";
    }
}
