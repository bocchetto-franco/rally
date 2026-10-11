using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class RallyLoadingCheck
{
    static readonly string[] Scenes = { "Circuit_01", "Circuit_02", "Circuit_03" };
    static readonly string[] Keys = { "Rally.SelectedCircuit", "Rally.SelectedVehicle", "Rally.SelectedVehicleTwo", "Rally.LocalPlayers" };
    static readonly Dictionary<Rigidbody, Vector3> initial = new Dictionary<Rigidbody, Vector3>();
    static bool[] had;
    static string[] saved;
    static int oldPlayers, test, phase, lastFrame;
    static bool oldBackground;
    static bool oldEnterOptionsEnabled;
    static EnterPlayModeOptions oldEnterOptions;
    static SceneAsset oldStart;
    static double deadline, started;
    static Gamepad[] pads;
    const string Report = "Logs/race-loading-check.txt";
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer) throw new Exception("Editor must be idle.");
        had = new bool[Keys.Length]; saved = new string[Keys.Length];
        for (int i = 0; i < Keys.Length; i++) { had[i] = PlayerPrefs.HasKey(Keys[i]); saved[i] = PlayerPrefs.GetString(Keys[i]); }
        oldPlayers = PlayerPrefs.GetInt(Keys[3], 1);
        oldStart = EditorSceneManager.playModeStartScene; oldBackground = Application.runInBackground;
        oldEnterOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldEnterOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        File.WriteAllText(Report, "Race loading gate: three scenes, 1P/2P, frozen physics/timer, full throttle held through loading\n");
        test = phase = 0; lastFrame = -1;
        deadline = EditorApplication.timeSinceStartup + 400;
        EditorApplication.update += Tick;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        Application.runInBackground = true;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (phase == 99) { if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Loading test timeout")); return; }
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (phase == 0)
            {
                if (SceneManager.GetActiveScene().name != "MainMenu" || UnityEngine.Object.FindAnyObjectByType<RallyMenuController>() == null) return;
                pads = new[] { InputSystem.AddDevice<Gamepad>(), InputSystem.AddDevice<Gamepad>() };
                phase = 1; return;
            }
            if (phase == 1)
            {
                initial.Clear();
                RallyGameSession.SelectLocalPlayers(test % 2 + 1);
                foreach (Gamepad pad in pads) InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 });
                started = EditorApplication.timeSinceStartup;
                RallyRaceLoading.Load(Scenes[test / 2]);
                Check(RallyRaceLoading.IsPreparing, "Loading starts immediately: " + Scenes[test / 2]);
                phase = 2; return;
            }
            if (phase == 2)
            {
                if (RallyRaceLoading.IsPreparing)
                {
                    if (SceneManager.GetActiveScene().name != Scenes[test / 2]) return;
                    CheckSilent(Time.timeScale == 0, "Physics not paused during loading");
                    if (test % 2 == 1 && (RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready)) return;
                    foreach (var controller in UnityEngine.Object.FindObjectsByType<JrsVehicleController>())
                    {
                        if (controller.gameObject.scene.name != Scenes[test / 2]) continue;
                        var body = controller.GetComponent<Rigidbody>();
                        if (!initial.ContainsKey(body)) initial.Add(body, body.position);
                        CheckSilent(Vector3.Distance(body.position, initial[body]) < .001f, "A car moved during loading");
                        CheckSilent(body.linearVelocity.sqrMagnitude < .0001f, "Car has velocity during loading");
                    }
                    foreach (var timer in UnityEngine.Object.FindObjectsByType<RallyCheckpointManager>())
                        CheckSilent(timer.ElapsedTime == 0 && timer.NextCheckpoint == 0, "Timer/checkpoint advanced during loading");
                    return;
                }
                Check(SceneManager.GetActiveScene().name == Scenes[test / 2], "Correct scene loaded");
                Check(Time.timeScale == 1, "Physics released after preparation");
                Check(initial.Count == (test % 2 == 0 ? 4 : 2), "Correct runners prepared, including bots or J2");
                var presentation = RallyVehiclePresentationCache.ForScene(SceneManager.GetActiveScene());
                File.AppendAllText(Report, "Presentation entries=" + presentation.Length + ": " + string.Join(", ", Array.ConvertAll(presentation, v => (v.car != null ? v.car.name : "null") + "/input=" + (v.input != null))) + "\n");
                Check(presentation.Length == (test % 2 == 0 ? 4 : 2), "Presentation cache contains the complete race roster");
                foreach (var vehicle in presentation)
                {
                    CheckSilent(vehicle.car != null && vehicle.body == vehicle.car.GetComponent<Rigidbody>(), "Presentation body belongs to another car");
                    CheckSilent(vehicle.input == vehicle.car.GetComponent<RallyLocalPlayerInput>(), "Presentation input belongs to another player");
                }
                var budget = UnityEngine.Object.FindAnyObjectByType<RallyRenderPerformance>();
                var viewField = typeof(RallyRenderPerformance).GetField("views", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Check(budget != null && ((Array)viewField.GetValue(budget)).Length == (test % 2 == 0 ? 1 : 2), "Render cache contains every player camera");
                foreach (var controller in UnityEngine.Object.FindObjectsByType<JrsVehicleController>())
                    Check(controller.enabled, "Controller restored: " + controller.name);
                var library = RallyAudioLibrary.Load();
                foreach (AudioClip clip in new[] { library.engineLoop, library.tyreSkidLoop, library.impact, library.raceMusic })
                    Check(clip.loadState == AudioDataLoadState.Loaded, "Audio ready: " + clip.name);
                File.AppendAllText(Report, "Preparation seconds=" + (EditorApplication.timeSinceStartup - started).ToString("F2") + "\n");
                if (test == 4) ScreenCapture.CaptureScreenshot("Logs/RaceLoading_Ready_C03.png");
                foreach (Gamepad pad in pads) InputSystem.QueueStateEvent(pad, new GamepadState());
                test++;
                if (test >= 6) { Finish(null); return; }
                // CaptureScreenshot is deferred until end of frame. Keep C03
                // active for that frame instead of accidentally photographing MainMenu.
                if (test == 5) { phase = 4; return; }
                SceneManager.LoadScene("MainMenu"); phase = 3; return;
            }
            if (phase == 3 && SceneManager.GetActiveScene().name == "MainMenu") phase = 1;
            if (phase == 4) { SceneManager.LoadScene("MainMenu"); phase = 3; }
        }
        catch (Exception error) { Finish(error); }
    }
    static void CheckSilent(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Check(bool condition, string message) { CheckSilent(condition, message); File.AppendAllText(Report, "PASS: " + message + "\n"); }
    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        if (pads != null) foreach (Gamepad pad in pads) if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        phase = 99; EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!had[i]) PlayerPrefs.DeleteKey(Keys[i]);
            else if (i == 3) PlayerPrefs.SetInt(Keys[i], oldPlayers); else PlayerPrefs.SetString(Keys[i], saved[i]);
        }
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
        EditorSceneManager.playModeStartScene = oldStart; Application.runInBackground = oldBackground;
        EditorSettings.enterPlayModeOptionsEnabled = oldEnterOptionsEnabled;
        EditorSettings.enterPlayModeOptions = oldEnterOptions;
    }
}
