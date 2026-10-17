using System.Collections;
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
    int displayedTimerTick = -1;
    int displayedTimeMilliseconds = -1;
    bool displayedFinish;
    RallyVehicleRecovery recovery;
    RallyLocalPlayerInput localInput;
    Gamepad displayedRecoveryPad;
    int displayedRecoveryPlayer = -1;
    bool displayedRecoveryKeyboard, displayedRecoverySplit, recoveryLabelCached;
    GameObject recoveryPanel;
    TMP_Text recoveryText;

    public bool RecoveryPromptVisible => recoveryPanel != null && recoveryPanel.activeSelf;
    public string RecoveryPromptText => recoveryText == null ? "" : recoveryText.text;

    void Awake()
    {
        PreparePositionPanel();
        PrepareSpeedPanel();
        PrepareTimingPanel();
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
        bool visible = recovery != null && recovery.CanRecover &&
            (checkpointManager == null || !checkpointManager.IsFinished);
        if (recoveryPanel.activeSelf != visible) recoveryPanel.SetActive(visible);
        if (!visible) return;

        RallyLocalPlayerInput local = localInput;
        Gamepad pad = local != null ? local.AssignedGamepad : Gamepad.current;
        bool keyboard = local == null || local.Device == RallyLocalPlayerInput.InputDevice.Keyboard;
        int player = local != null ? local.PlayerIndex : -1;
        bool split = RallyLocalKeyboard.Split;
        if (recoveryLabelCached && displayedRecoveryPad == pad && displayedRecoveryPlayer == player &&
            displayedRecoveryKeyboard == keyboard && displayedRecoverySplit == split) return;
        string control = keyboard ? (local != null ? RallyLocalKeyboard.RecoveryLabel(local.PlayerIndex) : "M") : "";
        if (pad != null && (local == null || !keyboard))
            control += (control.Length > 0 ? " / " : "") + (pad is DualShockGamepad ? "Cuadrado" : "X");
        recoveryText.SetText("RESTABLECER AUTO · " + control);
        displayedRecoveryPad = pad;
        displayedRecoveryPlayer = player;
        displayedRecoveryKeyboard = keyboard;
        displayedRecoverySplit = split;
        recoveryLabelCached = true;
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
        CacheVehicleReferences();
        ResetDisplayedValues();
        speedText = speed;
        PrepareSpeedPanel();
        timeText = time;
        checkpointText = checkpoint;
        PrepareTimingPanel();
        Refresh();
    }

    public void ConfigurePositions(RallyRacePositions positions, TMP_Text position)
    {
        racePositions = positions;
        positionText = position;
        displayedPosition = displayedRacerCount = displayedLap = displayedTotalLaps = -1;
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

    void PrepareTimingPanel()
    {
        if (timeText == null || !(timeText.transform.parent is RectTransform panel)) return;
        panel.anchorMin = panel.anchorMax = Vector2.one;
        panel.pivot = Vector2.one;
        panel.anchoredPosition = new Vector2(-28f, -28f);
        panel.sizeDelta = new Vector2(500f, 132f);
        timeText.fontSize = 44f;
        RectTransform time = timeText.rectTransform;
        time.anchorMin = time.anchorMax = time.pivot = new Vector2(.5f, 1f);
        time.anchoredPosition = new Vector2(0f, -12f);
        time.sizeDelta = new Vector2(470f, 58f);
        if (checkpointText != null)
        {
            RectTransform checkpoint = checkpointText.rectTransform;
            checkpoint.anchorMin = checkpoint.anchorMax = checkpoint.pivot = new Vector2(.5f, 1f);
            checkpoint.anchoredPosition = new Vector2(0f, -74f);
            checkpoint.sizeDelta = new Vector2(470f, 42f);
        }
    }

    IEnumerator Start()
    {
        // Newly added recovery components and both split-screen rigs finish Start
        // before this one-off bind. Never poll for optional components per frame.
        yield return null;
        CacheVehicleReferences();
        while (!RallyVehiclePresentationCache.IsRosterReady(gameObject.scene)) yield return null;
        var miniMap = GetComponent<RallyMiniMap>() ?? gameObject.AddComponent<RallyMiniMap>();
        miniMap.Configure(vehicleBody, speedText != null ? speedText.font : null);
    }

    void CacheVehicleReferences()
    {
        recovery = vehicleBody != null ? vehicleBody.GetComponent<RallyVehicleRecovery>() : null;
        localInput = vehicleBody != null ? vehicleBody.GetComponent<RallyLocalPlayerInput>() : null;
        recoveryLabelCached = false;
    }

    void Update() => Refresh();

    public void BindPlayer(RallyCheckpointManager manager, Rigidbody body, RallyRacePositions positions)
    {
        checkpointManager = manager;
        vehicleBody = body;
        recovery = null;
        CacheVehicleReferences();
        racePositions = positions;
        ResetDisplayedValues();
        Refresh();
    }

    void ResetDisplayedValues()
    {
        displayedSpeed = displayedPosition = displayedRacerCount = displayedLap = displayedTotalLaps = -1;
        displayedCheckpoint = displayedCheckpointCount = -1;
        displayedTimerTick = displayedTimeMilliseconds = -1;
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
                if (racePositions != null && checkpointManager != null)
                    positionText.SetText("{0}°/{1}\n<size=28>Vuelta {2}/{3}</size>", position, racerCount, lap, totalLaps);
                else if (racePositions != null)
                    positionText.SetText("{0}°/{1}\n<size=28>Vuelta —/—</size>", (float)position, (float)racerCount);
                else if (checkpointManager != null)
                    positionText.SetText("—/—\n<size=28>Vuelta {0}/{1}</size>", (float)lap, (float)totalLaps);
                else
                    positionText.SetText("—/—\n<size=28>Vuelta —/—</size>");
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
                speedText.SetText("{0:000} <size=40%>km/h</size>", roundedSpeed);
                displayedSpeed = roundedSpeed;
            }
        }

        if (checkpointManager == null)
            return;

        if (timeText != null)
        {
            float elapsed = checkpointManager.ElapsedTime;
            // Presentation only: 30 Hz instead of rebuilding two TMP meshes every
            // rendered frame. The actual clock and final milliseconds are unchanged.
            int timerTick = Mathf.FloorToInt(elapsed * 30f);
            bool finished = checkpointManager.IsFinished;
            int milliseconds = Mathf.Max(0, Mathf.RoundToInt(elapsed * 1000f));
            if (timerTick != displayedTimerTick || finished != displayedFinish || milliseconds < displayedTimeMilliseconds)
            {
                timeText.SetText(finished ? "META  {0:00}:{1:00}.{2:000}" : "TIEMPO  {0:00}:{1:00}.{2:000}",
                    milliseconds / 60000, milliseconds / 1000 % 60, milliseconds % 1000);
                displayedTimerTick = timerTick;
                displayedTimeMilliseconds = milliseconds;
                displayedFinish = finished;
            }
        }
        if (checkpointText != null)
        {
            int completed = checkpointManager.CompletedRaceCheckpoints;
            int count = checkpointManager.RaceCheckpointCount;
            if (completed != displayedCheckpoint || count != displayedCheckpointCount)
            {
                checkpointText.SetText("Checkpoint {0}/{1}", (float)completed, (float)count);
                displayedCheckpoint = completed;
                displayedCheckpointCount = count;
            }
        }
    }
}
