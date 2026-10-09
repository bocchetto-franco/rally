using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class Circuit01DressingBake
{
    const string Output="Assets/Scenes/Circuit_01/OcclusionCullingData.asset";
    const string Report="Logs/Circuit01Dressing/bake.txt";
    static SceneSetup[] previous;
    static DateTime started;
    static bool active;
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor unavailable for bake.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
        previous=EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity");
        if(File.Exists(Output))File.Copy(Output,"Logs/Circuit01Dressing/PreviousOcclusion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".asset");
        started=DateTime.UtcNow;File.WriteAllText(Report,"RUNNING: current Circuit_01 scenery\n");
        if(!StaticOcclusionCulling.GenerateInBackground()){EditorSceneManager.RestoreSceneManagerSetup(previous);throw new Exception("Could not start occlusion bake.");}
        active=true;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!active||StaticOcclusionCulling.isRunning)return;
        active=false;EditorApplication.update-=Tick;
        try
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Circuit_01.unity")throw new Exception("Active scene changed during bake.");
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save bake.");
            if(!File.Exists(Output)||new FileInfo(Output).Length<100||File.GetLastWriteTimeUtc(Output)<started)throw new Exception("Missing or stale occlusion output.");
            File.AppendAllText(Report,"BAKE OK: "+new FileInfo(Output).Length+" bytes; "+File.GetLastWriteTimeUtc(Output).ToString("O")+"\nCOMPLETE: PASS\n");
        }
        catch(Exception e){File.AppendAllText(Report,"FAILED: "+e+"\n");}
        finally{EditorSceneManager.RestoreSceneManagerSetup(previous);}
    }
}
