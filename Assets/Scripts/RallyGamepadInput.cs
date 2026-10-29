using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Optional gamepad input for the player; the bot never reads this class.</summary>
public static class RallyGamepadInput
{
    private static bool resetWasHeld;
    private static bool recoveryWasHeld;
    private static bool turboWasHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEdges() { resetWasHeld = recoveryWasHeld = turboWasHeld = false; }

    public static float Throttle => ReadTrigger(Gamepad.current == null ? 0f :
        Gamepad.current.rightTrigger.ReadValue());

    public static float Brake => ReadTrigger(Gamepad.current == null ? 0f :
        Gamepad.current.leftTrigger.ReadValue());

    public static float Steering => Gamepad.current == null ? 0f :
        Gamepad.current.leftStick.x.ReadValue();

    // West = X on Xbox / Square on PlayStation: hold for drift/handbrake.
    public static bool HandbrakePressed => Gamepad.current != null &&
        Gamepad.current.buttonWest.isPressed;

    // East = B / Circle: right the car, without resetting checkpoint progress.
    public static bool RecoverPressedThisFrame
    {
        get
        {
            bool held = Gamepad.current != null && Gamepad.current.buttonEast.isPressed;
            bool pressed = held && !recoveryWasHeld;
            recoveryWasHeld = held;
            return pressed;
        }
    }

    public static bool ResetPressedThisFrame
    {
        get
        {
            bool held = Gamepad.current != null && Gamepad.current.rightStickButton.isPressed;
            bool pressed = held && !resetWasHeld;
            resetWasHeld = held;
            return pressed;
        }
    }

    public static bool TurboPressedThisFrame
    {
        get {
            bool held = Gamepad.current != null && Gamepad.current.buttonNorth.isPressed;
            bool pressed = held && !turboWasHeld; turboWasHeld = held; return pressed;
        }
    }

    public static float GetVerticalInput(float legacyInput)
    {
        if (Mathf.Abs(legacyInput) > 0.1f)
            return legacyInput;
        return Throttle - Brake;
    }

    public static float GetHorizontalInput(float legacyInput)
    {
        return Mathf.Abs(legacyInput) > 0.1f ? legacyInput : Steering;
    }

    // The legacy steering value eases back to zero after a key is released.
    // Do not let that residual value mask a newly moved gamepad stick.
    public static float GetHorizontalInput(float legacyInput, bool legacySteeringHeld)
    {
        if (legacySteeringHeld)
            return legacyInput;
        float gamepadSteering = Steering;
        return Mathf.Abs(gamepadSteering) > 0.1f ? gamepadSteering : legacyInput;
    }

    // Input Manager's Horizontal/Vertical axes also include joystick axes.
    // The fallback scenes must read keys explicitly so a stick cannot masquerade
    // as keyboard input and override the analog trigger/stick mapping.
    public static float KeyboardVertical => Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)
        ? 1f : Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;

    public static float KeyboardHorizontal => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)
        ? -1f : Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;

    private static float ReadTrigger(float value) => value < 0.02f ? 0f : Mathf.Clamp01(value);
}
