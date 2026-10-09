using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Waits for Terrain Details to populate before each capture. Does not save scenes.
public static class Circuit02SceneryPreview
{
 static SceneSetup[] previous;static Camera camera;static RenderTexture target;static int frame,index;
 static Vector3[] positions,look;static readonly string[] Names={"start","climb","hairpin","return","overview"};
 const string Log="Logs/Circuit02SceneryUpgrade";
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
  Directory.CreateDirectory(Log);previous=EditorSceneManager.GetSceneManagerSetup();EditorSceneManager.OpenScene("Assets/Scenes/Circuit_02.unity");
  var terrain=Object.FindObjectsByType<Terrain>().Single();var w=GameObject.Find("AI_Waypoints").transform;
  positions=new Vector3[5];look=new Vector3[5];int[] ids={3,28,60,104};
  for(int i=0;i<ids.Length;i++){var p=w.GetChild(ids[i]).position;positions[i]=p+Vector3.up*2.5f;look[i]=w.GetChild((ids[i]+3)%w.childCount).position+Vector3.up*2;}
  positions[0]=new Vector3(0,3,42);look[0]=positions[0]+new Vector3(-.15f,0,1)*40;
  positions[4]=new Vector3(-70,230,-185);look[4]=new Vector3(160,5,70);
  camera=new GameObject("Temporary serrano preview",typeof(Camera)).GetComponent<Camera>();camera.fieldOfView=65;camera.farClipPlane=650;camera.useOcclusionCulling=false;
  var extra=camera.GetUniversalAdditionalCameraData();extra.requiresColorTexture=true;extra.requiresDepthTexture=true;
  target=new RenderTexture(1440,900,24);camera.targetTexture=target;index=0;frame=0;Pose();File.WriteAllText(Log+"/preview.txt","RUNNING\n");EditorApplication.update+=Tick;
 }
 static void Pose(){camera.transform.SetPositionAndRotation(positions[index],Quaternion.LookRotation(look[index]-positions[index]));frame=0;}
 static void Tick()
 {
  try {
   EditorApplication.QueuePlayerLoopUpdate();camera.Render();if(++frame<24)return;
   var old=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
   try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Log+"/after-"+Names[index]+".png",texture.EncodeToPNG());}
   finally{RenderTexture.active=old;Object.DestroyImmediate(texture);}
   File.AppendAllText(Log+"/preview.txt","Captured "+Names[index]+"\n");if(++index<positions.Length){Pose();return;}
   File.AppendAllText(Log+"/preview.txt","COMPLETE: PASS\n");Cleanup();
  }catch(Exception e){File.AppendAllText(Log+"/preview.txt","FAILED: "+e);Cleanup();}
 }
 static void Cleanup(){EditorApplication.update-=Tick;if(camera!=null){camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);}if(target!=null){target.Release();Object.DestroyImmediate(target);}EditorSceneManager.RestoreSceneManagerSetup(previous);}
}
