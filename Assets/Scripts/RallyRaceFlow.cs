using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RallyRaceFlow : MonoBehaviour
{
    const int SavedTimeCount = 5;
    const string BestTimesPrefix = "Rally.BestTimes.";
    [SerializeField] RallyCheckpointManager checkpointManager;

    bool finishHandled;
    Rigidbody vehicleBody;
    JrsVehicleController vehicleController;
    RallyVehicleDynamics vehicleDynamics;
    GameObject timesPanel;
    TMP_Text timesText;

    public void Configure(RallyCheckpointManager manager) => checkpointManager = manager;

    void Awake()
    {
        Time.timeScale = 1f;
        if (checkpointManager == null) checkpointManager = FindAnyObjectByType<RallyCheckpointManager>();
        vehicleController = FindAnyObjectByType<JrsVehicleController>();
        vehicleBody = vehicleController != null ? vehicleController.GetComponent<Rigidbody>() : null;
        vehicleDynamics = FindAnyObjectByType<RallyVehicleDynamics>();
    }

    void Update()
    {
        if (!finishHandled && checkpointManager != null && checkpointManager.IsFinished)
            FinishRace(checkpointManager.ElapsedTime);
    }

    void FinishRace(float elapsedTime)
    {
        finishHandled = true;
        elapsedTime = Mathf.Max(0f, elapsedTime);
        RallyGameSession.RecordResult(elapsedTime);
        SaveBestTime(elapsedTime);
        FreezeVehicle();
        BuildFinishMenu(elapsedTime);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    void FreezeVehicle()
    {
        if (vehicleController != null) vehicleController.enabled = false;
        if (vehicleDynamics != null) vehicleDynamics.enabled = false;
        if (vehicleBody == null) return;
        vehicleBody.linearVelocity = Vector3.zero;
        vehicleBody.angularVelocity = Vector3.zero;
        vehicleBody.isKinematic = true;
        vehicleBody.Sleep();
    }

    void BuildFinishMenu(float elapsedTime)
    {
        EnsureEventSystem();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
        GameObject canvasObject = new GameObject("Race Finish Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;

        RectTransform shade = Panel(canvasObject.transform, "Pause Shade", new Color(.01f, .015f, .025f, .82f), Vector2.zero, new Vector2(1920f, 1080f)); Stretch(shade);
        RectTransform card = Panel(shade, "Finish Card", new Color(.045f, .06f, .085f, .98f), Vector2.zero, new Vector2(760f, 710f));
        Panel(card, "Accent", new Color(1f, .48f, .06f), new Vector2(-374f, 0f), new Vector2(12f, 710f));
        Label(card, font, "Finished", "VUELTA COMPLETADA", 26f, FontStyles.Bold, new Vector2(0f, 278f), new Vector2(640f, 42f), new Color(1f, .48f, .06f));
        Label(card, font, "Time Caption", "TIEMPO FINAL", 20f, FontStyles.Bold, new Vector2(0f, 218f), new Vector2(600f, 34f), new Color(.66f, .71f, .77f));
        Label(card, font, "Final Time", RallyGameSession.FormatTime(elapsedTime), 68f, FontStyles.Bold, new Vector2(0f, 155f), new Vector2(650f, 84f), Color.white);
        CreateButton(card, font, "Restart Button", "REINICIAR", new Vector2(0f, 50f), RestartRace, true);
        CreateButton(card, font, "Times Button", "VER TIEMPOS", new Vector2(0f, -50f), ToggleTimes, false);
        CreateButton(card, font, "Menu Button", "VOLVER AL MENÚ", new Vector2(0f, -150f), BackToSelection, false);
        timesPanel = Panel(card, "Best Times Panel", new Color(.075f, .095f, .125f, 1f), new Vector2(0f, -265f), new Vector2(610f, 125f)).gameObject;
        timesText = Label(timesPanel.transform, font, "Best Times", BestTimesLabel(), 19f, FontStyles.Normal, Vector2.zero, new Vector2(560f, 105f), Color.white);
        timesPanel.SetActive(false);
    }

    void RestartRace() { ResumeTime(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    void BackToSelection() { ResumeTime(); SceneManager.LoadScene(RallyGameSession.SelectionScene); }
    void ToggleTimes() { if (timesPanel == null) return; timesText.text = BestTimesLabel(); timesPanel.SetActive(!timesPanel.activeSelf); }
    static void ResumeTime() => Time.timeScale = 1f;

    static string CircuitKey
    {
        get { string circuit = string.IsNullOrWhiteSpace(RallyGameSession.SelectedCircuit) ? RallyGameSession.CircuitName : RallyGameSession.SelectedCircuit; return BestTimesPrefix + circuit.Replace(" ", "_"); }
    }

    static void SaveBestTime(float elapsedTime)
    {
        List<float> times = LoadBestTimes(); times.Add(elapsedTime);
        times = times.Where(t => t >= 0f && float.IsFinite(t)).OrderBy(t => t).Take(SavedTimeCount).ToList();
        PlayerPrefs.SetInt(CircuitKey + ".Count", times.Count);
        for (int i = 0; i < times.Count; i++) PlayerPrefs.SetFloat(CircuitKey + "." + i, times[i]);
        PlayerPrefs.SetFloat(CircuitKey + ".Best", times[0]); PlayerPrefs.Save();
    }

    static List<float> LoadBestTimes()
    {
        int count = Mathf.Clamp(PlayerPrefs.GetInt(CircuitKey + ".Count", 0), 0, SavedTimeCount); var times = new List<float>(count);
        for (int i = 0; i < count; i++) { float time = PlayerPrefs.GetFloat(CircuitKey + "." + i, -1f); if (time >= 0f && float.IsFinite(time)) times.Add(time); }
        return times;
    }

    static string BestTimesLabel()
    {
        List<float> times = LoadBestTimes();
        return times.Count == 0 ? "TODAVÍA NO HAY TIEMPOS GUARDADOS" : "MEJORES TIEMPOS  •  CIRCUIT 01\n" + string.Join("     ", times.Select((time, index) => $"{index + 1}. {RallyGameSession.FormatTime(time)}"));
    }

    static void EnsureEventSystem() { if (FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); }
    static RectTransform Panel(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; go.GetComponent<Image>().color = color; return rect;
    }
    static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string value, float size, FontStyles style, Vector2 position, Vector2 dimensions, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = TextAlignmentOptions.Center; text.color = color; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; return text;
    }
    static void CreateButton(Transform parent, TMP_FontAsset font, string name, string caption, Vector2 position, UnityEngine.Events.UnityAction action, bool primary)
    {
        Color baseColor = primary ? new Color(1f, .48f, .06f) : new Color(.105f, .13f, .17f); RectTransform rect = Panel(parent, name, baseColor, position, new Vector2(540f, 76f));
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); ColorBlock colors = button.colors; colors.normalColor = baseColor; colors.highlightedColor = primary ? new Color(1f, .62f, .18f) : new Color(.17f, .2f, .26f); colors.pressedColor = primary ? new Color(.82f, .32f, .02f) : new Color(.055f, .07f, .1f); colors.selectedColor = colors.highlightedColor; button.colors = colors; button.onClick.AddListener(action);
        Label(rect, font, "Label", caption, 23f, FontStyles.Bold, Vector2.zero, new Vector2(510f, 60f), Color.white);
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
