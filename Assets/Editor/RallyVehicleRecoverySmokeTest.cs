using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Exercises real input layouts and recovery without saving/modifying scenes.</summary>
[InitializeOnLoad]
public static class RallyVehicleRecoverySmokeTest
{
    const string Key = "RallyRecoveryTest.";
    static RallyVehicleRecoverySmokeTest() => EditorApplication.update += Tick;

    [MenuItem("Tools/Rally/Verify Vehicle Rollover Recovery")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save scene and leave Play before this recovery test.");
        SessionState.SetString(Key + "StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + "Active", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (SessionState.GetBool(Key + "Active", false) && EditorApplication.isPlaying)
        {
            SessionState.SetBool(Key + "Active", false);
            bool success = false;
            try { Validate(); success = true; }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                SessionState.SetBool(Key + "Success", success);
                SessionState.SetBool(Key + "Restore", true);
                EditorApplication.isPlaying = false;
            }
        }
        else if (SessionState.GetBool(Key + "Restore", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "StartScene", ""));
            SessionState.SetBool(Key + "Restore", false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + "Success", false) ? 0 : 1);
        }
    }

    static void Validate()
    {
        GameObject car = new GameObject("Temporary recovery test");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject gates = new GameObject("Temporary recovery checkpoints");
        Keyboard keyboard = null;
        Gamepad pad = null;
        float originalTimeScale = Time.timeScale;
        Directory.CreateDirectory("Logs");
        const string report = "Logs/vehicle-recovery-test.txt";
        File.WriteAllText(report, "Vehicle rollover recovery test\n");
        try
        {
            Time.timeScale = 1f;
            Vector3 position = new Vector3(-20000f, 500f, -20000f);
            car.transform.position = position;
            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 1450f;
            body.angularDamping = 3f;
            body.useGravity = false;
            var collider = car.AddComponent<BoxCollider>();
            var controller = car.AddComponent<JrsVehicleController>();
            var wheelObject = new GameObject("Recovery wheel");
            wheelObject.transform.SetParent(car.transform, false);
            wheelObject.transform.localPosition = new Vector3(0f, -.2f, 0f);
            controller.frontLeftWheel = wheelObject.AddComponent<WheelCollider>();
            controller.frontLeftWheel.radius = .3f;
            var recovery = car.AddComponent<RallyVehicleRecovery>();
            recovery.SendMessage("Awake");
            ground.transform.position = position + Vector3.down * .55f;
            ground.transform.localScale = new Vector3(10f, .1f, 10f);
            var manager = gates.AddComponent<RallyCheckpointManager>();
            var first = new GameObject("Start");
            first.transform.SetParent(gates.transform);
            var start = first.AddComponent<RallyCheckpointTrigger>();
            start.Configure(manager, 0);
            var last = new GameObject("Finish");
            last.transform.SetParent(gates.transform);
            var finish = last.AddComponent<RallyCheckpointTrigger>();
            finish.Configure(manager, 1);
            manager.Configure(new[] { start, finish });
            manager.TryPass(0, collider);
            float elapsed = manager.ElapsedTime;
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<DualSenseGamepadHID>();

            Quaternion yaw = Quaternion.Euler(0f, 37f, 0f);
            Vector3 velocity = new Vector3(2f, 0f, 3f);
            body.rotation = yaw * Quaternion.Euler(0f, 0f, 90f);
            body.angularVelocity = Vector3.up * 3f;
            body.linearVelocity = velocity;
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.T));
            InputSystem.Update();
            PollRecoveryInput(recovery);
            Check(Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > .999f, "T rights a car on its side");
            Check((body.rotation * Vector3.forward - yaw * Vector3.forward).sqrMagnitude < .001f, "Heading preserved");
            Check(body.position.x == position.x && body.position.z == position.z, "No horizontal teleport");
            Check(body.angularVelocity.sqrMagnitude < .001f && (body.linearVelocity - velocity).sqrMagnitude < .001f,
                "Angular velocity cleared; linear velocity preserved");
            Check(body.position.y >= position.y && body.position.y <= position.y + .04f, "Only minimal ground clearance lift");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            body.rotation = yaw * Quaternion.Euler(0f, 0f, 180f);
            body.angularVelocity = Vector3.one;
            Physics.SyncTransforms();
            using (StateEvent.From(pad, out InputEventPtr pressEvent))
            {
                pad.buttonWest.WriteValueIntoEvent(1f, pressEvent);
                InputState.Change(pad, pressEvent);
            }
            pad.MakeCurrent();
            Check(pad.buttonWest.isPressed, "DualSense layout delivers Square press");
            Check(pad.squareButton == pad.buttonWest && !RallyGamepadInput.HandbrakePressed &&
                !RallyGamepadInput.ResetPressedThisFrame, "DualSense Square independent from Cross/Triangle");
            PollRecoveryInput(recovery);
            Check(Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > .999f && body.angularVelocity.sqrMagnitude < .001f,
                "DualSense Square rights an upside-down car");
            Check(manager.NextCheckpoint == 1 && manager.IsRunning && !manager.IsFinished && manager.ElapsedTime == elapsed,
                "Checkpoint order, timer and lap progress unchanged");

            body.rotation = yaw * Quaternion.Euler(0f, 0f, 90f);
            Quaternion tilted = body.rotation;
            Time.timeScale = 0f;
            recovery.RightVehicle();
            Check(Quaternion.Angle(body.rotation, tilted) < .01f, "Recovery blocked while paused");
            Time.timeScale = 1f;
            controller.enabled = false;
            recovery.RightVehicle();
            Check(Quaternion.Angle(body.rotation, tilted) < .01f, "Recovery blocked after control is disabled");
            controller.enabled = true;
            car.AddComponent<RallyBotController>();
            recovery.RightVehicle();
            Check(Quaternion.Angle(body.rotation, tilted) < .01f, "Bots unaffected");
            Check(body.mass == 1450f && body.angularDamping == 3f, "Player mass and damping unchanged");
            File.AppendAllText(report, "COMPLETE: PASS\n");
            Debug.Log("Rally rollover recovery PASS. See " + report);

            void Check(bool condition, string label)
            {
                if (!condition) throw new Exception(label);
                File.AppendAllText(report, "PASS: " + label + "\n");
            }
        }
        catch (Exception error)
        {
            File.AppendAllText(report, "FAIL: " + error + "\n");
            throw;
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (pad != null) InputSystem.RemoveDevice(pad);
            UnityEngine.Object.DestroyImmediate(car);
            UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(gates);
        }
    }

    static void PollRecoveryInput(RallyVehicleRecovery recovery)
        => typeof(RallyVehicleRecovery).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(recovery, null);
}
