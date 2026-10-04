using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real menu regression, with a virtual gamepad and screenshot evidence.</summary>
[InitializeOnLoad]
public static class RallyMenuArtVerification
{
    const string Request = "Temp/rally-menu-verify.request", Report = "Logs/menu-ui-test.txt", Key = "Rally.MenuArtTest.";
    static int phase, track;
    static double since, deadline, idle;
    static Vector3 cameraPosition;
    static float motionTime;
    static Gamepad pad;
    static RallyMenuArtVerification() => EditorApplication.update += Tick;

    [MenuItem("Tools/Rally/Verify Menu Art and Car Selection")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
        SessionState.SetString(Key + "Scene", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + "Start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        RallyGameSession.RestoreSavedState();
        SessionState.SetInt(Key + "Vehicle", RallyGameSession.SelectedVehicleIndex);
        SessionState.SetInt(Key + "Circuit", Mathf.Max(0, Array.IndexOf(RallyGameSession.CircuitNames, RallyGameSession.SelectedCircuit)));
        SessionState.SetBool(Key + "Active", true);
        File.WriteAllText(Report, "Menu art / car selection Play test\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        phase = track = 0; since = deadline = 0;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
        {
            if (SessionState.GetBool(Key + "Restore", false))
            {
                SessionState.SetBool(Key + "Restore", false);
                RallyGameSession.SelectVehicle(SessionState.GetInt(Key + "Vehicle", 0));
                RallyGameSession.SelectCircuit(SessionState.GetInt(Key + "Circuit", 0));
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Start", ""));
                string scene = SessionState.GetString(Key + "Scene", "");
                if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + "Success", false) ? 0 : 1);
            }
            if (!File.Exists(Request) || File.Exists("Temp/rally-menu-art.request") || SessionState.GetBool("Rally.SplitTest.Active", false) || SessionState.GetBool("Rally.RenderBenchmark.Running", false)) { idle = 0; return; }
            if (idle == 0) { idle = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - idle < 3) return;
            File.Move(Request, Request + ".consumed-" + DateTime.UtcNow.Ticks); idle = 0; Run(); return;
        }
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key + "Active", false)) return;
        double now = EditorApplication.timeSinceStartup;
        if (deadline == 0) { deadline = now + 150; since = now; }
        if (now > deadline) { Finish(new Exception("Menu verification timed out at " + phase)); return; }
        try
        {
            if (now - since < 2) return;
            if (phase == 0)
            {
                if (SceneManager.GetActiveScene().name != "MainMenu" || Object.FindAnyObjectByType<RallyMenuBackdropScene>() == null) return;
                Object.FindAnyObjectByType<RallyMenuController>().SendMessage("RevealMainMenu");
                cameraPosition = Camera.main.transform.position;
                motionTime = Time.unscaledTime;
                ScreenCapture.CaptureScreenshot("Logs/Menu_Main.png");
                phase = 1; since = now;
            }
            else if (phase == 1)
            {
                // Editor ticks can continue while the Game view is not rendering.
                // Measure actual game frames/time, not only wall-clock editor time.
                if (Time.unscaledTime - motionTime < 2f) return;
                Require(Vector3.Distance(cameraPosition, Camera.main.transform.position) > .1f, "Real camera is animated");
                GameObject.Find("Play Button").GetComponent<Button>().onClick.Invoke(); phase = 2; since = now;
            }
            else if (phase == 2)
            {
                if (SceneManager.GetActiveScene().name != "VehicleCircuitSelection") return;
                for (int i = 0; i < 3; i++)
                {
                    Button button = GameObject.Find("Vehicle " + i + " Button").GetComponent<Button>();
                    Require(button.GetComponentInChildren<TextMeshProUGUI>().text == RallyGameSession.VehicleDisplayName(i), "Separate correct name: " + RallyGameSession.VehicleDisplayName(i));
                    button.onClick.Invoke();
                    Require(GameObject.Find("Selected Vehicle").GetComponent<TextMeshProUGUI>().text == RallyGameSession.VehicleDisplayName(i), "Selected name updates");
                    Require(GameObject.Find("Vehicle Counter").GetComponent<TextMeshProUGUI>().text == (i + 1) + "/3", "Index is independent of name");
                }
                Require(GameObject.Find("Selected Vehicle").GetComponent<TextMeshProUGUI>().font.name.Contains("Oswald"), "Non-default heading font");
                RectTransform carCard = GameObject.Find("Vehicle Card").GetComponent<RectTransform>();
                RectTransform difficulty = GameObject.Find("Bot Difficulty Card").GetComponent<RectTransform>();
                Require(difficulty.anchoredPosition.y + difficulty.sizeDelta.y / 2f < carCard.anchoredPosition.y - carCard.sizeDelta.y / 2f, "Difficulty panel does not overlap selection panels");
                Object.FindAnyObjectByType<RallyMenuNavigation>().ResetSelection();
                pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
                phase = 3; since = now;
            }
            else if (phase == 3)
            {
                Require(RallyGameSession.SelectedVehicleIndex == 1, "D-Pad navigation updates displayed and saved car");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                GameObject.Find("Circuit 1 Button").GetComponent<Button>().onClick.Invoke(); phase = 4; since = now;
            }
            else if (phase == 4)
            {
                RallyMenuBackdropScene environment = Object.FindAnyObjectByType<RallyMenuBackdropScene>();
                if (environment == null || !environment.name.StartsWith(RallyGameSession.CircuitScenes[track])) return;
                Require(environment.GetComponentsInChildren<Rigidbody>(true).Length == 0 && environment.GetComponentsInChildren<Collider>(true).Length == 0, "Background has no gameplay physics");
                RawImage preview = GameObject.Find("Selected Track Preview").GetComponent<RawImage>();
                Require(preview.texture != null && preview.texture.name == RallyGameSession.CircuitScenes[track], "Real thumbnail for " + RallyGameSession.CircuitScenes[track]);
                Require(environment.GetComponentsInChildren<MeshRenderer>().Length > 0, "Real circuit geometry loaded");
                ScreenCapture.CaptureScreenshot("Logs/Menu_Selection_" + RallyGameSession.CircuitScenes[track] + ".png");
                phase = 5; since = now; // Let the screenshot render before changing its subject.
            }
            else if (phase == 5)
            {
                track++;
                if (track == 3) { phase = 6; since = now; }
                else { GameObject.Find("Circuit " + (track + 1) + " Button").GetComponent<Button>().onClick.Invoke(); phase = 4; since = now; }
            }
            else Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Require(bool success, string message)
    {
        if (!success) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    static void Finish(Exception error)
    {
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        pad = null;
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL\n" + error);
        SessionState.SetBool(Key + "Active", false);
        SessionState.SetBool(Key + "Success", error == null);
        SessionState.SetBool(Key + "Restore", true);
        EditorApplication.isPlaying = false;
    }
}
