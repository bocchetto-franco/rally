using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using System.IO;
public static class RallyFiveBlocksRefresh
{
    public static string PrepareLightEditor()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) throw new Exception("Editor must be idle");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        long before=Profiler.GetTotalAllocatedMemoryLong();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect(); GC.WaitForPendingFinalizers();
        string message="Editor switched to saved MainMenu without modifying assets; allocated bytes before="+before+", after="+Profiler.GetTotalAllocatedMemoryLong();
        File.AppendAllText("Logs/five-blocks-editor-recovery.txt",DateTime.Now.ToString("O")+" "+message+"\n");
        return message;
    }
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before refreshing");
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        CompilationPipeline.RequestScriptCompilation();
        return "Refresh and script compilation requested outside Play.";
    }
    public static string Status() => "Playing=" + EditorApplication.isPlaying + "; dust Update=" +
        (typeof(RallyVehicleDust).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic) != null) +
        "; compiling=" + EditorApplication.isCompiling + "; difficulty enum=" + string.Join(",",Enum.GetNames(typeof(RallyBotDifficulty))) +
        "; tests=" + string.Join(",", Array.ConvertAll(new[] { "RallyAudioTest.Active", "Rally.MenuArtTest.Active", "Rally.MenuPagesTest.Active", "Rally.SplitTest.Active", "Rally.RenderBenchmark.Running", "RallyRecoveryTest.Active", "Rally.PaceNotesTest.Active" }, k => k + "=" + SessionState.GetBool(k,false))) +
        "; botTestIndex=" + SessionState.GetInt("RallyBotSmoke.Index",-1);
    public static string FinalStatus()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying || EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
            throw new Exception("Editor must be idle with valid compilation");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        foreach(string circuit in RallyGameSession.CircuitScenes)
        {
            string path="Logs/five-blocks-bot-"+circuit+".txt";
            string log=File.ReadAllText(path);
            if(!log.Contains("COMPLETE: PASS") || !log.Contains("RESTORED:")) throw new Exception("Incomplete contact test: "+path);
        }
        string result="PASS: three circuit contact tests complete/restored, compilation valid, no Play, no dirty scenes; scene="+SceneManager.GetActiveScene().name+"; timeScale="+Time.timeScale;
        File.WriteAllText("Logs/five-blocks-final-status.txt",DateTime.Now.ToString("O")+"\n"+result+"\n");
        return result;
    }
    public static string RestoreStoppedChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Editor must be idle");
        int restored = 0;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("RallySelectionUxCheck"); if (type == null) continue;
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            bool subscribed=false;
            foreach(var callback in EditorApplication.update.GetInvocationList()) if(callback.Method.DeclaringType==type) { subscribed=true; break; }
            if(!subscribed) continue;
            if (!(bool)type.GetField("stopping", flags).GetValue(null))
                type.GetMethod("Finish",flags).Invoke(null,new object[] { new Exception("Interrupted Play session; restoring before retry") });
            type.GetMethod("Restore",flags).Invoke(null,null); restored++;
        }
        return "Restored interrupted verification sessions: " + restored;
    }
}
