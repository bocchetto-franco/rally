using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Verify every native physics field and dynamic tuning field is unchanged by selection.</summary>
public static class RallyVehiclePhysicsParityTest
{
    [MenuItem("Tools/Rally/Verify Identical Player Physics")]
    public static void Run()
    {
        var previous = EditorSceneManager.GetActiveScene();
        if (previous.isDirty && !Application.isBatchMode) throw new InvalidOperationException("Save the open scene first.");
        string previousPath = previous.path;
        try
        {
            foreach (string scene in RallyGameSession.CircuitScenes)
            foreach (string choice in RallyPlayerVehicleSelection.Names.Skip(1))
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
                var car = GameObject.Find(RallyGameSession.VehicleName).GetComponent<JrsVehicleController>();
                var dynamics = GameObject.Find("Porsche Rally Dynamics").GetComponent<RallyVehicleDynamics>();
                dynamics.ApplySetup();
                var body = car.GetComponent<Rigidbody>();
                var colliders = car.GetComponentsInChildren<Collider>(true);
                var bots = UnityEngine.Object.FindObjectsByType<RallyBotController>();
                string before = Snapshot(car, dynamics, body, colliders);
                string[] botBefore = bots.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
                var visual = RallyPlayerVehicleSelection.ApplyVisual(car, choice);
                string after = Snapshot(car, dynamics, body, colliders);
                if (before != after)
                {
                    System.IO.File.WriteAllText("Logs/physics-before.txt", before);
                    System.IO.File.WriteAllText("Logs/physics-after.txt", after);
                    throw new Exception("Physics was changed by " + choice + " in " + scene);
                }
                if (car.GetComponentsInChildren<Collider>(true).Length != colliders.Length)
                    throw new Exception("Visual added collision geometry.");
                if (!botBefore.SequenceEqual(bots.Select(b => EditorJsonUtility.ToJson(b)))) throw new Exception("Bot was changed.");
                var wheels = new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel };
                for (int i = 0; i < wheels.Length; i++)
                {
                    Vector3 expected = wheels[i].transform.TransformPoint(wheels[i].center);
                    if (Vector3.Distance(visual.wheels[i].position, expected) > .0001f)
                        throw new Exception("Visual wheel is not fitted to Porsche axle.");
                    float radius = visual.radii[i] * visual.wheels[i].localScale.x;
                    if (Mathf.Abs(radius - wheels[i].radius) > .0001f) throw new Exception("Visual wheel radius differs.");
                }
                Debug.Log("PLAYER_PHYSICS_IDENTICAL " + scene + " / " + choice +
                    ": Rigidbody, inertia, all colliders/transforms, controller parameters, dynamics and bots unchanged.");
            }
        }
        finally
        {
            if (string.IsNullOrEmpty(previousPath)) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.OpenScene(previousPath);
        }
    }

    static string Snapshot(JrsVehicleController car, RallyVehicleDynamics dynamics, Rigidbody body, Collider[] colliders)
    {
        var serializedController = new SerializedObject(car);
        var iterator = serializedController.GetIterator();
        string tuning = "";
        while (iterator.Next(true))
        {
            if (new[] { "frontLeftWheelTransform", "frontRightWheelTransform", "rearLeftWheelTransform", "rearRightWheelTransform" }
                .Any(p => iterator.propertyPath == p || iterator.propertyPath.StartsWith(p + "."))) continue;
            // Wheel visual references are intentionally different; all numbers, booleans,
            // arrays and physics-object references must stay identical.
            if (iterator.propertyType == SerializedPropertyType.Float) tuning += iterator.propertyPath + iterator.floatValue.ToString("R");
            if (iterator.propertyType == SerializedPropertyType.Integer) tuning += iterator.propertyPath + iterator.intValue;
            if (iterator.propertyType == SerializedPropertyType.Boolean) tuning += iterator.propertyPath + iterator.boolValue;
        }
        return EditorJsonUtility.ToJson(body) + body.centerOfMass.ToString("F8") + body.inertiaTensor.ToString("F8") +
            body.inertiaTensorRotation.ToString("F8") + tuning + EditorJsonUtility.ToJson(dynamics) +
            string.Join("\n", colliders.Select(c => EditorJsonUtility.ToJson(c) + c.transform.localToWorldMatrix.ToString("F8")));
    }
}
