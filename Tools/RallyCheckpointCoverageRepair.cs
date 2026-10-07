using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
public static class RallyCheckpointCoverageRepair
{
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene; refusing to replace it.");
  string backup="Logs/SceneBackups/CheckpointCoverage_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
  File.WriteAllText("Logs/checkpoint-coverage-repair.txt","Checkpoint coverage repair\nBackup: "+backup+"\n");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  try {
   foreach(var name in new[]{"Circuit_01","Circuit_02","Circuit_03"})
   {
    string path="Assets/Scenes/"+name+".unity";File.Copy(path,backup+"/"+name+".unity");
    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
    var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RallyCheckpointManager>(true)).Single();
    var gates=manager.OrderedCheckpoints;
    string before=Protected(scene);
    string roadName=name=="Circuit_01"?"Rally_Road_Start_to_Finish":name.Replace("_","")+"_Road";
    var road=GameObject.Find(roadName).GetComponent<ProBuilderMesh>();
    var vertices=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
    for(int i=0;i<gates.Length;i++)
    {
     var gate=gates[i];if(gate==null||gate.CheckpointIndex!=i)throw new Exception(name+": invalid checkpoint reference/index "+i);
     var box=gate.GetComponent<BoxCollider>();
     float measuredHalfWidth=0;
     foreach(var p in vertices)
     {
      var delta=p-gate.transform.position;
      float side=Vector3.Dot(delta,gate.transform.right);
      if(Mathf.Abs(Vector3.Dot(delta,gate.transform.forward))<2 && Mathf.Abs(side)<9)
       measuredHalfWidth=Mathf.Max(measuredHalfWidth,Mathf.Abs(side));
     }
     if(measuredHalfWidth<2)throw new Exception(name+": cannot measure road at checkpoint "+i);
     // Authored roads are 7–12 m (bridge 10.4 m). Bounded sampling avoids nearby lanes.
     float width=Mathf.Clamp(measuredHalfWidth*2,7,13)+14;
     var oldSize=box.size;
     // Preserve gate Transform (including reset pose and visible start/finish lines).
     if(box.size.y<8)box.center+=Vector3.up*((8-box.size.y)*.5f-.5f);
     box.size=new Vector3(Mathf.Max(width,box.size.x),Mathf.Max(8,box.size.y),Mathf.Max(6,box.size.z));
     box.isTrigger=true;box.enabled=true;
     File.AppendAllText("Logs/checkpoint-coverage-repair.txt",name+" gate "+i+": "+oldSize.ToString("F2")+" -> "+box.size.ToString("F2")+"; ordered reference retained\n");
    }
    if(before!=Protected(scene))throw new Exception(name+": unrelated scene components changed; not saved.");
    Physics.SyncTransforms();
    for(int i=0;i<gates.Length;i++)for(int j=i+1;j<gates.Length;j++)
    {
     var a=gates[i].GetComponent<BoxCollider>();var b=gates[j].GetComponent<BoxCollider>();
     if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out _))
      throw new Exception(name+": overlapping checkpoints "+i+"/"+j);
    }
    EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save "+name);
    File.AppendAllText("Logs/checkpoint-coverage-repair.txt",name+": PASS protected scene components/Transforms unchanged; no gate overlap.\n");
   }
   File.AppendAllText("Logs/checkpoint-coverage-repair.txt","COMPLETE: PASS\n");
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 static string Protected(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null && !(c is BoxCollider b && b.GetComponent<RallyCheckpointTrigger>()!=null)).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)));
}
