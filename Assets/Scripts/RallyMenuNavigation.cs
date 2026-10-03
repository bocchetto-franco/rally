using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Explicit UI navigation for keyboard and any Input System gamepad (Options is handled by the pause menu).</summary>
[DisallowMultipleComponent]
public sealed class RallyMenuNavigation : MonoBehaviour
{
    Button[] buttons;
    Outline[] outlines;
    Vector3[] originalScales;
    int selectedIndex;
    EventSystem eventSystem;
    bool previousNavigationEvents;
    bool upHeld;
    bool downHeld;
    bool leftHeld;
    bool rightHeld;
    bool southHeld;

    public int SelectedIndex => selectedIndex;

    public void ResetSelection()
    {
        if (buttons != null && buttons.Length > 0) Select(0);
    }

    public void Configure(params Button[] orderedButtons)
    {
        buttons = orderedButtons;
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
        Select(0);
    }

    void OnEnable()
    {
        Gamepad pad = Gamepad.current;
        upHeld = pad != null && pad.dpad.up.isPressed;
        downHeld = pad != null && pad.dpad.down.isPressed;
        leftHeld = pad != null && pad.dpad.left.isPressed;
        rightHeld = pad != null && pad.dpad.right.isPressed;
        southHeld = pad != null && pad.buttonSouth.isPressed;
        eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        previousNavigationEvents = eventSystem.sendNavigationEvents;
        // Keep pointer events for mouse, but avoid a second submit from StandaloneInputModule.
        eventSystem.sendNavigationEvents = false;
    }

    void OnDisable()
    {
        if (eventSystem != null) eventSystem.sendNavigationEvents = previousNavigationEvents;
    }

    void Update()
    {
        if (buttons == null || buttons.Length == 0) return;
        Gamepad pad = Gamepad.current;
        bool up = pad != null && pad.dpad.up.isPressed;
        bool down = pad != null && pad.dpad.down.isPressed;
        bool left = pad != null && pad.dpad.left.isPressed;
        bool right = pad != null && pad.dpad.right.isPressed;
        bool south = pad != null && pad.buttonSouth.isPressed;
        bool previous = (up && !upHeld) || (left && !leftHeld)
            || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool next = (down && !downHeld) || (right && !rightHeld)
            || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow);
        bool confirm = (south && !southHeld)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        upHeld = up;
        downHeld = down;
        leftHeld = left;
        rightHeld = right;
        southHeld = south;
        if (previous) Select((selectedIndex - 1 + buttons.Length) % buttons.Length);
        else if (next) Select((selectedIndex + 1) % buttons.Length);

        if (confirm && buttons[selectedIndex] != null && buttons[selectedIndex].IsInteractable())
            buttons[selectedIndex].onClick.Invoke();
    }

    void Select(int index)
    {
        selectedIndex = index;
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null) outlines[i].enabled = i == selectedIndex;
            if (buttons[i] != null) buttons[i].transform.localScale = originalScales[i] * (i == selectedIndex ? 1.04f : 1f);
        }
        if (eventSystem != null && buttons.Length > selectedIndex && buttons[selectedIndex] != null)
            eventSystem.SetSelectedGameObject(buttons[selectedIndex].gameObject);
    }
}
