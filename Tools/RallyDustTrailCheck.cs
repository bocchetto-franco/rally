using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated physics/presentation check: no Play, saved-scene or tuning writes.
public static class RallyDustTrailCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> report = new List<string>();
    static void Check(bool pass, string text) { if (!pass) throw new Exception(text); report.Add("PASS: " + text); }
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static float Rate(ParticleSystem p) => p.emission.rateOverTime.constant;
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Editor must be idle");
        report.Clear(); report.Add("Dust trail: isolated Unity wheel contacts and particle simulation. No full lap/FPS measurement.");
        Scene source = default, scene = default;
        float oldTimeScale = Time.timeScale;
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_01.unity");
            var original = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JrsVehicleController>(true))
                .Single(c => c.name == RallyGameSession.VehicleName);
            scene = EditorSceneManager.NewPreviewScene();
            var ground = new GameObject("off-road ground"); SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(0, -.52f, 0); ground.AddComponent<BoxCollider>().size = new Vector3(200, 1, 400);
            var road = new GameObject("Rally_Road_Start_to_Finish"); SceneManager.MoveGameObjectToScene(road, scene);
            road.transform.position = new Vector3(0, -.5f, 0); road.AddComponent<BoxCollider>().size = new Vector3(8, 1, 400);
            var root = Object.Instantiate(original.gameObject); SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(true); var car = root.GetComponent<JrsVehicleController>(); car.enabled = true;
            var body = root.GetComponent<Rigidbody>(); body.isKinematic = false;
            var classifier = root.AddComponent<RallyOffTrackSpeedPenalty>(); classifier.Configure(car, null);
            var dust = root.AddComponent<RallyVehicleDust>(); Call(dust, "Awake"); Call(dust, "Start");
            var left = Field<ParticleSystem>(dust, "leftDust"); var right = Field<ParticleSystem>(dust, "rightDust");
            var front = Field<ParticleSystem>(dust, "leftCornerDust");
            Check(left != null && right != null && front != null, "existing driving/corner emitters initialized");
            int count = root.GetComponentsInChildren<ParticleSystem>(true).Length;
            var cameraObject = new GameObject("Dust check camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.farClipPlane = 300;
            camera.transform.position = new Vector3(0, 3, -8); camera.transform.LookAt(new Vector3(0, .5f, 2));
            var wheels = new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel };
            foreach (var wheel in wheels) { wheel.motorTorque = wheel.brakeTorque = wheel.steerAngle = 0; }
            var physics = scene.GetPhysicsScene();
            body.position = new Vector3(0, .7f, 0); body.rotation = Quaternion.identity;
            for (int i = 0; i < 90; i++) physics.Simulate(.02f);
            Check(wheels.All(w => w.GetGroundHit(out _)), "four real WheelCollider contacts on road");
            Vector3 settled = body.position;
            Camera secondCamera = null;
            Action<float, float> tick = (speed, steer) =>
            {
                body.linearVelocity = Vector3.forward * (speed / 3.6f);
                car.frontLeftWheel.steerAngle = car.frontRightWheel.steerAngle = steer;
                // Preview cameras do not enter Unity's live Camera.allCameras
                // registry. Feed their real projection through the same predicate.
                var sees = typeof(RallyVehicleDust).GetMethod("CameraCanSee", Private);
                bool inView = (bool)sees.Invoke(dust, new object[] { camera }) ||
                    (secondCamera != null && (bool)sees.Invoke(dust, new object[] { secondCamera }));
                typeof(RallyVehicleDust).GetField("visible", Private).SetValue(dust, inView);
                typeof(RallyVehicleDust).GetField("nextVisibilityCheck", Private).SetValue(dust, float.MaxValue);
                Call(dust, "LateUpdate");
            };
            Time.timeScale = 1;
            tick(8, 0); float slowRate = Rate(left), slowSize = left.main.startSize.constantMax;
            report.Add($"DIAGNOSTIC rate={slowRate}, speed={body.linearVelocity.magnitude * 3.6f}, up={Vector3.Dot(root.transform.up,Vector3.up)}, cameraCount={Camera.allCamerasCount}, cameraType={camera.cameraType}, enabled={camera.isActiveAndEnabled}, visible={Field<bool>(dust,"visible")}, road={dust.CanEmitAt(road.GetComponent<Collider>())}, controller={car.isActiveAndEnabled}, dust={dust.isActiveAndEnabled}, loading={RallyRaceLoading.IsPreparing}");
            Check(slowRate > 0 && slowRate < .5f && Rate(front) == 0, "low-speed straight trail almost imperceptible");
            tick(80, 0); float straightRate = Rate(left), straightSize = left.main.startSize.constantMax;
            Check(straightRate > slowRate && straightSize > slowSize, "speed increases emission and size");
            tick(80, car.maxSteerAngle); float cornerRate = Rate(left);
            Check(cornerRate > straightRate * 2 && left.main.startSize.constantMax > straightSize * 1.35f && Rate(front) > 0, "steering noticeably increases rear trail plus existing front dust");
            report.Add($"MEASURED rear particles/s slow={slowRate:F3}, straight80={straightRate:F3}, corner80={cornerRate:F3}; size {slowSize:F3}/{straightSize:F3}/{left.main.startSize.constantMax:F3}");
            Check(left.main.simulationSpace == ParticleSystemSimulationSpace.World && left.main.startLifetime.constantMax == 1.5f, "world-space cloud lingers for up to 1.5 seconds");
            Check(left.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader == car.rearLeftDustParticleSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader, "existing drift shader reused");
            left.Simulate(.35f, false, false); right.Simulate(.35f, false, false);
            Check(left.particleCount > 0, "trail produces real particles");
            var particles = new ParticleSystem.Particle[64]; int alive = left.GetParticles(particles);
            float oldZ = particles[0].position.z;
            body.position += Vector3.forward * 3; physics.Simulate(.02f); tick(80, car.maxSteerAngle);
            left.GetParticles(particles);
            Check(alive > 0 && Mathf.Abs(particles[0].position.z - oldZ) < .1f, "existing particles stay behind when the car moves");
            tick(0, 0); Check(Rate(left) == 0 && Rate(right) == 0 && Rate(front) == 0 && !left.isEmitting, "stationary car stops emitting");
            left.Simulate(1.6f, false, false); Check(left.particleCount == 0, "old cloud expires without accumulating");
            Time.timeScale = 0; tick(80, 30); Check(Rate(left) == 0, "pause blocks emission"); Time.timeScale = 1;
            body.rotation = Quaternion.Euler(0, 0, 90); tick(80, 30); Check(Rate(left) == 0, "overturned car blocks emission");
            body.rotation = Quaternion.identity; body.position = new Vector3(0, 6, 0); physics.Simulate(.02f);
            tick(80, 30); Check(Rate(left) == 0 && Rate(right) == 0, "airborne rear wheels block emission");
            body.position = new Vector3(10, settled.y + .05f, 0); body.linearVelocity = body.angularVelocity = Vector3.zero;
            for (int i = 0; i < 90; i++) physics.Simulate(.02f);
            tick(80, 30); Check(Rate(left) == 0 && Rate(right) == 0, "actual off-road wheel contacts block emission");
            body.position = settled; body.rotation = Quaternion.identity; body.linearVelocity = body.angularVelocity = Vector3.zero;
            for (int i = 0; i < 90; i++) physics.Simulate(.02f);
            camera.transform.position = new Vector3(0, 3, -150); tick(80, 30); Check(Rate(left) == 0, "distant camera blocks emission");
            camera.transform.position = new Vector3(0, 3, -8); camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            tick(80, 30); Check(Rate(left) == 0, "outside camera view blocks emission");
            var secondCameraObject = new GameObject("Second local player camera"); SceneManager.MoveGameObjectToScene(secondCameraObject, scene);
            secondCamera = secondCameraObject.AddComponent<Camera>(); secondCamera.transform.position = new Vector3(0, 3, -8);
            secondCamera.transform.LookAt(new Vector3(0, .5f, 2));
            tick(80, 30); Check(Rate(left) > 0, "either local-player camera keeps visible trail active");
            for (int i = 0; i < 100; i++) tick(80, 30);
            Check(root.GetComponentsInChildren<ParticleSystem>(true).Length == count && left.main.maxParticles == 64 && front.main.maxParticles == 20, "no emitter duplication; fixed particle budgets");
            car.enabled = false; tick(80, 30); Check(Rate(left) == 0, "finished/disabled control blocks trail");
            report.Add("COMPLETE: PASS");
        }
        catch (Exception e) { report.Add("FAIL: " + e); throw; }
        finally
        {
            Time.timeScale = oldTimeScale;
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            File.WriteAllLines("Logs/dust-trail-check.txt", report);
        }
    }
}
