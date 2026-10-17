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
    static string Report;
    static bool menusOnly;
    static bool sharedRaceCheck;
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

    public static void Run() => Start(false);
    public static void RunMenusOnly() => Start(true);
    public static void RunSharedRow() => Start(true, true);
    public static void RunGroupB()
    {
        Start(true);
        Report = "Logs/group-b-play.txt";
        File.WriteAllText(Report, "Group B: real Play, menus with virtual DualSense, 3 circuits x 1P/2P. Not a full driving lap or hardware test.\n");
        steps = GroupBSteps();
    }
    public static void RunGroupBRaces()
    {
        Start(true);
        Report = "Logs/group-b-races.txt";
        File.WriteAllText(Report, "Group B: 3 circuits x 1P/2P, spawn/contact/short acceleration, virtual gamepads.\n");
        steps = GroupBSteps(true);
    }
    static void Start(bool onlyMenus, bool checkSharedRace = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new Exception("Unity must be idle.");
        if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
            throw new Exception("External check requires the project's Disable Domain Reload setting.");
        menusOnly = onlyMenus; sharedRaceCheck = checkSharedRace;
        Report = checkSharedRace ? "Logs/shared-circuit-row-check.txt" : onlyMenus ? "Logs/selection-panel-check.txt" : "Logs/selection-ux-check.txt";
        scenes = EditorSceneManager.GetSceneManagerSetup(); start = EditorSceneManager.playModeStartScene;
        background = Application.runInBackground; paused = EditorApplication.isPaused; timeScale = Time.timeScale;
        had = Keys.Select(PlayerPrefs.HasKey).ToArray(); strings = Keys.Select(k => PlayerPrefs.GetString(k)).ToArray(); ints = Keys.Select(k => PlayerPrefs.GetInt(k)).ToArray();
        if (InputSystem.settings == null) RepairInputs();
        originalInput = InputSystem.settings; inputJson = EditorJsonUtility.ToJson(originalInput);
        testInput = Object.Instantiate(originalInput); testInput.hideFlags = HideFlags.HideAndDontSave;
        testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = testInput;
        RallyGameSession.SelectLocalPlayers(1); RallyGameSession.SelectVehicleForPlayer(0, 0); RallyGameSession.SelectVehicleForPlayer(1, 0);
        RallyGameSession.SelectCircuit(0);
        File.WriteAllText(Report, onlyMenus ? "Selection panel boundary: real Play, virtual DualSense/keyboard, 1P/2P menus only\n" : "Selection UX: real Play, virtual DualSense/keyboard, 1P/2P, HUD in all circuits\n");
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
    static void State(Gamepad pad, byte dpad = 8, bool cross = false, Vector2 stick = default, bool leftTrigger = false, bool rightTrigger = false)
    {
        InputSystem.QueueStateEvent(pad, new DualSenseHIDInputReport {
            leftStickX = (byte)Mathf.RoundToInt((stick.x + 1) * 127.5f), leftStickY = (byte)Mathf.RoundToInt((1 - stick.y) * 127.5f),
            rightStickX = 128, rightStickY = 128, buttons0 = (byte)(dpad | (cross ? 1 << 5 : 0)),
            leftTrigger = leftTrigger ? (byte)255 : (byte)0, rightTrigger = rightTrigger ? (byte)255 : (byte)0 });
    }
    static void Cross(Gamepad pad) => State(pad, cross: true);
    static void Dpad(Gamepad pad, Vector2 direction) => State(pad, direction.y > 0 ? (byte)0 : direction.y < 0 ? (byte)4 : direction.x > 0 ? (byte)2 : direction.x < 0 ? (byte)6 : (byte)8);
    static void Stick(Gamepad pad, Vector2 direction) => State(pad, stick: direction);
    static void KeysDown(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static void Horizontal(int input, bool right)
    {
        if (input == 0) Dpad(first, right ? Vector2.right : Vector2.left);
        else if (input == 1) KeysDown(right ? Key.RightArrow : Key.LeftArrow);
        else Stick(first, right ? Vector2.right : Vector2.left);
    }
    static bool Yellow(Color c) => c.r > .9f && c.g > .75f && c.b < .25f;
    static void DisplayOutput()
    {
        var output = Object.FindObjectsByType<Camera>().Single(c => c.name == "Menu Display 1 - clear only");
        Check(output.isActiveAndEnabled && output.targetTexture == null && output.targetDisplay == 0, "real camera renders to Display 1, not only RenderTextures");
        Check(output.cullingMask == 0 && output.clearFlags == CameraClearFlags.SolidColor, "display output does not render the landscape a second time");
        Check(Object.FindObjectsByType<AudioListener>().Count(l => l.enabled) == 1, "menu retains a single active AudioListener");
    }
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
        for (int input = 0; input < 3; input++)
        {
            nav.FocusVehicle(cars[0]);
            for (int step = 1; step <= 3; step++)
            {
                Horizontal(input, true); yield return null; Neutral(first); KeysDown(); yield return null;
                Check(Focus(nav) == (step < 3 ? cars[step] : B("Circuit 1 Button")), "right input " + input + ": Lancia -> Peugeot -> Audi -> terrain, step " + step);
                Check(RallyGameSession.SelectedVehicleIndex == 0, "browsing across panel boundary does not confirm a different car");
            }
            Horizontal(input, false); yield return null; Neutral(first); KeysDown(); yield return null;
            Check(Focus(nav) == cars[2], "left input " + input + " returns from terrain to Audi");
        }
        nav.FocusVehicle(B("Circuit 1 Button"));
        Dpad(first, Vector2.down); yield return null; Neutral(first); yield return null; Cross(first); yield return null; Neutral(first); yield return .15f;
        Check(RallyGameSession.SelectedRaceScene == "Circuit_02" && B("Circuit 2 Button").colors.normalColor.g > .5f && B("Circuit 2 Button").colors.normalColor.b < .2f, "track confirmation retains orange highlight");
        Dpad(first, Vector2.left); yield return null; Neutral(first); yield return null;
        Check(Focus(nav) == cars[2], "return from track list remembers Audi focus");
        State(first, rightTrigger: true); yield return .5f; Neutral(first); yield return null;
        Check(Focus(nav) == cars[2], "R2 no longer moves from Audi to terrain");
        nav.FocusVehicle(B("Circuit 2 Button"));
        State(first, leftTrigger: true); yield return .5f;
        Check(Focus(nav) == B("Circuit 2 Button") && RallyGameSession.SelectedVehicleIndex == 0, "L2 no longer moves from terrain to cars");
        Neutral(first); yield return null;
        nav.FocusVehicle(cars[2]);
        Dpad(first, Vector2.left); yield return null; Neutral(first); yield return .15f;
        Check(Focus(nav) == cars[1] && RallyGameSession.SelectedVehicleIndex == 0, "left still browses every car without confirming"); Chosen(cars, 0);
        Cross(first); yield return null; Neutral(first); yield return .15f;
        Check(RallyGameSession.SelectedVehicleIndex == 1, "DualSense Cross confirms focused Peugeot"); Chosen(cars, 1);
        Dpad(first, Vector2.left); yield return null; Neutral(first); yield return .15f;
        Check(Focus(nav) == cars[0] && RallyGameSession.SelectedVehicleIndex == 1, "moving focus preserves confirmed Peugeot"); Chosen(cars, 1);
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
        Check(preview.VisibleModel != null && GameObject.Find("Selected Vehicle").GetComponent<TMP_Text>().text == RallyGameSession.VehicleDisplayName(1), "preview model and name remain visible after cleanup");
        DisplayOutput();
        ScreenCapture.CaptureScreenshot("Logs/SelectionUX_1P.png"); yield return .3f;
        B("Local Players 2").onClick.Invoke(); yield return .4f;
        CleanSelection();
        var local = Object.FindObjectsByType<RallyMenuNavigation>().OrderBy(n => n.LocalPlayer).ToArray();
        Check(local.Length == 2, "two independent local navigation owners");
        menu = Object.FindAnyObjectByType<RallyMenuController>(); var localCars = Field<Button[][]>(menu, "localVehicleButtons");
        var shared = Field<Button[]>(menu, "circuitButtons");
        Check(GameObject.Find("Shared Circuit Card").GetComponentsInChildren<Button>(true).Length == 3 &&
            !menu.GetComponentsInChildren<Button>(true).Any(b => b.name.StartsWith("Player 2 Circuit")), "only one real track row exists, with three buttons and no duplicate/proxy row");
        foreach (var navigation in local)
            Check(shared.All(b => Field<Selectable[]>(navigation, "buttons").Contains(b)), "both navigators use the same physical circuit buttons");
        Gamepad padOne = RallyLocalDevices.GamepadFor(0), padTwo = RallyLocalDevices.GamepadFor(1);
        for (int p = 0; p < 2; p++)
        {
            var pad = p == 0 ? padOne : padTwo;
            int confirmed = RallyGameSession.VehicleIndexForPlayer(p), other = RallyGameSession.VehicleIndexForPlayer(1 - p);
            local[p].FocusVehicle(localCars[p][0]); Dpad(pad, Vector2.right); yield return null; Neutral(pad); yield return .15f;
            Check(Focus(local[p]) == localCars[p][1] && RallyGameSession.VehicleIndexForPlayer(p) == confirmed, "local browsing does not implicitly confirm J" + (p + 1));
            if (p == 1) { Dpad(pad, Vector2.right); yield return null; Neutral(pad); yield return null; }
            Cross(pad); yield return null; Neutral(pad); yield return .15f;
            Check(RallyGameSession.VehicleIndexForPlayer(p) == p + 1 && RallyGameSession.VehicleIndexForPlayer(1 - p) == other, "Cross confirms only its own player's car"); Chosen(localCars[p], p + 1);
            Dpad(pad, Vector2.down); yield return null; Neutral(pad); yield return null;
            Check(Focus(local[p]) == B("Player " + (p + 1) + " Ready"), "down from local car row goes to Ready, not a side car");
            Stick(pad, Vector2.up); yield return null; Neutral(pad); yield return null; Check(Focus(local[p]) == localCars[p][1], "up from Ready returns to central car");
            local[p].FocusVehicle(shared[0]);
            Dpad(pad, Vector2.right); yield return null; Neutral(pad); yield return null;
            Check(Focus(local[p]) == shared[1], "either player's left/right traverses the single shared track row");
            Dpad(pad, Vector2.up); yield return null; Neutral(pad); yield return null;
            Check(Focus(local[p]) == B("Player " + (p + 1) + " Ready"), "up from any local track returns to Ready row");
            Chosen(localCars[p], p + 1);
        }
        local[0].FocusVehicle(shared[0]); local[1].FocusVehicle(shared[2]);
        Check(shared[0].transform.Find("Local Focus 1").gameObject.activeInHierarchy && shared[2].transform.Find("Local Focus 2").gameObject.activeInHierarchy, "two independent focus frames coexist on one row");
        Dpad(padTwo, Vector2.left); yield return null; Neutral(padTwo); yield return null;
        Check(Focus(local[0]) == shared[0] && Focus(local[1]) == shared[1] && shared[0].transform.Find("Local Focus 1").gameObject.activeInHierarchy,
            "moving J2 focus does not erase J1 shared-row focus");
        local[0].FocusVehicle(shared[1]);
        Check(shared[1].transform.Find("Local Focus 1").gameObject.activeInHierarchy && shared[1].transform.Find("Local Focus 2").gameObject.activeInHierarchy && shared[1].transform.localScale == Vector3.one,
            "both players can focus the same button without fighting over its outline or scale");
        for (int p = 0; p < 2; p++)
        {
            var pad = p == 0 ? padOne : padTwo;
            local[p].FocusVehicle(shared[p]); Cross(pad); yield return null; Neutral(pad); yield return .15f;
            Check(RallyGameSession.SelectedRaceScene == RallyGameSession.CircuitScenes[p] &&
                shared.Count(b => b.colors.normalColor.r > .9f && b.colors.normalColor.g > .5f) == 1,
                "J" + (p + 1) + " confirmation sets the single shared race circuit and highlight");
            Check(RallyGameSession.VehicleIndexForPlayer(0) == 1 && RallyGameSession.VehicleIndexForPlayer(1) == 2, "changing shared circuit preserves distinct Peugeot/Audi choices");
        }
        B("Player 1 Ready").onClick.Invoke(); B("Player 2 Ready").onClick.Invoke();
        Check(B("Race Button").interactable, "both independent Ready states unlock race");
        Cross(padTwo); yield return null; Neutral(padTwo); yield return null;
        Check(!B("Race Button").interactable && Field<bool[]>(menu, "localReady").All(r => !r), "shared track confirmation invalidates both Ready states");
        var previews = Field<RallyMenuVehiclePreview[]>(menu, "localVehiclePreviews");
        while (previews.Where((p, i) => p.IsTransitioning || p.ShownIndex != i + 1 || p.Opacity < .99f).Any()) yield return null;
        Check(previews.All(p => p.VisibleModel != null), "both local preview models remain visible");
        DisplayOutput();
        ScreenCapture.CaptureScreenshot("Logs/SelectionUX_2P.png"); yield return .3f;
        if (sharedRaceCheck)
        {
            B("Player 1 Ready").onClick.Invoke(); B("Player 2 Ready").onClick.Invoke();
            local[0].FocusVehicle(B("Race Button")); Cross(padOne); yield return null; Neutral(padOne);
            while (RallyRaceLoading.IsPreparing || SceneManager.GetActiveScene().name != "Circuit_02") yield return null;
            var split = RallySplitScreen.Active;
            Check(split != null && split.Ready && RallyGameSession.LocalPlayerCount == 2, "shared row launches Circuit_02 for both local players");
            Check(split.PlayerOne.GetComponentInChildren<RallyVehicleVisual>(true).name == RallyGameSession.VehicleForPlayer(0) + " Visual" &&
                split.PlayerTwo.GetComponentInChildren<RallyVehicleVisual>(true).name == RallyGameSession.VehicleForPlayer(1) + " Visual", "each player's selected car reaches its own race rig");
            Check(Object.FindObjectsByType<RallyBotController>().Length == 0, "two-player race still has no bots");
            SceneManager.LoadScene("MainMenu");
        }
        else B("Back Button").onClick.Invoke();
        while (SceneManager.GetActiveScene().name != "MainMenu") yield return null;
        yield return .3f; Cross(first); yield return null; Neutral(first); yield return null;
        B("Credits Button").onClick.Invoke(); yield return .3f;
        string creditText = string.Join("\n", Object.FindAnyObjectByType<RallyMenuController>().GetComponentsInChildren<TMP_Text>().Select(t => t.text));
        Check(creditText.Contains("DisneyCars") && creditText.Contains("TonyWony") && creditText.Contains("CC-BY 4.0"), "current car authors/licenses appear on Credits page");
        Check(File.ReadAllText("CREDITS.md").Contains("DisneyCars") && File.ReadAllText("CREDITS.md").Contains("TonyWony"), "canonical CREDITS.md preserves current car attribution");
        if (menusOnly) yield break;
        for (int scene = 0; scene < 3; scene++) for (int players = 1; players <= 2; players++)
        {
            RallyGameSession.SelectLocalPlayers(players); RallyRaceLoading.Load(RallyGameSession.CircuitScenes[scene]);
            while (RallyRaceLoading.IsPreparing || SceneManager.GetActiveScene().name != RallyGameSession.CircuitScenes[scene]) yield return null;
            yield return .15f;
            var huds = Object.FindObjectsByType<RallyRaceHud>(); Check(huds.Length == players, "correct HUD count " + RallyGameSession.CircuitScenes[scene] + " " + players + "P");
            Check(!Object.FindObjectsByType<Camera>().Any(c => c.name == "Menu Display 1 - clear only"), "menu display camera is destroyed on entering race");
            foreach (var zone in Object.FindObjectsByType<RallyTurboZone>())
            {
                var mesh = zone.GetComponentInChildren<MeshFilter>().sharedMesh;
                var mat = zone.GetComponentInChildren<MeshRenderer>().sharedMaterials[1];
                Check(mesh.GetIndexCount(0) == 0 && mesh.GetIndexCount(1) == 48 && mesh.vertexCount == 32, "turbo has only four chevrons, zero dark backing geometry");
                Check(Mathf.Abs(mesh.bounds.size.x - 2.4f) < .1f && Mathf.Abs(mesh.bounds.size.z - 8.3f) < .1f, "turbo arrows use larger visual bounds");
                Check(Mathf.Abs(mat.GetColor("_BaseColor").a - .55f) < .001f && mat.GetFloat("_Surface") == 1 && mat.GetFloat("_ZWrite") == 0 && mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "turbo material uses real alpha transparency at 55 percent");
                Check(zone.ActivationHalfWidth == .7f && zone.GetComponent<BoxCollider>().size == new Vector3(2.2f, 3, 8), "narrow centre activation collider is unchanged");
            }
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
    static IEnumerator GroupBSteps(bool racesOnly = false)
    {
        if (racesOnly)
        {
            while (Object.FindAnyObjectByType<RallyMenuController>() == null) yield return null;
            yield return .4f;
            disabled = InputSystem.devices.Where(d => (d is Gamepad || d is Keyboard) && d.enabled).ToArray();
            foreach (var device in disabled) InputSystem.DisableDevice(device);
            first = InputSystem.AddDevice<DualSenseGamepadHID>(); second = InputSystem.AddDevice<DualSenseGamepadHID>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Neutral(first); Neutral(second); yield return null;
        }
        else
        {
        var menus = Steps();
        while (menus.MoveNext()) yield return menus.Current;
        RallyGameSession.SelectLocalPlayers(1);
        SceneManager.LoadScene(RallyGameSession.SelectionScene);
        while (SceneManager.GetActiveScene().name != RallyGameSession.SelectionScene || Object.FindAnyObjectByType<RallyMenuController>() == null) yield return null;
        yield return .4f;
        for (int i = 0; i < 3; i++)
        {
            B("Vehicle " + i + " Button").onClick.Invoke();
            var preview = Field<RallyMenuVehiclePreview>(Object.FindAnyObjectByType<RallyMenuController>(), "vehiclePreview");
            while (preview.ShownIndex != i || preview.IsTransitioning || preview.Opacity < .99f) yield return null;
            Check(preview.VisibleModel != null, "selected preview loaded: " + RallyPlayerVehicleSelection.Names[i]);
            ScreenCapture.CaptureScreenshot("Logs/GroupB_Menu_" + i + ".png"); yield return .3f;
        }
        }
        for (int scene = 0; scene < 3; scene++) for (int players = 1; players <= 2; players++)
        {
            RallyGameSession.SelectLocalPlayers(players);
            RallyGameSession.SelectVehicleForPlayer(0, players == 2 ? 0 : scene);
            RallyGameSession.SelectVehicleForPlayer(1, 1);
            RallyGameSession.SelectCircuit(scene);
            RallyRaceLoading.Load(RallyGameSession.CircuitScenes[scene]);
            while (RallyRaceLoading.IsPreparing || SceneManager.GetActiveScene().name != RallyGameSession.CircuitScenes[scene]) yield return null;
            yield return .5f;
            string label = RallyGameSession.CircuitScenes[scene] + " " + players + "P";
            var bots = Object.FindObjectsByType<RallyBotController>();
            var cars = Object.FindObjectsByType<JrsVehicleController>().Where(c => c.isActiveAndEnabled && c.GetComponentInParent<RallyBotController>() == null).OrderBy(c => c.name).ToArray();
            if (players == 2) cars = new[] { RallySplitScreen.Active.PlayerOne, RallySplitScreen.Active.PlayerTwo }.Select(g => g.GetComponent<JrsVehicleController>()).ToArray();
            Check(cars.Length == players && bots.Length == (players == 1 ? 3 : 0), label + " correct player/bot counts");
            for (int p = 0; p < cars.Length; p++)
            {
                var c = cars[p]; var visual = c.GetComponentInChildren<RallyVehicleVisual>();
                Check(visual != null && visual.name == RallyGameSession.VehicleForPlayer(p) + " Visual", label + " selected model J" + (p + 1));
                Check(c.GetComponentsInChildren<RallyVehicleVisual>().Length == 1, label + " no duplicate presentation");
                var ws = new[] { c.frontLeftWheel, c.frontRightWheel, c.rearLeftWheel, c.rearRightWheel };
                Check(ws.All(w => w != null) && ws.Distinct().Count() == 4, label + " four wheel colliders");
                Check(Mathf.Abs(c.GetComponent<Rigidbody>().mass - 1160) < .1f, label + " player mass unchanged");
                Check(c.frontLeftWheelTransform == visual.wheels[0] && c.rearRightWheelTransform == visual.wheels[3], label + " controller follows new wheels");
                for (int w = 0; w < 4; w++) Check(Mathf.Abs(ws[w].radius - visual.radii[w]) < .0001f, label + " fitted wheel radius " + w);
            }
            foreach (var bot in bots)
            {
                var c = bot.GetComponentInChildren<JrsVehicleController>();
                Check(c != null && c.GetComponentInChildren<RallyVehicleVisual>() != null, label + " bot has current model");
                Check(RallyPlayerVehicleSelection.Names.Any(n => c.GetComponentInChildren<RallyVehicleVisual>().name == n + " Visual"), label + " bot picks from Group B roster");
                Check(Mathf.Abs(c.GetComponent<Rigidbody>().mass - 1450) < .1f, label + " separate bot mass retained");
            }
            yield return 2f;
            foreach (var c in cars)
            {
                var ws = new[] { c.frontLeftWheel, c.frontRightWheel, c.rearLeftWheel, c.rearRightWheel };
                Check(ws.Count(w => w.isGrounded) >= 2, label + " wheels contact grid ground");
                Check(Vector3.Dot(c.transform.up, Vector3.up) > .95f, label + " car stays upright");
            }
            var origins = cars.Select(c => c.transform.position).ToArray();
            State(RallyLocalDevices.GamepadFor(0), rightTrigger: true);
            if (players == 2) State(RallyLocalDevices.GamepadFor(1), rightTrigger: true);
            yield return 1.5f;
            Neutral(first); Neutral(second);
            for (int p = 0; p < cars.Length; p++) Check(Vector3.Distance(cars[p].transform.position, origins[p]) > .2f, label + " J" + (p + 1) + " accelerates with assigned controller");
            ScreenCapture.CaptureScreenshot("Logs/GroupB_Race_" + scene + "_" + players + ".png"); yield return .3f;
            SceneManager.LoadScene("MainMenu"); while (SceneManager.GetActiveScene().name != "MainMenu") yield return null; yield return .3f;
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
