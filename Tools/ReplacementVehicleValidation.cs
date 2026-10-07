using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReplacementVehicleValidation
{
 public static void Run()
 {
  if (Application.isPlaying) throw new Exception("Run outside Play.");
  File.WriteAllText("Logs/replacement-vehicle-validation.txt","RUNNING\n");
  var scene=SceneManager.GetActiveScene();
  var sources=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<JrsVehicleController>(true)).ToArray();
  var source=sources.First(c=>c.name==RallyGameSession.VehicleName);
  var dynamics=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RallyVehicleDynamics>(true)).First(d=>d.name=="Porsche Rally Dynamics");
  string original=EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.GetComponent<Rigidbody>());
  var report=new System.Text.StringBuilder();
  foreach (string choice in RallyPlayerVehicleSelection.Names.Skip(1))
  foreach (bool bot in new[]{false,true})
  {
   var template=bot?sources.First(c=>c.GetComponentInParent<RallyBotController>()!=null):source;
   var staging=new GameObject("Replacement validation staging"); staging.SetActive(false);
   GameObject clone=null;
   try {
    clone=UnityEngine.Object.Instantiate(bot?template.GetComponentInParent<RallyBotController>().gameObject:template.gameObject,staging.transform);
    var car=clone.GetComponentInChildren<JrsVehicleController>(true);
    // These snapshots operate on the same inactive clone; no scene objects are saved.
    var rb=car.GetComponent<Rigidbody>(); var colliders=car.GetComponentsInChildren<Collider>(true);
    string before=RallyVehiclePhysicsParityTest.Snapshot(car,dynamics,rb,colliders);
    var visual=bot?RallyPlayerVehicleSelection.ApplyBotVisual(car,choice):RallyPlayerVehicleSelection.ApplyVisual(car,choice);
    if(before!=RallyVehiclePhysicsParityTest.Snapshot(car,dynamics,rb,colliders)) throw new Exception("Tuning changed: bot="+bot);
    if(!visual.useAuthoredScale || visual.transform.localScale!=Vector3.one) throw new Exception("Authored scale not preserved");
    var wheels=new[]{car.frontLeftWheel,car.frontRightWheel,car.rearLeftWheel,car.rearRightWheel};
    for(int i=0;i<4;i++) {
     if(Vector3.Distance(wheels[i].transform.TransformPoint(wheels[i].center),visual.wheels[i].position)>.001f) throw new Exception("Axle mismatch");
     if(Mathf.Abs(wheels[i].radius-visual.radii[i])>.001f) throw new Exception("Radius mismatch");
     if(visual.wheels[i].localScale!=Vector3.one) throw new Exception("Wheel stretched");
    }
    if(visual.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>m==null||m.shader.name!="Universal Render Pipeline/Lit"))) throw new Exception("Missing/non-URP material");
    if(visual.bodyBounds.size.z<4f||visual.bodyBounds.size.z>4.31f) throw new Exception("Wrong length");
    if(car.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled&&!r.transform.IsChildOf(visual.transform))) throw new Exception("Old mesh still visible");
    report.AppendLine("PASS "+choice+" "+(bot?"bot grip":"player tuning")+" preserved, 4 aligned wheels, uniform scale, body="+visual.bodyBounds.size+", radii="+string.Join(",",visual.radii));
   } finally {UnityEngine.Object.DestroyImmediate(staging);}
  }
  if(original!=EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.GetComponent<Rigidbody>())) throw new Exception("Original Porsche changed");
  report.AppendLine("PASS Original Porsche unchanged; no scene saved.");
  File.WriteAllText("Logs/replacement-vehicle-validation.txt",report.ToString());
  Debug.Log(report.ToString());
 }
}
