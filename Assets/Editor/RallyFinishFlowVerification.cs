using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RallyFinishFlowVerification
{
    static RallyFinishFlowVerification() { EditorApplication.update += Poll; }

    static void Poll()
    {
        const string request = "Logs/finish-flow-verify-request.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Move(request, request + ".consumed-" + DateTime.Now.Ticks);
        try { Verify(); }
        catch (Exception e) { Time.timeScale = 1f; File.WriteAllText("Logs/finish-flow-verify-error.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Rally/Verify Finish Menu")]
    public static void Verify()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity");
        RallyRaceFlow flow = Object.FindAnyObjectByType<RallyRaceFlow>();
        RallyCheckpointManager manager = Object.FindAnyObjectByType<RallyCheckpointManager>();
        JrsVehicleController controller = Object.FindAnyObjectByType<JrsVehicleController>();
        RallyVehicleDynamics dynamics = Object.FindAnyObjectByType<RallyVehicleDynamics>();
        Rigidbody body = controller.GetComponent<Rigidbody>();
        if (flow == null || manager == null || controller == null || dynamics == null || body == null) throw new Exception("Required race components missing.");

        bool controllerEnabled = controller.enabled, dynamicsEnabled = dynamics.enabled, wasKinematic = body.isKinematic;
        bool hadEventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null;
        float oldScale = Time.timeScale;
        string prefix = "Rally.BestTimes.Circuit_01";
        string[] keys = Enumerable.Range(0, 5).Select(i => prefix + "." + i).Concat(new[] { prefix + ".Count", prefix + ".Best", "Rally.LastRaceTime", "Rally.SelectedVehicle", "Rally.SelectedCircuit" }).ToArray();
        var existed = keys.ToDictionary(k => k, PlayerPrefs.HasKey);
        var floats = keys.ToDictionary(k => k, k => PlayerPrefs.GetFloat(k, float.NaN));
        var strings = keys.ToDictionary(k => k, k => PlayerPrefs.GetString(k, null));
        var ints = keys.ToDictionary(k => k, k => PlayerPrefs.GetInt(k, int.MinValue));
        try
        {
            Invoke(flow, "Awake");
            Invoke(flow, "FinishRace", 83.456f);
            GameObject canvas = GameObject.Find("Race Finish Canvas");
            if (canvas == null || !canvas.activeInHierarchy) throw new Exception("Finish Canvas was not created.");
            if (Time.timeScale != 0f || controller.enabled || dynamics.enabled || !body.isKinematic || body.linearVelocity != Vector3.zero || body.angularVelocity != Vector3.zero) throw new Exception("Vehicle/pause state invalid after finish.");
            string[] buttonNames = { "Restart Button", "Times Button", "Menu Button" };
            foreach (string name in buttonNames) if (canvas.GetComponentsInChildren<Button>(true).All(b => b.name != name)) throw new Exception("Missing button: " + name);
            TMP_Text final = canvas.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Final Time");
            string formatted = RallyGameSession.FormatTime(83.456f);
            if (final == null || final.text != formatted) throw new Exception("Final time text invalid: " + (final == null ? "missing" : final.text));
            Invoke(flow, "ToggleTimes");
            Transform panel = canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Best Times Panel");
            if (panel == null || !panel.gameObject.activeSelf || !panel.GetComponentInChildren<TMP_Text>().text.Contains(formatted)) throw new Exception("Best-times panel did not display saved result.");
            for (int i = 0; i < 8; i++) InvokeStatic("SaveBestTime", 120f - i * 7f);
            int count = PlayerPrefs.GetInt(prefix + ".Count", 0);
            if (count != 5 || Mathf.Abs(PlayerPrefs.GetFloat(prefix + ".0") - 71f) > .001f || Mathf.Abs(PlayerPrefs.GetFloat(prefix + ".4") - 92f) > .001f) throw new Exception("Top-five ranking was not sorted/limited correctly.");
            InvokeStatic("ResumeTime");
            if (Time.timeScale != 1f) throw new Exception("ResumeTime did not unpause.");
            File.WriteAllText("Logs/finish-flow-verification.txt", "PASS: finish created paused Canvas; Porsche control and dynamics disabled; Rigidbody frozen; final time formatted; Reiniciar/Ver tiempos/Volver al menú buttons present; times panel toggled; PlayerPrefs ranking sorted and limited to five; resume restores Time.timeScale. Scene remained unmodified.\n" + DateTime.Now.ToString("O"));
            if (File.Exists("Logs/finish-flow-verify-error.txt")) File.Delete("Logs/finish-flow-verify-error.txt");
        }
        finally
        {
            GameObject temporaryCanvas = GameObject.Find("Race Finish Canvas");
            if (temporaryCanvas != null) Object.DestroyImmediate(temporaryCanvas);
            if (!hadEventSystem)
            {
                var temporaryEventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
                if (temporaryEventSystem != null) Object.DestroyImmediate(temporaryEventSystem.gameObject);
            }
            Time.timeScale = oldScale; controller.enabled = controllerEnabled; dynamics.enabled = dynamicsEnabled; body.isKinematic = wasKinematic;
            foreach (string key in keys)
            {
                if (!existed[key]) { PlayerPrefs.DeleteKey(key); continue; }
                if (strings[key] != null && (key.EndsWith("Vehicle") || key.EndsWith("Circuit"))) PlayerPrefs.SetString(key, strings[key]);
                else if (key.EndsWith("Count")) PlayerPrefs.SetInt(key, ints[key]);
                else PlayerPrefs.SetFloat(key, floats[key]);
            }
            PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
            if (scene.isDirty) EditorSceneManager.OpenScene(scene.path);
        }
    }

    static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static object InvokeStatic(string method, params object[] args) => typeof(RallyRaceFlow).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
}
