using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Circuit01VehicleSetup
{
    const string Target = "Assets/Scenes/Circuit_01.unity";
    static Circuit01VehicleSetup() { EditorApplication.delayCall += Once; }
    static void Once()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Once; return; }
        if (SceneManager.GetActiveScene().path == Target && !SessionState.GetBool("Circuit01.StartPlacement.v2", false)) { Install(); SessionState.SetBool("Circuit01.StartPlacement.v2", true); }
    }
    [MenuItem("Tools/Rally/Place Porsche at Circuit 01 Start")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != Target || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var car = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Porsche 911 SC Rally");
        if (car == null)
        {
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Circuit_Test_01.unity", OpenSceneMode.Additive);
            try
            {
                foreach (string name in new[] { "Porsche 911 SC Rally", "Circuit Keyboard Input", "Main Camera", "Directional Light" })
                {
                    if (scene.GetRootGameObjects().Any(g => g.name == name)) continue;
                    var original = source.GetRootGameObjects().Single(g => g.name == name);
                    var copy = UnityEngine.Object.Instantiate(original);
                    copy.name = name;
                    SceneManager.MoveGameObjectToScene(copy, scene);
                }
            }
            finally { EditorSceneManager.CloseScene(source, true); }
            SceneManager.SetActiveScene(scene);
            car = scene.GetRootGameObjects().Single(g => g.name == "Porsche 911 SC Rally");
        }
        var start = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Checkpoint_00_Start");
        var heading = start != null ? start.rotation : Quaternion.identity;
        var position = start != null ? start.position : Vector3.zero;
        // Begin just inside the starting gate so entering Play starts the clock.
        position.y = 0;
        position += heading * Vector3.forward * 3;
        if (start != null)
        {
            // Cover the grid so all four wheels rest on the road while the timer starts.
            var gate = start.GetComponent<BoxCollider>();
            gate.size = new Vector3(gate.size.x, gate.size.y, 10);
        }
        car.transform.SetPositionAndRotation(position, heading);
        car.SetActive(true);
        var controller = car.GetComponent<JrsVehicleController>();
        var wheels = new[] { controller.frontLeftWheel, controller.frontRightWheel, controller.rearLeftWheel, controller.rearRightWheel };
        if (wheels.Any(w => w == null)) throw new InvalidOperationException("Missing Porsche wheel reference.");
        var road = scene.GetRootGameObjects().Single(g => g.name == "Rally_Road_Start_to_Finish").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        if (!road.Raycast(new Ray(position + Vector3.up * 100, Vector3.down), out var hit, 200)) throw new InvalidOperationException("No road at start.");
        float lowest = wheels.Min(w => w.transform.position.y - (w.radius + w.suspensionDistance) * Mathf.Abs(w.transform.lossyScale.y));
        car.transform.position += Vector3.up * (hit.point.y + .05f - lowest);
        var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Single(c => c.name == "Main Camera");
        var follow = camera.GetComponent<JrsFollowCamera>();
        follow.target = car.transform;
        camera.transform.position = car.transform.TransformPoint(follow.offset);
        camera.transform.LookAt(car.transform.position + Vector3.up * .5f);
        var input = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JrsInputController>(true)).Single();
        input.cameras = new[] { camera };
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Porsche placement.");
        Selection.activeGameObject = car;
        SceneView.lastActiveSceneView?.LookAt(car.transform.position, Quaternion.Euler(35, 0, 0), 12);
        Debug.Log("CIRCUIT_01_PORSCHE_READY: active Porsche, four wheels, keyboard input and follow camera saved at " + car.transform.position);
    }
}
