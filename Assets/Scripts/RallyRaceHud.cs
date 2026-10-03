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
    int displayedSpeed = -1;
    int displayedPosition = -1;
    int displayedRacerCount = -1;
    int displayedLap = -1;
    int displayedTotalLaps = -1;
    int displayedCheckpoint = -1;
    int displayedCheckpointCount = -1;
    float displayedElapsedTime = -1f;
    bool displayedFinish;

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

        positionText.fontSize = 64f;
        positionText.richText = true;
        RectTransform label = positionText.rectTransform;
        RectTransform panel = label.parent as RectTransform;
        if (panel != null && panel.name == "Position Panel")
            panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, 300f), Mathf.Max(panel.sizeDelta.y, 168f));
        label.sizeDelta = new Vector2(Mathf.Max(label.sizeDelta.x, 280f), Mathf.Max(label.sizeDelta.y, 144f));
    }

    void Update() => Refresh();

    void Refresh()
    {
        if (positionText != null)
        {
            int position = racePositions == null ? 0 : racePositions.PlayerPosition;
            int racerCount = racePositions == null ? 0 : racePositions.RacerCount;
            int lap = checkpointManager == null ? 0 : checkpointManager.CurrentLap;
            int totalLaps = checkpointManager == null ? 0 : checkpointManager.TotalLaps;
            if (position != displayedPosition || racerCount != displayedRacerCount ||
                lap != displayedLap || totalLaps != displayedTotalLaps)
            {
                string rank = racePositions == null ? "—/—" : $"{position}°/{racerCount}";
                string lapLabel = checkpointManager == null ? "Vuelta —/—" : $"Vuelta {lap}/{totalLaps}";
                positionText.text = $"{rank}\n<size=28>{lapLabel}</size>";
                displayedPosition = position;
                displayedRacerCount = racerCount;
                displayedLap = lap;
                displayedTotalLaps = totalLaps;
            }
        }

        if (speedText != null)
        {
            float speedKph = vehicleBody == null ? 0f : vehicleBody.linearVelocity.magnitude * 3.6f;
            int roundedSpeed = Mathf.RoundToInt(speedKph);
            if (roundedSpeed != displayedSpeed)
            {
                speedText.text = $"{roundedSpeed:000} <size=40%>km/h</size>";
                displayedSpeed = roundedSpeed;
            }
        }

        if (checkpointManager == null)
            return;

        if (timeText != null)
        {
            float elapsed = checkpointManager.ElapsedTime;
            bool finished = checkpointManager.IsFinished;
            if (elapsed != displayedElapsedTime || finished != displayedFinish)
            {
                timeText.text = $"{(finished ? "META" : "TIEMPO")}  {checkpointManager.FormattedTime}";
                displayedElapsedTime = elapsed;
                displayedFinish = finished;
            }
        }
        if (checkpointText != null)
        {
            int completed = checkpointManager.CompletedRaceCheckpoints;
            int count = checkpointManager.RaceCheckpointCount;
            if (completed != displayedCheckpoint || count != displayedCheckpointCount)
            {
                checkpointText.text = $"Checkpoint {completed}/{count}";
                displayedCheckpoint = completed;
                displayedCheckpointCount = count;
            }
        }
    }
}
