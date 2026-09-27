using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class RallyGamepadInputSmokeTest
{
    [MenuItem("Tools/Rally/Verify Gamepad Mapping")]
    public static void Run()
    {
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            if (pad.aButton != pad.buttonSouth || pad.crossButton != pad.buttonSouth ||
                pad.yButton != pad.buttonNorth || pad.triangleButton != pad.buttonNorth)
                throw new Exception("Xbox/PlayStation button aliases do not match the generic layout.");

            InputSystem.QueueStateEvent(pad, new GamepadState
            {
                rightTrigger = 0.4f,
                leftTrigger = 0.25f,
                leftStick = new Vector2(0.7f, 0f)
            });
            InputSystem.Update();

            AssertNear(RallyGamepadInput.Throttle, 0.4f, "right trigger");
            AssertNear(RallyGamepadInput.Brake, 0.25f, "left trigger");
            AssertNear(RallyGamepadInput.GetVerticalInput(0f), 0.15f, "analog drive");
            if (RallyGamepadInput.Steering < 0.5f)
                throw new Exception("Left stick did not steer right.");
            AssertNear(RallyGamepadInput.GetVerticalInput(1f), 1f, "keyboard drive");
            AssertNear(RallyGamepadInput.GetHorizontalInput(-1f), -1f, "keyboard steering");

            InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.South, GamepadButton.North));
            InputSystem.Update();
            if (!RallyGamepadInput.HandbrakePressed || !RallyGamepadInput.ResetPressedThisFrame)
                throw new Exception("South/North button mapping failed: south=" +
                    pad.buttonSouth.isPressed + ", north=" + pad.buttonNorth.isPressed +
                    ", resetThisFrame=" + RallyGamepadInput.ResetPressedThisFrame +
                    ", current=" + (Gamepad.current == pad));
            if (RallyGamepadInput.ResetPressedThisFrame)
                throw new Exception("Reset repeated while North was held.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.Update();
            if (RallyGamepadInput.ResetPressedThisFrame)
                throw new Exception("Reset fired on release.");
            InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.North));
            InputSystem.Update();
            if (!RallyGamepadInput.ResetPressedThisFrame)
                throw new Exception("Reset did not fire after a new press.");

            Debug.Log("Rally gamepad mapping PASS: analog triggers, left stick, Xbox/PlayStation button aliases, keyboard fallback.");
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
        }
    }

    private static void AssertNear(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > 0.025f)
            throw new Exception($"{label}: expected {expected}, got {actual}.");
    }
}
