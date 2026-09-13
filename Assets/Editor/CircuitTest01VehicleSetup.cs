using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CircuitTest01VehicleSetup
{
    const string TargetPath = "Assets/Scenes/Circuit_Test_01.unity";
    const string SourcePath = "Assets/JS Vehicle Physics Controller/Scene AMR/PC Controller Scene AMR 01.unity";
    static CircuitTest01VehicleSetup() { EditorApplication.delayCall += InstallOnce; }

    static void InstallOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += InstallOnce; return; }
        if (SessionState.GetBool("CircuitVehicle.Installed", false)) return;
        if (SceneManager.GetActiveScene().path != TargetPath) return;
        if (GameObject.Find("Porsche 911 SC Rally") != null) return;
        SessionState.SetBool("CircuitVehicle.Installed", true);
        Install();
    }

    [MenuItem("Tools/Rally/Place Porsche in Circuit Test 01")]
    public static void Install()
    {
        var target = SceneManager.GetActiveScene();
        if (target.path != TargetPath || EditorApplication.isPlaying) return;
        if (target.GetRootGameObjects().Any(g => g.name == "Porsche 911 SC Rally")) return;
        var source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        GameObject car;
        try
        {
            var original = source.GetRootGameObjects().Single(g => g.name == "Porsche 911 SC Rally");
            car = UnityEngine.Object.Instantiate(original);
            car.name = "Porsche 911 SC Rally";
            SceneManager.MoveGameObjectToScene(car, target);
        }
        finally { EditorSceneManager.CloseScene(source, true); }
        SceneManager.SetActiveScene(target);
        car.transform.SetPositionAndRotation(new Vector3(0, 0, 6), Quaternion.identity);
        car.SetActive(true);
        var binder = car.GetComponentInChildren<PorscheVehicleBinder>(true);
        binder.SendMessage("ConfigureVehicle", SendMessageOptions.RequireReceiver);
        var controller = car.GetComponent<JrsVehicleController>();
        var wheels = new[] { controller.frontLeftWheel, controller.frontRightWheel, controller.rearLeftWheel, controller.rearRightWheel };
        if (wheels.Any(w => w == null)) throw new InvalidOperationException("Porsche has missing wheels.");
        foreach (var wheel in wheels) wheel.transform.localRotation = Quaternion.identity;
        float lowest = wheels.Min(w => w.transform.position.y - (w.radius + w.suspensionDistance) * Mathf.Abs(w.transform.lossyScale.y));
        car.transform.position += Vector3.up * (.05f - lowest);
        var body = car.GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        foreach (var cam in car.GetComponentsInChildren<Camera>(true)) cam.gameObject.SetActive(false);
        foreach (var listener in car.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

        // The supplied controller requires button references even for keyboard input.
        var input = new GameObject("Circuit Keyboard Input").AddComponent<JrsInputController>();
        input.accelerateButton = Button(input.transform, "Accelerate W");
        input.revButton = Button(input.transform, "Reverse S");
        input.leftButton = Button(input.transform, "Left A");
        input.rightButton = Button(input.transform, "Right D");
        input.brakeButton = Button(input.transform, "Brake Space");
        input.headLightsButton = Button(input.transform, "Headlights");
        input.sirenButton = Button(input.transform, "Siren");
        input.signalLightsButton = Button(input.transform, "Signals");
        input.extraLightsButton = Button(input.transform, "Extra Lights");
        var camera = target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Single(c => c.gameObject.name == "Main Camera");
        camera.orthographic = false;
        camera.fieldOfView = 60;
        camera.nearClipPlane = .1f;
        var follow = camera.gameObject.AddComponent<JrsFollowCamera>();
        follow.target = car.transform;
        follow.offset = new Vector3(0, 3.5f, -7);
        follow.horizontalSpringConstant = 40;
        follow.horizontalDampingConstant = 12;
        camera.transform.position = car.transform.TransformPoint(follow.offset);
        camera.transform.LookAt(car.transform.position + Vector3.up * .5f);
        input.cameras = new[] { camera };
        Physics.SyncTransforms();
        if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save circuit vehicle.");
        Selection.activeGameObject = car;
        SceneView.lastActiveSceneView?.LookAt(car.transform.position + Vector3.forward * 8, Quaternion.Euler(40, 0, 0), 18, false, true);
        Debug.Log("CIRCUIT_VEHICLE_READY: Porsche at " + car.transform.position + "; keyboard WASD + Space; follow camera assigned; scene saved.");
    }

    static JrsCustomButton Button(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        return go.AddComponent<JrsCustomButton>();
    }
}
