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

public static class BridgeAndSurfacePhysicsCheck
{
 [Serializable] class Layout {public Vector3[] centerline=Array.Empty<Vector3>();public float[] distances=Array.Empty<float>();}
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<string> report=new List<string>();
 static bool integrated;
 public static void RunIntegrated() {integrated=true;try{Run();}finally{integrated=false;}}
 static void Step(JrsVehicleController car,RallyVehicleDynamics dynamics,PhysicsScene physics)
 {
  typeof(JrsVehicleController).GetMethod("FixedUpdate",Private).Invoke(car,null);
  typeof(RallyVehicleDynamics).GetMethod("FixedUpdate",Private).Invoke(dynamics,null);
  var penalty=car.GetComponent<RallyOffTrackSpeedPenalty>();
  if(penalty!=null)typeof(RallyOffTrackSpeedPenalty).GetMethod("FixedUpdate",Private).Invoke(penalty,null);
  physics.Simulate(.02f);
 }
 static void Require(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS: "+text);}
 static void Input(RallyLocalPlayerInput input,float steer,float throttle)
 {
  foreach(var pair in new[]{("Horizontal",steer),("Vertical",throttle),("Brake",0f)})
   typeof(RallyLocalPlayerInput).GetProperty(pair.Item1).GetSetMethod(true).Invoke(input,new object[]{pair.Item2});
 }
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new Exception("Editor must be idle");
  report.Clear();
  var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_03.unity");
  try {
   var roots=scene.GetRootGameObjects();var physics=scene.GetPhysicsScene();
   Require(physics!=Physics.defaultPhysicsScene,"isolated real WheelCollider physics, no changes to live scene");
   foreach(var rb in roots.SelectMany(r=>r.GetComponentsInChildren<Rigidbody>(true)))rb.isKinematic=true;
   var tuning=roots.SelectMany(r=>r.GetComponentsInChildren<RallyVehicleDynamics>(true)).Single(d=>d.name=="Porsche Rally Dynamics"&&d.VehicleController!=null);
   var bridge=roots.Single(r=>r.name=="Circuit 03 - Timber River Crossing").transform;
   var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Art/Forest/Circuit03/Circuit03Layout.json"));
   Func<float,Vector3> point=s=>{int i=Array.FindLastIndex(layout.distances,d=>d<=s);i=Mathf.Clamp(i,0,layout.centerline.Length-2);return Vector3.Lerp(layout.centerline[i],layout.centerline[i+1],Mathf.InverseLerp(layout.distances[i],layout.distances[i+1],s));};
   foreach(float lane in new[]{0f,-2.5f,2.5f}) {
    var root=Object.Instantiate(tuning.VehicleController.gameObject);SceneManager.MoveGameObjectToScene(root,scene);
    var car=root.GetComponent<JrsVehicleController>();var body=root.GetComponent<Rigidbody>();body.isKinematic=false;
    var rig=new GameObject("isolated bridge dynamics");SceneManager.MoveGameObjectToScene(rig,scene);
    var dynamics=rig.AddComponent<RallyVehicleDynamics>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(tuning),dynamics);dynamics.BindVehicle(car);
    var input=root.GetComponent<RallyLocalPlayerInput>()??root.AddComponent<RallyLocalPlayerInput>();car.SetLocalInput(input);dynamics.SetLocalInput(input);dynamics.ApplySetup();
    if(integrated)typeof(RallyVehicleDynamics).GetMethod("Start",Private).Invoke(dynamics,null);
    typeof(JrsVehicleController).GetField("rb",Private).SetValue(car,body);
    float station=850;var f=(point(station+1)-point(station)).normalized;var r=Vector3.Cross(Vector3.up,f).normalized;
    body.position=point(station)+r*lane+Vector3.up*.65f;body.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(f,Vector3.up));body.linearVelocity=body.transform.forward*15;
    bool crossed=false;float minSpeed=999,maxLateral=0;
    for(int step=0;step<1200;step++) {
     station=Enumerable.Range(0,layout.centerline.Length).Where(i=>layout.distances[i]>station-8&&layout.distances[i]<station+20).OrderBy(i=>(layout.centerline[i]-body.position).sqrMagnitude).Select(i=>layout.distances[i]).First();
     var target=point(station+7);var direction=(point(station+8)-point(station+6)).normalized;target+=Vector3.Cross(Vector3.up,direction).normalized*lane;
     var aim=body.transform.InverseTransformPoint(target);float steer=Mathf.Clamp(Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg/40,-1,1);
     float speed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude*3.6f;
     Input(input,steer,Mathf.Clamp01((75-speed)/20));
     Step(car,dynamics,physics);
     if(station>870&&station<930){minSpeed=Mathf.Min(minSpeed,speed);maxLateral=Mathf.Max(maxLateral,Mathf.Abs(bridge.InverseTransformPoint(body.position).x));}
     if(station>=947){crossed=true;break;}
     if(body.position.y<bridge.position.y-5)throw new Exception("Vehicle fell below river at step "+step);
    }
    report.Add($"METRICS lane={lane}: crossed={crossed}, minimum speed={minSpeed:F2}km/h, lateral={maxLateral:F2}m, final={body.position}");
    Require(crossed&&minSpeed>15,"actual car crossed approach/deck/exit without getting stuck, lane "+lane);
    if(integrated)Require(car.GetComponent<RallyOffTrackSpeedPenalty>().AppliedCount==0,"bridge route does not activate off-road penalty, lane "+lane);
    Object.DestroyImmediate(root);Object.DestroyImmediate(rig);
   }
   if(integrated){report.Add("COMPLETE: PASS");return;}
   var speeds=new List<float>();
   foreach(float offset in new[]{0f,8f}) {
    var root=Object.Instantiate(tuning.VehicleController.gameObject);SceneManager.MoveGameObjectToScene(root,scene);
    var car=root.GetComponent<JrsVehicleController>();var body=root.GetComponent<Rigidbody>();body.isKinematic=false;
    var rig=new GameObject("isolated surface dynamics");SceneManager.MoveGameObjectToScene(rig,scene);
    var dynamics=rig.AddComponent<RallyVehicleDynamics>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(tuning),dynamics);dynamics.BindVehicle(car);
    var input=root.GetComponent<RallyLocalPlayerInput>()??root.AddComponent<RallyLocalPlayerInput>();car.SetLocalInput(input);dynamics.SetLocalInput(input);dynamics.ApplySetup();
    typeof(JrsVehicleController).GetField("rb",Private).SetValue(car,body);
    body.position=new Vector3(offset,.65f,25);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.forward*(50f/3.6f);
    Input(input,0,1);
    string support="";
    for(int i=0;i<100;i++) {
     Step(car,dynamics,physics);
     if(car.frontLeftWheel.GetGroundHit(out var hit))support=hit.collider.name;
    }
    float speed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude*3.6f;speeds.Add(speed);
    report.Add($"SURFACE offset={offset}: 50 -> {speed:F2}km/h, support={support}, automaticBrake={dynamics.AutomaticBrakeAmount:F3}, position={body.position}");
    Object.DestroyImmediate(root);Object.DestroyImmediate(rig);
   }
   report.Add($"SURFACE measured off-road/on-road ratio = {speeds[1]/speeds[0]:F3}; not a configured speed penalty.");
   report.Add("COMPLETE: PASS");
  }catch(Exception e){report.Add("FAIL: "+e);throw;}
  finally {EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines(integrated?"Logs/bridge-penalty-integration.txt":"Logs/bridge-isolated-physics.txt",report);}
 }
}
