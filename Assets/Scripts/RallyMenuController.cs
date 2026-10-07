using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum RallyBotDifficulty
{
    Easy,
    Medium,
    Hard
}

public static class RallyGameSession
{
    public const string MainMenuScene = "MainMenu";
    public const string SelectionScene = "VehicleCircuitSelection";
    public const string RaceScene = "Circuit_01";
    public const string ResultsScene = "RaceResults";
    public const string VehicleName = "Porsche 911 SC Rally";
    public static readonly string[] VehicleDisplayNames = { "Porsche 911", "BMW M3 E30", "Audi Quattro S1" };
    public const string CircuitName = "Circuit 01";
    public static readonly string[] CircuitNames = { "Circuit 01", "Circuit 02", "Circuit 03" };
    public static readonly string[] CircuitScenes = { "Circuit_01", "Circuit_02", "Circuit_03" };

    const string TimeKey = "Rally.LastRaceTime";
    const string VehicleKey = "Rally.SelectedVehicle";
    const string VehicleTwoKey = "Rally.SelectedVehicleTwo";
    const string CircuitKey = "Rally.SelectedCircuit";
    const string DifficultyKey = "Rally.BotDifficulty";
    public static int LocalPlayerCount { get; private set; } = 1;
    public static void SelectLocalPlayers(int count)
    {
        LocalPlayerCount = Mathf.Clamp(count, 1, 2);
        PlayerPrefs.SetInt("Rally.LocalPlayers", LocalPlayerCount);
        PlayerPrefs.Save();
    }

