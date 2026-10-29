using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Opt-in batch Play check: actual six scene/mode combinations, no shader warmup.</summary>
[InitializeOnLoad]
public static class RallyStartLightsCheck
{
    const string Key = "Rally.StartLightsCheck";
    static string Report => "Logs/start-lights-case-" + SessionState.GetInt(Key + "Case", 0) + ".txt";
    static double deadline, greenAt;
    static RallyRaceStartLights gate;
    static Rigidbody[] bodies;
    static Vector3[] poses;
    static int lastCount = -1;
    static bool green;
    static bool sceneRequested;
    static Gamepad[] pads;
    static RallyStartLightsCheck() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-rally-start-case");
        int first = at >= 0 ? int.Parse(args[at + 1]) : 0;
        SessionState.SetInt(Key + "Case", first);
        Directory.CreateDirectory("Logs"); File.WriteAllText(Report, "Start lights Play case " + first + " " + DateTimeOffset.Now.ToString("O") + "\n");
        if (EditorUtility.scriptCompilationFailed) throw new Exception("Compilation failed");
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + "Index", first);
        SessionState.SetInt(Key + "Players", PlayerPrefs.GetInt("Rally.LocalPlayers", 1));
        SessionState.SetBool(Key + "HadPlayers", PlayerPrefs.HasKey("Rally.LocalPlayers"));
        SessionState.SetString(Key + "StartScene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + "FastPlay", EditorSettings.enterPlayModeOptionsEnabled);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        Next();
    }
    static void Next()
    {
        int index = SessionState.GetInt(Key + "Index", 0);
        if (index > SessionState.GetInt(Key + "Case", 0)) { End(null); return; }
        RallyGameSession.SelectLocalPlayers(index % 2 + 1);
        EditorSceneManager.playModeStartScene = null;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            gate = null; bodies = null; lastCount = -1; green = false;
            sceneRequested = false;
            deadline = EditorApplication.timeSinceStartup + 90;
            pads = new[] { InputSystem.AddDevice<Gamepad>(), InputSystem.AddDevice<Gamepad>() };
        }
        if (state == PlayModeStateChange.EnteredEditMode) Next();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || !Application.isPlaying || deadline == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (!sceneRequested)
            {
                if (EditorApplication.timeSinceStartup < deadline - 89) return;
                int index = SessionState.GetInt(Key + "Index", 0);
                RallyGameSession.SelectLocalPlayers(index % 2 + 1);
                sceneRequested = true;
                SceneManager.LoadScene("Circuit_0" + (index / 2 + 1), LoadSceneMode.Single);
                return;
            }
            foreach (var pad in pads) InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 });
            if (gate == null)
            {
                gate = Object.FindAnyObjectByType<RallyRaceStartLights>();
                if (gate == null) return;
            }
            if (!gate.Released)
            {
                if (Time.timeScale != 0) throw new Exception("Physics released during red");
                if (!RallyRaceLoading.IsInputBlocked) throw new Exception("Input gate missing");
                if (RallyGameSession.LocalPlayerCount == 2 && (RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready)) return;
                var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.name == "Shared Start Lights Canvas").ToArray();
                if (canvases.Length != 1) throw new Exception("Expected exactly one shared light Canvas");
                if (bodies == null)
                {
                    bodies = Object.FindObjectsByType<JrsVehicleController>().Where(c => c.isActiveAndEnabled).Select(c => c.GetComponent<Rigidbody>()).Where(b => b != null).ToArray();
                    if (bodies.Length != (RallyGameSession.LocalPlayerCount == 2 ? 2 : 4)) throw new Exception("Unexpected race roster: " + bodies.Length);
                    poses = bodies.Select(b => b.position).ToArray();
                    foreach (var timer in Object.FindObjectsByType<RallyCheckpointManager>())
                    {
                        timer.TryPass(0, bodies[0].GetComponentInChildren<Collider>());
                        if (timer.IsRunning || timer.ElapsedTime != 0) throw new Exception("Timer started under red");
                    }
                }
                for (int i = 0; i < bodies.Length; i++) if (Vector3.Distance(poses[i], bodies[i].position) > .001f) throw new Exception("Car moved under red");
                if (gate.LitCount != lastCount)
                {
                    if (lastCount >= 0 && gate.LitCount != lastCount + 1) throw new Exception("Red light sequence skipped");
                    lastCount = gate.LitCount;
                }
            }
            else
            {
                if (!green)
                {
                    if (lastCount != 5 || Time.timeScale != 1 || RallyRaceLoading.IsInputBlocked) throw new Exception("Invalid simultaneous release: count=" + lastCount + ", scale=" + Time.timeScale + ", blocked=" + RallyRaceLoading.IsInputBlocked);
                    green = true; greenAt = EditorApplication.timeSinceStartup;
                    foreach (var light in gate.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Start Light ") && i.name != "Start Light Housing"))
                        if (light.color.g < .9f || light.color.r > .2f) throw new Exception("Lamp not green");
                }
                if (EditorApplication.timeSinceStartup - greenAt < 2.7 && !gate.IsGreen) throw new Exception("Green hidden too early");
                if (!gate.IsGreen)
                {
                    if (EditorApplication.timeSinceStartup - greenAt < 2.7) throw new Exception("Green duration");
                    if (!bodies.Any(b => b != null && Vector3.Distance(poses[Array.IndexOf(bodies, b)], b.position) > .2f)) throw new Exception("No car moved after release");
                    File.AppendAllText(Report, "PASS: " + SceneManager.GetActiveScene().name + " " + RallyGameSession.LocalPlayerCount + "P; 5 sequential reds; roster held against throttle; timer gated; one Canvas; all green/release; 3 seconds; motion resumes\n");
                    foreach (var pad in pads) InputSystem.RemoveDevice(pad);
                    deadline = 0;
                    SessionState.SetInt(Key + "Index", SessionState.GetInt(Key + "Index", 0) + 1);
                    EditorApplication.ExitPlaymode();
                }
            }
        }
        catch (Exception error) { End(error); }
    }
    static void End(Exception error)
    {
        SessionState.SetBool(Key, false);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "FastPlay", false);
        if (SessionState.GetBool(Key + "HadPlayers", false)) PlayerPrefs.SetInt("Rally.LocalPlayers", SessionState.GetInt(Key + "Players", 1)); else PlayerPrefs.DeleteKey("Rally.LocalPlayers");
        PlayerPrefs.Save();
        string start = SessionState.GetString(Key + "StartScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(start) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS; preferences restored\n" : "FAIL: " + error + "\n");
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
