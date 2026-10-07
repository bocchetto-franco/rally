using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Exclusive device ownership for local players; no global input consumption.</summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class RallyLocalPlayerInput : MonoBehaviour
{
    public enum InputDevice { Keyboard, Gamepad, Automatic }
    [SerializeField] InputDevice device;
    [SerializeField] int playerIndex;
    [SerializeField] float steeringResponse = 14f;
    Gamepad assignedGamepad;
    bool resetHeld, recoverHeld;
    public float Vertical { get; private set; }
    public float Horizontal { get; private set; }
    public float Brake { get; private set; }
    public bool Handbrake { get; private set; }
    public bool ResetPressed { get; private set; }
    public bool RecoverPressed { get; private set; }
    public InputDevice Device => device == InputDevice.Automatic ?
        (RallyLocalDevices.GamepadFor(playerIndex) != null ? InputDevice.Gamepad : InputDevice.Keyboard) : device;
    public int PlayerIndex => playerIndex;
    public Gamepad AssignedGamepad => device == InputDevice.Automatic ? RallyLocalDevices.GamepadFor(playerIndex) : assignedGamepad;

    public void ConfigurePlayer(int player, float response)
    {
        Configure(InputDevice.Automatic, response);
        playerIndex = Mathf.Clamp(player, 0, 1);
    }
    public RallyCheckpointManager Checkpoints { get; set; }
    public RallyBrakeWarningSystem BrakeWarnings { get; set; }
    public void PairGamepad(Gamepad gamepad) => assignedGamepad = gamepad;

    public void Configure(InputDevice source, float response)
    {
        device = source;
        steeringResponse = response;
        assignedGamepad = null;
    }

    void Update()
    {
        float steer;
        bool reset, recover;
        if (device == InputDevice.Keyboard || (device == InputDevice.Automatic && RallyLocalDevices.UsesKeyboard(playerIndex)))
        {
            Keyboard keyboard = Keyboard.current;
            bool accelerate = RallyGamepadInput.KeyboardVertical > 0f ||
                (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed));
            bool brake = RallyGamepadInput.KeyboardVertical < 0f ||
                (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed));
            Vertical = accelerate ? 1f : brake ? -1f : 0f;
            Brake = brake ? 1f : 0f;
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ||
                (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed));
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ||
                (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed));
            steer = left ? -1f : right ? 1f : 0f;
            Horizontal = Mathf.MoveTowards(Horizontal, steer, steeringResponse * Time.deltaTime);
            Handbrake = Input.GetKey(KeyCode.Space) || (keyboard != null && keyboard.spaceKey.isPressed);
            reset = Input.GetKey(KeyCode.R) || (keyboard != null && keyboard.rKey.isPressed);
            recover = Input.GetKey(KeyCode.M) || Input.GetKey(KeyCode.T) ||
                (keyboard != null && (keyboard.mKey.isPressed || keyboard.tKey.isPressed));
        }
        else
        {
            // Keep the device paired even when another device becomes Gamepad.current.
            // Reacquire automatically after disconnect/reconnect.
            if (device == InputDevice.Automatic)
                assignedGamepad = RallyLocalDevices.GamepadFor(playerIndex);
            else if (assignedGamepad == null || !assignedGamepad.added)
                assignedGamepad = RallyLocalDevices.GamepadFor(playerIndex);
            float throttle = assignedGamepad == null ? 0f : assignedGamepad.rightTrigger.ReadValue();
            Brake = assignedGamepad == null ? 0f : assignedGamepad.leftTrigger.ReadValue();
            if (throttle < .02f) throttle = 0f;
            if (Brake < .02f) Brake = 0f;
            Vertical = throttle - Brake;
            Horizontal = assignedGamepad == null ? 0f : assignedGamepad.leftStick.x.ReadValue();
            Handbrake = assignedGamepad != null && assignedGamepad.buttonSouth.isPressed;
            reset = assignedGamepad != null && assignedGamepad.buttonNorth.isPressed;
            recover = assignedGamepad != null && assignedGamepad.buttonWest.isPressed;
        }
        ResetPressed = reset && !resetHeld;
        RecoverPressed = recover && !recoverHeld;
        resetHeld = reset;
        recoverHeld = recover;
    }
}
