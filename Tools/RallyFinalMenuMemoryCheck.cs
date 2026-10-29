using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// On-demand memory/residency check. No source scene/quality/tuning writes.
public static class RallyFinalMenuMemoryCheck
{
    static IEnumerator steps;
    static string report;
    static double deadline, waitUntil;
    static bool stopping, entered, background, paused, hadPlayers, hadCircuit;
    static int players, frame;
    static string circuit;
    static float scale;
    static SceneAsset start;
    static SceneSetup[] setup;
    public static void Before() => Begin("before");
    public static void After() => Begin("after");
    static void Begin(string label)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Editor must be idle");
        for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene");
        setup=EditorSceneManager.GetSceneManagerSetup(); start=EditorSceneManager.playModeStartScene;
        background=Application.runInBackground; paused=EditorApplication.isPaused; scale=Time.timeScale;
        hadPlayers=PlayerPrefs.HasKey("Rally.LocalPlayers"); players=PlayerPrefs.GetInt("Rally.LocalPlayers",1);
        hadCircuit=PlayerPrefs.HasKey("Rally.SelectedCircuit"); circuit=PlayerPrefs.GetString("Rally.SelectedCircuit");
        RallyGameSession.SelectLocalPlayers(1); RallyGameSession.SelectCircuit(0);
        Directory.CreateDirectory("Logs/FinalAudit"); report="Logs/FinalAudit/menu-memory-"+label+".txt";
        File.WriteAllText(report,"Menu residency / native memory; Editor overhead included, not FPS\n"+DateTimeOffset.Now.ToString("O")+"\n");
        stopping=entered=false; frame=-1; deadline=EditorApplication.timeSinceStartup+300; waitUntil=0;
        steps=Steps(); EditorApplication.update+=Tick;
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/VehicleCircuitSelection.unity");
        Application.runInBackground=true; EditorApplication.isPaused=false; Time.timeScale=1; EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(stopping) { if(!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if(EditorApplication.timeSinceStartup>deadline) { End(new Exception("Deadline exceeded")); return; }
        if(EditorApplication.isPlaying) { entered=true; EditorApplication.QueuePlayerLoopUpdate(); }
        else if(entered&&!EditorApplication.isPlayingOrWillChangePlaymode) { End(new Exception("Play stopped before completion")); return; }
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating||frame==Time.frameCount||EditorApplication.timeSinceStartup<waitUntil) return;
        frame=Time.frameCount;
        try {
            if(!steps.MoveNext()) { End(null); return; }
            waitUntil=EditorApplication.timeSinceStartup+(steps.Current is float seconds?seconds:0);
        } catch(Exception error) { End(error); }
    }
    static bool Ready(string name)
    {
        var active=Object.FindObjectsByType<RallyMenuBackdropScene>();
        return active.Length==1 && active[0].name.Contains(name);
    }
    static IEnumerator Steps()
    {
        while(SceneManager.GetActiveScene().name!="VehicleCircuitSelection") yield return null;
        while(!Ready("Circuit_01") || Object.FindObjectsByType<RallyMenuVehiclePreview>().Any(v=>v.IsTransitioning)) yield return null;
        yield return 2f;
        Snapshot("initial-C01");
        var backdrop=Object.FindAnyObjectByType<RallyMenuBackdrop>();
        foreach(string track in new[]{"Circuit_02","Circuit_03","Circuit_01","Circuit_03","Circuit_02"}) {
            // Bound the diagnostic rather than provoking a known low-memory dialog.
            if(Profiler.GetTotalAllocatedMemoryLong()>1800L*1024*1024) throw new Exception("Memory safety ceiling reached; remaining switches skipped");
            backdrop.Show(track);
            while(!Ready(track)) yield return null;
            yield return 3f;
            Snapshot("settled-"+track);
        }
        yield return Resources.UnloadUnusedAssets();
        yield return 1f;
        Snapshot("explicit-unused-cleanup-control");
    }
    static void Snapshot(string phase)
    {
        var prefabs=Resources.FindObjectsOfTypeAll<RallyMenuBackdropScene>().Where(c=>!c.gameObject.scene.IsValid()).Select(c=>c.name).OrderBy(n=>n).ToArray();
        File.AppendAllText(report,$"{phase}: nativeAllocated={Profiler.GetTotalAllocatedMemoryLong()}; reserved={Profiler.GetTotalReservedMemoryLong()}; graphics={Profiler.GetAllocatedMemoryForGraphicsDriver()}; textures={Texture.currentTextureMemory}; terrainAssets={Resources.FindObjectsOfTypeAll<TerrainData>().Length}; backdropPrefabs=[{string.Join(",",prefabs)}]; activeBackdrops={Object.FindObjectsByType<RallyMenuBackdropScene>().Length}\n");
    }
    static void End(Exception error)
    {
        File.AppendAllText(report,error==null?"COMPLETE: PASS\n":"INCOMPLETE: "+error+"\n");
        stopping=true; Time.timeScale=1; EditorApplication.isPlaying=false;
    }
    static void Restore()
    {
        EditorApplication.update-=Tick;
        if(hadPlayers) PlayerPrefs.SetInt("Rally.LocalPlayers",players); else PlayerPrefs.DeleteKey("Rally.LocalPlayers");
        if(hadCircuit) PlayerPrefs.SetString("Rally.SelectedCircuit",circuit); else PlayerPrefs.DeleteKey("Rally.SelectedCircuit");
        PlayerPrefs.Save(); RallyGameSession.RestoreSavedState();
        EditorSceneManager.playModeStartScene=start; Application.runInBackground=background;
        EditorApplication.isPaused=paused; Time.timeScale=scale;
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        File.AppendAllText(report,"RESTORED: preferences/play settings/source scenes; no scene saved\n");
    }
}
