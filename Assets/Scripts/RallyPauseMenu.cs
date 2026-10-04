using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Race pause menu. Escape and gamepad Start/Options toggle the same pause state.</summary>
[DisallowMultipleComponent]
public sealed class RallyPauseMenu : MonoBehaviour
{
    RallyCheckpointManager checkpointManager;
    GameObject canvasObject;
    GameObject confirmationObject;
    RallyMenuNavigation pauseNavigation;
    RallyMenuNavigation confirmationNavigation;
    bool isOpen;
    bool cursorWasVisible;
    bool optionsHeld;
    CursorLockMode previousCursorLock;

    public bool IsOpen => isOpen;
    public bool IsConfirmingExit => confirmationObject != null && confirmationObject.activeSelf;
    public void Configure(RallyCheckpointManager manager) => checkpointManager = manager;

    void Start()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        BuildMenu();
        canvasObject.SetActive(false);
    }

    void Update()
    {
        if (checkpointManager != null && (RallySplitScreen.Active != null ?
            RallySplitScreen.Active.AllFinished : checkpointManager.IsFinished))
        {
            if (isOpen) Close(false);
            return;
        }

        Gamepad pad = Gamepad.current;
        bool options = pad != null && pad.startButton.isPressed;
        bool toggle = (options && !optionsHeld) || Input.GetKeyDown(KeyCode.Escape);
        optionsHeld = options;
        if (toggle)
        {
            if (IsConfirmingExit) CancelExit();
            else if (isOpen) Close(true);
            else Open();
        }
    }

    void OnDisable()
    {
        if (isOpen) Close(true);
    }

    void Open()
    {
        if (canvasObject == null) return;
        cursorWasVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        isOpen = true;
        canvasObject.SetActive(true);
        pauseNavigation.enabled = true;
        pauseNavigation.ResetSelection();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    void Close(bool resumeTime)
    {
        isOpen = false;
        if (confirmationObject != null) confirmationObject.SetActive(false);
        if (canvasObject != null) canvasObject.SetActive(false);
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = previousCursorLock;
        if (resumeTime) Time.timeScale = 1f;
    }

    void RestartRace()
    {
        Close(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void ConfirmExit()
    {
        pauseNavigation.enabled = false;
        confirmationObject.SetActive(true);
        confirmationNavigation.ResetSelection();
    }

    void CancelExit()
    {
        confirmationObject.SetActive(false);
        pauseNavigation.enabled = true;
        pauseNavigation.ResetSelection();
    }

    void BackToMainMenu()
    {
        Close(true);
        SceneManager.LoadScene(RallyGameSession.MainMenuScene);
    }

    void BuildMenu()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
        canvasObject = new GameObject("Race Pause Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        RectTransform shade = Panel(canvasObject.transform, "Pause Shade", new Color(.01f, .015f, .025f, .8f), Vector2.zero, new Vector2(1920f, 1080f));
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = Vector2.zero;
        shade.offsetMax = Vector2.zero;
        RectTransform card = Panel(shade, "Pause Card", new Color(.045f, .06f, .085f, .98f), Vector2.zero, new Vector2(700f, 550f));
        Panel(card, "Accent", new Color(1f, .48f, .06f), new Vector2(-344f, 0f), new Vector2(12f, 550f));
        Label(card, font, "Title", "PAUSA", 60f, new Vector2(0f, 185f), new Vector2(600f, 80f));
        Button resume = CreateButton(card, font, "Resume Button", "REANUDAR", new Vector2(0f, 65f), () => Close(true), true);
        Button restart = CreateButton(card, font, "Restart Button", "REINICIAR CARRERA", new Vector2(0f, -35f), RestartRace, false);
        Button menu = CreateButton(card, font, "Main Menu Button", "VOLVER AL MENÚ PRINCIPAL", new Vector2(0f, -135f), ConfirmExit, false);
        Label(card, font, "Hint", "D-PAD: navegar   •   CRUZ: elegir   •   ESC / OPTIONS: reanudar", 18f,
            new Vector2(0f, -225f), new Vector2(620f, 35f));
        pauseNavigation = canvasObject.AddComponent<RallyMenuNavigation>();
        pauseNavigation.Configure(resume, restart, menu);

        confirmationObject = new GameObject("Confirm Exit", typeof(RectTransform), typeof(Image));
        confirmationObject.transform.SetParent(canvasObject.transform, false);
        RectTransform veil = (RectTransform)confirmationObject.transform;
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.offsetMin = Vector2.zero;
        veil.offsetMax = Vector2.zero;
        confirmationObject.GetComponent<Image>().color = new Color(.01f, .015f, .025f, .9f);
        RectTransform confirmationCard = Panel(veil, "Confirmation Card", new Color(.045f, .06f, .085f, 1f),
            Vector2.zero, new Vector2(740f, 390f));
        Label(confirmationCard, font, "Confirmation Title", "¿SEGURO?", 48f,
            new Vector2(0f, 105f), new Vector2(660f, 70f));
        Label(confirmationCard, font, "Progress Warning", "Se perderá el progreso de la vuelta actual.", 26f,
            new Vector2(0f, 30f), new Vector2(680f, 65f));
        Button no = CreateButton(confirmationCard, font, "No Button", "NO, SEGUIR EN LA CARRERA",
            new Vector2(0f, -60f), CancelExit, true);
        Button yes = CreateButton(confirmationCard, font, "Yes Button", "SÍ, VOLVER AL MENÚ",
            new Vector2(0f, -145f), BackToMainMenu, false);
        confirmationNavigation = confirmationObject.AddComponent<RallyMenuNavigation>();
        confirmationNavigation.Configure(no, yes);
        confirmationObject.SetActive(false);
    }

    static RectTransform Panel(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    static void Label(Transform parent, TMP_FontAsset font, string name, string caption, float fontSize, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = caption;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    static Button CreateButton(Transform parent, TMP_FontAsset font, string name, string caption, Vector2 position,
        UnityEngine.Events.UnityAction action, bool primary)
    {
        Color color = primary ? new Color(1f, .48f, .06f) : new Color(.105f, .13f, .17f);
        RectTransform rect = Panel(parent, name, color, position, new Vector2(560f, 76f));
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = primary ? new Color(1f, .62f, .18f) : new Color(.17f, .2f, .26f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = primary ? new Color(.82f, .32f, .02f) : new Color(.055f, .07f, .1f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Label(rect, font, "Label", caption, 23f, Vector2.zero, new Vector2(530f, 60f));
        return button;
    }
}
