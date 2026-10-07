using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Isolated PhysicsScene: runs real Rigidbody/WheelCollider steps without changing
// the saved circuit, keyboard/gamepad pairing, preferences, race, or open scenes.
public static class ArcadeHandlingPhysicsCheck
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> report=new List<string>();
    static void Require(bool condition,string text){if(!condition)throw new Exception(text);report.Add("PASS "+text);}
    static void Input(RallyLocalPlayerInput input,float steer,float throttle,float brake=0,bool handbrake=false)
    {
        foreach(var p in new[]{("Horizontal",steer),("Vertical",throttle),("Brake",brake)})
            typeof(RallyLocalPlayerInput).GetProperty(p.Item1).GetSetMethod(true).Invoke(input,new object[]{p.Item2});
        typeof(RallyLocalPlayerInput).GetProperty("Handbrake").GetSetMethod(true).Invoke(input,new object[]{handbrake});
    }
    static void Step(PhysicsScene physics,JrsVehicleController car,RallyVehicleDynamics dynamics)
    {
        car.SendMessage("FixedUpdate",SendMessageOptions.RequireReceiver);
        dynamics.SendMessage("FixedUpdate",SendMessageOptions.RequireReceiver);
        physics.Simulate(Time.fixedDeltaTime);
    }
    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new Exception("Editor must be idle");
        report.Clear();
        Scene original=SceneManager.GetActiveScene(),source=SceneManager.GetSceneByPath("Assets/Scenes/Circuit_01.unity");
        bool opened=!source.isLoaded;
        if(opened)source=EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity",OpenSceneMode.Additive);
        Scene test=default;
        try
        {
            var tuning=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RallyVehicleDynamics>(true)).Single(d=>d.name=="Porsche Rally Dynamics" && d.VehicleController!=null && d.VehicleController.name==RallyGameSession.VehicleName);
            var calc=typeof(RallyVehicleDynamics).GetMethod("CalculateAutomaticBrakeAmount",Private);
            float Brake(float steer,float speed)=>(float)calc.Invoke(tuning,new object[]{steer,speed});
            Require(Brake(0,180)==0 && Brake(.14f,180)==0,"no automatic braking on straight or within steering deadzone");
            Require(Brake(1,55)==0 && Brake(1,-60)==0,"no automatic braking below corner target or in reverse");
            Require(Brake(.5f,110)>0 && Brake(.5f,110)<Brake(1,110),"braking increases with steering at high speed");
            Require(Mathf.Abs(Brake(1,110)-.55f)<.0001f,"automatic braking capped at 55 percent");
            foreach(string choice in RallyPlayerVehicleSelection.Names)
            {
                test=EditorSceneManager.NewPreviewScene();
                var physics=test.GetPhysicsScene();
                Require(physics.IsValid() && physics != Physics.defaultPhysicsScene,"isolated preview physics scene");
                var floor=new GameObject("Check floor");SceneManager.MoveGameObjectToScene(floor,test);
                floor.transform.position=new Vector3(0,-1,0);var col=floor.AddComponent<BoxCollider>();col.size=new Vector3(500,2,500);
                var root=UnityEngine.Object.Instantiate(tuning.VehicleController.gameObject);
                SceneManager.MoveGameObjectToScene(root,test);root.name="Check player";
                root.transform.SetPositionAndRotation(new Vector3(0,1.5f,0),Quaternion.identity);
                var car=root.GetComponent<JrsVehicleController>();var rb=root.GetComponent<Rigidbody>();
                if(choice!=RallyGameSession.VehicleName)RallyPlayerVehicleSelection.ApplyVisual(car,choice);
                var rig=new GameObject("Check dynamics");SceneManager.MoveGameObjectToScene(rig,test);
                var dynamics=rig.AddComponent<RallyVehicleDynamics>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(tuning),dynamics);
                dynamics.BindVehicle(car);var input=root.GetComponent<RallyLocalPlayerInput>()??root.AddComponent<RallyLocalPlayerInput>();
                car.SetLocalInput(input);dynamics.SetLocalInput(input);dynamics.ApplySetup();
                typeof(JrsVehicleController).GetField("rb",Private).SetValue(car,rb);
                Require(Mathf.Abs(rb.mass-1160)<.01f && Mathf.Abs(rb.angularDamping-2.6f)<.01f,choice+" uses same lighter player tune");
                var keyboard=GameObject.Find("Circuit Keyboard Input").GetComponent<JrsInputController>();
                Require(Mathf.Approximately(keyboard.steerSpeed,22),"player keyboard steering response is 22");
                var bot=root.AddComponent<RallyBotController>();
                var botSetup=new SerializedObject(dynamics);botSetup.FindProperty("steeringResponse").floatValue=6;
                botSetup.ApplyModifiedPropertiesWithoutUndo();dynamics.ApplySetup();
                Require(Mathf.Approximately(keyboard.steerSpeed,22),"bot component cannot overwrite keyboard response before input binding");
                dynamics.SetBotInput(bot);dynamics.ApplySetup();
                Require(Mathf.Approximately(keyboard.steerSpeed,22),"bound bot cannot overwrite player keyboard response");
                dynamics.SetBotInput(null);UnityEngine.Object.DestroyImmediate(bot);
                botSetup.Update();botSetup.FindProperty("steeringResponse").floatValue=22;
                botSetup.ApplyModifiedPropertiesWithoutUndo();dynamics.ApplySetup();
                var wheels=new[]{car.frontLeftWheel,car.frontRightWheel,car.rearLeftWheel,car.rearRightWheel};
                Input(input,0,0);for(int i=0;i<100;i++)Step(physics,car,dynamics);
                Require(wheels.Count(w=>w.isGrounded)==4,choice+" has four grounded wheels on test surface");
                Vector3 pose=rb.position;Quaternion rotation=rb.rotation;
                rb.linearVelocity=rb.transform.forward*(110f/3.6f);rb.angularVelocity=Vector3.zero;
                Input(input,0,1);Step(physics,car,dynamics);
                Require(dynamics.AutomaticBrakeAmount==0 && wheels.Any(w=>w.motorTorque>0),choice+" accelerates without automatic braking in straight line");
                rb.position=pose;rb.rotation=rotation;rb.linearVelocity=rb.transform.forward*(110f/3.6f);rb.angularVelocity=Vector3.zero;
                Input(input,.65f,1);
                float maxAuto=0,maxYaw=0;int grounded=0;float firstAuto=0;
                for(int i=0;i<150;i++)
                {
                    Step(physics,car,dynamics);
                    if(i==0)firstAuto=dynamics.AutomaticBrakeAmount;
                    maxAuto=Mathf.Max(maxAuto,dynamics.AutomaticBrakeAmount);
                    maxYaw=Mathf.Max(maxYaw,Mathf.Abs(Vector3.Dot(rb.angularVelocity,rb.transform.up)));
                    if(wheels.Count(w=>w.isGrounded)>=3)grounded++;
                }
                float finalSpeed=Vector3.ProjectOnPlane(rb.linearVelocity,Vector3.up).magnitude*3.6f;
                float turn=Quaternion.Angle(rotation,rb.rotation);
                report.Add($"METRICS {choice}: 110 -> {finalSpeed:F1} km/h; rotation={turn:F1} deg; peak auto={maxAuto:F3}; peak yaw={maxYaw:F3} rad/s; grounded={grounded}/150");
                Require(maxAuto>.1f && firstAuto<=4f*Time.fixedDeltaTime+.0001f,choice+" corner brake engages progressively while throttle remains held");
                Require(finalSpeed<105 && finalSpeed>35,choice+" slows to a playable corner speed without stopping");
                Require(turn>15 && maxYaw<2.5f && grounded>=135,choice+" turns and retains contact without uncontrolled yaw");
                Input(input,0,1);for(int i=0;i<15;i++)Step(physics,car,dynamics);
                Require(dynamics.AutomaticBrakeAmount==0 && wheels.Any(w=>w.motorTorque>0),choice+" releases corner brake and restores drive after steering release");
                Input(input,0,-1,1);Step(physics,car,dynamics);
                Require(dynamics.AutomaticBrakeAmount==0 && wheels.All(w=>Mathf.Approximately(w.motorTorque,0)),choice+" manual brake retains priority");
                Input(input,.7f,1,0,true);Step(physics,car,dynamics);
                Require(dynamics.AutomaticBrakeAmount==0 && wheels.Take(2).All(w=>w.brakeTorque==0) && wheels.Skip(2).All(w=>w.brakeTorque>0),choice+" handbrake retains rear-only braking and bypasses automatic brake");
                EditorSceneManager.ClosePreviewScene(test);test=default;
            }
            report.Add("COMPLETE: PASS");return string.Join("\n",report);
        }
        catch(Exception e){report.Add("FAIL: "+e);throw;}
        finally
        {
            if(test.IsValid()&&test.isLoaded)EditorSceneManager.ClosePreviewScene(test);
            if(opened)EditorSceneManager.CloseScene(source,true);
            if(original.isLoaded)SceneManager.SetActiveScene(original);
            File.WriteAllText("Logs/light-arcade-physics-check.txt",string.Join("\n",report));
        }
    }
}
