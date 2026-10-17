using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class OffTrackPenaltyPhysicsCheck
{
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<string> report=new List<string>();
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS: "+message);}
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new Exception("Editor must be idle");
  report.Clear();var source=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_03.unity");Scene test=default;
  try {
   var tuning=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RallyVehicleDynamics>(true)).Single(d=>d.name=="Porsche Rally Dynamics"&&d.VehicleController!=null);
   foreach(string choice in RallyPlayerVehicleSelection.Names) {
    test=EditorSceneManager.NewPreviewScene();var physics=test.GetPhysicsScene();
    var terrain=new GameObject("off-road ground");SceneManager.MoveGameObjectToScene(terrain,test);terrain.transform.position=new Vector3(0,-.52f,0);terrain.AddComponent<BoxCollider>().size=new Vector3(80,1,200);
    var road=new GameObject("Circuit03_Road");SceneManager.MoveGameObjectToScene(road,test);road.transform.position=new Vector3(0,-.5f,0);road.AddComponent<BoxCollider>().size=new Vector3(8,1,200);
    var root=Object.Instantiate(tuning.VehicleController.gameObject);SceneManager.MoveGameObjectToScene(root,test);var car=root.GetComponent<JrsVehicleController>();var body=root.GetComponent<Rigidbody>();
    if(choice!=RallyGameSession.VehicleName)RallyPlayerVehicleSelection.ApplyVisual(car,choice);
    var rig=new GameObject("test dynamics");SceneManager.MoveGameObjectToScene(rig,test);var dynamics=rig.AddComponent<RallyVehicleDynamics>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(tuning),dynamics);dynamics.BindVehicle(car);dynamics.ApplySetup();
    typeof(RallyVehicleDynamics).GetMethod("Start",Private).Invoke(dynamics,null);
    var penalty=root.GetComponent<RallyOffTrackSpeedPenalty>();
    Require(penalty.enabled&&Mathf.Approximately(penalty.RetainedSpeed,.75f),choice+" receives new 75% rule");
    var tick=typeof(RallyOffTrackSpeedPenalty).GetMethod("FixedUpdate",Private);
    body.position=new Vector3(0,.7f,0);body.rotation=Quaternion.identity;body.isKinematic=false;
    foreach(var wheel in root.GetComponentsInChildren<WheelCollider>()){wheel.motorTorque=wheel.brakeTorque=wheel.steerAngle=0;}
    Action step=()=>{physics.Simulate(.02f);tick.Invoke(penalty,null);};
    for(int i=0;i<90;i++)step();
    Require(penalty.AppliedCount==0&&!penalty.IsOffTrack,"road itself is not penalized");
    float height=body.position.y;body.position=new Vector3(8,height,0);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.forward*(100f/3.6f);body.angularVelocity=Vector3.zero;
    bool applied=false;
    for(int i=0;i<50;i++) {
     physics.Simulate(.02f);var velocity=body.linearVelocity;var angular=body.angularVelocity;int count=penalty.AppliedCount;tick.Invoke(penalty,null);
     if(penalty.AppliedCount>count){
      float ratio=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude/Vector3.ProjectOnPlane(velocity,Vector3.up).magnitude;
      Require(Mathf.Abs(ratio-.75f)<.00001f,"road exit retains exactly 75% horizontal velocity");
      Require(Mathf.Abs(body.linearVelocity.y-velocity.y)<.00001f&&body.angularVelocity==angular,"vertical speed and angular velocity preserved");applied=true;
     }
    }
    Require(applied&&penalty.AppliedCount==1,"staying off road does not compound the 25% loss");
    body.position=new Vector3(0,height,0);body.rotation=Quaternion.identity;body.linearVelocity=body.angularVelocity=Vector3.zero;for(int i=0;i<90;i++)step();
    Require(!penalty.IsOffTrack,"returning to road rearms the next exit");
    body.position=new Vector3(8,10,0);body.linearVelocity=Vector3.zero;for(int i=0;i<10;i++)step();
    Require(penalty.AppliedCount==1,"airborne car is not penalized");
    body.position=new Vector3(8,height,0);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.forward*20;for(int i=0;i<30;i++)step();
    Require(penalty.AppliedCount==2,"a second distinct exit applies once again");
    penalty.Configure(car,dynamics);car.enabled=false;body.linearVelocity=Vector3.forward*20;var before=body.linearVelocity;tick.Invoke(penalty,null);Require(body.linearVelocity==before,"disabled/finished player not modified");car.enabled=true;
    var bot=root.AddComponent<RallyBotController>();penalty.Configure(car,dynamics);Require(!penalty.enabled,"bot profile excluded from the new player rule");Object.DestroyImmediate(bot);
    EditorSceneManager.ClosePreviewScene(test);test=default;
   }
   foreach(string path in new[]{"Assets/Scenes/Circuit_01.unity","Assets/Scenes/Circuit_02.unity","Assets/Scenes/Circuit_03.unity"}) {
    var scene=EditorSceneManager.OpenPreviewScene(path);
    try {var dynamics=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RallyVehicleDynamics>(true)).Single(d=>d.name=="Porsche Rally Dynamics"&&d.VehicleController!=null);
     var p=dynamics.VehicleController.gameObject.AddComponent<RallyOffTrackSpeedPenalty>();p.Configure(dynamics.VehicleController,dynamics);Require(p.enabled,path+" detects authored road surfaces");
     if(path.Contains("03")) {
      var deck=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>(true)).Single(c=>c.name=="Continuous smooth deck collider");
      var surfaces=(HashSet<Collider>)typeof(RallyOffTrackSpeedPenalty).GetField("roadSurfaces",Private).GetValue(p);Require(surfaces.Contains(deck),"bridge deck remains on-road, not a penalty surface");
     }
    }finally {EditorSceneManager.ClosePreviewScene(scene);}
   }
   report.Add("COMPLETE: PASS");
  }catch(Exception e){report.Add("FAIL: "+e);throw;}
  finally {if(test.IsValid()&&test.isLoaded)EditorSceneManager.ClosePreviewScene(test);EditorSceneManager.ClosePreviewScene(source);File.WriteAllLines("Logs/off-track-penalty-physics.txt",report);}
 }
}
