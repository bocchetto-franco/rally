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
    static DualSenseGamepadHID pad, padOne;
    static Gamepad[] disabledPads;
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
        SessionState.SetString("Rally.SplitTest.VehicleTwo", RallyGameSession.SelectedVehicleTwo);
        SessionState.SetBool("Rally.SplitTest.HasVehicleTwo", PlayerPrefs.HasKey("Rally.SelectedVehicleTwo"));
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
        RallyGameSession.SelectLocalPlayers(1);
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
        if (deadline == 0) { deadline = now + 480; phaseAt = now; }
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
                disabledPads = Gamepad.all.Where(p => p.enabled).ToArray();
                foreach (Gamepad existing in disabledPads) InputSystem.DisableDevice(existing);
                keyboard = InputSystem.AddDevice<Keyboard>("Split Test Keyboard");
                padOne = InputSystem.AddDevice<DualSenseGamepadHID>("Split Test DualSense J1");
                pad = InputSystem.AddDevice<DualSenseGamepadHID>("Split Test DualSense J2");
                ButtonNamed("Local Players 2").onClick.Invoke();
                Require(RallyGameSession.LocalPlayerCount == 2, "Selection button enables two-player mode");
                Require(RallyLocalDevices.GamepadFor(0) == padOne && RallyLocalDevices.GamepadFor(1) == pad,
                    "First and second gamepads are assigned automatically and exclusively");
                Require(Navigation(0) != Navigation(1) && !ButtonNamed("Race Button").interactable,
                    "Two independent selection panels wait for both players to be ready");
                Require(!UnityEngine.Object.FindObjectsByType<Button>().Any(b => b.name.StartsWith("Difficulty ")),
                    "Split-screen selection has no bot difficulty");
                SendPad(target: padOne, dpad: 4);
                SendPad(dpad: 4);
                Next(20);
            }
            else if (phase == 20)
            {
                Require(Navigation(0).SelectedIndex == 1 && Navigation(1).SelectedIndex == 1, "Both assigned D-Pads move their own focus");
                SendPad(target: padOne); SendPad(); Next(21);
            }
            else if (phase == 21)
            {
                SendPad(target: padOne, buttons: 1 << 5); SendPad(buttons: 1 << 5); Next(22);
            }
            else if (phase == 22)
            {
                Require(RallyGameSession.VehicleIndexForPlayer(0) == 1 && RallyGameSession.VehicleIndexForPlayer(1) == 1,
                    "Each Cross button confirms its own selected car");
                SendPad(target: padOne); SendPad(); Next(23);
            }
            else if (phase == 23) { SendPad(dpad: 4); Next(24); }
            else if (phase == 24)
            {
                Require(Navigation(0).SelectedIndex == 1 && Navigation(1).SelectedIndex == 2, "J2 navigation cannot move J1 focus");
                SendPad(); Next(25);
            }
            else if (phase == 25) { SendPad(buttons: 1 << 5); Next(26); }
            else if (phase == 26)
            {
                Require(RallyGameSession.VehicleIndexForPlayer(0) == 1 && RallyGameSession.VehicleIndexForPlayer(1) == 2,
                    "Different car selections persist independently");
                SendPad(target: padOne); SendPad();
                RallyGameSession.SelectCircuit(0);
                ButtonNamed("Player 1 Ready").onClick.Invoke();
                Require(!ButtonNamed("Race Button").interactable, "One ready player cannot launch the race");
                ButtonNamed("Player 2 Ready").onClick.Invoke();
                Require(ButtonNamed("Race Button").interactable, "Both ready players enable the shared race");
                ScreenCapture.CaptureScreenshot("Logs/dual-gamepad-selection.png");
                // ScreenCapture writes at the end of the frame: let the selection
                // render before loading the race, otherwise the capture shows the track.
                Next(27);
            }
            else if (phase == 27)
            {
                ButtonNamed("Race Button").onClick.Invoke();
                Next(1);
            }
            else if (phase == 1 && split != null && split.Ready)
            {
                ValidateStructure(split);
                // This suite drives controls near the grid then dispatches gates explicitly.
                // Disable automatic gate overlaps only in the disposable Play scene,
                // so physics during reset/recovery cannot advance either timer unexpectedly.
                foreach (var gate in split.TimerOne.OrderedCheckpoints)
                    gate.GetComponent<BoxCollider>().enabled = false;
                one = split.PlayerOne.GetComponent<Rigidbody>();
                two = split.PlayerTwo.GetComponent<Rigidbody>();
                colliderOne = BodyBox(split.PlayerOne);
                colliderTwo = BodyBox(split.PlayerTwo);
                resetOne = one.position;
                resetTwo = two.position;
                Require(split.PlayerOne.GetComponent<RallyLocalPlayerInput>().AssignedGamepad == padOne &&
                    split.PlayerTwo.GetComponent<RallyLocalPlayerInput>().AssignedGamepad == pad, "Menu device ownership persists into the race");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.A));
                SendPad(target: padOne, throttle: 1f, steer: 1f);
                SendPad(throttle: .65f, steer: -.4f);
                Next(2);
            }
            else if (phase == 2)
            {
                var inputOne = split.PlayerOne.GetComponent<RallyLocalPlayerInput>();
                var inputTwo = split.PlayerTwo.GetComponent<RallyLocalPlayerInput>();
                File.AppendAllText(Report, $"INPUT: J1 enabled={inputOne.enabled} v={inputOne.Vertical} h={inputOne.Horizontal}; J2 enabled={inputTwo.enabled} v={inputTwo.Vertical} h={inputTwo.Horizontal}; pad trigger={pad.rightTrigger.ReadValue()} stick={pad.leftStick.x.ReadValue()}; keyboard={keyboard.wKey.isPressed}\n");
                Require(inputOne.Vertical == 1f && inputOne.Horizontal > .9f && inputTwo.Vertical > .5f && inputTwo.Horizontal < -.3f,
                    "Both DualSense devices steer/throttle independently; unassigned keyboard cannot override");
                Require(split.PlayerOne.frontLeftWheel.steerAngle > 30f && split.PlayerTwo.frontLeftWheel.steerAngle < -10f,
                    "Independent inputs reach the existing wheel physics");
                Require(split.PlayerOne.frontLeftWheel.motorTorque > 0f && split.PlayerTwo.frontLeftWheel.motorTorque > 0f, "Both controllers produce motor torque");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                SendPad(target: padOne);
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
                // Recovery/physics can overlap the start gate before this isolated dispatch assertion.
                split.TimerOne.ResetTimer();
                split.TimerTwo.ResetTimer();
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
                SendPad(options: true);
                Next(8);
            }
            else if (phase == 8)
            {
                Require(Time.timeScale == 0f && UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().PausedByPlayer == 2,
                    "J2 Options pauses both players and identifies player two");
                pauseTimeOne = split.TimerOne.ElapsedTime; pauseTimeTwo = split.TimerTwo.ElapsedTime;
                SendPad(); SendPad(target: padOne, dpad: 4, options: true);
                Next(30);
            }
            else if (phase == 9)
            {
                Require(one.isKinematic && !two.isKinematic && Time.timeScale == 1f && !split.AllFinished,
                    "First finisher stops without ending the second player's race");
                Require(Bots().Length == 0 && UnityEngine.Object.FindAnyObjectByType<RallyRacePositions>().RacerCount == 2,
                    "Only the two local players compete throughout the race");
                ScreenCapture.CaptureScreenshot("Logs/split-screen-" + SceneManager.GetActiveScene().name + ".png");
                for (int i = 1; i < split.TimerTwo.CheckpointCount; i++)
                    split.TimerTwo.OrderedCheckpoints[i].SendMessage("OnTriggerEnter", colliderTwo);
                Next(10);
            }
            else if (phase == 10)
            {
                Require(Time.timeScale == 0f && two.isKinematic && GameObject.Find("Race Finish Canvas") != null, "Both finishers display the paused results menu");
                circuit++;
                if (circuit < 3)
                {
                    RallyGameSession.SelectVehicle(circuit);
                    RallyGameSession.SelectVehicleForPlayer(1, (circuit + 1) % 3);
                    RallyGameSession.SelectCircuit(circuit);
                    SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);
                    Next(1);
                }
                else
                {
                    InputSystem.RemoveDevice(pad); pad = null;
                    circuit = 0;
                    RallyGameSession.SelectCircuit(0);
                    SceneManager.LoadScene(RallyGameSession.SelectionScene);
                    Next(43);
                }
            }
            else if (phase == 30)
            {
                var pause = UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>();
                Require(pause.IsOpen && pause.PausedByPlayer == 2 &&
                    UnityEngine.Object.FindObjectsByType<RallyMenuNavigation>().First(n => n.isActiveAndEnabled).SelectedIndex == 0 &&
                    split.TimerOne.ElapsedTime == pauseTimeOne && split.TimerTwo.ElapsedTime == pauseTimeTwo,
                    "The other player's Options/D-Pad cannot steal pause ownership; both clocks remain frozen");
                SendPad(target: padOne); SendPad(dpad: 4); Next(31);
            }
            else if (phase == 31)
            {
                Require(UnityEngine.Object.FindObjectsByType<RallyMenuNavigation>().First(n => n.isActiveAndEnabled).SelectedIndex == 1,
                    "Pause owner D-Pad navigates the shared menu");
                SendPad(); Next(32);
            }
            else if (phase == 32) { SendPad(options: true); Next(33); }
            else if (phase == 33)
            {
                Require(Time.timeScale == 1f && !UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().IsOpen,
                    "Pause owner Options resumes both players");
                SendPad();
                // Also verify player one can open it and use Cross to resume.
                SendPad(target: padOne, options: true); Next(34);
            }
            else if (phase == 34)
            {
                Require(Time.timeScale == 0f && UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().PausedByPlayer == 1,
                    "J1 can independently open the pause menu");
                SendPad(target: padOne); Next(35);
            }
            else if (phase == 35) { SendPad(target: padOne, buttons: 1 << 5); Next(36); }
            else if (phase == 36)
            {
                Require(Time.timeScale == 1f && !UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().IsOpen,
                    "Owner Cross confirms resume in pause");
                SendPad(target: padOne);
                for (int i = 1; i < split.TimerOne.CheckpointCount; i++)
                    split.TimerOne.OrderedCheckpoints[i].SendMessage("OnTriggerEnter", colliderOne);
                Next(9);
            }
            else if (phase == 43)
            {
                Require(RallyLocalDevices.UsesKeyboard(1), "Selection automatically offers keyboard to J2 when only one controller remains");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow)); Next(44);
            }
            else if (phase == 44)
            {
                Require(Navigation(1).SelectedIndex == 1 && Navigation(0).SelectedIndex == 0,
                    "Keyboard fallback navigates only player two's car panel");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(45);
            }
            else if (phase == 45)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); Next(46);
            }
            else if (phase == 46)
            {
                Require(RallyGameSession.VehicleIndexForPlayer(1) == 1, "Keyboard Enter confirms J2's car");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                ButtonNamed("Player 1 Ready").onClick.Invoke(); ButtonNamed("Player 2 Ready").onClick.Invoke();
                ButtonNamed("Race Button").onClick.Invoke(); Next(40);
            }
            else if (phase == 40 && split != null && split.Ready)
            {
                Require(RallyLocalDevices.GamepadFor(0) == padOne && RallyLocalDevices.GamepadFor(1) == null &&
                    RallyLocalDevices.KeyboardPlayer == 1, "With one gamepad, J1 keeps the controller and J2 automatically gets keyboard");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftArrow));
                SendPad(target: padOne, throttle: .8f, steer: .5f);
                Next(41);
            }
            else if (phase == 41)
            {
                var inputOne = split.PlayerOne.GetComponent<RallyLocalPlayerInput>();
                var inputTwo = split.PlayerTwo.GetComponent<RallyLocalPlayerInput>();
                Require(inputOne.Vertical > .7f && inputOne.Horizontal > .4f && inputTwo.Vertical == 1f && inputTwo.Horizontal < -.9f,
                    "One-gamepad and keyboard fallback drive different cars");
                InputSystem.RemoveDevice(padOne);
                padOne = InputSystem.AddDevice<DualSenseGamepadHID>("Split Test Reconnected J1");
                Next(42);
            }
            else if (phase == 42)
            {
                Require(split.PlayerOne.GetComponent<RallyLocalPlayerInput>().AssignedGamepad == padOne &&
                    RallyLocalDevices.KeyboardPlayer == 1, "Replacement controller is paired without stealing the keyboard player");
                SendPad(target: padOne);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.RemoveDevice(padOne);
                padOne = null;
                SceneManager.LoadScene(RallyGameSession.SelectionScene); Next(50);
            }
            else if (phase == 50)
            {
                Require(RallyLocalDevices.UsesKeyboard(0) && RallyLocalDevices.UsesKeyboard(1), "Both players can use shared keyboard without controllers");
                ButtonNamed("Player 2 Circuit 2").onClick.Invoke();
                Require(RallyGameSession.SelectedRaceScene == "Circuit_02", "J2 can select the shared track");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.DownArrow)); Next(57);
            }
            else if (phase == 57)
            {
                Require(Navigation(0).SelectedIndex == 1 && Navigation(1).SelectedIndex == 1, "Shared keyboard moves both independent selection focuses");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(58);
            }
            else if (phase == 58)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.Enter)); Next(59);
            }
            else if (phase == 59)
            {
                Require(RallyGameSession.VehicleIndexForPlayer(0) == 1 && RallyGameSession.VehicleIndexForPlayer(1) == 1, "Space and Enter confirm their own car selections");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                ButtonNamed("Player 1 Ready").onClick.Invoke(); ButtonNamed("Player 2 Ready").onClick.Invoke();
                ButtonNamed("Race Button").onClick.Invoke(); Next(51);
            }
            else if (phase == 51 && split != null && split.Ready)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.Space)); Next(52);
            }
            else if (phase == 52)
            {
                var first = split.PlayerOne.GetComponent<RallyLocalPlayerInput>();
                var second = split.PlayerTwo.GetComponent<RallyLocalPlayerInput>();
                Require(first.Vertical == 1f && first.Horizontal < -.9f && first.Handbrake && second.Vertical == 0f && second.Horizontal == 0f && !second.Handbrake,
                    "WASD and Space affect J1 only");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.RightArrow, Key.Enter)); Next(53);
            }
            else if (phase == 53)
            {
                var first = split.PlayerOne.GetComponent<RallyLocalPlayerInput>();
                var second = split.PlayerTwo.GetComponent<RallyLocalPlayerInput>();
                Require(second.Vertical == 1f && second.Horizontal > .9f && second.Handbrake && first.Vertical == 0f && first.Horizontal == 0f && !first.Handbrake,
                    "Arrows and Enter affect J2 only");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P)); Next(54);
            }
            else if (phase == 54)
            {
                var pause = UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>();
                Require(pause.IsOpen && pause.PausedByPlayer == 2 && Time.timeScale == 0f, "P pauses as J2");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(55);
            }
            else if (phase == 55)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P)); Next(56);
            }
            else if (phase == 56)
            {
                Require(!UnityEngine.Object.FindAnyObjectByType<RallyPauseMenu>().IsOpen && Time.timeScale == 1f, "J2 resumes using P");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                RallyGameSession.SelectLocalPlayers(1);
                SceneManager.LoadScene("Circuit_01"); Next(11);
            }
            else if (phase == 11)
            {
                Require(RallySplitScreen.Active == null && UnityEngine.Object.FindObjectsByType<RallyRaceHud>().Length == 1 &&
                    UnityEngine.Object.FindAnyObjectByType<RallyRacePositions>().RacerCount == 4 && Camera.main.rect == new Rect(0f, 0f, 1f, 1f) &&
                    Bots().Length == 3 && Bots().All(b => b.isActiveAndEnabled && b.HasRoute),
                    "Single-player keeps one full-screen camera/HUD and three active bots: " + SceneManager.GetActiveScene().name);
                if (++circuit < 3)
                {
                    RallyGameSession.SelectCircuit(circuit);
                    SceneManager.LoadScene(RallyGameSession.SelectedRaceScene);
                    Next(11);
                }
                else Finish(null);
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
        Require(UnityEngine.Object.FindAnyObjectByType<RallyRacePositions>().RacerCount == 2, "Ranking includes only the two local players");
        Require(UnityEngine.Object.FindObjectsByType<RallyBotController>(FindObjectsInactive.Include).Length == 0,
            "No active or inactive bot instances remain in split screen");
        var cars = new[] { split.PlayerOne, split.PlayerTwo };
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
        Require(VisualMatches(split.PlayerOne, RallyGameSession.VehicleForPlayer(0)) &&
            VisualMatches(split.PlayerTwo, RallyGameSession.VehicleForPlayer(1)), "Both selected car visuals reach their own physics rig");
        File.AppendAllText(Report, "STRUCTURE PASS: " + SceneManager.GetActiveScene().name + "\n");
    }

    static RallyBotController[] Bots() => UnityEngine.Object.FindObjectsByType<RallyBotController>().OrderBy(b => b.name).ToArray();
    static void SendPad(float throttle = 0f, float brake = 0f, float steer = 0f, byte buttons = 0, byte dpad = 8, bool options = false, DualSenseGamepadHID target = null)
    {
        InputSystem.QueueStateEvent(target ?? pad, new DualSenseHIDInputReport
        {
            leftStickX = (byte)Mathf.RoundToInt((steer + 1f) * 127.5f), leftStickY = 128,
            rightStickX = 128, rightStickY = 128,
            rightTrigger = (byte)Mathf.RoundToInt(throttle * 255f),
            leftTrigger = (byte)Mathf.RoundToInt(brake * 255f), buttons0 = (byte)(dpad | buttons), buttons1 = options ? (byte)(1 << 5) : (byte)0
        });
    }
    static Button ButtonNamed(string name) => UnityEngine.Object.FindObjectsByType<Button>().First(b => b.name == name);
    static RallyMenuNavigation Navigation(int player) => UnityEngine.Object.FindObjectsByType<RallyMenuNavigation>().First(n => n.LocalPlayer == player);
    static bool VisualMatches(JrsVehicleController car, string expected)
    {
        var visual = car.GetComponentInChildren<RallyVehicleVisual>(true);
        return expected == RallyGameSession.VehicleName ? visual == null : visual != null && visual.name == expected + " Visual";
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
        if (padOne != null && padOne.added) InputSystem.RemoveDevice(padOne);
        foreach (Gamepad physical in disabledPads ?? Array.Empty<Gamepad>())
            if (physical.added) InputSystem.EnableDevice(physical);
        disabledPads = null;
        keyboard = null; pad = padOne = null;
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
        RallyGameSession.SelectVehicleForPlayer(1, Mathf.Max(0, Array.IndexOf(RallyPlayerVehicleSelection.Names, SessionState.GetString("Rally.SplitTest.VehicleTwo", ""))));
        if (!SessionState.GetBool("Rally.SplitTest.HasVehicleTwo", false)) PlayerPrefs.DeleteKey("Rally.SelectedVehicleTwo");
        PlayerPrefs.Save();
        RallyGameSession.RestoreSavedState();
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
