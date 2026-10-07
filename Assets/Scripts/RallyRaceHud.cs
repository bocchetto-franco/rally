using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

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
    RallyVehicleRecovery recovery;
    GameObject recoveryPanel;
    TMP_Text recoveryText;

    public bool RecoveryPromptVisible => recoveryPanel != null && recoveryPanel.activeSelf;
    public string RecoveryPromptText => recoveryText == null ? "" : recoveryText.text;

    void Awake()
    {
        PreparePositionPanel();
        PrepareSpeedPanel();
        PrepareRecoveryPanel();
    }

    void PrepareRecoveryPanel()
    {
        if (!Application.isPlaying || recoveryPanel != null) return;
        // Reuse the cloned panel when creating the second player's HUD.
        Transform existing = transform.Find("Recovery Prompt");
        recoveryPanel = existing != null ? existing.gameObject :
            new GameObject("Recovery Prompt", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = recoveryPanel.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 80f);
        rect.sizeDelta = new Vector2(600f, 76f);
        Image background = recoveryPanel.GetComponent<Image>();
        background.color = new Color(.12f, .10f, .06f, .94f);
        Button button = recoveryPanel.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(RecoverVehicle);
        recoveryText = recoveryPanel.GetComponentInChildren<TMP_Text>(true);
        if (recoveryText == null)
        {
            recoveryText = new GameObject("Recovery Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            recoveryText.transform.SetParent(rect, false);
        }
        RectTransform label = recoveryText.rectTransform;
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(14f, 8f);
        label.offsetMax = new Vector2(-14f, -8f);
        if (speedText != null) recoveryText.font = speedText.font;
        recoveryText.fontSize = 32f;
        recoveryText.alignment = TextAlignmentOptions.Center;
        recoveryText.color = new Color(1f, .84f, .25f);
        recoveryText.raycastTarget = false;
        recoveryPanel.SetActive(false);
    }

    void RecoverVehicle()
    {
        if (recovery != null) recovery.RightVehicle();
        RefreshRecoveryPrompt();
    }

    void RefreshRecoveryPrompt()
    {
        if (!Application.isPlaying) return;
        PrepareRecoveryPanel();
        if (recovery == null && vehicleBody != null)
            recovery = vehicleBody.GetComponent<RallyVehicleRecovery>();
        bool visible = recovery != null && recovery.CanRecover &&
            (checkpointManager == null || !checkpointManager.IsFinished);
        if (recoveryPanel.activeSelf != visible) recoveryPanel.SetActive(visible);
        if (!visible) return;

        RallyLocalPlayerInput local = vehicleBody.GetComponent<RallyLocalPlayerInput>();
        Gamepad pad = local != null ? local.AssignedGamepad : Gamepad.current;
        bool keyboard = local == null || local.Device == RallyLocalPlayerInput.InputDevice.Keyboard;
        string control = keyboard ? "M" : "";
        if (pad != null && (local == null || !keyboard))
            control += (control.Length > 0 ? " / " : "") + (pad is DualShockGamepad ? "Cuadrado" : "X");
        string text = "RESTABLECER AUTO · " + control;
        if (recoveryText.text != text) recoveryText.text = text;
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
        recovery = null;
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

    public void BindPlayer(RallyCheckpointManager manager, Rigidbody body, RallyRacePositions positions)
    {
        checkpointManager = manager;
        vehicleBody = body;
        recovery = null;
        racePositions = positions;
        displayedSpeed = displayedPosition = displayedRacerCount = displayedLap = displayedTotalLaps = -1;
        displayedCheckpoint = displayedCheckpointCount = -1;
        displayedElapsedTime = -1f;
        Refresh();
    }

    void Refresh()
    {
        RefreshRecoveryPrompt();
        if (positionText != null)
        {
            int position = racePositions == null ? 0 : racePositions.GetPosition(vehicleBody);
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
