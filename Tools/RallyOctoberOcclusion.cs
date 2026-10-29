using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RallyOctoberOcclusion
{
    const string Report="Logs/Gameplay_20261028/occlusion.txt";
    static SceneSetup[] previous;
    static int index;
    static DateTime started;
    static bool active;
    [MenuItem("Tools/Rally/Rebake October Circuits")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning)throw new Exception("Editor unavailable");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");
        previous=EditorSceneManager.GetSceneManagerSetup();index=0;active=true;
        File.WriteAllText(Report,"Rebake after October road widening\n");
        EditorApplication.update+=Tick;Begin();
    }
    static void Begin()
    {
        string name="Circuit_0"+(index+1);EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
        // MakeUnique rebuilt native road meshes; keep their triangle cooking prepaid in the build.
        foreach(var collider in UnityEngine.Object.FindObjectsByType<MeshCollider>())
        {
            if(collider.sharedMesh==null)continue;
            string meshPath=AssetDatabase.GetAssetPath(collider.sharedMesh);
            if(!string.IsNullOrEmpty(meshPath) && meshPath!=SceneManager.GetActiveScene().path)continue;
            var data=new SerializedObject(collider.sharedMesh);
            var flag=data.FindProperty("m_PreBakeTriangleCollisionMesh");
            if(flag!=null && !flag.boolValue){flag.boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();}
        }
        string path="Assets/Scenes/"+name+"/OcclusionCullingData.asset";
        if(File.Exists(path))File.Copy(path,"Logs/Gameplay_20261028/"+name+"_PreviousOcclusion.asset",true);
        started=DateTime.UtcNow;
        if(!StaticOcclusionCulling.GenerateInBackground())throw new Exception("Could not start bake "+name);
    }
    static void Tick()
    {
        if(!active || StaticOcclusionCulling.isRunning)return;
        try
        {
            string name="Circuit_0"+(index+1),path="Assets/Scenes/"+name+"/OcclusionCullingData.asset";
            if(SceneManager.GetActiveScene().name!=name)throw new Exception("Scene changed during bake");
            if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Save failed");
            var file=new FileInfo(path);
            if(!file.Exists || file.Length==0 || file.LastWriteTimeUtc<started)throw new Exception("Stale bake "+name);
            File.AppendAllText(Report,"PASS: "+name+" "+file.Length+" bytes\n");
            if(++index<3){Begin();return;}
            File.AppendAllText(Report,"COMPLETE: PASS\n");Stop();
        }
        catch(Exception e){File.AppendAllText(Report,"FAIL: "+e+"\n");Stop();}
    }
    static void Stop(){active=false;EditorApplication.update-=Tick;EditorSceneManager.RestoreSceneManagerSetup(previous);}
}
