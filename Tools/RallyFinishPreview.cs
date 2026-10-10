using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class RallyFinishPreview
{
    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save open scenes.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach(string name in RallyGameSession.CircuitScenes)
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
                var arch=scene.GetRootGameObjects().First(g=>g.name=="Finish - Checkered Rally Arch").transform;
                var go=new GameObject("Finish preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();
                go.transform.SetPositionAndRotation(arch.position-arch.forward*33f+arch.right*4+Vector3.up*7,
                    Quaternion.LookRotation(arch.position+Vector3.up*3.5f-(arch.position-arch.forward*33f+arch.right*4+Vector3.up*7)));
                camera.fieldOfView=55;camera.farClipPlane=450;camera.allowHDR=false;camera.allowMSAA=false;
                var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/"+name+"_FinishArch.png",texture.EncodeToPNG());}
                finally {RenderTexture.active=old;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);}
            }
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        return "Three finish previews captured; no scenes saved.";
    }
}
