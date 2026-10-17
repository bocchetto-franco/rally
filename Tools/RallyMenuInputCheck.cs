using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// External integration check: real menu Updates and queued device events in Play.
public static class RallyMenuInputCheck
{
    const string Report = "Logs/menu-input-check.txt";
    static Gamepad first, second;
    static Keyboard keyboard;
    static InputDevice[] disabled;
    static InputSettings originalInput, testInput;
    static string originalInputJson;
    static SceneAsset originalStart;
    static bool originalBackground, originalPaused;
    static float originalTimeScale;
    static IEnumerator steps;
    static int frame;
    static double next, deadline;
    static bool stopping;
    static readonly string[] Keys = { "Rally.LocalPlayers", "Rally.SelectedVehicle", "Rally.SelectedVehicleTwo", "Rally.SelectedCircuit", "Rally.BotDifficulty", "Rally.Options.MusicaVolume", "Rally.Options.EfectosVolume", "Rally.Options.MotorVolume", "Rally.Options.Quality" };
    static bool[] had;
    static string[] strings;
    static int[] ints;
    static float[] floats;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new Exception("Editor must be idle before menu test.");
        if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
            throw new Exception("External test needs the project's Disable Domain Reload Play setting.");
        originalStart = EditorSceneManager.playModeStartScene;
        originalBackground = Application.runInBackground; originalPaused = EditorApplication.isPaused;
        originalTimeScale = Time.timeScale;
        originalInput = InputSystem.settings; testInput = Object.Instantiate(originalInput);
        originalInputJson = EditorJsonUtility.ToJson(originalInput);
        testInput.hideFlags = HideFlags.HideAndDontSave;
        testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = testInput;
        had = Keys.Select(PlayerPrefs.HasKey).ToArray();
        strings = Keys.Select(k => PlayerPrefs.GetString(k)).ToArray();
        ints = Keys.Select(k => PlayerPrefs.GetInt(k)).ToArray(); floats = Keys.Select(k => PlayerPrefs.GetFloat(k)).ToArray();
        RallyGameSession.SelectLocalPlayers(1);
        Directory.CreateDirectory("Logs"); File.WriteAllText(Report, "Menu navigation regression: real Play, two virtual gamepads, keyboard/pointer, preserved preferences\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        stopping = false; frame = -1; next = 0; deadline = EditorApplication.timeSinceStartup + 150;
        steps = Steps(); EditorApplication.update += Tick;
        EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (stopping) { if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timed out")); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !EditorApplication.isPlaying ||
            Object.FindAnyObjectByType<RallyMenuController>() == null || Time.frameCount == frame || EditorApplication.timeSinceStartup < next) return;
        frame = Time.frameCount;
        try
        {
            if (!steps.MoveNext()) { Finish(null); return; }
            next = EditorApplication.timeSinceStartup + (steps.Current is float delay ? delay : 0);
        }
        catch (Exception e) { Finish(e); }
    }

