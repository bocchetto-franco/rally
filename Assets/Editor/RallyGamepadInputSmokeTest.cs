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
        GameObject inputObject = new GameObject("Gamepad Input Smoke Test");
        try
        {
            JrsInputController inputController = inputObject.AddComponent<JrsInputController>();
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
            AssertNear(inputController.GetVerticalInput(), 0.15f, "scene input controller drive");
            if (RallyGamepadInput.Steering < 0.5f)
                throw new Exception("Left stick did not steer right.");
            AssertNear(inputController.GetHorizontalInput(), 0.7f, "scene input controller steering");
            AssertNear(RallyGamepadInput.GetVerticalInput(1f), 1f, "keyboard drive");
            AssertNear(RallyGamepadInput.GetHorizontalInput(-1f), -1f, "keyboard steering");
            AssertNear(RallyGamepadInput.GetHorizontalInput(0.35f, false), 0.7f, "stick after key release");
            AssertNear(RallyGamepadInput.GetHorizontalInput(0.35f, true), 0.35f, "held keyboard steering");

            InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 0.8f });
            InputSystem.Update();
            AssertNear(RallyGamepadInput.Brake, 0.8f, "analog brake");
            AssertNear(RallyGamepadInput.GetVerticalInput(0f), -0.8f, "left trigger braking input");
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 0.6f });
            InputSystem.Update();
            AssertNear(RallyGamepadInput.Throttle, 0.6f, "analog throttle");
            AssertNear(RallyGamepadInput.GetVerticalInput(0f), 0.6f, "right trigger driving input");

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
            UnityEngine.Object.DestroyImmediate(inputObject);
            InputSystem.RemoveDevice(pad);
        }
    }

    private static void AssertNear(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > 0.025f)
            throw new Exception($"{label}: expected {expected}, got {actual}.");
    }
}
