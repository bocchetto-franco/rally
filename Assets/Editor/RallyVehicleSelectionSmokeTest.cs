using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Batch Play-mode regression: actual menu buttons, all cars, all three tracks.</summary>
[InitializeOnLoad]
public static class RallyVehicleSelectionSmokeTest
{
    const string Key = "RallyVehicleSelectionTest.";
    static Gamepad pad;
    static double deadline;
    static float raceStart;
    static Vector3 initialPosition;
    static bool prepared;
    static RallyVehicleVisual activeVisual;

    static RallyVehicleSelectionSmokeTest() => EditorApplication.update += Tick;

    public static void Run()
    {
        RallyVehiclePhysicsParityTest.Run();
        SessionState.SetString(Key + "Vehicle", PlayerPrefs.GetString("Rally.SelectedVehicle", RallyGameSession.VehicleName));
        SessionState.SetString(Key + "Circuit", PlayerPrefs.GetString("Rally.SelectedCircuit", "Circuit 01"));
        SessionState.SetString(Key + "Start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetInt(Key + "Case", 0);
        SessionState.SetInt(Key + "Phase", 0);
        EditorSceneManager.OpenScene("Assets/Scenes/VehicleCircuitSelection.unity");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/VehicleCircuitSelection.unity");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key + "Active", false) || !EditorApplication.isPlaying) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 240;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Vehicle test timed out.")); return; }
        try
        {
            int test = SessionState.GetInt(Key + "Case", 0);
            int vehicleCount = RallyPlayerVehicleSelection.Names.Length;
            int vehicleIndex = test % vehicleCount, circuitIndex = test / vehicleCount;
            if (SessionState.GetInt(Key + "Phase", 0) == 0)
            {
                if (SceneManager.GetActiveScene().name != RallyGameSession.SelectionScene) return;
                var button = GameObject.Find("Vehicle " + vehicleIndex + " Button")?.GetComponent<Button>();
                if (button == null) return;
                button.onClick.Invoke();
                GameObject.Find("Circuit " + (circuitIndex + 1) + " Button").GetComponent<Button>().onClick.Invoke();
                if (RallyGameSession.SelectedVehicle != RallyPlayerVehicleSelection.Names[vehicleIndex])
                    throw new Exception("Vehicle button did not change selection.");
                SessionState.SetInt(Key + "Phase", 1);
                prepared = false;
                GameObject.Find("Race Button").GetComponent<Button>().onClick.Invoke();
                return;
            }
            if (SceneManager.GetActiveScene().name != RallyGameSession.CircuitScenes[circuitIndex]) return;
            var player = GameObject.Find(RallyGameSession.VehicleName).GetComponent<JrsVehicleController>();
            var rb = player.GetComponent<Rigidbody>();
            if (!prepared)
            {
                activeVisual = player.GetComponentInChildren<RallyVehicleVisual>();
                if ((activeVisual != null) != (vehicleIndex > 0)) throw new Exception("Wrong car instantiated.");
                if (activeVisual != null && activeVisual.name != RallyGameSession.SelectedVehicle + " Visual") throw new Exception("Wrong visual variant.");
                if (!player.enabled || !GameObject.Find("Porsche Rally Dynamics").GetComponent<RallyVehicleDynamics>().enabled)
                    throw new Exception("Player physics was disabled by model binding.");
                if (Mathf.Abs(rb.mass - 1450) > .01f || Mathf.Abs(rb.angularDamping - 3) > .01f)
                    throw new Exception("Arcade player tuning changed.");
                if (activeVisual != null)
                {
                    var meshRenderers = player.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                    if (meshRenderers.Any(r => !r.transform.IsChildOf(activeVisual.transform)))
                        throw new Exception("Old body/wheels still rendered.");
                    if (activeVisual.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit")))
                        throw new Exception("Mini material is not URP Lit.");
                }
                pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = .65f });
                initialPosition = rb.position;
                raceStart = Time.time;
                prepared = true;
                return;
            }
            if (Time.time - raceStart < 2.5f) return;
            if (Vector3.Distance(initialPosition, rb.position) < 1f || rb.linearVelocity.magnitude < .5f)
                throw new Exception("Selected car did not accelerate: " + RallyGameSession.SelectedVehicle);
            if (activeVisual != null)
            {
                var wheels = new[] { player.frontLeftWheel, player.frontRightWheel, player.rearLeftWheel, player.rearRightWheel };
                for (int i = 0; i < 4; i++)
                {
                    wheels[i].GetWorldPose(out Vector3 p, out Quaternion q);
                    if (Vector3.Distance(p, activeVisual.wheels[i].position) > .15f)
                        throw new Exception("Visual wheel is disconnected from suspension.");
                }
                if (!wheels.Any(w => w.isGrounded)) throw new Exception("Mini has no grounded wheels.");
            }
            Debug.Log("VEHICLE_PLAY_PASS " + SceneManager.GetActiveScene().name + " / " + RallyGameSession.SelectedVehicle +
                " travelled=" + Vector3.Distance(initialPosition, rb.position).ToString("F2"));
            InputSystem.RemoveDevice(pad);
            pad = null;
            if (test == vehicleCount * RallyGameSession.CircuitScenes.Length - 1) { Finish(null); return; }
            SessionState.SetInt(Key + "Case", test + 1);
            SessionState.SetInt(Key + "Phase", 0);
            SceneManager.LoadScene(RallyGameSession.SelectionScene);
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception error)
    {
        SessionState.SetBool(Key + "Active", false);
        if (pad != null) InputSystem.RemoveDevice(pad);
        PlayerPrefs.SetString("Rally.SelectedVehicle", SessionState.GetString(Key + "Vehicle", RallyGameSession.VehicleName));
        PlayerPrefs.SetString("Rally.SelectedCircuit", SessionState.GetString(Key + "Circuit", "Circuit 01"));
        PlayerPrefs.Save();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Start", "Assets/Scenes/MainMenu.unity"));
        if (error == null) Debug.Log("VEHICLE_SELECTION_ALL_PASS: " + RallyPlayerVehicleSelection.Names.Length + " cars, three tracks, menu, player physics and wheel animation.");
        else Debug.LogError("VEHICLE_SELECTION_FAIL " + error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
