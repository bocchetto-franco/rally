using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// External Play verification. No scene/physics/asset writes, restores the user's session.
public static class RallySelectionUxCheck
{
    const string Report = "Logs/selection-ux-check.txt";
    static readonly string[] Keys = { "Rally.LocalPlayers", "Rally.SelectedVehicle", "Rally.SelectedVehicleTwo", "Rally.SelectedCircuit", "Rally.BotDifficulty" };
    static bool[] had;
    static string[] strings;
    static int[] ints;
    static SceneSetup[] scenes;
    static SceneAsset start;
    static bool background, paused, stopping, exited;
    static float timeScale;
    static InputSettings originalInput, testInput;
    static string inputJson, runtimeError;
    static Gamepad first, second;
    static Keyboard keyboard;
    static InputDevice[] disabled;
    static IEnumerator steps;
    static double next, deadline;
    static int lastFrame;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new Exception("Unity must be idle.");
        if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
            throw new Exception("External check requires the project's Disable Domain Reload setting.");
        scenes = EditorSceneManager.GetSceneManagerSetup(); start = EditorSceneManager.playModeStartScene;
        background = Application.runInBackground; paused = EditorApplication.isPaused; timeScale = Time.timeScale;
        had = Keys.Select(PlayerPrefs.HasKey).ToArray(); strings = Keys.Select(k => PlayerPrefs.GetString(k)).ToArray(); ints = Keys.Select(k => PlayerPrefs.GetInt(k)).ToArray();
        if (InputSystem.settings == null) RepairInputs();
        originalInput = InputSystem.settings; inputJson = EditorJsonUtility.ToJson(originalInput);
        testInput = Object.Instantiate(originalInput); testInput.hideFlags = HideFlags.HideAndDontSave;
        testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = testInput;
        RallyGameSession.SelectLocalPlayers(1); RallyGameSession.SelectVehicleForPlayer(0, 0); RallyGameSession.SelectVehicleForPlayer(1, 0);
        File.WriteAllText(Report, "Selection UX: real Play, virtual DualSense/keyboard, 1P/2P, HUD in all circuits\n");
        stopping = exited = false; runtimeError = null; next = 0; lastFrame = -1; deadline = EditorApplication.timeSinceStartup + 1500;
        steps = Steps(); EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; Application.logMessageReceived += Log;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.ExecuteMenuItem("Window/General/Game"); EditorApplication.isPlaying = true;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!stopping && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) runtimeError = message + "\n" + stack;
    }
    static void Mode(PlayModeStateChange mode) { if (mode == PlayModeStateChange.EnteredEditMode) { exited = true; next = EditorApplication.timeSinceStartup + 1; } }
    static void Tick()
    {
        if (stopping) { if (exited && EditorApplication.timeSinceStartup >= next && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying) Restore(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Test deadline exceeded")); return; }
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.frameCount == lastFrame || EditorApplication.timeSinceStartup < next) return;
        lastFrame = Time.frameCount;
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            if (!steps.MoveNext()) { Finish(null); return; }
            next = EditorApplication.timeSinceStartup + (steps.Current is float delay ? delay : 0);
        }
        catch (Exception e) { Finish(e); }
    }
    static Button B(string name) => GameObject.Find(name).GetComponent<Button>();
    static RallyMenuNavigation Nav() => Object.FindObjectsByType<RallyMenuNavigation>().Single(n => n.isActiveAndEnabled);
    static Selectable Focus(RallyMenuNavigation nav) => ((Selectable[])typeof(RallyMenuNavigation).GetField("buttons", Private).GetValue(nav))[nav.SelectedIndex];
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); File.AppendAllText(Report, "PASS: " + message + "\n"); }
    static void Neutral(Gamepad pad)
    {
        State(pad);
    }
    static void State(Gamepad pad, byte dpad = 8, bool cross = false, Vector2 stick = default)
    {
        InputSystem.QueueStateEvent(pad, new DualSenseHIDInputReport {
            leftStickX = (byte)Mathf.RoundToInt((stick.x + 1) * 127.5f), leftStickY = (byte)Mathf.RoundToInt((1 - stick.y) * 127.5f),
            rightStickX = 128, rightStickY = 128, buttons0 = (byte)(dpad | (cross ? 1 << 5 : 0)) });
    }
    static void Cross(Gamepad pad) => State(pad, cross: true);
    static void Dpad(Gamepad pad, Vector2 direction) => State(pad, direction.y > 0 ? (byte)0 : direction.y < 0 ? (byte)4 : direction.x > 0 ? (byte)2 : direction.x < 0 ? (byte)6 : (byte)8);
    static void Stick(Gamepad pad, Vector2 direction) => State(pad, stick: direction);
    static void KeysDown(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static bool Yellow(Color c) => c.r > .9f && c.g > .75f && c.b < .25f;
    static void Chosen(Button[] cars, int expected)
    {
        Check(cars.Count(b => Yellow(b.colors.normalColor)) == 1 && Yellow(cars[expected].colors.normalColor), "exactly one confirmed yellow car per panel");
        Check(Yellow(cars[expected].colors.highlightedColor) && Yellow(cars[expected].colors.selectedColor), "confirmed yellow persists under hover/focus");
        Check(Yellow(cars[expected].targetGraphic.canvasRenderer.GetColor()), "rendered confirmed button is yellow");
    }
    static void CleanSelection()
    {
        var menu = Object.FindAnyObjectByType<RallyMenuController>();
        var labels = menu.GetComponentsInChildren<TMP_Text>(true);
        Check(!labels.Any(t => t.name == "Vehicle Counter" || t.name == "Vehicle Details" || t.name == "Asset Credit"), "counter, physics subtitle and attribution strip removed from selection");
        Check(!labels.Any(t => t.text.Contains("CIRCUIT_") || t.text.Contains("Circuit 0") || t.text.Contains("FÍSICA COMPARTIDA")), "no technical scene names or physics subtitle in visible selection");
        string[] expected = { "Desierto", "Sierras", "Bosque" };
        for (int i = 0; i < 3; i++) Check(B("Circuit " + (i + 1) + " Button").GetComponentInChildren<TMP_Text>().text == expected[i], "friendly circuit name " + expected[i]);
    }
    static IEnumerator Steps()
    {
        while (Object.FindAnyObjectByType<RallyMenuController>() == null) yield return null;
        yield return .4f;
        disabled = InputSystem.devices.Where(d => (d is Gamepad || d is Keyboard) && d.enabled).ToArray();
        foreach (var device in disabled) InputSystem.DisableDevice(device);
        first = InputSystem.AddDevice<DualSenseGamepadHID>(); second = InputSystem.AddDevice<DualSenseGamepadHID>(); keyboard = InputSystem.AddDevice<Keyboard>();
        _ = first.buttonSouth.wasPressedThisFrame; _ = second.buttonSouth.wasPressedThisFrame;
        Neutral(first); Neutral(second); yield return null;
        Cross(first); yield return null; Neutral(first); yield return null;
        B("Play Button").onClick.Invoke();
        while (SceneManager.GetActiveScene().name != RallyGameSession.SelectionScene || Object.FindAnyObjectByType<RallyMenuController>() == null) yield return null;
        yield return .4f;
        CleanSelection();
        var nav = Nav(); var menu = Object.FindAnyObjectByType<RallyMenuController>(); var cars = Field<Button[]>(menu, "vehicleButtons");
        nav.FocusVehicle(cars[0]); yield return .15f; Chosen(cars, 0);
        Dpad(first, Vector2.right); yield return null; Neutral(first); yield return .15f;
        Check(Focus(nav) == cars[1] && RallyGameSession.SelectedVehicleIndex == 0, "D-Pad browses BMW without confirming it"); Chosen(cars, 0);
        Cross(first); yield return null; Neutral(first); yield return .15f;
        Check(RallyGameSession.SelectedVehicleIndex == 1, "DualSense Cross confirms focused BMW"); Chosen(cars, 1);
        Dpad(first, Vector2.left); yield return null; Neutral(first); yield return .15f;
        Check(Focus(nav) == cars[0] && RallyGameSession.SelectedVehicleIndex == 1, "moving focus preserves confirmed BMW"); Chosen(cars, 1);
        Check(cars[0].GetComponent<Outline>().enabled && !cars[1].GetComponent<Outline>().enabled, "focus outline remains separate from yellow selection");
        Dpad(first, Vector2.down); yield return null; Neutral(first); yield return null;
        Check(Focus(nav) == B("Difficulty FÁCIL Button"), "down from car row moves below, not to adjacent car");
        nav.FocusVehicle(cars[1]); Stick(first, Vector2.down); yield return null; Neutral(first); yield return null;
        Check(Focus(nav) == B("Difficulty FÁCIL Button"), "left stick down respects vertical row");
        nav.FocusVehicle(cars[2]); KeysDown(Key.DownArrow); yield return null; KeysDown(); yield return null;
        Check(Focus(nav) == B("Difficulty FÁCIL Button"), "keyboard down respects vertical row");
        nav.FocusVehicle(B("Circuit 1 Button"));
        Dpad(first, Vector2.down); yield return null; Neutral(first); yield return null; Check(Focus(nav) == B("Circuit 2 Button"), "track column down goes to next track below");
        Dpad(first, Vector2.down); yield return null; Neutral(first); yield return null; Check(Focus(nav) == B("Circuit 3 Button"), "track column continues downward");
        Dpad(first, Vector2.up); yield return null; Neutral(first); yield return null; Check(Focus(nav) == B("Circuit 2 Button"), "track column up is inverse");
        for (int i = 0; i < 3; i++)
        {
            B("Circuit " + (i + 1) + " Button").onClick.Invoke();
            Check(RallyGameSession.SelectedRaceScene == RallyGameSession.CircuitScenes[i] && GameObject.Find("Selected Circuit").GetComponent<TMP_Text>().text == RallyGameSession.CircuitDisplayNames[i], "friendly label preserves scene mapping " + i);
            nav.FocusVehicle(cars[i]); Cross(first); yield return null; Neutral(first); yield return .15f;
            Check(RallyGameSession.SelectedVehicleIndex == i, "Cross confirms car " + i); Chosen(cars, i);
        }
        // Confirmation by keyboard and mouse remain available too.
        nav.FocusVehicle(cars[0]); KeysDown(Key.Enter); yield return null; KeysDown(); yield return .15f; Chosen(cars, 0);
        ExecuteEvents.Execute(cars[1].gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        yield return .15f; Check(RallyGameSession.SelectedVehicleIndex == 1, "mouse confirms the clicked car"); Chosen(cars, 1);
        nav.FocusVehicle(B("Difficulty FÁCIL Button"));
        Dpad(first, Vector2.right); yield return null; Neutral(first); yield return null; Check(Focus(nav) == B("Difficulty MEDIO Button"), "right stays within difficulty row");
        Dpad(first, Vector2.down); yield return null; Neutral(first); yield return null;
        Check(Focus(nav).name.StartsWith("Local Players"), "down from difficulty reaches player-count row");
        var preview = Field<RallyMenuVehiclePreview>(menu, "vehiclePreview");
        while (preview.IsTransitioning || preview.ShownIndex != 1 || preview.Opacity < .99f) yield return null;
        Check(preview.VisibleModel != null && GameObject.Find("Selected Vehicle").GetComponent<TMP_Text>().text == "BMW M3 E30", "preview model and name remain visible after cleanup");
        ScreenCapture.CaptureScreenshot("Logs/SelectionUX_1P.png"); yield return .3f;
        B("Local Players 2").onClick.Invoke(); yield return .4f;
        CleanSelection();
        var local = Object.FindObjectsByType<RallyMenuNavigation>().OrderBy(n => n.LocalPlayer).ToArray();
        Check(local.Length == 2, "two independent local navigation owners");
        menu = Object.FindAnyObjectByType<RallyMenuController>(); var localCars = Field<Button[][]>(menu, "localVehicleButtons");
        Gamepad padOne = RallyLocalDevices.GamepadFor(0), padTwo = RallyLocalDevices.GamepadFor(1);
        for (int p = 0; p < 2; p++)
        {
            var pad = p == 0 ? padOne : padTwo;
            int confirmed = RallyGameSession.VehicleIndexForPlayer(p), other = RallyGameSession.VehicleIndexForPlayer(1 - p);
            local[p].FocusVehicle(localCars[p][0]); Dpad(pad, Vector2.right); yield return null; Neutral(pad); yield return .15f;
            Check(Focus(local[p]) == localCars[p][1] && RallyGameSession.VehicleIndexForPlayer(p) == confirmed, "local browsing does not implicitly confirm J" + (p + 1));
            Cross(pad); yield return null; Neutral(pad); yield return .15f;
            Check(RallyGameSession.VehicleIndexForPlayer(p) == 1 && RallyGameSession.VehicleIndexForPlayer(1 - p) == other, "Cross confirms only its own player's car"); Chosen(localCars[p], 1);
            Dpad(pad, Vector2.down); yield return null; Neutral(pad); yield return null;
            Check(Focus(local[p]) == B("Player " + (p + 1) + " Ready"), "down from local car row goes to Ready, not a side car");
            Stick(pad, Vector2.up); yield return null; Neutral(pad); yield return null; Check(Focus(local[p]) == localCars[p][1], "up from Ready returns to central car");
            local[p].FocusVehicle(p == 0 ? B("Circuit 1 Button") : B("Player 2 Circuit 1"));
            Dpad(pad, Vector2.right); yield return null; Neutral(pad); yield return null;
            Check(Focus(local[p]) == (p == 0 ? B("Circuit 2 Button") : B("Player 2 Circuit 2")), "left/right traverses only its own track row");
            Dpad(pad, Vector2.up); yield return null; Neutral(pad); yield return null;
            Check(Focus(local[p]) == B("Player " + (p + 1) + " Ready"), "up from any local track returns to Ready row");
            Chosen(localCars[p], 1);
        }
        var previews = Field<RallyMenuVehiclePreview[]>(menu, "localVehiclePreviews");
        while (previews.Any(p => p.IsTransitioning || p.ShownIndex != 1 || p.Opacity < .99f)) yield return null;
        Check(previews.All(p => p.VisibleModel != null), "both local preview models remain visible");
        ScreenCapture.CaptureScreenshot("Logs/SelectionUX_2P.png"); yield return .3f;
        B("Back Button").onClick.Invoke();
        while (SceneManager.GetActiveScene().name != "MainMenu") yield return null;
        yield return .3f; Cross(first); yield return null; Neutral(first); yield return null;
        B("Credits Button").onClick.Invoke(); yield return .3f;
        string creditText = string.Join("\n", Object.FindAnyObjectByType<RallyMenuController>().GetComponentsInChildren<TMP_Text>().Select(t => t.text));
        Check(creditText.Contains("Martin Trafas") && creditText.Contains("TonyWony") && creditText.Contains("CC-BY 4.0"), "both car authors/licenses still appear on Credits page");
        Check(File.ReadAllText("CREDITS.md").Contains("Martin Trafas") && File.ReadAllText("CREDITS.md").Contains("TonyWony"), "canonical CREDITS.md preserves car attribution");
        for (int scene = 0; scene < 3; scene++) for (int players = 1; players <= 2; players++)
        {
            RallyGameSession.SelectLocalPlayers(players); RallyRaceLoading.Load(RallyGameSession.CircuitScenes[scene]);
            while (RallyRaceLoading.IsPreparing || SceneManager.GetActiveScene().name != RallyGameSession.CircuitScenes[scene]) yield return null;
            yield return .15f;
            var huds = Object.FindObjectsByType<RallyRaceHud>(); Check(huds.Length == players, "correct HUD count " + RallyGameSession.CircuitScenes[scene] + " " + players + "P");
            foreach (var bot in Object.FindObjectsByType<RallyBotController>()) bot.enabled = false;
            foreach (var dynamics in Object.FindObjectsByType<RallyVehicleDynamics>()) dynamics.enabled = false;
            foreach (var car in Object.FindObjectsByType<JrsVehicleController>()) { car.enabled = false; var body = car.GetComponent<Rigidbody>(); body.linearVelocity = body.angularVelocity = Vector3.zero; body.isKinematic = true; }
            foreach (var hud in huds)
            {
                var label = Field<TMP_Text>(hud, "checkpointText"); Check(label != null && !label.gameObject.activeSelf && !label.gameObject.activeInHierarchy, "checkpoint label hidden, reference retained");
                var clock = Field<TMP_Text>(hud, "timeText"); Check(clock.isActiveAndEnabled && clock.transform.parent.GetComponent<RectTransform>().sizeDelta.y == 82, "timer remains visible with compact background");
                var manager = Field<RallyCheckpointManager>(hud, "checkpointManager"); manager.ResetTimer();
                var collider = manager.VehicleBody.GetComponentsInChildren<Collider>().First(c => c.attachedRigidbody == manager.VehicleBody && !c.isTrigger);
                manager.TryPass(0, collider); Check(manager.NextCheckpoint == 1 && manager.IsRunning, "hidden label does not disable checkpoint counting/timer");
            }
            yield return .2f;
            foreach (var hud in huds) Check(Field<RallyCheckpointManager>(hud, "checkpointManager").ElapsedTime > 0, "race clock advances without checkpoint text");
            ScreenCapture.CaptureScreenshot("Logs/SelectionUX_HUD_" + RallyGameSession.CircuitScenes[scene] + "_" + players + "P.png"); yield return .3f;
            SceneManager.LoadScene("MainMenu"); while (SceneManager.GetActiveScene().name != "MainMenu") yield return null; yield return null;
        }
    }
    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n"); stopping = true;
        Application.logMessageReceived -= Log;
        foreach (var device in new InputDevice[] { first, second, keyboard }) if (device != null && device.added) InputSystem.RemoveDevice(device);
        if (disabled != null) foreach (var device in disabled) if (device.added) InputSystem.EnableDevice(device);
        EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
        for (int i = 0; i < Keys.Length; i++)
            if (!had[i]) PlayerPrefs.DeleteKey(Keys[i]); else if (i == 0 || i == 4) PlayerPrefs.SetInt(Keys[i], ints[i]); else PlayerPrefs.SetString(Keys[i], strings[i]);
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
        if (originalInput != null) InputSystem.settings = originalInput;
        else { var restored = ScriptableObject.CreateInstance<InputSettings>(); EditorJsonUtility.FromJsonOverwrite(inputJson, restored); restored.hideFlags = HideFlags.HideAndDontSave; InputSystem.settings = restored; }
        if (testInput != null && testInput != InputSystem.settings) Object.DestroyImmediate(testInput);
        EditorSceneManager.playModeStartScene = start; EditorSceneManager.RestoreSceneManagerSetup(scenes);
        Application.runInBackground = background; EditorApplication.isPaused = paused; Time.timeScale = timeScale;
        File.AppendAllText(Report, "RESTORED: preferences, devices/InputSettings, scenes, background, pause and timeScale\n");
    }

    // Unity can retire the pre-Play transient InputSettings during exit. Repair
    // only from the exact saved JSON of this verifier, never substitute defaults.
    public static string RepairInputs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Leave Play first");
        if (InputSystem.settings != null) return "InputSettings already valid";
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var previous = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(nameof(RallySelectionUxCheck)))
            .First(t => t != null && t.GetField("steps", flags)?.GetValue(null) != null);
        string json = (string)previous.GetField("inputJson", flags).GetValue(null);
        if (string.IsNullOrEmpty(json) || json == "null") throw new Exception("Original settings JSON unavailable");
        var restored = ScriptableObject.CreateInstance<InputSettings>();
        EditorJsonUtility.FromJsonOverwrite(json, restored); restored.hideFlags = HideFlags.HideAndDontSave; InputSystem.settings = restored;
        return "Preserved InputSettings fields restored after Play cleanup";
    }
}
