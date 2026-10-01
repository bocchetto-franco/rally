using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Play-mode integration: routing, real wheel slip/collision, pause and scene lifetime.</summary>
[InitializeOnLoad]
public static class RallyAudioSmokeTest
{
    const string Active = "RallyAudioTest.Active";
    const string Restore = "RallyAudioTest.Restore";
    const string StartScene = "RallyAudioTest.StartScene";
    const string Request = "Temp/rally-audio-test.request";
    const string Report = "Logs/audio-play-test.txt";
    static int phase;
    static double phaseAt;
    static double deadline;
    static RallyAudioSystem musicInstance;
    static bool sawSkid;
    static JrsVehicleController car;
    static Rigidbody body;
    static Vector3 forward;
    static Vector3 right;

    static RallyAudioSmokeTest() => EditorApplication.update += Tick;

    [MenuItem("Tools/Rally/Verify Audio In Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save scene and leave Play before the audio test.");
        RallyAudioSetup.Install();
        SessionState.SetString(StartScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Active, true);
        SessionState.SetBool(Restore, false);
        File.WriteAllText(Report, "Audio Play-mode integration test\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        phase = 0;
        phaseAt = deadline = 0;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (SessionState.GetBool(Restore, false))
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(StartScene, ""));
                SessionState.SetBool(Restore, false);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(SessionState.GetBool("RallyAudioTest.Success", false) ? 0 : 1);
                    return;
                }
            }
            if (File.Exists(Request))
            {
                File.Delete(Request);
                try { Run(); } catch (Exception e) { File.WriteAllText(Report, "FAIL\n" + e); }
            }
            return;
        }
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Active, false)) return;
        double now = EditorApplication.timeSinceStartup;
        if (deadline == 0) { deadline = now + 240; phaseAt = now; }
        if (now > deadline) { Finish(new Exception("Audio integration test timed out.")); return; }
        double elapsed = now - phaseAt;
        try
        {
            var library = RallyAudioLibrary.Load();
            if (phase == 0 && elapsed > 2)
            {
                var music = UnityEngine.Object.FindObjectsByType<RallyAudioSystem>();
                Require(music.Length == 1, "One music singleton in MainMenu");
                musicInstance = music[0];
                VerifyMusic(library.menuMusic, library);
                SceneManager.LoadScene(RallyGameSession.SelectionScene);
                Next();
            }
            else if (phase == 1 && elapsed > 1)
            {
                VerifyMusic(library.menuMusic, library);
                SceneManager.LoadScene("Circuit_01");
                Next();
            }
            else if (phase == 2 && elapsed > 2)
            {
                VerifyCar(library);
                forward = car.transform.forward;
                right = car.transform.right;
                sawSkid = false;
                Next();
            }
            else if (phase == 3)
            {
                body.linearVelocity = forward * 12f + right * 7f;
                AudioSource skid = Effect(library.tyreSkidLoop);
                sawSkid |= skid.isPlaying && skid.volume > .01f;
                if (elapsed > 1.2)
                {
                    Require(sawSkid, "Grounded lateral slip starts the skid loop");
                    Next();
                }
            }
            else if (phase == 4)
            {
                body.linearVelocity = forward * 30f;
                if (elapsed > .7)
                {
                    Require(car.engineAudioSource.pitch > 1f, "Engine pitch rises with chassis speed");
                    body.position += Vector3.up * 2000f;
                    body.useGravity = false;
                    body.angularVelocity = Vector3.zero;
                    body.linearVelocity = forward * 10f;
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Temporary audio collision test";
                    wall.transform.SetPositionAndRotation(body.position + forward * 9f, car.transform.rotation);
                    wall.transform.localScale = new Vector3(10f, 6f, 1f);
                    Physics.SyncTransforms();
                    Next();
                }
            }
            else if (phase == 5)
            {
                if (Effect(library.impact).isPlaying)
                {
                    Require(true, "Real BoxCollider impact plays the one-shot");
                    Time.timeScale = 0f;
                    Next();
                }
                else if (elapsed > 4) throw new Exception("No collision sound detected in the Play test.");
            }
            else if (phase == 6 && elapsed > .4)
            {
                Require(!car.engineAudioSource.isPlaying && !Effect(library.tyreSkidLoop).isPlaying
                    && !Effect(library.impact).isPlaying, "Pause stops vehicle audio");
                VerifyMusic(library.raceMusic, library);
                Time.timeScale = 1f;
                Next();
            }
            else if (phase == 7 && elapsed > .4)
            {
                Require(car.engineAudioSource.isPlaying, "Resume restores engine loop");
                SceneManager.LoadScene("Circuit_02");
                Next();
            }
            else if (phase == 8 && elapsed > 2)
            {
                VerifyCar(library);
                SceneManager.LoadScene("Circuit_03");
                Next();
            }
            else if (phase == 9 && elapsed > 2)
            {
                VerifyCar(library);
                SceneManager.LoadScene(RallyGameSession.MainMenuScene);
                Next();
            }
            else if (phase == 10 && elapsed > 1.5)
            {
                VerifyMusic(library.menuMusic, library);
                Require(UnityEngine.Object.FindObjectsByType<RallyVehicleAudio>().Length == 0, "No vehicle sounds survive returning to menu");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }

    static void Next() { phase++; phaseAt = EditorApplication.timeSinceStartup; }

    static void VerifyCar(RallyAudioLibrary library)
    {
        car = GameObject.Find(RallyGameSession.VehicleName).GetComponent<JrsVehicleController>();
        body = car.GetComponent<Rigidbody>();
        Require(car.GetComponent<RallyVehicleAudio>() != null && car.engineAudioSource.isPlaying
            && car.engineAudioSource.clip == library.engineLoop && car.engineAudioSource.outputAudioMixerGroup == library.motorGroup,
            SceneManager.GetActiveScene().name + ": player motor is playing through Motor");
        Require(Effect(library.tyreSkidLoop).outputAudioMixerGroup == library.effectsGroup
            && Effect(library.impact).outputAudioMixerGroup == library.effectsGroup, "Skid and impact routed to Efectos");
        Require(UnityEngine.Object.FindObjectsByType<RallyVehicleAudio>().Length == 1, "Only player has vehicle audio; bots remain silent");
        Require(Mathf.Approximately(body.mass, 1450f) && Mathf.Approximately(body.angularDamping, 3f), "Player mass and damping preserved");
        VerifyMusic(library.raceMusic, library);
    }

    static AudioSource Effect(AudioClip clip) => car.GetComponents<AudioSource>().Single(a => a.clip == clip);

    static void VerifyMusic(AudioClip clip, RallyAudioLibrary library)
    {
        var all = UnityEngine.Object.FindObjectsByType<RallyAudioSystem>();
        Require(all.Length == 1 && all[0] == musicInstance, "Music singleton survives scene transition");
        Require(all[0].GetComponents<AudioSource>().Any(a => a.clip == clip && a.isPlaying
            && a.outputAudioMixerGroup == library.musicGroup), clip.name + " playing through Musica");
    }

    static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        File.AppendAllText(Report, "PASS: " + description + "\n");
    }

    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        SessionState.SetBool(Active, false);
        SessionState.SetBool("RallyAudioTest.Success", error == null);
        SessionState.SetBool(Restore, true);
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }
}