    static RallyMenuNavigation Nav() => Object.FindObjectsByType<RallyMenuNavigation>().Single(n => n.isActiveAndEnabled);
    static Button Button(string name) => GameObject.Find(name).GetComponent<Button>();
    static void State(Gamepad pad, GamepadState state) => InputSystem.QueueStateEvent(pad, state);
    static void Press(Gamepad pad, GamepadButton button) => State(pad, new GamepadState().WithButton(button));
    static void Tap(Gamepad pad, GamepadButton button) { Press(pad, button); State(pad, new GamepadState()); }
    static void Neutral() { State(first, new GamepadState()); State(second, new GamepadState()); }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }

    static IEnumerator Steps()
    {
        yield return .5f;
        disabled = InputSystem.devices.Where(d => (d is Gamepad || d is Keyboard) && d.enabled).ToArray();
        foreach (var d in disabled) InputSystem.DisableDevice(d);
        first = InputSystem.AddDevice<Gamepad>(); second = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>();
        Neutral(); yield return null;
        Press(first, GamepadButton.South); yield return null;
        yield return .1f;
        Check(GameObject.Find("Controls Button") != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu", "Cross dismisses intro without also starting selection");
        Neutral(); yield return null;
        var nav = Nav();
        Press(first, GamepadButton.DpadDown); yield return null;
        Neutral(); yield return null;
        Check(nav.SelectedIndex == 1, "D-Pad moves exactly one option on a short press");
        first.MakeCurrent();
        // Switching Gamepad.current on the same Cross event used to seed/lose that event.
        Press(second, GamepadButton.South); yield return null;
        Check(GameObject.Find("Keyboard Controls") != null, "First Cross on a different active gamepad opens Controls");
        yield return .1f;
        Check(GameObject.Find("Keyboard Controls") != null, "Holding Cross does not submit the new page's Back button");
        Neutral(); yield return null;
        Tap(second, GamepadButton.South); yield return null;
        Check(GameObject.Find("Options Button") != null, "Press+release inside one input update confirms Back");
        yield return null;
        // Twenty changes of page exercise rapid taps and navigation acquire/release.
        for (int i = 0; i < 10; i++)
        {
            Nav().FocusVehicle(Button("Controls Button"));
            Tap(second, GamepadButton.South); yield return null;
            Check(GameObject.Find("Keyboard Controls") != null && Nav().isActiveAndEnabled, "Quick confirmation opens page, iteration " + i);
            yield return null;
            Tap(second, GamepadButton.South); yield return null;
            Check(GameObject.Find("Options Button") != null && !EventSystem.current.sendNavigationEvents, "Quick confirmation returns once with one input owner, iteration " + i);
            yield return null;
        }
        nav = Nav(); nav.ResetSelection();
        EventSystem.current.SetSelectedGameObject(null); yield return null;
        Check(EventSystem.current.currentSelectedGameObject == Button("Play Button").gameObject && Button("Play Button").GetComponent<Outline>().enabled, "Lost EventSystem focus is restored with a visible outline");
        State(second, new GamepadState { leftStick = new Vector2(0, -.9f) }); yield return null;
        Check(nav.SelectedIndex == 1, "Left stick down moves focus");
        Neutral(); yield return null;
        State(second, new GamepadState { leftStick = new Vector2(0, .9f) }); yield return null;
        Check(nav.SelectedIndex == 0, "Left stick up moves focus back");
        Neutral(); yield return null;
        State(second, new GamepadState { leftStick = new Vector2(.1f, -.1f) }); yield return .45f;
        Check(nav.SelectedIndex == 0, "Stick drift below deadzone leaves focus stable");
        Neutral(); yield return null;
        State(second, new GamepadState { leftTrigger = .9f }); yield return null;
        Check(nav.SelectedIndex == 1, "L2 advances one option");
        yield return .15f;
        Check(nav.SelectedIndex == 1, "L2 repeat waits before advancing again");
        yield return .3f;
        Check(nav.SelectedIndex != 1, "Holding L2 repeats at a bounded rate");
        Neutral(); yield return null;
        nav.FocusVehicle(Button("Options Button")); Tap(second, GamepadButton.South); yield return null;
        Check(GameObject.Find("Audio Options") != null, "Cross opens Options");
        yield return null;
        var slider = GameObject.Find("MÚSICA Slider").GetComponent<Slider>();
        slider.value = .4f;
        State(second, new GamepadState { leftStick = Vector2.right }); yield return null;
        Check(Mathf.Abs(slider.value - .45f) < .001f, "Stick right adjusts the focused volume slider by one step");
        Neutral(); yield return null;
        State(second, new GamepadState { leftTrigger = 1 }); yield return null;
        Check(Nav().SelectedIndex == 1, "L2 changes the selected control rather than the volume");
        Neutral(); yield return null;
        Time.timeScale = 0;
        State(second, new GamepadState { leftStick = Vector2.down }); yield return null;
        Check(Nav().SelectedIndex == 2, "Navigation works while timeScale is zero");
        Neutral(); yield return null; Time.timeScale = 1;
        Tap(second, GamepadButton.East); yield return null; yield return null;
        Check(GameObject.Find("Play Button") != null, "Circle returns to main menu");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow)); yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(Nav().SelectedIndex == 1, "Keyboard navigation still works alongside gamepad");
        ExecuteEvents.Execute(Button("Options Button").gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        yield return null;
        Check(GameObject.Find("Audio Options") != null, "Mouse/pointer click still opens Options");
        Button("Back Button").onClick.Invoke(); yield return null;
        Button("Play Button").onClick.Invoke(); yield return .5f;
        Button("Local Players 2").onClick.Invoke(); yield return .5f;
        var local = Object.FindObjectsByType<RallyMenuNavigation>().Where(n => n.isActiveAndEnabled).OrderBy(n => n.LocalPlayer).ToArray();
        Check(local.Length == 2 && RallyLocalDevices.GamepadFor(0) != RallyLocalDevices.GamepadFor(1), "Two local menus retain exclusive device owners");
        var p1 = RallyLocalDevices.GamepadFor(0); var p2 = RallyLocalDevices.GamepadFor(1);
        int p1Before = local[0].SelectedIndex, p2Before = local[1].SelectedIndex;
        State(p2, new GamepadState { leftStick = Vector2.down }); yield return null;
        Check(local[0].SelectedIndex == p1Before && local[1].SelectedIndex != p2Before, "J2 stick moves only J2 focus");
        Neutral(); yield return null;
        int p2After = local[1].SelectedIndex;
        State(p1, new GamepadState { leftTrigger = 1 }); yield return null;
        Check(local[0].SelectedIndex != p1Before && local[1].SelectedIndex == p2After, "J1 L2 moves only J1 focus");
        Neutral(); yield return null;
        local[1].FocusVehicle(Button("Player 2 Vehicle 2"));
        Tap(p2, GamepadButton.South); yield return null;
        Check(RallyGameSession.VehicleIndexForPlayer(1) == 2, "J2 Cross confirms its own car after browsing");
        var outlines = local[1].GetComponentsInChildren<Outline>();
        Check(outlines.Count(o => o.enabled) == 1, "J2 has exactly one visible focus outline");
    }

    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        stopping = true;
        foreach (var d in new InputDevice[] { first, second, keyboard }) if (d != null && d.added) InputSystem.RemoveDevice(d);
        if (disabled != null) foreach (var d in disabled) if (d.added) InputSystem.EnableDevice(d);
        EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!had[i]) PlayerPrefs.DeleteKey(Keys[i]);
            else if (Keys[i].Contains("Volume")) PlayerPrefs.SetFloat(Keys[i], floats[i]);
            else if (Keys[i].Contains("Vehicle") || Keys[i].Contains("Circuit")) PlayerPrefs.SetString(Keys[i], strings[i]);
            else PlayerPrefs.SetInt(Keys[i], ints[i]);
        }
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState(); RallyUserSettings.ApplyAudio();
        if (originalInput != null) InputSystem.settings = originalInput;
        else
        {
            var restored = ScriptableObject.CreateInstance<InputSettings>();
            EditorJsonUtility.FromJsonOverwrite(originalInputJson, restored);
            InputSystem.settings = restored;
        }
        if (testInput != null && testInput != InputSystem.settings) Object.DestroyImmediate(testInput);
        EditorSceneManager.playModeStartScene = originalStart;
        Application.runInBackground = originalBackground; EditorApplication.isPaused = originalPaused; Time.timeScale = originalTimeScale;
        File.AppendAllText(Report, "RESTORED: input settings/devices, preferences, play-start scene, background, pause and timeScale\n");
    }

    // Repairs cleanup from the first preserved run if Unity destroyed its transient
    // pre-Play InputSettings. Copies the original managed serialized fields only.
    public static string RepairPreviousRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Leave Play first.");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var previous = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(nameof(RallyMenuInputCheck)))
            .First(t => t != null && t != typeof(RallyMenuInputCheck) && t.GetField("steps", flags)?.GetValue(null) != null);
        var original = (InputSettings)previous.GetField("originalInput", flags).GetValue(null);
        var restored = ScriptableObject.CreateInstance<InputSettings>();
        foreach (var field in typeof(InputSettings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            if (field.IsPublic || field.IsDefined(typeof(SerializeField), true)) field.SetValue(restored, field.GetValue(original));
        InputSystem.settings = restored;
        EditorSceneManager.playModeStartScene = (SceneAsset)previous.GetField("originalStart", flags).GetValue(null);
        Application.runInBackground = (bool)previous.GetField("originalBackground", flags).GetValue(null);
        EditorApplication.isPaused = (bool)previous.GetField("originalPaused", flags).GetValue(null);
        Time.timeScale = (float)previous.GetField("originalTimeScale", flags).GetValue(null);
        File.AppendAllText(Report, "RESTORED: original serialized input fields, play-start scene, background, pause and timeScale (transient settings reference repaired)\n");
        return "Cleanup restored, out of Play; no scene or asset writes.";
    }
}
