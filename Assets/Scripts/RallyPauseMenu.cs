using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Race pause menu. DualSense Options maps to Gamepad.startButton.</summary>
[DisallowMultipleComponent]
public sealed class RallyPauseMenu : MonoBehaviour
{
    RallyCheckpointManager checkpointManager;
    GameObject canvasObject;
    bool isOpen;
    bool cursorWasVisible;
    bool optionsHeld;
    CursorLockMode previousCursorLock;

    public bool IsOpen => isOpen;
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
        if (checkpointManager != null && checkpointManager.IsFinished)
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
            if (isOpen) Close(true);
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
        canvasObject.GetComponent<RallyMenuNavigation>().ResetSelection();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    void Close(bool resumeTime)
    {
        isOpen = false;
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
        Button menu = CreateButton(card, font, "Main Menu Button", "VOLVER AL MENÚ PRINCIPAL", new Vector2(0f, -135f), BackToMainMenu, false);
        Label(card, font, "Hint", "D-PAD: navegar   •   CRUZ: elegir   •   OPTIONS: reanudar", 18f,
            new Vector2(0f, -225f), new Vector2(620f, 35f));
        canvasObject.AddComponent<RallyMenuNavigation>().Configure(resume, restart, menu);
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
