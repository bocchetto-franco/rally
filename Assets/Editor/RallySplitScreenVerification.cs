using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real Play-mode coverage for scene setup, exclusive input and independent race state.</summary>
[InitializeOnLoad]
public static class RallySplitScreenVerification
{
    const string Request = "Temp/rally-split-screen-test.request";
    const string Report = "Logs/split-screen-test.txt";
    const string ActiveKey = "Rally.SplitTest.Active";
    static int phase, circuit, minimumFrame;
    static double phaseAt, deadline;
    static double nextStatusAt;
    static Keyboard keyboard;
    static DualSenseGamepadHID pad;
    static Vector3[] botStart;
    static Rigidbody one, two;
    static Collider colliderOne, colliderTwo;
    static Vector3 resetOne, resetTwo;
    static float pauseTimeOne, pauseTimeTwo;
    static string[] TimeKeys => RallyGameSession.CircuitScenes.SelectMany(scene =>
        new[] { "Rally.BestTimes." + scene + ".Count", "Rally.BestTimes." + scene + ".Best" }
            .Concat(Enumerable.Range(0, 5).Select(i => "Rally.BestTimes." + scene + "." + i)))
        .Concat(new[] { "Rally.LastRaceTime" }).ToArray();

    static RallySplitScreenVerification()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            Finish(new InvalidOperationException("Scripts changed during the test; retrying with a fresh Play session."));
            File.WriteAllText(Request, "retry");
        };
    }

    [MenuItem("Tools/Rally/Verify Local Split Screen In All Circuits")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene and leave Play before testing split screen.");
        // Take ownership only after the other task has deferred its benchmark requests.
        if (SessionState.GetBool("Rally.RenderBenchmark.Running", false))
        {
            PlayerPrefs.SetInt("Rally.LocalPlayers", SessionState.GetInt("Rally.RenderBenchmark.Players", 1));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString("Rally.RenderBenchmark.PreviousStart", ""));
            SessionState.SetBool("Rally.RenderBenchmark.Running", false);
            SessionState.SetBool("Rally.RenderBenchmark.Advance", false);
        }
        SessionState.SetString("Rally.SplitTest.Scene", SceneManager.GetActiveScene().path);
        SessionState.SetString("Rally.SplitTest.StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("Rally.SplitTest.Background", Application.runInBackground);
        SessionState.SetInt("Rally.SplitTest.InputFocus", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
        SessionState.SetInt("Rally.SplitTest.InputBackground", (int)InputSystem.settings.backgroundBehavior);
        RallyGameSession.RestoreSavedState();
        SessionState.SetInt("Rally.SplitTest.Players", RallyGameSession.LocalPlayerCount);
        SessionState.SetString("Rally.SplitTest.Vehicle", RallyGameSession.SelectedVehicle);
        SessionState.SetString("Rally.SplitTest.Circuit", RallyGameSession.SelectedCircuit);
        foreach (string key in TimeKeys)
        {
            SessionState.SetBool("Rally.SplitTest.Has." + key, PlayerPrefs.HasKey(key));
            if (key.EndsWith(".Count")) SessionState.SetInt("Rally.SplitTest.Value." + key, PlayerPrefs.GetInt(key));
            else SessionState.SetFloat("Rally.SplitTest.Value." + key, PlayerPrefs.GetFloat(key));
        }
        File.WriteAllText(Report, "Split-screen Play-mode integration\n");
        SessionState.SetBool(ActiveKey, true);
        phase = circuit = minimumFrame = 0;
        deadline = phaseAt = 0;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
        EditorApplication.isPaused = false;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup >= nextStatusAt)
        {
            nextStatusAt = EditorApplication.timeSinceStartup + 1;
            File.WriteAllText("Logs/split-screen-editor-state.txt", $"{DateTime.Now:O}\nPlaying={EditorApplication.isPlaying}\nChangingPlayMode={EditorApplication.isPlayingOrWillChangePlaymode}\nCompiling={EditorApplication.isCompiling}\nUpdating={EditorApplication.isUpdating}\nTestActive={SessionState.GetBool(ActiveKey, false)}\n");
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool("Rally.SplitTest.Restore", true);
            }
            if (SessionState.GetBool("Rally.SplitTest.Restore", false))
            {
                SessionState.SetBool("Rally.SplitTest.Restore", false);
                RestoreChoices();
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("Rally.SplitTest.StartScene", ""));
                string original = SessionState.GetString("Rally.SplitTest.Scene", "");
                if (!string.IsNullOrEmpty(original) && SceneManager.GetActiveScene().path != original)
                    EditorSceneManager.OpenScene(original);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool("Rally.SplitTest.Success", false) ? 0 : 1);
            }
            if (File.Exists(Request))
            {
                File.Delete(Request);
                try { Run(); } catch (Exception e) { File.WriteAllText(Report, "FAIL\n" + e); }
            }
            return;
        }
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ActiveKey, false)) return;
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        // Virtual devices must reach the game even when this editor test focuses
        // Console/Inspector or the automation window is temporarily occluded.
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        double now = EditorApplication.timeSinceStartup;
        if (deadline == 0) { deadline = now + 240; phaseAt = now; }
        if (now > deadline) { Finish(new Exception("Split-screen test timed out at phase " + phase)); return; }
        if (now - phaseAt < .4 || Time.frameCount < minimumFrame) return;
        try
        {
            RallySplitScreen split = RallySplitScreen.Active;
            if (phase == 0)
            {
                if (SceneManager.GetActiveScene().name != RallyGameSession.SelectionScene)
                {
                    SceneManager.LoadScene(RallyGameSession.SelectionScene);
                    phaseAt = now;
                    return;
                }
                EditorApplication.isPaused = false;
                Button twoPlayers = UnityEngine.Object.FindObjectsByType<Button>().First(b => b.name == "Local Players 2");
                twoPlayers.onClick.Invoke();
                Require(RallyGameSession.LocalPlayerCount == 2, "Selection button enables two-player mode");
                RallyGameSession.SelectCircuit(0);
                RallyGameSession.SelectVehicle(0);
                UnityEngine.Object.FindObjectsByType<Button>().First(b => b.name == "Race Button").onClick.Invoke();
                Next(1);
            }
            else if (phase == 1 && split != null && split.Ready)
            {
                ValidateStructure(split);
                one = split.PlayerOne.GetComponent<Rigidbody>();
                two = split.PlayerTwo.GetComponent<Rigidbody>();
                colliderOne = BodyBox(split.PlayerOne);
                colliderTwo = BodyBox(split.PlayerTwo);
                resetOne = one.position;
                resetTwo = two.position;
                keyboard = InputSystem.AddDevice<Keyboard>("Split Test Keyboard");
                pad = InputSystem.AddDevice<DualSenseGamepadHID>("Split Test DualSense");
                split.PlayerTwo.GetComponent<RallyLocalPlayerInput>().PairGamepad(pad);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
                SendPad(throttle: .65f, steer: -.4f);
                botStart = Bots().Select(b => b.GetComponentInChildren<Rigidbody>().position).ToArray();
                Next(2);
            }
            else if (phase == 2)
            {
                var inputOne = split.PlayerOne.GetComponent<RallyLocalPlayerInput>();
                var inputTwo = split.PlayerTwo.GetComponent<RallyLocalPlayerInput>();
                File.AppendAllText(Report, $"INPUT: J1 enabled={inputOne.enabled} v={inputOne.Vertical} h={inputOne.Horizontal}; J2 enabled={inputTwo.enabled} v={inputTwo.Vertical} h={inputTwo.Horizontal}; pad trigger={pad.rightTrigger.ReadValue()} stick={pad.leftStick.x.ReadValue()}; keyboard={keyboard.wKey.isPressed}\n");
                Require(inputOne.Vertical == 1f && inputOne.Horizontal > .9f && inputTwo.Vertical > .5f && inputTwo.Horizontal < -.3f,
                    "Keyboard and DualSense steer/throttle independently");
                Require(split.PlayerOne.frontLeftWheel.steerAngle > 30f && split.PlayerTwo.frontLeftWheel.steerAngle < -10f,
                    "Independent inputs reach the existing wheel physics");
                Require(split.PlayerOne.frontLeftWheel.motorTorque > 0f && split.PlayerTwo.frontLeftWheel.motorTorque > 0f, "Both controllers produce motor torque");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                SendPad(buttons: 1 << 5);
                Next(3);
            }
            else if (phase == 3)
            {
                Require(split.PlayerTwo.rearLeftWheel.brakeTorque > 100f && split.PlayerOne.rearLeftWheel.brakeTorque < .1f,
                    "DualSense handbrake affects only player two");
                ScreenCapture.CaptureScreenshot("Logs/split-screen-gameplay-" + SceneManager.GetActiveScene().name + ".png");
                SendPad(brake: .55f);
                Next(4);
            }
            else if (phase == 4)
            {
                Require(Mathf.Abs(split.PlayerTwo.GetComponent<RallyLocalPlayerInput>().Brake - .55f) < .03f &&
                    split.PlayerOne.GetComponent<RallyLocalPlayerInput>().Brake == 0f, "Analog service brake has exclusive device ownership");
                SendPad();
                one.linearVelocity = two.linearVelocity = Vector3.zero;
                one.angularVelocity = two.angularVelocity = Vector3.zero;
                split.TimerOne.ResetTimer();
                split.TimerTwo.ResetTimer();
                // Steering/throttle testing may already cross the start gate.
                // Return both cars behind it before testing reset ownership.
                one.transform.position = resetOne;
                one.position = resetOne;
                two.transform.position = resetTwo;
                two.position = resetTwo;
                Physics.SyncTransforms();
                split.TimerTwo.BindVehicle(two);
                two.transform.position = resetTwo + Vector3.up * 2f;
                two.position = resetTwo + Vector3.up * 2f;
                Physics.SyncTransforms();
                SendPad(buttons: 1 << 7);
                Next(5);
            }
            else if (phase == 5)
            {
                File.AppendAllText(Report, $"RESET: actual={two.position} expected={resetTwo} input={split.PlayerTwo.GetComponent<RallyLocalPlayerInput>().ResetPressed} next={split.TimerOne.NextCheckpoint}/{split.TimerTwo.NextCheckpoint}\n");
                Require(Vector3.Distance(two.position, resetTwo) < 1f && split.TimerOne.NextCheckpoint == 0 && split.TimerTwo.NextCheckpoint == 0,
                    "DualSense reset returns only player two to its own grid pose");
                SendPad();
                Quaternion inverted = two.rotation * Quaternion.Euler(0f, 0f, 180f);
                two.transform.rotation = inverted;
                two.rotation = inverted;
                Physics.SyncTransforms();
                SendPad(buttons: 1 << 4);
                Next(6);
            }
            else if (phase == 6)
            {
                Require(Vector3.Dot(two.transform.up, Vector3.up) > .9f, "DualSense recovery rights only player two");
                SendPad();
                split.TimerOne.OrderedCheckpoints[0].SendMessage("OnTriggerEnter", colliderTwo);
                Require(split.TimerTwo.NextCheckpoint == 1 && split.TimerOne.NextCheckpoint == 0, "Shared gate dispatches to the correct player's timer");
                split.TimerOne.OrderedCheckpoints[0].SendMessage("OnTriggerEnter", colliderOne);
                Next(7);
            }
            else if (phase == 7)
            {
                Require(split.TimerOne.ElapsedTime > 0f && split.TimerTwo.ElapsedTime > 0f, "Both lap clocks tick independently");
                pauseTimeOne = split.TimerOne.ElapsedTime;
                pauseTimeTwo = split.TimerTwo.ElapsedTime;
                UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().SendMessage("Open");
                Next(8);
            }
            else if (phase == 8)
            {
                Require(Time.timeScale == 0f && split.TimerOne.ElapsedTime == pauseTimeOne && split.TimerTwo.ElapsedTime == pauseTimeTwo,
                    "Shared pause freezes both clocks");
                UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().SendMessage("Close", true);
                for (int i = 1; i < split.TimerOne.CheckpointCount; i++)
                    split.TimerOne.OrderedCheckpoints[i].SendMessage("OnTriggerEnter", colliderOne);
                Next(9);
            }
            else if (phase == 9)
            {
                Require(one.isKinematic && !two.isKinematic && Time.timeScale == 1f && !split.AllFinished,
                    "First finisher stops without ending the second player's race");
                Require(Bots().Where((b, i) => Vector3.Distance(b.GetComponentInChildren<Rigidbody>().position, botStart[i]) > 2f).Any(), "Existing bots keep moving in the shared race");
                ScreenCapture.CaptureScreenshot("Logs/split-screen-" + SceneManager.GetActiveScene().name + ".png");
                for (int i = 1; i < split.TimerTwo.CheckpointCount; i++)
                    split.TimerTwo.OrderedCheckpoints[i].SendMessage("OnTriggerEnter", colliderTwo);
                Next(10);
            }
            else if (phase == 10)
            {
                Require(Time.timeScale == 0f && two.isKinematic && GameObject.Find("Race Finish Canvas") != null, "Both finishers display the paused results menu");
                RemoveDevices();
                circuit++;
                if (circuit < 3)
                {
                    RallyGameSession.SelectVehicle(circuit);
                    RallyGameSession.SelectCircuit(circuit);
                    SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);
                    Next(1);
                }
                else
                {
                    RallyGameSession.SelectLocalPlayers(1);
                    SceneManager.LoadScene("Circuit_01");
                    Next(11);
                }
            }
            else if (phase == 11)
            {
                Require(RallySplitScreen.Active == null && UnityEngine.Object.FindObjectsByType<RallyRaceHud>().Length == 1 &&
                    UnityEngine.Object.FindAnyObjectByType<RallyRacePositions>().RacerCount == 4 && Camera.main.rect == new Rect(0f, 0f, 1f, 1f),
                    "Single-player still uses one full-screen camera/HUD and three bots");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }

    static void ValidateStructure(RallySplitScreen split)
    {
        Require(split.CameraOne.rect == new Rect(0f, .5f, 1f, .5f) && split.CameraTwo.rect == new Rect(0f, 0f, 1f, .5f), "Complementary top/bottom camera viewports");
        Require(split.CameraOne.GetComponent<JrsFollowCamera>().target == split.PlayerOne.transform &&
            split.CameraTwo.GetComponent<JrsFollowCamera>().target == split.PlayerTwo.transform, "Each camera follows its own car");
        Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a => a.isActiveAndEnabled) == 1, "Exactly one audio listener");
        Require(UnityEngine.Object.FindAnyObjectByType<RallyRacePositions>().RacerCount == 5, "Ranking includes both players plus three bots");
        var cars = new[] { split.PlayerOne, split.PlayerTwo }.Concat(Bots().Select(b => b.GetComponentInChildren<JrsVehicleController>())).ToArray();
        string roadName = SceneManager.GetActiveScene().name == "Circuit_01" ? "Rally_Road_Start_to_Finish" : SceneManager.GetActiveScene().name.Replace("_", "") + "_Road";
        MeshCollider road = GameObject.Find(roadName).GetComponent<MeshCollider>();
        for (int i = 0; i < cars.Length; i++)
        {
            Require(cars[i].frontLeftWheel.transform.IsChildOf(cars[i].transform), "Car owns its wheel rig: " + cars[i].name);
            foreach (WheelCollider wheel in new[] { cars[i].frontLeftWheel, cars[i].frontRightWheel, cars[i].rearLeftWheel, cars[i].rearRightWheel })
                Require(road.Raycast(new Ray(wheel.transform.TransformPoint(wheel.center) + Vector3.up * 20f, Vector3.down), out _, 50f),
                    "Grid wheel supported by the road: " + cars[i].name + "/" + wheel.name);
            BoxCollider box = BodyBox(cars[i]);
            Collider[] overlaps = Physics.OverlapBox(box.transform.TransformPoint(box.center), Vector3.Scale(box.size * .5f, box.transform.lossyScale),
                box.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            Require(overlaps.All(c => c == road || c is TerrainCollider || c.attachedRigidbody == cars[i].GetComponent<Rigidbody>() ||
                c.GetComponentInParent<JrsVehicleController>() != null), "Grid body clear of circuit obstacles: " + cars[i].name);
            for (int j = 0; j < i; j++)
            {
                var a = BodyBox(cars[i]); var b = BodyBox(cars[j]);
                File.AppendAllText(Report, $"GRID: {cars[i].name} pos={cars[i].transform.position} rb={cars[i].GetComponent<Rigidbody>().position} size={a.size} / {cars[j].name} pos={cars[j].transform.position} size={b.size}\n");
                Require(!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out _),
                    "Grid vehicles do not overlap: " + cars[i].name + " / " + cars[j].name);
            }
        }
        var aBody = split.PlayerOne.GetComponent<Rigidbody>(); var bBody = split.PlayerTwo.GetComponent<Rigidbody>();
        Require(aBody.mass == bBody.mass && aBody.angularDamping == bBody.angularDamping &&
            split.PlayerOne.motorForce == split.PlayerTwo.motorForce && split.PlayerOne.maxSteerAngle == split.PlayerTwo.maxSteerAngle &&
            split.PlayerOne.rearLeftWheel.sidewaysFriction.Equals(split.PlayerTwo.rearLeftWheel.sidewaysFriction), "Cloned player preserves tuned physics");
        var huds = UnityEngine.Object.FindObjectsByType<RallyRaceHud>();
        Require(huds.Length == 2 && huds.All(h => h.transform.Find("Player Viewport") != null), "Two HUDs confined to their viewports");
        foreach (var hud in huds)
        {
            var serialized = new SerializedObject(hud);
            var manager = (RallyCheckpointManager)serialized.FindProperty("checkpointManager").objectReferenceValue;
            Require(serialized.FindProperty("vehicleBody").objectReferenceValue == manager.VehicleBody, "HUD speed and clock belong to the same player");
        }
        File.AppendAllText(Report, "STRUCTURE PASS: " + SceneManager.GetActiveScene().name + "\n");
    }

    static RallyBotController[] Bots() => UnityEngine.Object.FindObjectsByType<RallyBotController>().OrderBy(b => b.name).ToArray();
    static void SendPad(float throttle = 0f, float brake = 0f, float steer = 0f, byte buttons = 0)
    {
        InputSystem.QueueStateEvent(pad, new DualSenseHIDInputReport
        {
            leftStickX = (byte)Mathf.RoundToInt((steer + 1f) * 127.5f), leftStickY = 128,
            rightStickX = 128, rightStickY = 128,
            rightTrigger = (byte)Mathf.RoundToInt(throttle * 255f),
            leftTrigger = (byte)Mathf.RoundToInt(brake * 255f), buttons0 = (byte)(8 | buttons)
        });
    }
    static BoxCollider BodyBox(JrsVehicleController car) => car.GetComponentsInChildren<BoxCollider>().First(b => b.enabled && !b.isTrigger && b.name.Contains("BodyCollider"));
    static void Next(int next) { phase = next; phaseAt = EditorApplication.timeSinceStartup; minimumFrame = Time.frameCount + 3; }
    static void Require(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    static void RemoveDevices()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        keyboard = null; pad = null;
    }
    static void RestoreChoices()
    {
        Application.runInBackground = SessionState.GetBool("Rally.SplitTest.Background", false);
        InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt("Rally.SplitTest.InputFocus", 0);
        InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt("Rally.SplitTest.InputBackground", 0);
        foreach (string key in TimeKeys)
        {
            if (!SessionState.GetBool("Rally.SplitTest.Has." + key, false)) PlayerPrefs.DeleteKey(key);
            else if (key.EndsWith(".Count")) PlayerPrefs.SetInt(key, SessionState.GetInt("Rally.SplitTest.Value." + key, 0));
            else PlayerPrefs.SetFloat(key, SessionState.GetFloat("Rally.SplitTest.Value." + key, 0f));
        }
        RallyGameSession.SelectLocalPlayers(SessionState.GetInt("Rally.SplitTest.Players", 1));
        RallyGameSession.SelectVehicle(Mathf.Max(0, Array.IndexOf(RallyPlayerVehicleSelection.Names, SessionState.GetString("Rally.SplitTest.Vehicle", ""))));
        RallyGameSession.SelectCircuit(Mathf.Max(0, Array.IndexOf(RallyGameSession.CircuitNames, SessionState.GetString("Rally.SplitTest.Circuit", ""))));
    }
    static void Finish(Exception error)
    {
        RemoveDevices();
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool("Rally.SplitTest.Success", error == null);
        SessionState.SetBool("Rally.SplitTest.Restore", true);
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }
}
