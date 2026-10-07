using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Checks real trigger callbacks in the three tracks, then independent local HUDs.
[InitializeOnLoad]
public static class RallyPaceNotesPlayVerification
{
    const string Report = "Logs/pace-notes-play.txt";
    const string Key = "Rally.PaceNotesTest.";
    [Serializable] class SceneBackup { public SceneSetup[] scenes; }
    static RallyPaceNotesPlayVerification()
    {
        test = SessionState.GetInt(Key + "Case", 0);
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Restore;
    }
    static SceneSetup[] setup;
    static SceneAsset startScene;
    static bool background;
    static string[] values;
    static bool[] existed;
    static int players;
    static bool hadPlayers;
    static readonly string[] Keys = { "Rally.SelectedVehicle", "Rally.SelectedVehicleTwo", "Rally.SelectedCircuit" };
    static int test, phase, noteIndex, minimumFrame;
    static double deadline;
    static RallyBrakeWarningSystem[] systems;
    static Rigidbody[] bodies;
    static RallyBrakeWarningTrigger[][] notes;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before testing.");
        setup = EditorSceneManager.GetSceneManagerSetup();
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new Exception("Do not discard an unsaved scene to run this test.");
        SessionState.SetString(Key + "Setup", JsonUtility.ToJson(new SceneBackup { scenes = setup }));
        values = Keys.Select(k => PlayerPrefs.GetString(k)).ToArray();
        existed = Keys.Select(PlayerPrefs.HasKey).ToArray();
        hadPlayers = PlayerPrefs.HasKey("Rally.LocalPlayers"); players = PlayerPrefs.GetInt("Rally.LocalPlayers", 1);
        background = Application.runInBackground; Application.runInBackground = true;
        startScene = EditorSceneManager.playModeStartScene;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/VehicleCircuitSelection.unity");
        test = phase = noteIndex = 0; deadline = EditorApplication.timeSinceStartup + 600;
        SessionState.SetBool(Key + "Background", background);
        SessionState.SetString(Key + "Start", AssetDatabase.GetAssetPath(startScene));
        SessionState.SetBool(Key + "PlayersExists", hadPlayers); SessionState.SetInt(Key + "Players", players);
        for (int i = 0; i < Keys.Length; i++) { SessionState.SetBool(Key + Keys[i] + "Exists", existed[i]); SessionState.SetString(Key + Keys[i], values[i]); }
        SessionState.SetBool(Key + "Active", true); SessionState.SetBool(Key + "Restore", false); SessionState.SetInt(Key + "Case", 0);
        File.WriteAllText(Report, "RUNNING: all real note triggers in three circuits, then split-screen isolation\n");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key + "Active", false) || phase == 99 || !EditorApplication.isPlaying) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 600;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play check timeout.");
            if (phase == 0)
            {
                Application.runInBackground = true;
                foreach (var audio in UnityEngine.Object.FindObjectsByType<RallyAudioSystem>()) audio.enabled = false;
                RallyGameSession.SelectLocalPlayers(test >= 3 ? 2 : 1);
                RallyGameSession.SelectVehicleForPlayer(0, 0); RallyGameSession.SelectVehicleForPlayer(1, 0);
                SceneManager.LoadScene(RallyGameSession.CircuitScenes[test % 3]);
                Wait(1); return;
            }
            if (Time.frameCount < minimumFrame) return;
            if (phase == 1)
            {
                if (test >= 3 && (RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready)) return;
                Time.timeScale = 1f;
                foreach (var audio in UnityEngine.Object.FindObjectsByType<RallyAudioSystem>()) audio.enabled = false;
                foreach (var gate in UnityEngine.Object.FindObjectsByType<RallyCheckpointTrigger>()) gate.enabled = false;
                foreach (var flow in UnityEngine.Object.FindObjectsByType<RallyRaceFlow>()) flow.enabled = false;
                foreach (var bot in UnityEngine.Object.FindObjectsByType<RallyBotController>()) bot.gameObject.SetActive(false);
                var all = UnityEngine.Object.FindObjectsByType<RallyBrakeWarningSystem>();
                var bodyOne = GameObject.Find(RallyGameSession.VehicleName).GetComponent<Rigidbody>();
                systems = test < 3 ? new[] { all.Single(w => w.VehicleBody == bodyOne) } :
                    new[] { all.Single(w => w.VehicleBody == RallySplitScreen.Active.PlayerOne.GetComponent<Rigidbody>()),
                        all.Single(w => w.VehicleBody == RallySplitScreen.Active.PlayerTwo.GetComponent<Rigidbody>()) };
                bodies = systems.Select(s => s.VehicleBody).ToArray();
                foreach (var body in bodies)
                {
                    body.GetComponent<JrsVehicleController>().enabled = false;
                    foreach (var wheel in body.GetComponentsInChildren<WheelCollider>()) wheel.enabled = false;
                    body.useGravity = false; body.isKinematic = false;
                    body.constraints = RigidbodyConstraints.FreezeAll;
                }
                foreach (var dynamics in UnityEngine.Object.FindObjectsByType<RallyVehicleDynamics>()) dynamics.enabled = false;
                foreach (var recovery in UnityEngine.Object.FindObjectsByType<RallyVehicleRecovery>()) recovery.enabled = false;
                var representatives = all.SelectMany(w => w.GetComponentsInChildren<RallyBrakeWarningTrigger>())
                    .GroupBy(t => t.transform.parent).Select(g => g.First()).OrderBy(t => t.transform.parent.name).ToArray();
                if (test < 3) notes = new[] { representatives };
                else notes = new[] { new[] { representatives.First(t => t.Direction == RallyBrakeWarningTrigger.TurnDirection.Left) },
                    new[] { representatives.First(t => t.Direction == RallyBrakeWarningTrigger.TurnDirection.Right) } };
                noteIndex = 0; Wait(2); return;
            }
            if (phase == 2)
            {
                for (int p = 0; p < bodies.Length; p++)
                {
                    Vector3 position = notes[p][noteIndex].CurveEntry + Vector3.up * .8f;
                    bodies[p].transform.position = bodies[p].position = position;
                    bodies[p].linearVelocity = Vector3.zero;
                    bodies[p].angularVelocity = Vector3.zero;
                }
                Physics.SyncTransforms(); Wait(3); return;
            }
            if (phase == 3)
            {
                for (int p = 0; p < systems.Length; p++)
                {
                    var expected = notes[p][noteIndex]; var actual = systems[p].ActiveNote;
                    Require(actual != null && actual.transform.parent == expected.transform.parent,
                        "Wrong/missing trigger: " + expected.transform.parent.name + " / actual=" + actual?.transform.parent.name);
                    var labels = systems[p].GetComponentsInChildren<TMP_Text>();
                    var number = labels.Single(t => t.name == "Corner Grade");
                    var direction = labels.Single(t => t.name == "Corner Direction");
                    Require(number.text == expected.CornerGrade.ToString() && direction.text.StartsWith(expected.Direction ==
                        RallyBrakeWarningTrigger.TurnDirection.Right ? "DERECHA" : "IZQUIERDA"), "Incorrect arrow label/number.");
                    Require(direction.text.Contains("HORQUILLA") == expected.IsHairpin, "Incorrect hairpin label.");
                    Require(systems[p].NoteOpacity >= .77f, "Note hidden at low speed.");
                    // FreezeAll suppresses velocity on some PhysX versions. Allow
                    // motion only for this synchronous read; no simulation runs.
                    bodies[p].constraints = RigidbodyConstraints.None;
                    bodies[p].linearVelocity = Vector3.forward * ((expected.TargetSpeedKph + 40f) / 3.6f);
                    systems[p].SendMessage("Update");
                    Require(systems[p].NoteOpacity > .99f, "Note does not intensify above recommended speed: velocity=" + bodies[p].linearVelocity + ", alpha=" + systems[p].NoteOpacity);
                    bodies[p].linearVelocity = Vector3.zero;
                    bodies[p].constraints = RigidbodyConstraints.FreezeAll;
                    systems[p].SendMessage("Update");
                    Canvas.ForceUpdateCanvases();
                    var icon = systems[p].GetComponentInChildren<RallyPaceNoteArrow>();
                    var mesh = icon.canvasRenderer.GetMesh();
                    Require(mesh != null && mesh.vertexCount > 60, "Arrow did not generate UI geometry.");
                    Require(!labels.Any(t => t.text.Contains("BRAKE")), "Old BRAKE label remains.");
                    if (test >= 3) Require(icon.transform.IsChildOf(systems[p].GetComponentInChildren<Canvas>().transform.Find("Player Viewport")), "Note outside its local viewport.");
                    File.AppendAllText(Report, $"PASS {SceneManager.GetActiveScene().name} J{p + 1}: {expected.NoteLabel}{(expected.IsHairpin ? " HORQUILLA" : "")}, real entry, UI and speed intensity.\n");
                }
                if (test == 0 && notes[0][noteIndex].Direction == RallyBrakeWarningTrigger.TurnDirection.Right && notes[0][noteIndex].CornerGrade == 4)
                    ScreenCapture.CaptureScreenshot("Logs/PaceNote_Right4.png");
                if (test == 3) ScreenCapture.CaptureScreenshot("Logs/PaceNotes_SplitScreen.png");
                Wait(4); return;
            }
            if (phase == 4)
            {
                foreach (var body in bodies) { body.transform.position = body.position = new Vector3(0f, 300f, 0f); }
                Physics.SyncTransforms(); Wait(5); return;
            }
            if (phase == 5)
            {
                foreach (var system in systems) Require(system.ActiveNote == null && system.NoteOpacity == 0f, "Note did not clear after exit.");
                noteIndex++;
                if (noteIndex < notes[0].Length) { Wait(2); return; }
                File.AppendAllText(Report, "PASS all exits: " + SceneManager.GetActiveScene().name + (test >= 3 ? " split screen" : " solo") + "\n");
                test++; SessionState.SetInt(Key + "Case", test);
                if (test < 6) { Wait(0); return; }
                Finish(null);
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void Wait(int next) { phase = next; minimumFrame = Time.frameCount + 4; }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        phase = 99; SessionState.SetBool(Key + "Active", false); SessionState.SetBool(Key + "Restore", true);
        Time.timeScale = 1f; EditorApplication.isPlaying = false;
    }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || (!SessionState.GetBool(Key + "Active", false) && !SessionState.GetBool(Key + "Restore", false))) return;
        SessionState.SetBool(Key + "Active", false); SessionState.SetBool(Key + "Restore", false);
        Application.runInBackground = SessionState.GetBool(Key + "Background", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Start", ""));
        for (int i = 0; i < Keys.Length; i++)
        {
            if (SessionState.GetBool(Key + Keys[i] + "Exists", false)) PlayerPrefs.SetString(Keys[i], SessionState.GetString(Key + Keys[i], ""));
            else PlayerPrefs.DeleteKey(Keys[i]);
        }
        if (SessionState.GetBool(Key + "PlayersExists", false)) PlayerPrefs.SetInt("Rally.LocalPlayers", SessionState.GetInt(Key + "Players", 1)); else PlayerPrefs.DeleteKey("Rally.LocalPlayers");
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
        var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + "Setup", ""));
        if (backup?.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(backup.scenes);
        phase = test = noteIndex = 0; deadline = 0;
    }
}
