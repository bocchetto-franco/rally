using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class RallyPauseMenuSmokeTest
{
    [MenuItem("Tools/Rally/Verify Pause Menu Gamepad Mapping")]
    public static void Run()
    {
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        GameObject eventObject = null;
        GameObject navigationObject = null;
        GameObject pauseObject = null;
        try
        {
            if (pad.startButton != pad[GamepadButton.Start] || pad.crossButton != pad.buttonSouth)
                throw new Exception("DualSense Options/Cross aliases do not match Start/South.");

            eventObject = new GameObject("Test EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            navigationObject = new GameObject("Test Navigation", typeof(RectTransform));
            Button first = CreateButton(navigationObject.transform, "First");
            Button second = CreateButton(navigationObject.transform, "Second");
            int firstClicks = 0, secondClicks = 0;
            first.onClick.AddListener(() => firstClicks++);
            second.onClick.AddListener(() => secondClicks++);
            RallyMenuNavigation navigation = navigationObject.AddComponent<RallyMenuNavigation>();
            navigation.Configure(first, second);
            Press(pad, new GamepadState(GamepadButton.DpadDown));
            InvokeUpdate(navigation);
            if (navigation.SelectedIndex != 1) throw new Exception("D-Pad Down did not select the next button.");
            Press(pad, new GamepadState(GamepadButton.South));
            InvokeUpdate(navigation);
            if (firstClicks != 0 || secondClicks != 1)
                throw new Exception("Cross did not invoke exactly one selected button.");
            Press(pad, new GamepadState());
            Press(pad, new GamepadState(GamepadButton.DpadUp));
            InvokeUpdate(navigation);
            if (navigation.SelectedIndex != 0) throw new Exception("D-Pad Up did not select the previous button.");

            navigationObject.SetActive(false);
            pauseObject = new GameObject("Test Pause", typeof(RallyPauseMenu));
            RallyPauseMenu pause = pauseObject.GetComponent<RallyPauseMenu>();
            typeof(RallyPauseMenu).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pause, null);
            Press(pad, new GamepadState());
            Press(pad, new GamepadState(GamepadButton.Start));
            InvokeUpdate(pause);
            if (!pause.IsOpen || Time.timeScale != 0f) throw new Exception("Options did not open and pause the game.");
            Press(pad, new GamepadState());
            InvokeUpdate(pause);
            Press(pad, new GamepadState(GamepadButton.Start));
            InvokeUpdate(pause);
            if (pause.IsOpen || Time.timeScale != 1f) throw new Exception("Options did not close and resume the game.");
            Press(pad, new GamepadState());
            InvokeUpdate(pause);
            Press(pad, new GamepadState(GamepadButton.Start));
            InvokeUpdate(pause);
            Press(pad, new GamepadState());
            Press(pad, new GamepadState(GamepadButton.South));
            InvokeUpdate(pause.GetComponentInChildren<RallyMenuNavigation>(true));
            if (pause.IsOpen || Time.timeScale != 1f) throw new Exception("Cross did not activate Reanudar.");

            Debug.Log("Rally pause menu PASS: DualSense Options toggle, D-Pad navigation, single Cross submit, resume/timeScale.");
        }
        finally
        {
            Time.timeScale = 1f;
            if (pauseObject != null) UnityEngine.Object.DestroyImmediate(pauseObject);
            if (navigationObject != null) UnityEngine.Object.DestroyImmediate(navigationObject);
            if (eventObject != null) UnityEngine.Object.DestroyImmediate(eventObject);
            InputSystem.RemoveDevice(pad);
        }
    }

    static Button CreateButton(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Button>();
    }

    static void Press(Gamepad pad, GamepadState state)
    {
        InputSystem.QueueStateEvent(pad, state);
        InputSystem.Update();
    }

    static void InvokeUpdate(MonoBehaviour behaviour)
    {
        behaviour.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(behaviour, null);
    }
}
