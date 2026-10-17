using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Independent focus/input per local player, plus the existing shared single-player UI.</summary>
[DisallowMultipleComponent]
public sealed class RallyMenuNavigation : MonoBehaviour
{
    public event System.Action<Button> FocusChanged;
    static readonly Dictionary<EventSystem, int> navigationUsers = new Dictionary<EventSystem, int>();
    static readonly Dictionary<EventSystem, bool> previousNavigationEvents = new Dictionary<EventSystem, bool>();
    Selectable[] buttons;
    Outline[] outlines;
    Vector3[] originalScales;
    int selectedIndex;
    int localPlayer = -1;
    bool keyboardOverride;
    EventSystem eventSystem;
    bool southHeld;
    bool suppressSouthUntilRelease;
    int heldDirection;
    int activatedFrame;
    float nextMoveTime;
    const float RepeatDelay = .35f, RepeatInterval = .12f;
    Gamepad previousPad;
    Button[] vehicleCycle;

    public int SelectedIndex => selectedIndex;
    public int LocalPlayer => localPlayer;
    Gamepad Pad => localPlayer >= 0 ? RallyLocalDevices.GamepadFor(localPlayer) :
        RallyGameSession.LocalPlayerCount == 2 ? RallyLocalDevices.GamepadFor(0) : Gamepad.current;
    bool KeyboardAllowed => localPlayer < 0 || keyboardOverride || RallyLocalDevices.UsesKeyboard(localPlayer);

    public void ConfigureForLocalPlayer(int player, bool allowKeyboard = false)
    {
        localPlayer = Mathf.Clamp(player, 0, 1);
        keyboardOverride = allowKeyboard;
        SeedHeld();
    }

    public void SetFocusColor(Color color)
    {
        if (outlines == null) return;
        foreach (Outline outline in outlines) if (outline != null) outline.effectColor = color;
    }

