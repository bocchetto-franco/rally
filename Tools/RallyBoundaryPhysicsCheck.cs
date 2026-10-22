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

public static class RallyBoundaryPhysicsCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Editor must be idle");
        var report = new List<string>(); Scene source = default, scene = default;
        Action<bool,string> check = (ok,text) => { if (!ok) throw new Exception(text); report.Add("PASS: " + text); };
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_03.unity");
            var tuning = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RallyVehicleDynamics>(true)).Single(d => d.name == "Porsche Rally Dynamics");
            scene = EditorSceneManager.NewPreviewScene(); var physics = scene.GetPhysicsScene();
            var road = new GameObject("Circuit03_Road"); SceneManager.MoveGameObjectToScene(road,scene);
            road.transform.position = new Vector3(0,-.5f,0); road.AddComponent<BoxCollider>().size = new Vector3(14,1,300);
            var wall = new GameObject("Boundary_test"); SceneManager.MoveGameObjectToScene(wall,scene);
            wall.transform.position = new Vector3(4,1,0); var boundary = wall.AddComponent<BoxCollider>(); boundary.size = new Vector3(.5f,3,300);
            boundary.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Scenes/Circuit_01_SoftBoundary.physicMaterial");
            check(boundary.sharedMaterial != null && boundary.sharedMaterial.bounciness == .15f && boundary.sharedMaterial.dynamicFriction == .05f,"existing soft material: bounce 0.15, friction 0.05");
            var root = Object.Instantiate(tuning.VehicleController.gameObject); SceneManager.MoveGameObjectToScene(root,scene);
            var car = root.GetComponent<JrsVehicleController>(); var body = root.GetComponent<Rigidbody>(); body.isKinematic = false;
            var rig = new GameObject("isolated tuning"); SceneManager.MoveGameObjectToScene(rig,scene);
            var dynamics = rig.AddComponent<RallyVehicleDynamics>(); EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(tuning),dynamics);
            dynamics.BindVehicle(car); dynamics.ApplySetup();
            typeof(RallyVehicleDynamics).GetMethod("Start",Private).Invoke(dynamics,null);
            var penalty = car.GetComponent<RallyOffTrackSpeedPenalty>(); var tick = typeof(RallyOffTrackSpeedPenalty).GetMethod("FixedUpdate",Private);
            body.position = new Vector3(2,.7f,0); body.rotation = Quaternion.identity;
            foreach (var wheel in root.GetComponentsInChildren<WheelCollider>()) wheel.motorTorque = wheel.brakeTorque = 0;
            for (int i=0;i<90;i++) { physics.Simulate(.02f); tick.Invoke(penalty,null); }
            body.linearVelocity = new Vector3(12,0,6); float minX=12,maxX=body.position.x;
            for (int i=0;i<100;i++)
            {
                physics.Simulate(.02f); tick.Invoke(penalty,null);
                minX = Mathf.Min(minX,body.linearVelocity.x); maxX = Mathf.Max(maxX,body.position.x);
            }
            check(maxX > 2.5f && body.position.x < 3.8f && minX < -.05f,"real road/wall collision produces a mild rebound, without passing through");
            check(penalty.AppliedCount == 0 && !penalty.IsOffTrack,"wall impact on driving surface never applies off-track penalty");
            body.position = new Vector3(0,body.position.y,body.position.z); body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.forward * (100f/3.6f); var velocity = body.linearVelocity;
            tick.Invoke(penalty,null);
            check(body.linearVelocity == velocity,"no lingering speed cap after wall impact");
            float z = body.position.z;
            for (int i=0;i<40;i++) { physics.Simulate(.02f); tick.Invoke(penalty,null); }
            check(body.position.z > z+5 && penalty.AppliedCount == 0,"player keeps advancing after the collision");
            report.Add($"METRICS maxX={maxX:F3}; rebound velocityX={minX:F3}m/s; forward advance={body.position.z-z:F2}m");
            report.Add("COMPLETE: PASS");
        }
        catch (Exception e) { report.Add("FAIL: " + e); throw; }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            File.WriteAllLines("Logs/five-blocks-boundary-physics.txt",report);
        }
    }
}
