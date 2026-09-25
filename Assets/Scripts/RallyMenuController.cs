using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RallyGameSession
{
    public const string MainMenuScene = "MainMenu";
    public const string SelectionScene = "VehicleCircuitSelection";
    public const string RaceScene = "Circuit_01";
    public const string ResultsScene = "RaceResults";
    public const string VehicleName = "Porsche 911 SC Rally";
    public const string CircuitName = "Circuit 01";
    public static readonly string[] CircuitNames = { "Circuit 01", "Circuit 02", "Circuit 03" };
    public static readonly string[] CircuitScenes = { "Circuit_01", "Circuit_02", "Circuit_03" };

    const string TimeKey = "Rally.LastRaceTime";
    const string VehicleKey = "Rally.SelectedVehicle";
    const string CircuitKey = "Rally.SelectedCircuit";

    public static string SelectedVehicle { get; private set; } = VehicleName;
    public static string SelectedCircuit { get; private set; } = CircuitName;
    public static float LastRaceTime { get; private set; } = -1f;
    public static string SelectedRaceScene
    {
        get
        {
            int index = Array.IndexOf(CircuitNames, SelectedCircuit);
            return CircuitScenes[index < 0 ? 0 : index];
        }
    }

    public static void SelectCircuit(int index)
    {
        if (index < 0 || index >= CircuitNames.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        SelectedCircuit = CircuitNames[index];
        PlayerPrefs.SetString(CircuitKey, SelectedCircuit);
        PlayerPrefs.Save();
    }

    public static void SelectCurrentOptions()
    {
        SelectedVehicle = VehicleName;
        PlayerPrefs.SetString(VehicleKey, SelectedVehicle);
        PlayerPrefs.SetString(CircuitKey, SelectedCircuit);
        PlayerPrefs.Save();
    }

    public static void RecordResult(float elapsedTime)
    {
        SelectCurrentOptions();
        LastRaceTime = Mathf.Max(0f, elapsedTime);
        PlayerPrefs.SetFloat(TimeKey, LastRaceTime);
        PlayerPrefs.Save();
    }

    public static void RestoreSavedState()
    {
        SelectedVehicle = PlayerPrefs.GetString(VehicleKey, VehicleName);
        SelectedCircuit = PlayerPrefs.GetString(CircuitKey, CircuitName);
        if (Array.IndexOf(CircuitNames, SelectedCircuit) < 0)
            SelectedCircuit = CircuitName;
        LastRaceTime = PlayerPrefs.GetFloat(TimeKey, -1f);
    }

    public static string FormatTime(float elapsedTime)
    {
        if (elapsedTime < 0f)
            return "--:--.---";

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        float seconds = elapsedTime - minutes * 60f;
        return $"{minutes:00}:{seconds:00.000}";
    }
}

public enum RallyMenuScreen
{
    MainMenu,
    Selection,
    Results
}

[DisallowMultipleComponent]
public sealed class RallyMenuController : MonoBehaviour
{
    static readonly Color Background = new Color(0.018f, 0.027f, 0.043f, 1f);
    static readonly Color Panel = new Color(0.055f, 0.071f, 0.095f, 0.96f);
    static readonly Color PanelLight = new Color(0.085f, 0.105f, 0.135f, 1f);
    static readonly Color Accent = new Color(1f, 0.48f, 0.06f, 1f);
    static readonly Color Muted = new Color(0.64f, 0.69f, 0.75f, 1f);

    [SerializeField] RallyMenuScreen screen;

    TMP_FontAsset font;
    TextMeshProUGUI selectedCircuitTitle;
    TextMeshProUGUI selectedCircuitDetails;
    Button[] circuitButtons;

    public void Configure(RallyMenuScreen targetScreen) => screen = targetScreen;

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RallyGameSession.RestoreSavedState();
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
        BuildInterface();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && screen != RallyMenuScreen.MainMenu)
            SceneManager.LoadScene(screen == RallyMenuScreen.Selection ? RallyGameSession.MainMenuScene : RallyGameSession.SelectionScene);
    }

    void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Rally Frontend Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform background = PanelRect(canvasObject.transform, "Background", Background, Vector2.zero, new Vector2(1920f, 1080f));
        Stretch(background);
        RectTransform orangeRail = PanelRect(background, "Orange Rail", Accent, new Vector2(-925f, 0f), new Vector2(10f, 1080f));
        orangeRail.anchorMin = orangeRail.anchorMax = new Vector2(0.5f, 0.5f);
        PanelRect(background, "Top Shade", new Color(0.08f, 0.105f, 0.14f, 0.55f), new Vector2(0f, 500f), new Vector2(1920f, 80f));
        Text(background, "Brand", "RALLY // PROTOTYPE", 22f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-600f, 500f), new Vector2(600f, 40f), Accent);
        Text(background, "Build", "3 CIRCUITOS DISPONIBLES", 18f, FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(680f, 500f), new Vector2(500f, 40f), Muted);

        switch (screen)
        {
            case RallyMenuScreen.MainMenu:
                BuildMainMenu(background);
                break;
            case RallyMenuScreen.Selection:
                BuildSelection(background);
                break;
            case RallyMenuScreen.Results:
                BuildResults(background);
                break;
        }
    }

    void BuildMainMenu(Transform root)
    {
        Text(root, "Eyebrow", "GRAVA  /  VELOCIDAD  /  CONTROL", 23f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-455f, 245f), new Vector2(770f, 42f), Accent);
        Text(root, "Title", "RALLY", 150f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-420f, 100f), new Vector2(840f, 190f), Color.white);
        Text(root, "Subtitle", "Un auto. Tres pistas. Todo por recorrer.", 34f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(-390f, -25f), new Vector2(900f, 60f), Muted);
        PanelRect(root, "Title Accent", Accent, new Vector2(-862f, 94f), new Vector2(12f, 250f));

        RectTransform card = PanelRect(root, "Start Card", Panel, new Vector2(525f, -15f), new Vector2(580f, 440f));
        Text(card, "Card Label", "PRÓXIMA ETAPA", 21f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 150f), new Vector2(460f, 36f), Accent);
        Text(card, "Circuit", RallyGameSession.SelectedCircuit.Replace(' ', '_').ToUpperInvariant(), 44f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 87f), new Vector2(460f, 60f), Color.white);
        Text(card, "Details", "3 PISTAS DISPONIBLES\nPORSCHE 911 SC RALLY", 23f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 5f), new Vector2(460f, 80f), Muted);
        Button(card, "Play Button", "JUGAR", new Vector2(0f, -125f), new Vector2(470f, 86f), StartSelection, true);
        Text(root, "Hint", "ENTER: seleccionar    ESC: volver", 18f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(-500f, -475f), new Vector2(800f, 32f), new Color(Muted.r, Muted.g, Muted.b, 0.75f));
    }

    void BuildSelection(Transform root)
    {
        Text(root, "Title", "CONFIGURACIÓN DE CARRERA", 52f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 345f), new Vector2(1500f, 70f), Color.white);
        Text(root, "Subtitle", "Elegí auto y circuito antes de salir a pista.", 25f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, 292f), new Vector2(1400f, 42f), Muted);

        RectTransform carCard = SelectionCard(root, "Vehicle Card", new Vector2(-375f, 40f), "AUTO", "PORSCHE 911 SC RALLY", "TRACCIÓN ARCADE\nAJUSTE DE RALLY ACTIVO");
        Text(carCard, "Vehicle Glyph", "911", 92f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 40f), new Vector2(300f, 115f), new Color(1f, 1f, 1f, 0.12f));

        RectTransform circuitCard = PanelRect(root, "Circuit Card", Panel, new Vector2(375f, 40f), new Vector2(650f, 470f));
        PanelRect(circuitCard, "Selected Border", Accent, new Vector2(-317f, 0f), new Vector2(8f, 470f));
        Text(circuitCard, "Category", "ELEGÍ CIRCUITO", 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 178f), new Vector2(540f, 34f), Accent);
        selectedCircuitTitle = Text(circuitCard, "Selected Circuit", "", 35f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 112f), new Vector2(540f, 52f), Color.white);
        selectedCircuitDetails = Text(circuitCard, "Circuit Details", "", 18f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 69f), new Vector2(540f, 34f), Muted);
        string[] options = { "CIRCUIT_01  ·  RALLYCROSS", "CIRCUIT_02  ·  DESIERTO", "CIRCUIT_03  ·  BOSQUE" };
        Color[] swatches = { new Color(.83f, .48f, .23f), new Color(.75f, .65f, .43f), new Color(.27f, .53f, .35f) };
        circuitButtons = new Button[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int choice = i;
            circuitButtons[i] = Button(circuitCard, "Circuit " + (i + 1) + " Button", options[i],
                new Vector2(0f, -9f - 70f * i), new Vector2(530f, 58f), () => ChooseCircuit(choice), false);
            PanelRect(circuitButtons[i].transform, "Track Color", swatches[i], new Vector2(-252f, 0f), new Vector2(12f, 58f));
        }
        RefreshCircuitSelection();

        Button(root, "Back Button", "VOLVER", new Vector2(-265f, -380f), new Vector2(300f, 74f), BackToMenu, false);
        Button(root, "Race Button", "COMENZAR CARRERA", new Vector2(180f, -380f), new Vector2(520f, 74f), StartRace, true);
    }

    void BuildResults(Transform root)
    {
        Text(root, "Eyebrow", "ETAPA COMPLETADA", 25f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(800f, 40f), Accent);
        Text(root, "Title", "RESULTADOS", 70f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 220f), new Vector2(900f, 90f), Color.white);
        RectTransform resultCard = PanelRect(root, "Result Card", Panel, new Vector2(0f, 25f), new Vector2(820f, 285f));
        Text(resultCard, "Time Label", "TIEMPO FINAL", 22f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 86f), new Vector2(500f, 35f), Muted);
        Text(resultCard, "Final Time", RallyGameSession.FormatTime(RallyGameSession.LastRaceTime), 78f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(700f, 90f), Color.white);
        Text(resultCard, "Run Details", $"{RallyGameSession.SelectedVehicle.ToUpperInvariant()}  •  {RallyGameSession.SelectedCircuit.ToUpperInvariant()}", 20f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -84f), new Vector2(720f, 34f), Muted);

        Button(root, "Retry Button", "VOLVER A CORRER", new Vector2(0f, -205f), new Vector2(480f, 74f), RetryRace, true);
        Button(root, "Selection Button", "CAMBIAR SELECCIÓN", new Vector2(-205f, -315f), new Vector2(380f, 64f), BackToSelection, false);
        Button(root, "Menu Button", "MENÚ PRINCIPAL", new Vector2(205f, -315f), new Vector2(380f, 64f), BackToMenu, false);
    }

    RectTransform SelectionCard(Transform root, string name, Vector2 position, string category, string title, string details)
    {
        RectTransform card = PanelRect(root, name, Panel, position, new Vector2(650f, 470f));
        PanelRect(card, "Selected Border", Accent, new Vector2(-317f, 0f), new Vector2(8f, 470f));
        Text(card, "Category", category, 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 178f), new Vector2(540f, 34f), Accent);
        Text(card, "Title", title, 34f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, -82f), new Vector2(540f, 52f), Color.white);
        Text(card, "Details", details, 19f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, -148f), new Vector2(540f, 62f), Muted);
        RectTransform badge = PanelRect(card, "Selected Badge", Accent, new Vector2(215f, 183f), new Vector2(150f, 36f));
        Text(badge, "Selected", "SELECCIONADO", 15f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, new Vector2(140f, 30f), Color.white);
        return card;
    }

    void StartSelection() => SceneManager.LoadScene(RallyGameSession.SelectionScene);
    void BackToMenu() => SceneManager.LoadScene(RallyGameSession.MainMenuScene);
    void BackToSelection() => SceneManager.LoadScene(RallyGameSession.SelectionScene);

    void StartRace()
    {
        RallyGameSession.SelectCurrentOptions();
        SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);
    }

    void RetryRace() => SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);

    void ChooseCircuit(int index)
    {
        RallyGameSession.SelectCircuit(index);
        RefreshCircuitSelection();
    }

    void RefreshCircuitSelection()
    {
        int selected = Array.IndexOf(RallyGameSession.CircuitNames, RallyGameSession.SelectedCircuit);
        selectedCircuitTitle.text = RallyGameSession.SelectedRaceScene.ToUpperInvariant();
        selectedCircuitDetails.text = selected == 0
            ? "RALLYCROSS  •  CRONÓMETRO Y RESULTADOS"
            : "RECORRIDO LIBRE  •  SISTEMA DE CARRERA PENDIENTE";
        for (int i = 0; i < circuitButtons.Length; i++)
        {
            bool active = i == selected;
            Color baseColor = active ? Accent : PanelLight;
            Image image = circuitButtons[i].GetComponent<Image>();
            image.color = baseColor;
            ColorBlock colors = circuitButtons[i].colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = active ? new Color(1f, .62f, .18f) : new Color(.14f, .17f, .22f);
            colors.selectedColor = colors.highlightedColor;
            circuitButtons[i].colors = colors;
        }
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    RectTransform PanelRect(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    TextMeshProUGUI Text(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, bool primary)
    {
        Color baseColor = primary ? Accent : PanelLight;
        RectTransform rect = PanelRect(parent, name, baseColor, position, size);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = primary ? new Color(1f, 0.62f, 0.18f) : new Color(0.14f, 0.17f, 0.22f);
        colors.pressedColor = primary ? new Color(0.82f, 0.32f, 0.02f) : new Color(0.04f, 0.055f, 0.075f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(action);
        Text(rect, "Label", label, primary ? 25f : 21f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(20f, 12f), Color.white);
        return button;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