    public void ResetSelection()
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
            if (Usable(i)) { Select(i); return; }
    }

    public void Configure(params Button[] orderedButtons)
        => ConfigureControls(orderedButtons);

    // Only selection panels opt in. Sliders and all other menus retain their controls.
    public void ConfigureVehicleCycle(Button[] cars) => vehicleCycle = cars;

    public void FocusVehicle(Button car)
    {
        if (buttons == null) return;
        int index = System.Array.IndexOf(buttons, car);
        if (Usable(index)) Select(index);
    }

    public void ConfigureControls(params Selectable[] orderedControls)
    {
        AttachEventSystem();
        buttons = orderedControls;
        outlines = new Outline[buttons.Length];
        originalScales = new Vector3[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            Outline outline = buttons[i].GetComponent<Outline>() ?? buttons[i].gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.15f, .9f, 1f, 1f);
            outline.effectDistance = new Vector2(7f, -7f);
            outlines[i] = outline;
            originalScales[i] = buttons[i].transform.localScale;
        }
        ResetSelection();
    }

    void SeedHeld()
    {
        activatedFrame = Time.frameCount;
        previousPad = Pad;
        // Suppress only the press that opened this page, including a quick tap
        // released within the same input update. The next new press is valid.
        southHeld = previousPad != null && (previousPad.buttonSouth.isPressed || previousPad.buttonSouth.wasPressedThisFrame);
        suppressSouthUntilRelease = southHeld;
        heldDirection = ReadDirection(previousPad, 0);
        nextMoveTime = Time.unscaledTime + RepeatDelay;
    }

    void OnEnable()
    {
        foreach (Gamepad pad in Gamepad.all) TrackPresses(pad);
        InputSystem.onDeviceChange += DeviceChanged;
        SeedHeld();
        AttachEventSystem();
    }

    static void DeviceChanged(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad pad && (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected))
            TrackPresses(pad);
    }

    static void TrackPresses(Gamepad pad)
    {
        // ButtonControl starts tracking short press/release transitions on its
        // first query. Prime every connected pad before its first menu event.
        _ = pad.buttonSouth.wasPressedThisFrame;
        _ = pad.dpad.up.wasPressedThisFrame;
        _ = pad.dpad.down.wasPressedThisFrame;
        _ = pad.dpad.left.wasPressedThisFrame;
        _ = pad.dpad.right.wasPressedThisFrame;
        _ = pad.leftTrigger.wasPressedThisFrame;
    }

    void AttachEventSystem()
    {
        if (!isActiveAndEnabled || eventSystem == EventSystem.current && eventSystem != null) return;
        ReleaseEventSystem();
        eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        if (!navigationUsers.ContainsKey(eventSystem))
        {
            navigationUsers[eventSystem] = 0;
            previousNavigationEvents[eventSystem] = eventSystem.sendNavigationEvents;
        }
        navigationUsers[eventSystem]++;
        // Pointer events stay enabled, but the global UI module must not submit twice
        // or move both local players through a single EventSystem focus.
        eventSystem.sendNavigationEvents = false;
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= DeviceChanged;
        ReleaseEventSystem();
    }

    void ReleaseEventSystem()
    {
        if (eventSystem == null || !navigationUsers.ContainsKey(eventSystem)) { eventSystem = null; return; }
        if (--navigationUsers[eventSystem] == 0)
        {
            eventSystem.sendNavigationEvents = previousNavigationEvents[eventSystem];
            navigationUsers.Remove(eventSystem);
            previousNavigationEvents.Remove(eventSystem);
        }
        eventSystem = null;
    }

    // One dominant stick axis prevents diagonal double steps. Separate press/
    // release thresholds prevent drift near the deadzone from moving the focus.
    static int ReadDirection(Gamepad pad, int previous)
    {
        if (pad == null) return 0;
        if (pad.dpad.up.isPressed) return -1;
        if (pad.dpad.down.isPressed) return 1;
        if (pad.dpad.left.isPressed) return -2;
        if (pad.dpad.right.isPressed) return 2;
        Vector2 stick = pad.leftStick.ReadValue();
        float threshold = previous == 0 ? .55f : .35f;
        if (Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y)) >= threshold)
            return Mathf.Abs(stick.y) >= Mathf.Abs(stick.x) ? (stick.y > 0 ? -1 : 1) : (stick.x > 0 ? 2 : -2);
        if (pad.leftTrigger.ReadValue() >= (previous == 1 ? .35f : .55f)) return 1;
        return 0;
    }

    int DirectionPressed(Gamepad pad)
    {
        int direction = ReadDirection(pad, heldDirection);
        // Digital taps can begin and end before Update; isPressed alone loses them.
        if (direction == 0 && pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) direction = -1;
            else if (pad.dpad.down.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame) direction = 1;
            else if (pad.dpad.left.wasPressedThisFrame) direction = -2;
            else if (pad.dpad.right.wasPressedThisFrame) direction = 2;
        }
        bool move = direction != 0 && (direction != heldDirection || Time.unscaledTime >= nextMoveTime);
        if (move) nextMoveTime = Time.unscaledTime + (direction == heldDirection ? RepeatInterval : RepeatDelay);
        heldDirection = direction;
        return move ? direction : 0;
    }

    static bool KeyPressed(Key key, KeyCode legacy) => Input.GetKeyDown(legacy) ||
        (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame);

    void Update()
    {
        if (RallyRaceLoading.IsInputBlocked) return;
        if (buttons == null || buttons.Length == 0) return;
        AttachEventSystem();
        // A newly opened page must not also consume its opening keyboard/gamepad event.
        if (Time.frameCount == activatedFrame) return;
        Gamepad pad = Pad;
        if (pad != previousPad)
        {
            previousPad = pad;
            // Gamepad.current can change on this very press: do not swallow it.
            // A reconnected controller with an already-held button stays blocked.
            southHeld = pad != null && pad.buttonSouth.isPressed && !pad.buttonSouth.wasPressedThisFrame;
            suppressSouthUntilRelease = southHeld;
            heldDirection = 0;
        }
        int direction = DirectionPressed(pad);
        bool south = pad != null && pad.buttonSouth.isPressed;
        bool southPressed = pad != null && pad.buttonSouth.wasPressedThisFrame;
        if (pad == null || !south || pad.buttonSouth.wasReleasedThisFrame) suppressSouthUntilRelease = false;
        if (!Usable(selectedIndex)) ResetSelection();
        if (localPlayer < 0 && eventSystem != null)
        {
            bool ownsFocus = false;
            for (int i = 0; i < buttons.Length; i++)
                if (Usable(i) && eventSystem.currentSelectedGameObject == buttons[i].gameObject)
                { ownsFocus = true; if (i != selectedIndex) Select(i); break; }
            if (!ownsFocus && Usable(selectedIndex))
                eventSystem.SetSelectedGameObject(buttons[selectedIndex].gameObject);
        }
        bool decrease = direction == -2 || (KeyboardAllowed && (localPlayer >= 0 ? RallyLocalKeyboard.MenuLeft(localPlayer) : KeyPressed(Key.LeftArrow, KeyCode.LeftArrow)));
        bool increase = direction == 2 || (KeyboardAllowed && (localPlayer >= 0 ? RallyLocalKeyboard.MenuRight(localPlayer) : KeyPressed(Key.RightArrow, KeyCode.RightArrow)));
        Slider slider = Usable(selectedIndex) ? buttons[selectedIndex] as Slider : null;
        bool previous = direction == -1 || (slider == null && decrease) ||
            (KeyboardAllowed && (localPlayer >= 0 ? RallyLocalKeyboard.MenuUp(localPlayer) : KeyPressed(Key.UpArrow, KeyCode.UpArrow)));
        bool next = direction == 1 || (slider == null && increase) ||
            (KeyboardAllowed && (localPlayer >= 0 ? RallyLocalKeyboard.MenuDown(localPlayer) : KeyPressed(Key.DownArrow, KeyCode.DownArrow)));
        bool confirm = (!suppressSouthUntilRelease && (southPressed || south && !southHeld)) ||
            (KeyboardAllowed && (localPlayer >= 0 ? RallyLocalKeyboard.Confirm(localPlayer) : KeyPressed(Key.Enter, KeyCode.Return) || KeyPressed(Key.NumpadEnter, KeyCode.KeypadEnter)));
        southHeld = south;
        int carIndex = vehicleCycle == null || !Usable(selectedIndex) ? -1 : System.Array.IndexOf(vehicleCycle, buttons[selectedIndex]);
        if (carIndex >= 0 && (decrease || increase))
            FocusVehicle(vehicleCycle[(carIndex + (increase ? 1 : vehicleCycle.Length - 1)) % vehicleCycle.Length]);
        else if (previous) Move(-1);
        else if (next) Move(1);
        else if (slider != null && (decrease || increase))
            slider.value = Mathf.Clamp(slider.value + (increase ? .05f : -.05f), slider.minValue, slider.maxValue);
        if (confirm && Usable(selectedIndex) && buttons[selectedIndex] is Button button) button.onClick.Invoke();
    }

    bool Usable(int index) => index >= 0 && index < buttons.Length && buttons[index] != null &&
        buttons[index].gameObject.activeInHierarchy && buttons[index].IsInteractable();

    void Move(int direction)
    {
        for (int step = 1; step <= buttons.Length; step++)
        {
            int candidate = (selectedIndex + step * direction + buttons.Length) % buttons.Length;
            if (Usable(candidate)) { Select(candidate); return; }
        }
    }

    void Select(int index)
    {
        selectedIndex = index;
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null) outlines[i].enabled = i == selectedIndex;
            if (buttons[i] != null) buttons[i].transform.localScale = originalScales[i] * (i == selectedIndex ? 1.04f : 1f);
        }
        // Local panels keep independent outlines; one global selected object cannot represent both.
        if (localPlayer < 0 && eventSystem != null && buttons.Length > selectedIndex && buttons[selectedIndex] != null)
            eventSystem.SetSelectedGameObject(buttons[selectedIndex].gameObject);
        if (buttons[selectedIndex] is Button button) FocusChanged?.Invoke(button);
    }
}
