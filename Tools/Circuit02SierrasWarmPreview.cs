using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class Circuit02SierrasWarmPreview
{
 static SceneSetup[] previous;static Camera camera;static RenderTexture target;static int frames;
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
  previous=EditorSceneManager.GetSceneManagerSetup();EditorSceneManager.OpenScene("Assets/Scenes/Circuit_02.unity");
  camera=new GameObject("Temporary warm scenery camera",typeof(Camera)).GetComponent<Camera>();
  camera.transform.SetPositionAndRotation(new Vector3(0,3,42),Quaternion.LookRotation(new Vector3(-.15f,0,1)));camera.fieldOfView=65;camera.farClipPlane=450;camera.useOcclusionCulling=false;
  var extra=camera.GetUniversalAdditionalCameraData();extra.requiresDepthTexture=true;extra.requiresColorTexture=true;
  target=new RenderTexture(1280,800,24);camera.targetTexture=target;frames=0;EditorApplication.update+=Tick;File.WriteAllText("Logs/circuit02-sierras-preview.txt","RUNNING\n");
 }
 static void Tick(){
  try{
   EditorApplication.QueuePlayerLoopUpdate();camera.Render();if(++frames<20)return;
   var old=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
   try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes("Logs/Circuit02_Sierras_Warm.png",image.EncodeToPNG());}
   finally{RenderTexture.active=old;Object.DestroyImmediate(image);}
   File.AppendAllText("Logs/circuit02-sierras-preview.txt","COMPLETE: PASS\n");Cleanup();
  }catch(Exception e){File.AppendAllText("Logs/circuit02-sierras-preview.txt",e+"\n");Cleanup();}
 }
 static void Cleanup(){EditorApplication.update-=Tick;if(camera!=null){camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);}if(target!=null){target.Release();Object.DestroyImmediate(target);}EditorSceneManager.RestoreSceneManagerSetup(previous);}
}
