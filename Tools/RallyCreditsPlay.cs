using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class RallyCreditsPlay
{
    const string Report = "Logs/credits-play-test.txt";
    static Keyboard keyboard;
    static Gamepad pad;
    static SceneAsset previousStart;
    static bool previousBackground;
    static double next, deadline;
    static int phase;
    static int lastFrame = -1;
    static ScrollRect scroll;
    static float before;
    static readonly string[] PrefKeys = { "Rally.SelectedVehicle", "Rally.SelectedVehicleTwo", "Rally.SelectedCircuit", "Rally.BotDifficulty", "Rally.LocalPlayers" };
    static bool[] had;
    static string[] saved;
    static int[] ints;
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new Exception("Editor must be idle.");
        Require(File.ReadAllBytes("CREDITS.md").SequenceEqual(File.ReadAllBytes("Assets/Resources/Credits/Credits.txt")), "Canonical/runtime text identical");
        Require(File.ReadAllBytes("CREDITS.md").SequenceEqual(File.ReadAllBytes("Assets/StreamingAssets/CREDITS.md")), "Distributed credits identical");
        previousStart = EditorSceneManager.playModeStartScene;
        previousBackground = Application.runInBackground;
        had = PrefKeys.Select(PlayerPrefs.HasKey).ToArray();
        saved = PrefKeys.Select(key => PlayerPrefs.GetString(key)).ToArray();
        ints = PrefKeys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
        PlayerPrefs.SetInt("Rally.LocalPlayers", 1); // Isolated virtual device test; restored afterward.
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, "Credits integration test: keyboard, virtual gamepad, pointer, scrolling and source parity\n");
        phase = 0; lastFrame = -1; next = 0; deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        Application.runInBackground = true;
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (phase == 99)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
            return;
        }
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timeout phase " + phase)); return; }
        if (!EditorApplication.isPlaying || Object.FindAnyObjectByType<RallyMenuController>() == null) return;
        if (next == 0) { next = EditorApplication.timeSinceStartup + 2; return; }
        if (EditorApplication.timeSinceStartup < next) return;
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        next = EditorApplication.timeSinceStartup + .5;
        try
        {
            switch (phase++)
            {
                case 0: keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>(); PressKey(Key.Enter); break;
                case 1: Release(); break;
                case 2: Check(GameObject.Find("Credits Button") != null, "Main menu exposes Credits"); Pad(GamepadButton.DpadDown); break;
                case 3: Release(); break;
                case 4: Pad(GamepadButton.DpadDown); break;
                case 5: Release(); break;
                case 6: Pad(GamepadButton.DpadDown); break;
                case 7: Release(); break;
                case 8: Pad(GamepadButton.South); break;
                case 9:
                    Release();
                    Check(GameObject.Find("Credits Panel") != null, "D-Pad + Cross opens Credits");
                    scroll = GameObject.Find("Credits Panel").GetComponent<ScrollRect>();
                    Check(scroll.content.rect.height > scroll.viewport.rect.height * 3f, "Long document is scrollable");
                    var texts = scroll.content.GetComponentsInChildren<TMP_Text>();
                    string all = string.Join("\n", texts.Select(text => text.text));
                    foreach (string required in new[] { "Martin Trafas", "TonyWony", "REQUIERE ATRIBUCIÓN", "CC0", "NO CLARA", "Porsche", "Dmitry_mansurev64", "Quaternius", "Unity Companion License", "Electronic Highway Sign" })
                        Check(all.Contains(required), "Displayed attribution: " + required);
                    foreach (var text in texts)
                        Check(text.rectTransform.rect.width <= scroll.viewport.rect.width, "Text contained horizontally");
                    ScreenCapture.CaptureScreenshot("Logs/Menu_Credits_Top.png");
                    break;
                case 10: PressKey(Key.End); break;
                case 11: Release(); Check(scroll.verticalNormalizedPosition < .001f, "End reaches final distribution notes"); break;
                case 12: PressKey(Key.Home); break;
                case 13: Release(); Check(scroll.verticalNormalizedPosition > .999f, "Home returns to start"); before = scroll.verticalNormalizedPosition; Pad(GamepadButton.DpadDown); break;
                case 14: Release(); Check(scroll.verticalNormalizedPosition < before, "Held D-Pad scrolls down"); before = scroll.verticalNormalizedPosition; PressKey(Key.PageDown); break;
                case 15: Release(); Check(scroll.verticalNormalizedPosition < before, "Keyboard PageDown scrolls"); before = scroll.verticalNormalizedPosition; break;
                case 16:
                    ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -3f) }, ExecuteEvents.scrollHandler);
                    Check(scroll.verticalNormalizedPosition < before, "Mouse wheel scrolls");
                    scroll.verticalNormalizedPosition = .83f;
                    ScreenCapture.CaptureScreenshot("Logs/Menu_Credits_Assets.png"); break;
                case 17: Pad(GamepadButton.South); break;
                case 18: Release(); Check(GameObject.Find("Credits Button") != null && GameObject.Find("Continue Card") == null, "Cross on Back returns without intro"); break;
                case 19: PointerClick("Credits Button"); break;
                case 20: Check(GameObject.Find("Credits Panel") != null, "Pointer opens Credits"); Pad(GamepadButton.East); break;
                case 21: Release(); Check(GameObject.Find("Credits Button") != null, "Circle returns"); PointerClick("Credits Button"); break;
                case 22: PressKey(Key.Escape); break;
                case 23: Release(); Check(GameObject.Find("Credits Button") != null, "Escape returns"); PointerClick("Credits Button"); break;
                case 24: PointerClick("Back Button"); break;
                case 25: Check(GameObject.Find("Credits Button") != null, "Visible Back pointer works"); ScreenCapture.CaptureScreenshot("Logs/Menu_WithCredits.png"); break;
                case 26: Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error); }
    }
    static void PointerClick(string name) => ExecuteEvents.Execute(GameObject.Find(name), new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    static void PressKey(Key key) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); }
    static void Pad(GamepadButton button) { InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); }
    static void Release()
    {
        if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        if (pad != null) InputSystem.QueueStateEvent(pad, new GamepadState());
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Check(bool condition, string message) { Require(condition, message); File.AppendAllText(Report, "PASS: " + message + "\n"); }
    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAILED: " + error + "\n");
        Release();
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (pad != null) InputSystem.RemoveDevice(pad);
        keyboard = null; pad = null;
        phase = 99;
        EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        EditorSceneManager.playModeStartScene = previousStart;
        Application.runInBackground = previousBackground;
        for (int i = 0; i < PrefKeys.Length; i++)
        {
            if (!had[i]) PlayerPrefs.DeleteKey(PrefKeys[i]);
            else if (i < 3) PlayerPrefs.SetString(PrefKeys[i], saved[i]);
            else PlayerPrefs.SetInt(PrefKeys[i], ints[i]);
        }
        PlayerPrefs.Save();
        RallyGameSession.RestoreSavedState();
    }
}
