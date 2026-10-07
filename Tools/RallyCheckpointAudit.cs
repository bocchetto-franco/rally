using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class RallyCheckpointAudit
{
 public static void Run()
 {
  var report=new StringBuilder();
  foreach(var name in new[]{"Circuit_01","Circuit_02","Circuit_03"})
  {
   var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+name+".unity");
   try {
    var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    var route=all.First(t=>t.name=="AI_Waypoints");
    var points=route.Cast<Transform>().Select(t=>t.position).ToArray();
    var manager=all.Select(t=>t.GetComponent<RallyCheckpointManager>()).First(m=>m!=null);
    var gates=manager.OrderedCheckpoints;
    var flow=all.Select(t=>t.GetComponent<RallyRaceFlow>()).First(f=>f!=null);
    var refManager=new SerializedObject(flow).FindProperty("checkpointManager").objectReferenceValue;
    report.AppendLine(name+": "+gates.Length+" gates; flow manager correct="+(refManager==manager));
    report.AppendLine("Scene gates="+all.Count(t=>t.GetComponent<RallyCheckpointTrigger>()!=null));
    foreach(var gate in gates)
    {
     if(gate==null){report.AppendLine("NULL gate");continue;}
     var box=gate.GetComponent<BoxCollider>();
     var proj=Project(points,gate.transform.position);
     var bodyPoint=proj.point+Vector3.up*.7f;
     var local=box.transform.InverseTransformPoint(bodyPoint)-box.center;
     bool inside=Mathf.Abs(local.x)<=box.size.x*.5f&&Mathf.Abs(local.y)<=box.size.y*.5f&&Mathf.Abs(local.z)<=box.size.z*.5f;
     var assigned=new SerializedObject(gate).FindProperty("manager").objectReferenceValue;
     report.AppendLine($"  {gate.CheckpointIndex}: {gate.name}; position={gate.transform.position:F2}; station={proj.station:F2}; lateral/height error={(gate.transform.position-proj.point):F2}; forward dot={Vector3.Dot(gate.transform.forward,proj.forward):F3}; size={box.size:F2}; center travel inside={inside}; trigger={box.isTrigger}; active={gate.isActiveAndEnabled}; manager={assigned==manager}");
    }
    foreach(var t in all.Where(t=>t.name.StartsWith("Grid_")))
    {
     var p=Project(points,t.position);
     report.AppendLine($"  {t.name}: station={p.station:F2}; delta from start gate={Vector3.Dot(t.position-gates[0].transform.position,gates[0].transform.forward):F2}m");
    }
   }finally{EditorSceneManager.ClosePreviewScene(scene);}
  }
  Directory.CreateDirectory("Logs");File.WriteAllText("Logs/checkpoint-audit.txt",report.ToString());
 }
 public static (float station,Vector3 point,Vector3 forward) Project(Vector3[] points,Vector3 p)
 {
  float best=float.MaxValue,station=0,total=0;Vector3 point=default,forward=default;
  for(int i=0;i<points.Length;i++)
  {
   var a=points[i];var d=points[(i+1)%points.Length]-a;float length=d.magnitude;
   if(length<.001f)continue;
   var horizontal=Vector3.ProjectOnPlane(d,Vector3.up);float t=Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(p-a,Vector3.up),horizontal)/horizontal.sqrMagnitude);
   var q=a+d*t;float sq=Vector3.ProjectOnPlane(q-p,Vector3.up).sqrMagnitude;
   if(sq<best){best=sq;station=total+length*t;point=q;forward=d.normalized;}total+=length;
  }return(station,point,forward);
 }
}