    public static string SelectedVehicle { get; private set; } = VehicleName;
    public static string SelectedVehicleTwo { get; private set; } = VehicleName;
    public static string VehicleForPlayer(int player) => player == 0 ? SelectedVehicle : SelectedVehicleTwo;
    public static int VehicleIndexForPlayer(int player) => Mathf.Max(0, Array.IndexOf(RallyPlayerVehicleSelection.Names, VehicleForPlayer(player)));
    public static void SelectVehicleForPlayer(int player, int index)
    {
        if (player == 0) { SelectVehicle(index); return; }
        if (player != 1 || index < 0 || index >= RallyPlayerVehicleSelection.Names.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        SelectedVehicleTwo = RallyPlayerVehicleSelection.Names[index];
        PlayerPrefs.SetString(VehicleTwoKey, SelectedVehicleTwo);
        PlayerPrefs.Save();
    }
    public static string SelectedCircuit { get; private set; } = CircuitName;
    public static RallyBotDifficulty SelectedBotDifficulty { get; private set; } = RallyBotDifficulty.Medium;
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

    public static void SelectDifficulty(int index)
    {
        if (index < 0 || index > (int)RallyBotDifficulty.Hard)
            throw new ArgumentOutOfRangeException(nameof(index));
        SelectedBotDifficulty = (RallyBotDifficulty)index;
        PlayerPrefs.SetInt(DifficultyKey, index);
        PlayerPrefs.Save();
    }

    public static void SelectCurrentOptions()
    {
        PlayerPrefs.SetString(VehicleKey, SelectedVehicle);
        PlayerPrefs.SetString(VehicleTwoKey, SelectedVehicleTwo);
        PlayerPrefs.SetString(CircuitKey, SelectedCircuit);
        PlayerPrefs.SetInt(DifficultyKey, (int)SelectedBotDifficulty);
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
        LocalPlayerCount = Mathf.Clamp(PlayerPrefs.GetInt("Rally.LocalPlayers", 1), 1, 2);
        SelectedVehicle = RallyPlayerVehicleSelection.MigrateSavedName(PlayerPrefs.GetString(VehicleKey, VehicleName));
        if (Array.IndexOf(RallyPlayerVehicleSelection.Names, SelectedVehicle) < 0)
            SelectedVehicle = VehicleName;
        SelectedVehicleTwo = RallyPlayerVehicleSelection.MigrateSavedName(PlayerPrefs.GetString(VehicleTwoKey, VehicleName));
        if (Array.IndexOf(RallyPlayerVehicleSelection.Names, SelectedVehicleTwo) < 0)
            SelectedVehicleTwo = VehicleName;
        SelectedCircuit = PlayerPrefs.GetString(CircuitKey, CircuitName);
        if (Array.IndexOf(CircuitNames, SelectedCircuit) < 0)
            SelectedCircuit = CircuitName;
        int difficulty = PlayerPrefs.GetInt(DifficultyKey, (int)RallyBotDifficulty.Medium);
        SelectedBotDifficulty = difficulty >= (int)RallyBotDifficulty.Easy && difficulty <= (int)RallyBotDifficulty.Hard
            ? (RallyBotDifficulty)difficulty : RallyBotDifficulty.Medium;
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

    public static void SelectVehicle(int index)
    {
        if (index < 0 || index >= RallyPlayerVehicleSelection.Names.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        SelectedVehicle = RallyPlayerVehicleSelection.Names[index];
        PlayerPrefs.SetString(VehicleKey, SelectedVehicle);
        PlayerPrefs.Save();
    }

    public static string SelectedVehicleDisplayName
    {
        get
        {
            int index = Array.IndexOf(RallyPlayerVehicleSelection.Names, SelectedVehicle);
            return VehicleDisplayName(index < 0 ? 0 : index);
        }
    }

    // Keep saved physics/prefab identifiers separate from the text shown to players.
    public static int SelectedVehicleIndex => Mathf.Max(0, Array.IndexOf(RallyPlayerVehicleSelection.Names, SelectedVehicle));
    public static string VehicleDisplayName(int index)
    {
        if (index < 0 || index >= RallyPlayerVehicleSelection.Names.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return VehicleDisplayNames[index];
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
    static readonly Color Background = new Color(.035f, .025f, .019f, .66f);
    static readonly Color Panel = new Color(.07f, .058f, .047f, .94f);
    static readonly Color PanelLight = new Color(.16f, .13f, .10f, 1f);
    static readonly Color Accent = new Color(1f, .57f, .13f, 1f);
    static readonly Color Muted = new Color(.76f, .72f, .64f, 1f);

    [SerializeField] RallyMenuScreen screen;

    TMP_FontAsset font;
    TMP_FontAsset headingFont;
    TextMeshProUGUI selectedVehicleCounter;
    RawImage selectedTrackPreview;
    RallyMenuBackdrop backdrop;
    TextMeshProUGUI selectedCircuitTitle;
    TextMeshProUGUI selectedCircuitDetails;
    Button[] circuitButtons;
    Button[] difficultyButtons;
    Button[] vehicleButtons;
    Button[] localPlayerButtons;
    TextMeshProUGUI selectedVehicleTitle;
    TextMeshProUGUI introPrompt;
    GameObject introCard;
    GameObject mainMenuCard;
    GameObject mainMenuHint;
    RallyMenuNavigation menuNavigation;
    bool waitingForStartInput;
    GameObject frontendCanvas;
    readonly bool[] localReady = new bool[2];
    Button[][] localVehicleButtons;
    Button[] localReadyButtons;
    TextMeshProUGUI[] localVehicleTitles, localDeviceLabels;
    RallyMenuNavigation[] localNavigations;
    Button localRaceButton;

    public void Configure(RallyMenuScreen targetScreen) => screen = targetScreen;

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RallyGameSession.RestoreSavedState();
        font = Resources.Load<TMP_FontAsset>("MenuFonts/Roboto") ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset;
        headingFont = Resources.Load<TMP_FontAsset>("MenuFonts/Oswald") ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF") ?? font;
        backdrop = gameObject.AddComponent<RallyMenuBackdrop>();
        backdrop.Show(RallyGameSession.SelectedRaceScene);
        BuildInterface();
    }

    void Update()
    {
        if (localDeviceLabels != null)
            for (int i = 0; i < 2; i++)
            {
                string caption = RallyLocalDevices.Label(i);
                if (localDeviceLabels[i].text != caption) localDeviceLabels[i].text = caption;
                bool available = RallyLocalDevices.GamepadFor(i) != null || RallyLocalDevices.UsesKeyboard(i);
                if (localReadyButtons[i].interactable != available)
                {
                    localReadyButtons[i].interactable = available;
                    if (!available) localReady[i] = false;
                    RefreshLocalSelection();
                }
            }
        if (waitingForStartInput)
        {
            Color color = introPrompt.color;
            color.a = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(Time.unscaledTime * 3.5f) + 1f) * 0.5f);
            introPrompt.color = color;
            if (Input.anyKeyDown || GamepadButtonPressed()) RevealMainMenu();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) && screen != RallyMenuScreen.MainMenu)
            SceneManager.LoadScene(screen == RallyMenuScreen.Selection ? RallyGameSession.MainMenuScene : RallyGameSession.SelectionScene);
    }

    static bool GamepadButtonPressed()
    {
        foreach (Gamepad pad in Gamepad.all)
            foreach (InputControl control in pad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        return false;
    }

    void RevealMainMenu()
    {
        waitingForStartInput = false;
        introCard.SetActive(false);
        mainMenuCard.SetActive(true);
        mainMenuHint.SetActive(true);
        StartCoroutine(EnableMenuNavigationNextFrame());
    }

    IEnumerator EnableMenuNavigationNextFrame()
    {
        yield return null; // The button that dismissed the prompt must not also press JUGAR.
        menuNavigation.enabled = true;
        menuNavigation.ResetSelection();
    }

    void BuildInterface()
    {
        if (frontendCanvas != null)
        {
            frontendCanvas.SetActive(false);
            Destroy(frontendCanvas);
        }
        localVehicleButtons = null;
        localDeviceLabels = null;
        localNavigations = null;
        difficultyButtons = null;
        vehicleButtons = null;
        localReady[0] = localReady[1] = false;
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Rally Frontend Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        frontendCanvas = canvasObject;
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        RectTransform background = PanelRect(canvasObject.transform, "Background", Background, Vector2.zero, new Vector2(1920f, 1080f));
        Stretch(background);
        RectTransform orangeRail = PanelRect(background, "Orange Rail", Accent, new Vector2(-925f, 0f), new Vector2(10f, 1080f));
        orangeRail.anchorMin = orangeRail.anchorMax = new Vector2(0.5f, 0.5f);
        PanelRect(background, "Top Shade", Panel, new Vector2(0f, 500f), new Vector2(1920f, 80f));
        Text(background, "Brand", "RALLY / CLUBSPORT", 24f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-600f, 500f), new Vector2(600f, 40f), Accent);
        Text(background, "Build", "TIERRA · BOSQUE · MONTAÑA", 18f, FontStyles.Normal, TextAlignmentOptions.Right, new Vector2(680f, 500f), new Vector2(500f, 40f), Muted);

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

        if (screen == RallyMenuScreen.Selection && RallyGameSession.LocalPlayerCount == 2) return;

        // The buttons are created in reading order: tracks, difficulties, then actions.
        // D-Pad up/down (and left/right) can therefore reach every option without a mouse.
        menuNavigation = canvasObject.AddComponent<RallyMenuNavigation>();
        menuNavigation.Configure(canvasObject.GetComponentsInChildren<Button>(true));
        menuNavigation.SetFocusColor(new Color(1f, .78f, .32f, 1f));
        menuNavigation.FocusChanged += PreviewFocusedVehicle;
        if (screen == RallyMenuScreen.MainMenu) menuNavigation.enabled = false;
    }

    void BuildMainMenu(Transform root)
    {
        Text(root, "Eyebrow", "GRAVA  /  VELOCIDAD  /  CONTROL", 23f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-455f, 245f), new Vector2(770f, 42f), Accent);
        Text(root, "Title", "RALLY", 150f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-420f, 100f), new Vector2(840f, 190f), Color.white);
        Text(root, "Subtitle", "Tres clásicos. Tres pistas. Todo por recorrer.", 34f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(-390f, -25f), new Vector2(900f, 60f), Muted);
        PanelRect(root, "Title Accent", Accent, new Vector2(-862f, 94f), new Vector2(12f, 250f));

