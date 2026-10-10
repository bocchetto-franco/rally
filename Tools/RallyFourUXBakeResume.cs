using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Restore the completion callback if Unity reloaded scripts during the native bake.
public static class RallyFourUXBakeResume
{
    static DateTime started;
    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new Exception("Editor busy.");
        if(SceneManager.GetActiveScene().path!="Assets/Scenes/Circuit_02.unity")throw new Exception("Do not switch the bake scene.");
        started=File.GetLastWriteTimeUtc("Logs/circuit02-sierras-bake.txt");
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        return "Monitoring existing native C02 bake; no restart or scene change.";
    }
    static void Tick()
    {
        if(StaticOcclusionCulling.isRunning)return;
        EditorApplication.update-=Tick;
        try
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Circuit_02.unity")throw new Exception("Bake scene changed.");
            EditorSceneManager.SaveScene(scene);
            var file=new FileInfo("Assets/Scenes/Circuit_02/OcclusionCullingData.asset");
            if(!file.Exists||file.Length==0||file.LastWriteTimeUtc<started)throw new Exception("Bake output is not fresh.");
            File.AppendAllText("Logs/circuit02-sierras-bake.txt","COMPLETE: PASS (post-UX pines removal, "+file.Length+" bytes)\n");
        }
        catch(Exception e){File.AppendAllText("Logs/circuit02-sierras-bake.txt","FAIL: "+e+"\n");}
    }
}
