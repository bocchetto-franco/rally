using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class Circuit02SierrasBake
{
 static SceneSetup[] previous;
 static bool active;
 static DateTime started;
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor unavailable for bake.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
  previous=EditorSceneManager.GetSceneManagerSetup();
  EditorSceneManager.OpenScene("Assets/Scenes/Circuit_02.unity");
  string path="Assets/Scenes/Circuit_02/OcclusionCullingData.asset";
  if(File.Exists(path))File.Copy(path,"Logs/Circuit02_Sierras_PreviousOcclusion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".asset");
  File.WriteAllText("Logs/circuit02-sierras-bake.txt","RUNNING: current Circuit_02 scenery\n");
  started=DateTime.UtcNow;
  if(!StaticOcclusionCulling.GenerateInBackground()){EditorSceneManager.RestoreSceneManagerSetup(previous);throw new Exception("Could not start occlusion bake.");}
  active=true;EditorApplication.update+=Tick;
 }
 static void Tick()
 {
  if(!active||StaticOcclusionCulling.isRunning)return;
  active=false;EditorApplication.update-=Tick;
  try {
   if(SceneManager.GetActiveScene().path!="Assets/Scenes/Circuit_02.unity")throw new Exception("Active scene changed during bake.");
   if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Could not save bake.");
   if(!File.Exists("Assets/Scenes/Circuit_02/OcclusionCullingData.asset"))throw new Exception("Missing occlusion output.");
   var output=new FileInfo("Assets/Scenes/Circuit_02/OcclusionCullingData.asset");
   if(output.Length==0||output.LastWriteTimeUtc<started)throw new Exception("Occlusion output was not regenerated.");
   File.AppendAllText("Logs/circuit02-sierras-bake.txt","COMPLETE: PASS\n");
  }catch(Exception e){File.AppendAllText("Logs/circuit02-sierras-bake.txt","FAILED: "+e+"\n");}
  finally{EditorSceneManager.RestoreSceneManagerSetup(previous);}
 }
}