        RectTransform card = PanelRect(root, "Start Card", Panel, new Vector2(525f, -15f), new Vector2(580f, 440f));
        mainMenuCard = card.gameObject;
        Text(card, "Card Label", "PRÓXIMA ETAPA", 21f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 150f), new Vector2(460f, 36f), Accent);
        Text(card, "Circuit", RallyGameSession.SelectedCircuit.Replace(' ', '_').ToUpperInvariant(), 44f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 87f), new Vector2(460f, 60f), Color.white);
        Text(card, "Details", "3 PISTAS DISPONIBLES\n" + RallyGameSession.SelectedVehicleDisplayName.ToUpperInvariant(), 23f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 5f), new Vector2(460f, 80f), Muted);
        Button(card, "Play Button", "JUGAR", new Vector2(0f, -125f), new Vector2(470f, 86f), StartSelection, true);
        mainMenuHint = Text(root, "Hint", "CRUZ / ENTER: seleccionar    D-PAD: navegar    ESC: volver", 18f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(-355f, -475f), new Vector2(970f, 32f), new Color(Muted.r, Muted.g, Muted.b, 0.85f)).gameObject;
        introCard = PanelRect(root, "Continue Card", Panel, new Vector2(525f, -15f), new Vector2(580f, 280f)).gameObject;
        Text(introCard.transform, "Continue Label", "PRESIONÁ CUALQUIER BOTÓN\nPARA CONTINUAR", 32f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, new Vector2(520f, 120f), Accent);
        introPrompt = introCard.GetComponentInChildren<TextMeshProUGUI>();
        mainMenuCard.SetActive(false);
        mainMenuHint.SetActive(false);
        waitingForStartInput = true;
    }

    void BuildSelection(Transform root)
    {
        if (RallyGameSession.LocalPlayerCount == 2) { BuildLocalSelection(root); return; }
        Text(root, "Title", "PREPARÁ TU PRÓXIMA ETAPA", 58f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 378f), new Vector2(1400f, 74f), Color.white);
        Text(root, "Subtitle", "Elegí tu clásico. Encontrá tu terreno. Salí a competir.", 22f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 315f), new Vector2(1400f, 38f), Muted);

        RectTransform carCard = PanelRect(root, "Vehicle Card", Panel, new Vector2(-365f, 25f), new Vector2(670f, 500f));
        PanelRect(carCard, "Selected Border", Accent, new Vector2(-332f, 0f), new Vector2(6f, 500f));
        Text(carCard, "Category", "01 / TU AUTO", 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-50f, 205f), new Vector2(480f, 34f), Accent);
        selectedVehicleCounter = Text(carCard, "Vehicle Counter", "", 22f, FontStyles.Normal, TextAlignmentOptions.Right, new Vector2(240f, 205f), new Vector2(100f, 34f), Muted);
        selectedVehicleTitle = Text(carCard, "Selected Vehicle", "", 46f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 143f), new Vector2(580f, 60f), Color.white);
        Text(carCard, "Vehicle Details", "TRES CLÁSICOS. UN MISMO ESPÍRITU.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 96f), new Vector2(580f, 34f), Muted);
        vehicleButtons = new Button[RallyPlayerVehicleSelection.Names.Length];
        for (int i = 0; i < vehicleButtons.Length; i++)
        {
            int choice = i;
            vehicleButtons[i] = Button(carCard, "Vehicle " + i + " Button", RallyGameSession.VehicleDisplayName(i),
                new Vector2(0f, 25f - 70f * i), new Vector2(580f, 58f), () => { RallyGameSession.SelectVehicle(choice); RefreshVehicleSelection(); }, false);
        }
        Text(carCard, "Handling Note", "MISMO MANEJO ARCADE · DISTINTO ESTILO", 17f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, -172f), new Vector2(580f, 30f), Muted);
        Text(carCard, "Asset Credit", "Mini: Gilang Romadhan · CC-BY 3.0\nDelta: TARANTULA · CC-BY 4.0 · modelos adaptados", 14f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, -216f), new Vector2(580f, 38f), Muted);
        RefreshVehicleSelection();

        RectTransform circuitCard = PanelRect(root, "Circuit Card", Panel, new Vector2(365f, 25f), new Vector2(670f, 500f));
        Text(circuitCard, "Category", "02 / TU TERRENO", 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 205f), new Vector2(580f, 34f), Accent);
        selectedCircuitTitle = Text(circuitCard, "Selected Circuit", "", 36f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 151f), new Vector2(580f, 46f), Color.white);
        selectedTrackPreview = Preview(circuitCard, "Selected Track Preview", new Vector2(0f, 58f), new Vector2(580f, 126f));
        selectedCircuitDetails = Text(circuitCard, "Circuit Details", "", 17f, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, -26f), new Vector2(580f, 28f), Muted);
        string[] options = { "CIRCUIT_01  ·  RALLYCROSS", "CIRCUIT_02  ·  DESIERTO", "CIRCUIT_03  ·  BOSQUE" };
        circuitButtons = new Button[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int choice = i;
            circuitButtons[i] = Button(circuitCard, "Circuit " + (i + 1) + " Button", options[i],
                new Vector2(0f, -85f - 64f * i), new Vector2(580f, 52f), () => ChooseCircuit(choice), false);
            RawImage thumbnail = Preview(circuitButtons[i].transform, "Track Thumbnail", new Vector2(-245f, 0f), new Vector2(80f, 46f));
            SetPreview(thumbnail, i);
            RectTransform label = circuitButtons[i].GetComponentInChildren<TextMeshProUGUI>().rectTransform;
            label.anchoredPosition = new Vector2(42f, 0f);
            label.sizeDelta = new Vector2(480f, 46f);
        }
        RefreshCircuitSelection();

        RectTransform difficultyCard = PanelRect(root, "Bot Difficulty Card", Panel, new Vector2(0f, -278f), new Vector2(1400f, 84f));
        Text(difficultyCard, "Category", "DIFICULTAD BOTS", 22f, FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(-515f, 0f), new Vector2(290f, 42f), Accent);
        string[] difficulties = { "FÁCIL", "MEDIO", "DIFÍCIL" };
        difficultyButtons = new Button[difficulties.Length];
        for (int i = 0; i < difficulties.Length; i++)
        {
            int choice = i;
            difficultyButtons[i] = Button(difficultyCard, "Difficulty " + difficulties[i] + " Button", difficulties[i],
                new Vector2(-225f + i * 310f, 0f), new Vector2(260f, 70f), () => ChooseDifficulty(choice), false);
        }
        RefreshDifficultySelection();

        RectTransform playersCard = PanelRect(root, "Local Players Card", Panel, new Vector2(0f, -365f), new Vector2(1400f, 68f));
        Text(playersCard, "Category", "JUGADORES", 20f, FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(-535f, 0f), new Vector2(250f, 36f), Accent);
        localPlayerButtons = new Button[2];
        for (int i = 0; i < 2; i++)
        {
            int count = i + 1;
            localPlayerButtons[i] = Button(playersCard, "Local Players " + count, count == 1 ? "1 JUGADOR" : "2 JUGADORES · PANTALLA DIVIDIDA",
                new Vector2(i == 0 ? -230f : 230f, 0f), new Vector2(i == 0 ? 280f : 600f, 52f),
                () => ChangePlayerCount(count), false);
        }
        RefreshLocalPlayers();
        Text(root, "Split Controls", "2 jugadores: J1 mando 1 · J2 mando 2 (o teclado si hay un solo mando) · sin bots", 17f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Vector2(0f, -412f), new Vector2(1400f, 30f), Muted);
        Button(root, "Back Button", "VOLVER", new Vector2(-265f, -472f), new Vector2(300f, 74f), BackToMenu, false);
        Button(root, "Race Button", "COMENZAR CARRERA", new Vector2(180f, -472f), new Vector2(520f, 74f), StartRace, true);
    }

    void ChangePlayerCount(int count)
    {
        RallyGameSession.SelectLocalPlayers(count);
        BuildInterface();
    }

    void BuildLocalSelection(Transform root)
    {
        Text(root, "Title", "CARRERA LOCAL · DOS PILOTOS", 48f, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 394f), new Vector2(1500f, 70f), Color.white);
        Text(root, "Subtitle", "Cada piloto elige su auto y confirma LISTO. J1 elige la pista y comienza la carrera.", 21f,
            FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, 342f), new Vector2(1600f, 40f), Muted);
        localVehicleButtons = new Button[2][];
        localReadyButtons = new Button[2];
        localVehicleTitles = new TextMeshProUGUI[2];
        localDeviceLabels = new TextMeshProUGUI[2];
        localNavigations = new RallyMenuNavigation[2];
        for (int p = 0; p < 2; p++)
        {
            int player = p;
            Color playerColor = p == 0 ? Accent : new Color(.2f, .78f, 1f);
            var card = PanelRect(root, "Player " + (p + 1) + " Selection", Panel, new Vector2(p == 0 ? -405f : 405f, 80f), new Vector2(770f, 470f));
            PanelRect(card, "Player Accent", playerColor, new Vector2(-380f, 0f), new Vector2(8f, 470f));
            Text(card, "Player Title", "JUGADOR " + (p + 1), 32f, FontStyles.Bold, TextAlignmentOptions.Left,
                new Vector2(-125f, 182f), new Vector2(430f, 50f), playerColor);
            localDeviceLabels[p] = Text(card, "Device", RallyLocalDevices.Label(p), 18f, FontStyles.Normal,
                TextAlignmentOptions.Right, new Vector2(215f, 182f), new Vector2(270f, 44f), Muted);
            localVehicleTitles[p] = Text(card, "Selected Vehicle", "", 40f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, 122f), new Vector2(680f, 56f), Color.white);
            localVehicleButtons[p] = new Button[3];
            for (int v = 0; v < 3; v++)
            {
                int choice = v;
                localVehicleButtons[p][v] = Button(card, "Player " + (p + 1) + " Vehicle " + v,
                    RallyGameSession.VehicleDisplayName(v), new Vector2(0f, 46f - v * 68f), new Vector2(640f, 56f),
                    () => ChooseLocalVehicle(player, choice), false);
            }
            localReadyButtons[p] = Button(card, "Player " + (p + 1) + " Ready", "LISTO",
                new Vector2(0f, -176f), new Vector2(640f, 65f), () => { localReady[player] = !localReady[player]; RefreshLocalSelection(); }, true);
            localNavigations[p] = card.gameObject.AddComponent<RallyMenuNavigation>();
            localNavigations[p].ConfigureForLocalPlayer(p);
        }
        var track = PanelRect(root, "Shared Circuit Card", Panel, new Vector2(0f, -231f), new Vector2(1580f, 132f));
        selectedCircuitTitle = Text(track, "Selected Circuit", "", 25f, FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(-530f, 38f), new Vector2(480f, 40f), Accent);
        selectedCircuitDetails = Text(track, "Circuit Details", "", 17f, FontStyles.Normal, TextAlignmentOptions.Right,
            new Vector2(440f, 38f), new Vector2(650f, 32f), Muted);
        selectedTrackPreview = Preview(track, "Selected Track Preview", new Vector2(-720f, -22f), new Vector2(88f, 50f));
        circuitButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int choice = i;
            circuitButtons[i] = Button(track, "Circuit " + (i + 1) + " Button", "CIRCUIT_0" + (i + 1),
                new Vector2(-440f + i * 475f, -25f), new Vector2(430f, 55f), () => ChooseCircuit(choice), false);
        }
        Text(root, "No Bots", "SOLO J1 Y J2 · SIN BOTS", 19f, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, -326f), new Vector2(1000f, 32f), Muted);
        localPlayerButtons = new Button[2];
        localPlayerButtons[0] = Button(root, "Local Players 1", "1 JUGADOR", new Vector2(-435f, -373f), new Vector2(290f, 55f), () => ChangePlayerCount(1), false);
        localPlayerButtons[1] = Button(root, "Local Players 2", "2 JUGADORES", new Vector2(-80f, -373f), new Vector2(330f, 55f), () => ChangePlayerCount(2), false);
        Button back = Button(root, "Back Button", "VOLVER", new Vector2(-380f, -457f), new Vector2(330f, 73f), BackToMenu, false);
        localRaceButton = Button(root, "Race Button", "COMENZAR CARRERA", new Vector2(255f, -457f), new Vector2(660f, 73f), StartRace, true);
        localNavigations[0].Configure(localVehicleButtons[0][0], localVehicleButtons[0][1], localVehicleButtons[0][2],
            localReadyButtons[0], circuitButtons[0], circuitButtons[1], circuitButtons[2], localPlayerButtons[0], back, localRaceButton);
        localNavigations[1].Configure(localVehicleButtons[1][0], localVehicleButtons[1][1], localVehicleButtons[1][2], localReadyButtons[1]);
        localNavigations[0].SetFocusColor(Accent);
        localNavigations[1].SetFocusColor(new Color(.2f, .78f, 1f));
        RefreshCircuitSelection();
        RefreshLocalPlayers();
        RefreshLocalSelection();
    }

    void ChooseLocalVehicle(int player, int index)
    {
        RallyGameSession.SelectVehicleForPlayer(player, index);
        localReady[player] = false;
        RefreshLocalSelection();
    }

    void RefreshLocalSelection()
    {
        if (localVehicleButtons == null) return;
        for (int p = 0; p < 2; p++)
        {
            int selected = RallyGameSession.VehicleIndexForPlayer(p);
            localVehicleTitles[p].text = RallyGameSession.VehicleDisplayName(selected);
            for (int v = 0; v < 3; v++)
            {
                bool active = v == selected;
                Button button = localVehicleButtons[p][v];
                ColorBlock colors = button.colors;
                colors.normalColor = active ? Accent : PanelLight;
                colors.selectedColor = colors.highlightedColor = active ? new Color(1f, .68f, .25f) : new Color(.28f, .22f, .15f);
                button.colors = colors;
                button.GetComponentInChildren<TextMeshProUGUI>().color = active ? new Color(.1f, .07f, .04f) : Color.white;
            }
            localReadyButtons[p].GetComponentInChildren<TextMeshProUGUI>().text = localReady[p] ? "LISTO" : "CONFIRMAR · LISTO";
        }
        localRaceButton.interactable = localReady[0] && localReady[1];
    }

    void BuildResults(Transform root)
    {
        Text(root, "Eyebrow", "ETAPA COMPLETADA", 25f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(800f, 40f), Accent);
        Text(root, "Title", "RESULTADOS", 70f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 220f), new Vector2(900f, 90f), Color.white);
        RectTransform resultCard = PanelRect(root, "Result Card", Panel, new Vector2(0f, 25f), new Vector2(820f, 285f));
        Text(resultCard, "Time Label", "TIEMPO FINAL", 22f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 86f), new Vector2(500f, 35f), Muted);
        Text(resultCard, "Final Time", RallyGameSession.FormatTime(RallyGameSession.LastRaceTime), 78f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(700f, 90f), Color.white);
        Text(resultCard, "Run Details", $"{RallyGameSession.SelectedVehicleDisplayName.ToUpperInvariant()}  •  {RallyGameSession.SelectedCircuit.ToUpperInvariant()}", 20f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -84f), new Vector2(720f, 34f), Muted);

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
        if (RallyGameSession.LocalPlayerCount == 2 && (!localReady[0] || !localReady[1])) return;
        RallyGameSession.SelectCurrentOptions();
        SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);
    }

    void RetryRace() => SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);

    void ChooseCircuit(int index)
    {
        RallyGameSession.SelectCircuit(index);
        RefreshCircuitSelection();
        if (localVehicleButtons != null)
        {
            localReady[0] = localReady[1] = false;
            RefreshLocalSelection();
        }
    }

    void RefreshLocalPlayers()
    {
        if (difficultyButtons != null)
        {
            foreach (Button button in difficultyButtons)
                button.interactable = RallyGameSession.LocalPlayerCount == 1;
            difficultyButtons[0].transform.parent.Find("Category").GetComponent<TMP_Text>().text =
                RallyGameSession.LocalPlayerCount == 1 ? "DIFICULTAD BOTS" : "SIN BOTS";
        }
        for (int i = 0; i < localPlayerButtons.Length; i++)
        {
            var colors = localPlayerButtons[i].colors;
            colors.normalColor = i + 1 == RallyGameSession.LocalPlayerCount ? Accent : PanelLight;
            localPlayerButtons[i].colors = colors;
            localPlayerButtons[i].GetComponent<Image>().color = colors.normalColor;
        }
    }

    void RefreshVehicleSelection()
    {
        selectedVehicleTitle.text = RallyGameSession.SelectedVehicleDisplayName;
        selectedVehicleCounter.text = $"{RallyGameSession.SelectedVehicleIndex + 1}/{vehicleButtons.Length}";
        for (int i = 0; i < vehicleButtons.Length; i++)
        {
            bool active = RallyPlayerVehicleSelection.Names[i] == RallyGameSession.SelectedVehicle;
            Color baseColor = active ? Accent : PanelLight;
            vehicleButtons[i].GetComponent<Image>().color = Color.white;
            vehicleButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = RallyGameSession.VehicleDisplayName(i);
            vehicleButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = active ? new Color(.10f, .07f, .04f) : Color.white;
            ColorBlock colors = vehicleButtons[i].colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = active ? new Color(1f, .62f, .18f) : new Color(.14f, .17f, .22f);
            colors.selectedColor = colors.highlightedColor;
            vehicleButtons[i].colors = colors;
        }
    }

    void ChooseDifficulty(int index)
    {
        RallyGameSession.SelectDifficulty(index);
        RefreshDifficultySelection();
    }

    void RefreshDifficultySelection()
    {
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            bool active = i == (int)RallyGameSession.SelectedBotDifficulty;
            Color baseColor = active ? Accent : PanelLight;
            Button button = difficultyButtons[i];
            button.GetComponent<Image>().color = Color.white;
            button.GetComponentInChildren<TextMeshProUGUI>().color = active ? new Color(.10f, .07f, .04f) : Color.white;
            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = active ? new Color(1f, .62f, .18f) : new Color(.14f, .17f, .22f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }
    }

    void RefreshCircuitSelection()
    {
        int selected = Array.IndexOf(RallyGameSession.CircuitNames, RallyGameSession.SelectedCircuit);
        selectedCircuitTitle.text = RallyGameSession.SelectedRaceScene.ToUpperInvariant();
        selectedCircuitDetails.text = selected == 2 ? "BOSQUE / MONTAÑA · TIERRA HÚMEDA" : "DESIERTO / RALLYCROSS · TIERRA Y RIPIO";
        SetPreview(selectedTrackPreview, Mathf.Max(0, selected));
        backdrop.Show(RallyGameSession.SelectedRaceScene);
        for (int i = 0; i < circuitButtons.Length; i++)
        {
            bool active = i == selected;
            Color baseColor = active ? Accent : PanelLight;
            Image image = circuitButtons[i].GetComponent<Image>();
            image.color = Color.white;
            circuitButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = active ? new Color(.10f, .07f, .04f) : Color.white;
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

    void PreviewFocusedVehicle(Button focused)
    {
        if (vehicleButtons == null) return;
        int index = Array.IndexOf(vehicleButtons, focused);
        if (index < 0) return;
        RallyGameSession.SelectVehicle(index);
        RefreshVehicleSelection();
    }

    static RawImage Preview(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    static void SetPreview(RawImage image, int index)
    {
        image.texture = Resources.Load<Texture2D>("MenuPreviews/" + RallyGameSession.CircuitScenes[index]);
        if (image.texture == null) { image.color = Color.clear; return; }
        image.color = Color.white;
        float imageAspect = (float)image.texture.width / image.texture.height;
        float frameAspect = image.rectTransform.sizeDelta.x / image.rectTransform.sizeDelta.y;
        float height = Mathf.Min(1f, imageAspect / frameAspect);
        image.uvRect = new Rect(0f, (1f - height) * .5f, 1f, height);
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
        label.font = size >= 32f || style == FontStyles.Bold ? headingFont : font;
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
        rect.GetComponent<Image>().color = Color.white;
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = primary ? new Color(1f, .68f, .25f) : new Color(.28f, .22f, .15f);
        colors.pressedColor = primary ? new Color(.82f, .42f, .08f) : new Color(.09f, .07f, .05f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(action);
        Text(rect, "Label", label, primary ? 27f : 23f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(20f, 12f), primary ? new Color(.10f, .07f, .04f) : Color.white);
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
