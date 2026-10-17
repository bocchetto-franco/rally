using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class RallyMenuVehiclePreviewCheck
{
    const string Report = "Logs/menu-vehicle-preview-test.txt";
    static int phase, frame;
    static double next, deadline;
    static Gamepad pad, second;
    static Keyboard keyboard;
    static InputDevice[] disabled;
    static SceneAsset oldStart;
    static SceneSetup[] scenes;
    static bool oldBackground, oldPaused;
    static InputSettings oldInputSettings, testInputSettings;
    static InputSettings.EditorInputBehaviorInPlayMode oldInputFocus;
    static InputSettings.BackgroundBehavior oldInputBackground;
    static bool observeFade, sawFade;
    static Quaternion rotation;
    static readonly string[] keys = { "Rally.SelectedVehicle", "Rally.SelectedVehicleTwo", "Rally.SelectedCircuit", "Rally.BotDifficulty", "Rally.LocalPlayers" };
    static bool[] had;
    static string[] strings;
    static int[] ints;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new Exception("Editor must be idle");
        if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
            throw new Exception("This external verifier requires the project's Disable Domain Reload Play setting.");
        oldStart = EditorSceneManager.playModeStartScene; scenes = EditorSceneManager.GetSceneManagerSetup();
        oldBackground = Application.runInBackground;
        oldPaused = EditorApplication.isPaused;
        EditorApplication.isPaused = false;
        oldInputSettings = InputSystem.settings;
        oldInputFocus = oldInputSettings.editorInputBehaviorInPlayMode;
        oldInputBackground = oldInputSettings.backgroundBehavior;
        testInputSettings = Object.Instantiate(oldInputSettings);
        testInputSettings.hideFlags = HideFlags.HideAndDontSave;
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = testInputSettings;
        had = keys.Select(PlayerPrefs.HasKey).ToArray(); strings = keys.Select(k => PlayerPrefs.GetString(k)).ToArray(); ints = keys.Select(k => PlayerPrefs.GetInt(k)).ToArray();
        RallyGameSession.SelectLocalPlayers(1); RallyGameSession.SelectVehicleForPlayer(0, 0); RallyGameSession.SelectVehicleForPlayer(1, 0);
        Directory.CreateDirectory("Logs"); File.WriteAllText(Report, "3D car preview: real Play, virtual keyboard/two gamepads, cleanup and model residency\n");
        phase = 0; frame = -1; next = 0; observeFade = sawFade = false; deadline = EditorApplication.timeSinceStartup + 600;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/VehicleCircuitSelection.unity");
        Application.runInBackground = true; EditorApplication.isPlaying = true;
    }

    static RallyMenuVehiclePreview[] Views() => Object.FindObjectsByType<RallyMenuVehiclePreview>().OrderBy(v => v.PreviewCamera.name).ToArray();
    static RallyMenuVehiclePreview View(int index = 0) => Views()[index];
    static bool Ready(int index, int model) => Views().Length > index && View(index).ShownIndex == model && !View(index).IsTransitioning;
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (phase == 99) { if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timeout phase " + phase)); return; }
        if (!EditorApplication.isPlaying || Object.FindAnyObjectByType<RallyMenuController>() == null) return;
        Application.runInBackground = true;
        if (observeFade && Views().Length > 0 && View().Opacity > 0 && View().Opacity < .99f) sawFade = true;
        if (next == 0) next = EditorApplication.timeSinceStartup + 2;
        if (EditorApplication.timeSinceStartup < next || frame == Time.frameCount) return;
        frame = Time.frameCount; next = EditorApplication.timeSinceStartup + .25;
        try
        {
            switch (phase)
            {
                case 0:
                    if (!Ready(0, 0)) return;
                    disabled = InputSystem.devices.Where(d => (d is Gamepad || d is Keyboard) && d.enabled).ToArray();
                    foreach (var device in disabled) InputSystem.DisableDevice(device);
                    pad = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>();
                    Validate(View()); rotation = View().VisibleModel.transform.parent.rotation;
                    Capture("Logs/Menu_Car_Porsche.png"); phase++; next += 1; break;
                case 1:
                    Check(Quaternion.Angle(rotation, View().VisibleModel.transform.parent.rotation) > 5, "Slow continuous vertical rotation");
                    observeFade = true;
                    Object.FindAnyObjectByType<RallyMenuNavigation>().FocusVehicle(GameObject.Find("Vehicle 1 Button").GetComponent<Button>());
                    Pad(pad, GamepadButton.South); phase++; break;
                case 2:
                    Release(); Check(View().RequestedIndex == 1, "Cross confirms the focused BMW preview");
                    phase++; break;
                case 3:
                    if (!Ready(0, 1)) return;
                    Check(sawFade, "Intermediate fade opacity observed during model transition"); observeFade = false;
                    Validate(View()); Capture("Logs/Menu_Car_Bmw.png"); phase = 30; break;
                case 30:
                    Object.FindAnyObjectByType<RallyMenuNavigation>().FocusVehicle(GameObject.Find("Vehicle 2 Button").GetComponent<Button>());
                    Pad(pad, GamepadButton.South); phase = 4; break;
                case 4: Release(); phase++; break;
                case 5:
                    if (!Ready(0, 2)) return;
                    Validate(View()); Capture("Logs/Menu_Car_Audi.png"); phase = 50; break;
                case 50: InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow)); phase = 6; break;
                case 6: Release(); Check(View().RequestedIndex == 1, "Keyboard left selects BMW"); phase++; break;
                case 7:
                    if (!Ready(0, 1)) return;
                    // Coalescing keeps just the final model, even if requested repeatedly mid-fade/load.
                    View().Show(2); View().Show(0); View().Show(2); phase++; break;
                case 8:
                    if (!Ready(0, 2)) return;
                    Validate(View()); phase++; next += 3; break;
                case 9:
                    var resident = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => EditorUtility.IsPersistent(g) &&
                        AssetDatabase.GetAssetPath(g).StartsWith("Assets/Resources/MenuVehicles/") && g.transform.parent == null).ToArray();
                    Check(resident.Length <= 1 && resident.All(g => g.name == "AudiQuattro"), "No discarded preview prefab remains resident after cleanup (count=" + resident.Length + ")");
                    RallyGameSession.SelectVehicleForPlayer(0, 0); RallyGameSession.SelectVehicleForPlayer(1, 0);
                    second = InputSystem.AddDevice<Gamepad>();
                    GameObject.Find("Local Players 2").GetComponent<Button>().onClick.Invoke(); phase++; break;
                case 10:
                    if (Views().Length != 2 || !Ready(0, 0) || !Ready(1, 0)) return;
                    Check(View(0).PreviewCamera.cullingMask != View(1).PreviewCamera.cullingMask, "Two separate studios/cameras");
                    Check(RallyLocalDevices.GamepadFor(0) == pad && RallyLocalDevices.GamepadFor(1) == second, "Virtual pads have exclusive panel owners");
                    Pad(second, GamepadButton.DpadRight); phase++; break;
                case 11:
                    Release(); Check(View(1).RequestedIndex == 1 && View(0).RequestedIndex == 0, "J2 browsing never changes J1 preview");
                    Check(RallyGameSession.VehicleIndexForPlayer(1) == 0, "Local browsing doesn't confirm the car prematurely"); phase++; break;
                case 12:
                    if (!Ready(1, 1)) return;
                    Pad(second, GamepadButton.South); phase++; break;
                case 13:
                    Release(); Check(RallyGameSession.VehicleIndexForPlayer(1) == 1, "Cross confirms J2 BMW"); Pad(pad, GamepadButton.DpadLeft); phase++; break;
                case 14: Release(); Check(View(0).RequestedIndex == 2, "D-Pad left wraps Porsche to Audi"); phase++; break;
                case 15:
                    if (!Ready(0, 2) || !Ready(1, 1)) return;
                    Validate(View(0)); Validate(View(1)); Capture("Logs/Menu_Cars_Local.png"); phase = 150; break;
                case 150: Pad(pad, GamepadButton.South); phase = 16; next += 2; break;
                case 16:
                    Release(); Check(RallyGameSession.VehicleIndexForPlayer(0) == 2 && RallyGameSession.VehicleIndexForPlayer(1) == 1, "Different cars confirmed independently");
                    var localResident = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => EditorUtility.IsPersistent(g) && AssetDatabase.GetAssetPath(g).StartsWith("Assets/Resources/MenuVehicles/") && g.transform.parent == null).ToArray();
                    Check(localResident.Length <= 2 && !localResident.Any(g => g.name == "LanciaS4"), "2P has no discarded preview prefab resident");
                    InputSystem.DisableDevice(pad); InputSystem.DisableDevice(second);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.RightArrow)); phase = 20; break;
                case 20:
                    Release(); Check(View(0).RequestedIndex == 0 && View(1).RequestedIndex == 2, "Shared keyboard browses each local preview independently");
                    Check(RallyGameSession.VehicleIndexForPlayer(0) == 2 && RallyGameSession.VehicleIndexForPlayer(1) == 1, "Keyboard browsing also requires confirmation"); phase++; break;
                case 21:
                    if (!Ready(0, 0) || !Ready(1, 2)) return;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.Enter)); phase++; break;
                case 22:
                    Release(); Check(RallyGameSession.VehicleIndexForPlayer(0) == 0 && RallyGameSession.VehicleIndexForPlayer(1) == 2, "Keyboard confirms separate local cars");
                    GameObject.Find("Player 1 Ready").GetComponent<Button>().onClick.Invoke();
                    GameObject.Find("Player 2 Ready").GetComponent<Button>().onClick.Invoke();
                    Check(GameObject.Find("Race Button").GetComponent<Button>().interactable, "Both ready confirmations still unlock the race");
                    GameObject.Find("Back Button").GetComponent<Button>().onClick.Invoke(); phase = 17; next += 2; break;
                case 17:
                    Check(Views().Length == 0, "No preview component outside selection");
                    Check(!Object.FindObjectsByType<Camera>().Any(c => c.name.StartsWith("Vehicle Preview Camera")), "Studio cameras destroyed on exit");
                    Check(!Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name.StartsWith("Car studio - Player")), "Render targets released on exit");
                    Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error); }
    }

    static void Validate(RallyMenuVehiclePreview view)
    {
        var model = view.VisibleModel;
        Check(model != null && model.GetComponentsInChildren<MeshRenderer>().Length >= 1, "Visible model " + view.ShownIndex);
        Check(model.GetComponentsInChildren<Component>(true).All(c => c is Transform || c is MeshFilter || c is MeshRenderer), "Mesh-only: no physics/input/audio/behaviour");
        Check(view.PreviewCamera.targetTexture.width <= 768 && view.PreviewCamera.targetTexture.height < 512, "Bounded render target");
        Check((view.PreviewCamera.cullingMask & (1 << 31)) == 0, "Camera cannot see circuit backdrop");
        Check(!view.PreviewCamera.TryGetComponent<AudioListener>(out _), "No additional AudioListener");
        Check(model.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader.name.Contains("Universal Render Pipeline"))), "URP materials valid");
        Check(view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == RallyGameSession.VehicleDisplayName(view.ShownIndex)), "Correct name below car");
        Check(model.GetComponentsInChildren<Transform>().All(t => (view.PreviewCamera.cullingMask & (1 << t.gameObject.layer)) != 0), "All geometry in isolated camera layer");
        Check(view.Opacity > .99f, "Smooth transition finishes fully visible");
    }
    static void Pad(Gamepad device, GamepadButton button) => InputSystem.QueueStateEvent(device, new GamepadState().WithButton(button));
    static void Capture(string path) => ScreenCapture.CaptureScreenshot(path);
    static void Release() { if (pad != null) InputSystem.QueueStateEvent(pad, new GamepadState()); if (second != null) InputSystem.QueueStateEvent(second, new GamepadState()); if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState()); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); File.AppendAllText(Report, "PASS: " + message + "\n"); }
    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad); if (second != null && second.added) InputSystem.RemoveDevice(second); if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (disabled != null) foreach (var device in disabled) if (device.added) InputSystem.EnableDevice(device);
        phase = 99; EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        for (int i = 0; i < keys.Length; i++) { if (!had[i]) PlayerPrefs.DeleteKey(keys[i]); else if (i < 3) PlayerPrefs.SetString(keys[i], strings[i]); else PlayerPrefs.SetInt(keys[i], ints[i]); }
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
        EditorSceneManager.playModeStartScene = oldStart; EditorSceneManager.RestoreSceneManagerSetup(scenes); Application.runInBackground = oldBackground;
        EditorApplication.isPaused = oldPaused;
        // Editor input state restoration/unload can replace a transient settings object.
        // Preserve the actual values even if the original native object no longer exists.
        if (oldInputSettings != null) InputSystem.settings = oldInputSettings;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldInputFocus;
        InputSystem.settings.backgroundBehavior = oldInputBackground;
        if (testInputSettings != null && InputSystem.settings != testInputSettings) Object.DestroyImmediate(testInputSettings);
    }
}
