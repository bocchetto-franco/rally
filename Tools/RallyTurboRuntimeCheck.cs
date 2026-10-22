using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Real trigger/force integration in Play. No saved scene, tuning or preferences changed.
public static class RallyTurboRuntimeCheck
{
    const string Report = "Logs/turbo-play-check.txt";
    static IEnumerator steps;
    static bool stopping, background, wasPaused, hasPlayed;
    static SceneAsset start;
    static int previousPlayers, frame;
    static bool hadPlayers;
    static float oldScale;
    static double deadline, next;
    static RallyTurboZone[] zones;
    static JrsVehicleController car;
    static Rigidbody body;
    static RallyVehicleTurbo turbo;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new Exception("Editor must be idle.");
        if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
            throw new Exception("External verifier requires the existing Disable Domain Reload setting.");
        start = EditorSceneManager.playModeStartScene; background = Application.runInBackground; wasPaused = EditorApplication.isPaused; oldScale = Time.timeScale;
        hadPlayers = PlayerPrefs.HasKey("Rally.LocalPlayers"); previousPlayers = PlayerPrefs.GetInt("Rally.LocalPlayers", 1);
        RallyGameSession.SelectLocalPlayers(1);
        stopping = false; hasPlayed = false; frame = -1; next = 0; deadline = EditorApplication.timeSinceStartup + 600;
        Directory.CreateDirectory("Logs"); File.WriteAllText(Report, "Turbo real Play: physical centre/edge/reverse passes, players/bots, expiry and base tuning\n");
        steps = Steps(); EditorApplication.update += Tick;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Circuit_01.unity");
        Application.runInBackground = true; EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (stopping) { if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if (EditorApplication.isPlaying) { hasPlayed = true; EditorApplication.QueuePlayerLoopUpdate(); }
        else if (hasPlayed && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(new Exception("Play stopped externally before checks completed")); return; }
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timeout")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || frame == Time.frameCount || Time.timeAsDouble < next) return;
        frame = Time.frameCount;
        try
        {
            if (!steps.MoveNext()) { Finish(null); return; }
            next = Time.timeAsDouble + (steps.Current is float wait ? wait : 0f);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    static void Place(RallyTurboZone zone, float x, float z, float speed)
    {
        turbo.CancelBoost();
        body.position = zone.transform.TransformPoint(new Vector3(x, .65f, z));
        body.rotation = zone.transform.rotation;
        body.angularVelocity = Vector3.zero;
        body.linearVelocity = zone.transform.forward * speed;
        foreach (var wheel in new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel })
            wheel.brakeTorque = wheel.motorTorque = wheel.steerAngle = 0;
        Physics.SyncTransforms();
    }
    static float Speed() => Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f;
    static IEnumerator Steps()
    {
        foreach (string sceneName in RallyGameSession.CircuitScenes)
        {
            if (SceneManager.GetActiveScene().name != sceneName) SceneManager.LoadScene(sceneName);
            yield return null;
            while (RallyRaceLoading.IsPreparing || Object.FindObjectsByType<RallyTurboZone>().Length != 3) yield return null;
            yield return .2f;
            zones = Object.FindObjectsByType<RallyTurboZone>().OrderBy(z => z.name).ToArray();
            var cars = Object.FindObjectsByType<JrsVehicleController>().Where(c => c.gameObject.activeInHierarchy).ToArray();
            Check(cars.Length == 4 && cars.All(c => c.GetComponent<RallyVehicleTurbo>() != null), sceneName + ": all four receivers preloaded");
            car = cars.Single(c => c.GetComponentInParent<RallyBotController>() == null);
            body = car.GetComponent<Rigidbody>(); turbo = car.GetComponent<RallyVehicleTurbo>();
            var dynamics = Object.FindObjectsByType<RallyVehicleDynamics>().Single(d => d.VehicleController == car);
            car.GetComponent<RallyLocalPlayerInput>()?.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, dynamics.SteeringResponse);
            string baseTuning = EditorJsonUtility.ToJson(dynamics);
            float mass = body.mass, damping = body.angularDamping;
            foreach (var otherBot in Object.FindObjectsByType<RallyBotController>())
            {
                otherBot.enabled = false; otherBot.GetComponentInChildren<Rigidbody>().linearVelocity = Vector3.zero;
                typeof(RallyBotController).GetProperty("VerticalInput").GetSetMethod(true).Invoke(otherBot, new object[] { 0f });
                typeof(RallyBotController).GetProperty("HorizontalInput").GetSetMethod(true).Invoke(otherBot, new object[] { 0f });
            }
            foreach (var zone in zones)
            {
                int count = turbo.ActivationCount;
                zone.enabled = false;
                Place(zone, Mathf.Min(1.65f, zone.ActivationHalfWidth - .1f), -7, 22);
                yield return .8f;
                float withoutBoost = Speed();
                Check(turbo.ActivationCount == count, "Disabled zone control pass does not boost");
                zone.enabled = true;
                Place(zone, zone.ActivationHalfWidth + .5f, -7, 22);
                yield return .85f;
                Check(turbo.ActivationCount == count, sceneName + "/" + zone.name + ": overlapping edge pass does not activate");
                Place(zone, Mathf.Min(1.65f, zone.ActivationHalfWidth - .1f), -7, 22);
                float before = Speed();
                yield return .3f;
                ScreenCapture.CaptureScreenshot("Logs/Turbo_" + sceneName + "_" + System.Array.IndexOf(zones, zone) + ".png");
                yield return .5f;
                Check(turbo.ActivationCount == count + 1 && turbo.IsBoosting, sceneName + "/" + zone.name + ": widened lane at 1.65m offset activates once");
                Check(Speed() > before + 3f, "Additional force measurably raises player speed (" + before.ToString("F1") + " -> " + Speed().ToString("F1") + " km/h)");
                Check(Speed() > withoutBoost + 2f, "Boosted pass is faster than the same lane with the zone disabled (control=" + withoutBoost.ToString("F1") + " km/h)");
                Check(!turbo.Activate(2.25f, 4.5f, 180, 35), "Active boosts cannot stack");
                yield return 2f;
                Check(!turbo.IsBoosting && turbo.ActivationCount == count + 1, "Boost expires without repeated overlap activation");
            }
            var first = zones[0];
            Place(first, 0, 0, -15);
            Check(!first.TryActivate(body), "Reverse passage rejected");
            Place(first, 0, 0, 0);
            Check(!first.TryActivate(body), "Stationary car rejected");
            Place(first, 0, 0, 20);
            Time.timeScale = 0;
            Check(!first.TryActivate(body), "Paused game cannot activate boost");
            Time.timeScale = 1;
            car.enabled = false;
            Check(!first.TryActivate(body), "Finished/disabled control cannot activate boost");
            car.enabled = true;
            Check(body.mass == mass && body.angularDamping == damping && EditorJsonUtility.ToJson(dynamics) == baseTuning, "Player serialized dynamics, mass and damping unchanged");
            body.isKinematic = true; body.position = first.transform.position + Vector3.up * 20;
            var botCar = cars.First(c => c.GetComponentInParent<RallyBotController>() != null);
            var bot = botCar.GetComponentInParent<RallyBotController>();
            car = botCar; body = car.GetComponent<Rigidbody>(); turbo = car.GetComponent<RallyVehicleTurbo>();
            float baseMax = bot.RaceMaxSpeedKph;
            Place(first, 0, -7, 22);
            float botBefore = Speed();
            yield return .8f;
            Check(turbo.IsBoosting && bot.TurboSpeedBonusKph == 35 && bot.RaceMaxSpeedKph == baseMax, sceneName + ": bot receives the same boost with temporary speed allowance");
            Check(Speed() > botBefore + 3f, "Additional force measurably raises bot speed (" + botBefore.ToString("F1") + " -> " + Speed().ToString("F1") + " km/h)");
            yield return 2f;
            Check(!turbo.IsBoosting && bot.TurboSpeedBonusKph == 0 && bot.RaceMaxSpeedKph == baseMax, "Bot restores original speed target after expiry");
        }
        RallyGameSession.SelectLocalPlayers(2);
        SceneManager.LoadScene("Circuit_03");
        yield return null;
        while (RallyRaceLoading.IsPreparing || RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready) yield return null;
        yield return .2f;
        var humans = Object.FindObjectsByType<RallyLocalPlayerInput>().OrderBy(i => i.PlayerIndex).ToArray();
        var shared = Object.FindObjectsByType<RallyTurboZone>().OrderBy(z => z.name).First();
        Check(humans.Length == 2 && humans.All(h => h.GetComponent<RallyVehicleTurbo>() != null), "2P: both receivers preloaded after split rig readiness");
        foreach (var human in humans)
        {
            var other = humans.First(h => h != human);
            var otherBody = other.GetComponent<Rigidbody>();
            other.GetComponent<RallyVehicleTurbo>().CancelBoost();
            otherBody.isKinematic = true; otherBody.position = shared.transform.position + Vector3.up * 20;
            int otherCount = other.GetComponent<RallyVehicleTurbo>().ActivationCount;
            car = human.GetComponent<JrsVehicleController>(); body = human.GetComponent<Rigidbody>(); turbo = human.GetComponent<RallyVehicleTurbo>();
            human.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, 22f);
            body.isKinematic = false;
            Place(shared, 0, -7, 22);
            yield return .8f;
            Check(turbo.IsBoosting && turbo.ActivationCount == 1, "2P: J" + (human.PlayerIndex + 1) + " activates the same zone independently");
            Check(other.GetComponent<RallyVehicleTurbo>().ActivationCount == otherCount && !other.GetComponent<RallyVehicleTurbo>().IsBoosting, "2P: no boost leaks to the other player and cooldown is per car");
        }
    }
    static void Finish(Exception error)
    {
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        stopping = true; Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        if (hadPlayers) PlayerPrefs.SetInt("Rally.LocalPlayers", previousPlayers); else PlayerPrefs.DeleteKey("Rally.LocalPlayers");
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
        EditorSceneManager.playModeStartScene = start; Application.runInBackground = background;
        EditorApplication.isPaused = wasPaused; Time.timeScale = oldScale;
        File.AppendAllText(Report, "RESTORED: player count, play-start scene, background, pause, timeScale; no scenes saved\n");
    }
}
