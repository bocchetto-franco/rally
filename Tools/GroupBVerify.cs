using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GroupBVerify
{
    const string Report = "Logs/group-b-static.txt";
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); File.AppendAllText(Report, "PASS: " + message + "\n"); }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene");
        File.WriteAllText(Report, "Unity final bounds (length / width / height in metres), physics parity, dependency checks\n");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string name in new[] { "LanciaS4", "Peugeot205T16", "AudiQuattro" })
            {
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/" + name + ".prefab"));
                SceneManager.MoveGameObjectToScene(model, preview);
                var renderers = model.GetComponentsInChildren<MeshRenderer>();
                var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
                File.AppendAllText(Report, $"BOUNDS {name}: {b.size.z:F5} / {b.size.x:F5} / {b.size.y:F5}\n");
                var visual = model.GetComponent<RallyVehicleVisual>();
                Check(visual.useAuthoredScale && visual.wheels.Length == 4 && visual.wheels.Distinct().Count() == 4, name + " uniform authored scale and separate wheels");
                Check(visual.wheels[0].localPosition.z > visual.wheels[2].localPosition.z && visual.wheels[0].localPosition.x < visual.wheels[1].localPosition.x, name + " wheel ordering FL FR RL RR");
                Check(renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported)), name + " valid materials");
                Check(model.GetComponentsInChildren<Collider>(true).Length == 0 && model.GetComponentsInChildren<Rigidbody>(true).Length == 0, name + " presentation only");
                Object.DestroyImmediate(model);
            }
            foreach (string sceneName in RallyGameSession.CircuitScenes)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
                var car = GameObject.Find(RallyGameSession.VehicleName).GetComponent<JrsVehicleController>();
                VerifyRig(car, false, sceneName);
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    CheckMissing(t.gameObject);
                File.AppendAllText(Report, "PASS no missing scripts: " + sceneName + "\n");
            }
            var bot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Bot_Car.prefab");
            try { VerifyRig(bot.GetComponentInChildren<JrsVehicleController>(true), true, "Bot_Car"); }
            finally { PrefabUtility.UnloadPrefabContents(bot); }
            Check(RallyPlayerVehicleSelection.MigrateSavedName("Porsche 911 SC Rally") == RallyPlayerVehicleSelection.LanciaName && RallyPlayerVehicleSelection.MigrateSavedName("BMW M3 E30") == RallyPlayerVehicleSelection.PeugeotName, "old saved selections migrate");
            Check(!Directory.Exists("Assets/auto/porsche-911-sc-rally") && !Directory.Exists("Assets/Art/Vehicles/BmwE30"), "retired source models removed from Assets");
            File.AppendAllText(Report, "COMPLETE: PASS\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    static void CheckMissing(GameObject o) { if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(o) > 0) throw new Exception("Missing script on " + o.name); }
    static void VerifyRig(JrsVehicleController car, bool bot, string label)
    {
        var body = car.GetComponent<Rigidbody>();
        var colliders = car.GetComponentsInChildren<Collider>(true);
        var dynamics = Object.FindObjectsByType<RallyVehicleDynamics>(FindObjectsInactive.Include).FirstOrDefault(d => d.VehicleController == car);
        string before = RallyVehiclePhysicsParityTest.Snapshot(car, dynamics, body, colliders);
        foreach (string choice in RallyPlayerVehicleSelection.Names.Concat(new[] { RallyPlayerVehicleSelection.LanciaName }))
        {
            var visual = bot ? RallyPlayerVehicleSelection.ApplyBotVisual(car, choice) : RallyPlayerVehicleSelection.ApplyVisual(car, choice);
            Check(before == RallyVehiclePhysicsParityTest.Snapshot(car, dynamics, body, colliders), label + " unchanged mass/inertia/friction/braking/suspension/downforce/control: " + choice);
            var wheels = new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel };
            for (int i = 0; i < 4; i++)
                Check(Vector3.Distance(wheels[i].transform.TransformPoint(wheels[i].center), visual.wheels[i].position) < .001f && Mathf.Abs(wheels[i].radius - visual.radii[i]) < .0001f, label + " wheel fit " + i + " / " + choice);
            Check(car.GetComponentsInChildren<Collider>(true).Length == colliders.Length && car.GetComponentsInChildren<RallyVehicleVisual>(true).Length == 1, label + " no added physics or duplicate model");
        }
    }
}
