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
    bool upHeld, downHeld, leftHeld, rightHeld, southHeld;
    Gamepad previousPad;

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

    public void ConfigureControls(params Selectable[] orderedControls)
    {
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
        previousPad = Pad;
        upHeld = previousPad != null && previousPad.dpad.up.isPressed;
        downHeld = previousPad != null && previousPad.dpad.down.isPressed;
        leftHeld = previousPad != null && previousPad.dpad.left.isPressed;
        rightHeld = previousPad != null && previousPad.dpad.right.isPressed;
        southHeld = previousPad != null && previousPad.buttonSouth.isPressed;
    }

    void OnEnable()
    {
        SeedHeld();
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
        if (eventSystem == null || !navigationUsers.ContainsKey(eventSystem)) return;
        if (--navigationUsers[eventSystem] == 0)
        {
            eventSystem.sendNavigationEvents = previousNavigationEvents[eventSystem];
            navigationUsers.Remove(eventSystem);
            previousNavigationEvents.Remove(eventSystem);
        }
    }

    static bool KeyPressed(Key key, KeyCode legacy) => Input.GetKeyDown(legacy) ||
        (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame);

    void Update()
    {
        if (buttons == null || buttons.Length == 0) return;
        Gamepad pad = Pad;
        if (pad != previousPad) SeedHeld(); // Reconnect without submitting a held button.
        bool up = pad != null && pad.dpad.up.isPressed;
        bool down = pad != null && pad.dpad.down.isPressed;
        bool left = pad != null && pad.dpad.left.isPressed;
        bool right = pad != null && pad.dpad.right.isPressed;
        bool south = pad != null && pad.buttonSouth.isPressed;
        if (localPlayer < 0 && eventSystem != null)
            for (int i = 0; i < buttons.Length; i++)
                if (Usable(i) && i != selectedIndex && eventSystem.currentSelectedGameObject == buttons[i].gameObject)
                { Select(i); break; }
        bool decrease = (left && !leftHeld) || (KeyboardAllowed && KeyPressed(Key.LeftArrow, KeyCode.LeftArrow));
        bool increase = (right && !rightHeld) || (KeyboardAllowed && KeyPressed(Key.RightArrow, KeyCode.RightArrow));
        Slider slider = Usable(selectedIndex) ? buttons[selectedIndex] as Slider : null;
        bool previous = (up && !upHeld) || (slider == null && decrease) ||
            (KeyboardAllowed && KeyPressed(Key.UpArrow, KeyCode.UpArrow));
        bool next = (down && !downHeld) || (slider == null && increase) ||
            (KeyboardAllowed && KeyPressed(Key.DownArrow, KeyCode.DownArrow));
        bool confirm = (south && !southHeld) ||
            (KeyboardAllowed && (KeyPressed(Key.Enter, KeyCode.Return) || KeyPressed(Key.NumpadEnter, KeyCode.KeypadEnter)));
        upHeld = up; downHeld = down; leftHeld = left; rightHeld = right; southHeld = south;
        if (previous) Move(-1);
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
